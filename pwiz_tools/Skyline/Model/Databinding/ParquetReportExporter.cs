/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Parquet;
using Parquet.Schema;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Skyline.Model.Databinding
{
    public class ParquetReportExporter : IReportExporter
    {
        public void Export(Stream stream, RowItemEnumerator rowItemEnumerator)
        {
            // Build columns and schema from item properties
            var columnValueTree = new ColumnValueTree();
            var columns = BuildColumns(rowItemEnumerator.ItemProperties, columnValueTree);
            var schema = new ParquetSchema(columns.Select(col => col.SchemaField).ToArray());

            var options = new ParquetOptions
            {
                CompressionMethod = CompressionMethod.Zstd,
                // Parquet.Net's default is SmallestSize, which is Zstd level 19 and many times slower
                // than the level 3 that Optimal maps to, for a few percent smaller file
                CompressionLevel = CompressionLevel.Optimal
            };
            var writer = ParquetWriter.CreateAsync(schema, stream, options).GetAwaiter().GetResult();
            using (var pipeline = new ExportPipeline(writer, columns, columnValueTree, rowItemEnumerator))
            {
                pipeline.RowsPerGroup = DecideRowCountPerGroup(rowItemEnumerator.ItemProperties);
                pipeline.Run();
            }
            // Disposing the writer writes the footer. On a stream the export has already failed on
            // that throws again, and from a "using" that exception would replace the one which says
            // why the export failed. The writer holds nothing but the caller's stream, so a failed
            // export leaves it undisposed.
            writer.DisposeAsync().GetAwaiter().GetResult();
        }

        private List<ColumnData> BuildColumns(ItemProperties itemProperties, ColumnValueTree columnValueTree)
        {
            var columns = new List<ColumnData>();
            var usedColumnNames = new HashSet<string>();

            foreach (DataPropertyDescriptor property in itemProperties)
            {
                var name = GetUniqueColumnName(property, usedColumnNames);
                columns.Add(new ColumnData(name, property, columnValueTree));
            }

            return columns;
        }

        /// <summary>
        /// Runs one export as three stages on separate threads: a reader which pulls rows from the
        /// <see cref="RowItemEnumerator"/> into chunks, the calling thread which calculates the column
        /// values of each chunk on the ParallelEx workers, and a writer which encodes and compresses each
        /// chunk into a row group. Each stage hands its output to the next through a queue holding one
        /// chunk, so a stage never gets more than one chunk ahead of the one after it.
        /// The first exception from any stage cancels the others and is rethrown by <see cref="Run"/>.
        /// </summary>
        private class ExportPipeline : IDisposable
        {
            private readonly ParquetWriter _writer;
            private readonly IList<ColumnData> _columns;
            private readonly ColumnValueTree _columnValueTree;
            private readonly RowItemEnumerator _rowItemEnumerator;
            private readonly BlockingCollection<List<RowItem>> _chunks = new BlockingCollection<List<RowItem>>(1);
            /// <summary>
            /// Chunks whose columns have been calculated and packed, waiting to be written as a row group.
            /// </summary>
            private readonly BlockingCollection<ColumnBuffer[]> _rowGroups = new BlockingCollection<ColumnBuffer[]>(1);
            /// <summary>
            /// Column buffers which the writer has finished with, ready to hold another chunk.
            /// One set is being populated, one is waiting to be written and one is being written,
            /// so the arrays which hold a chunk get allocated three times per export instead of once per chunk.
            /// </summary>
            private readonly BlockingCollection<ColumnBuffer[]> _freeBuffers = new BlockingCollection<ColumnBuffer[]>();
            private const int MAX_BUFFER_SETS = 3;
            private int _bufferSetCount;
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
            private readonly CancellationToken _cancellationToken;
            private Exception _exception;

            public ExportPipeline(ParquetWriter writer, IList<ColumnData> columns, ColumnValueTree columnValueTree,
                RowItemEnumerator rowItemEnumerator)
            {
                _writer = writer;
                _columns = columns;
                _columnValueTree = columnValueTree;
                _rowItemEnumerator = rowItemEnumerator;
                _cancellationToken = _cancellationTokenSource.Token;
            }

            public int RowsPerGroup { get; set; } = 1000;

            public void Run()
            {
                var readThread = StartThread(@"Parquet Row Reader", ReadChunks);
                var writeThread = StartThread(@"Parquet Writer", WriteRowGroups);
                RunStage(() =>
                {
                    foreach (var chunk in _chunks.GetConsumingEnumerable(_cancellationToken))
                    {
                        var buffers = TakeBuffers(chunk.Count);
                        PopulateChunk(chunk, buffers);
                        if (_rowItemEnumerator.IsCanceled)
                        {
                            // Stops the reader and the writer without waiting for what they are doing
                            _cancellationTokenSource.Cancel();
                            break;
                        }
                        _rowGroups.Add(buffers, _cancellationToken);
                    }
                });
                _rowGroups.CompleteAdding();
                readThread.Join();
                writeThread.Join();
                if (_exception != null)
                {
                    ExceptionDispatchInfo.Capture(_exception).Throw();
                }
            }

            public void Dispose()
            {
                _cancellationTokenSource.Dispose();
                _chunks.Dispose();
                _rowGroups.Dispose();
                _freeBuffers.Dispose();
            }

            /// <summary>
            /// Returns a set of column buffers with room for a chunk of the given size, waiting for the
            /// writer to finish with one if all <see cref="MAX_BUFFER_SETS"/> are in use. Every chunk but the
            /// last has <see cref="RowsPerGroup"/> rows, so a set made for one chunk fits all of the later ones.
            /// </summary>
            private ColumnBuffer[] TakeBuffers(int rowCount)
            {
                if (!_freeBuffers.TryTake(out var buffers))
                {
                    if (_bufferSetCount < MAX_BUFFER_SETS)
                    {
                        _bufferSetCount++;
                        buffers = _columns.Select(column => column.CreateBuffer(rowCount)).ToArray();
                    }
                    else
                    {
                        buffers = _freeBuffers.Take(_cancellationToken);
                    }
                }
                foreach (var buffer in buffers)
                {
                    buffer.Reset(rowCount);
                }
                return buffers;
            }

            private Thread StartThread(string name, Action stage)
            {
                var thread = new Thread(() =>
                {
                    LocalizationHelper.InitThread();
                    RunStage(stage);
                })
                {
                    Name = name,
                    IsBackground = true
                };
                thread.Start();
                return thread;
            }

            private void RunStage(Action stage)
            {
                try
                {
                    stage();
                }
                catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
                {
                    // Another stage failed, or the export was canceled
                }
                catch (Exception exception)
                {
                    // The first exception wins. Cancelling unblocks the other stages, whose
                    // OperationCanceledExceptions are then ignored above
                    Interlocked.CompareExchange(ref _exception, exception, null);
                    _cancellationTokenSource.Cancel();
                }
            }

            private void ReadChunks()
            {
                try
                {
                    while (true)
                    {
                        var chunk = new List<RowItem>();
                        while (chunk.Count < RowsPerGroup && _rowItemEnumerator.MoveNext())
                        {
                            chunk.Add(_rowItemEnumerator.Current);
                        }

                        if (chunk.Count == 0)
                        {
                            return;
                        }
                        _chunks.Add(chunk, _cancellationToken);
                    }
                }
                finally
                {
                    _chunks.CompleteAdding();
                }
            }

            private void WriteRowGroups()
            {
                foreach (var buffers in _rowGroups.GetConsumingEnumerable(_cancellationToken))
                {
                    using (var groupWriter = _writer.CreateRowGroup())
                    {
                        // A row group's columns have to be written in schema order, one after another
                        foreach (var buffer in buffers)
                        {
                            buffer.WriteAsync(groupWriter, _cancellationToken).GetAwaiter().GetResult();
                        }
                    }
                    // Nothing refers to the buffers any more, so the next chunk can be stored in them
                    _freeBuffers.Add(buffers);
                }
            }

            /// <summary>
            /// Calculates every column's value for every row in the chunk into the buffers and packs
            /// them, ready to be written as one row group.
            /// </summary>
            private void PopulateChunk(IList<RowItem> rowItems, ColumnBuffer[] buffers)
            {
                // Values with no Parquet storage type get stored as strings by calling ToString(),
                // which formats using the thread's culture, so the values have to be converted under
                // the culture this report is being exported with. All of the columns come from the
                // same DataSchema, so the culture only needs to be set once per row.
                var dataSchemaLocalizer = _columns.FirstOrDefault()?.PropertyDescriptor.DataSchemaLocalizer
                                          ?? DataSchemaLocalizer.INVARIANT;
                // Consecutive rows which share the same Value object (for instance the rows expanded from one
                // transition by a "Results" sublist) form a run. Columns which depend only on the Value have
                // the same value for every row in the run, so they are calculated once per run.
                var runStarts = new List<int>();
                for (int rowIndex = 0; rowIndex < rowItems.Count; rowIndex++)
                {
                    if (rowIndex == 0 || !ReferenceEquals(rowItems[rowIndex].Value, rowItems[rowIndex - 1].Value))
                    {
                        runStarts.Add(rowIndex);
                    }
                }
                ParallelEx.For(0, runStarts.Count, runIndex =>
                {
                    if (_rowItemEnumerator.IsCanceled)
                    {
                        return;
                    }
                    int startRow = runStarts[runIndex];
                    int endRow = runIndex + 1 < runStarts.Count ? runStarts[runIndex + 1] : rowItems.Count;
                    dataSchemaLocalizer.CallWithCultureInfo(() =>
                    {
                        var nodeValues = new object[_columnValueTree.NodeCount];
                        _columnValueTree.Evaluate(rowItems[startRow], nodeValues, true);
                        for (int colIndex = 0; colIndex < _columns.Count; colIndex++)
                        {
                            var column = _columns[colIndex];
                            if (!column.DependsOnlyOnRowValue)
                            {
                                continue;
                            }
                            var value = column.ConvertToStorageValue(nodeValues[column.NodeIndex]);
                            if (value != null)
                            {
                                for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                                {
                                    buffers[colIndex].Store(rowIndex, value);
                                }
                            }
                        }
                        for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                        {
                            var rowItem = rowItems[rowIndex];
                            _columnValueTree.Evaluate(rowItem, nodeValues, false);
                            for (int colIndex = 0; colIndex < _columns.Count; colIndex++)
                            {
                                var column = _columns[colIndex];
                                if (column.DependsOnlyOnRowValue)
                                {
                                    continue;
                                }
                                var value = column.NodeIndex >= 0
                                    ? column.ConvertToStorageValue(nodeValues[column.NodeIndex])
                                    : column.GetStorageValue(rowItem);
                                if (value != null)
                                {
                                    buffers[colIndex].Store(rowIndex, value);
                                }
                            }
                        }
                    });
                    for (int rowIndex = startRow; rowIndex < endRow; rowIndex++)
                    {
                        rowItems[rowIndex] = null;
                    }
                }, threadName: nameof(PopulateChunk));
                // A column with nulls has to be packed, and a list column flattened, so the columns are
                // packed in parallel rather than one after another on this thread while the workers wait
                // for the next chunk
                ParallelEx.For(0, _columns.Count, colIndex =>
                {
                    buffers[colIndex].Pack();
                }, threadName: nameof(ColumnBuffer.Pack));
            }
        }

        /// <summary>
        /// The distinct ColumnDescriptors which the report's columns and their ancestors consist of, ordered so
        /// that every parent precedes its children. Evaluating the nodes in that order calculates an ancestor
        /// shared by several columns (such as "Results!*.Value" for every result column) once per row instead
        /// of once per column.
        /// </summary>
        private class ColumnValueTree
        {
            private readonly List<ColumnDescriptor> _nodes = new List<ColumnDescriptor>();
            private readonly List<int> _parentIndexes = new List<int>();
            private readonly List<bool> _dependsOnlyOnRowValue = new List<bool>();
            private readonly Dictionary<ColumnDescriptor, int> _nodeIndexes = new Dictionary<ColumnDescriptor, int>();

            public int NodeCount
            {
                get { return _nodes.Count; }
            }

            /// <summary>
            /// Adds the column and any of its ancestors which are not already present,
            /// and returns the index of the column's node.
            /// </summary>
            public int AddNode(ColumnDescriptor columnDescriptor)
            {
                if (_nodeIndexes.TryGetValue(columnDescriptor, out int nodeIndex))
                {
                    return nodeIndex;
                }
                int parentIndex = columnDescriptor.Parent == null ? -1 : AddNode(columnDescriptor.Parent);
                nodeIndex = _nodes.Count;
                _nodes.Add(columnDescriptor);
                _parentIndexes.Add(parentIndex);
                _dependsOnlyOnRowValue.Add(columnDescriptor.DependsOnlyOnRowValue);
                _nodeIndexes.Add(columnDescriptor, nodeIndex);
                return nodeIndex;
            }

            /// <summary>
            /// Calculates the values of the nodes which depend only on the row's Value, or of the other nodes,
            /// into <paramref name="nodeValues"/>. The values of the nodes which depend only on the Value have
            /// to be there already when the other nodes are calculated.
            /// </summary>
            public void Evaluate(RowItem rowItem, object[] nodeValues, bool dependsOnlyOnRowValue)
            {
                for (int nodeIndex = 0; nodeIndex < _nodes.Count; nodeIndex++)
                {
                    if (_dependsOnlyOnRowValue[nodeIndex] != dependsOnlyOnRowValue)
                    {
                        continue;
                    }
                    int parentIndex = _parentIndexes[nodeIndex];
                    var parentValue = parentIndex < 0 ? null : nodeValues[parentIndex];
                    nodeValues[nodeIndex] = _nodes[nodeIndex].GetValueFromParent(parentValue, rowItem, null);
                }
            }
        }

        public static IEnumerable<string> MakeValidColumnNames(IEnumerable<string> columnNames)
        {
            var usedColumnNames = new HashSet<string>();
            foreach (var name in columnNames)
            {
                yield return MakeUniqueColumnName(name, usedColumnNames);
            }
        }

        private string GetUniqueColumnName(PropertyDescriptor propertyDescriptor, HashSet<string> usedColumnNames)
        {
            // Get the display name from DisplayNameAttribute, or fall back to property name
            string baseName = propertyDescriptor.DisplayName;
            if (string.IsNullOrEmpty(baseName) || baseName == propertyDescriptor.Name)
            {
                baseName = propertyDescriptor.Name;
            }

            return MakeUniqueColumnName(baseName, usedColumnNames);
        }

        private static string MakeUniqueColumnName(string name, HashSet<string> usedColumnNames)
        {
            // Sanitize the column name - replace illegal characters with underscores
            // Parquet column names should be valid identifiers (alphanumeric and underscore)
            var sanitized = SanitizeColumnName(name);

            // Ensure uniqueness by appending a number if needed
            string uniqueName = Helpers.GetUniqueName(sanitized, usedColumnNames);
            usedColumnNames.Add(uniqueName);
            return uniqueName;
        }

        private static string SanitizeColumnName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return @"Column";
            }

            var sanitized = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                // Allow alphanumeric characters and underscores
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sanitized.Append(c);
                }
                else
                {
                    // Replace illegal characters with underscores
                    sanitized.Append('_');
                }
            }

            // Ensure the name doesn't start with a digit
            string result = sanitized.ToString();
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = @"_" + result;
            }

            return result;
        }

        private class ColumnData
        {
            public ColumnData(string name, DataPropertyDescriptor propertyDescriptor, ColumnValueTree columnValueTree)
            {
                Name = name;
                PropertyDescriptor = propertyDescriptor;
                NodeIndex = -1;
                if (propertyDescriptor is ColumnPropertyDescriptor columnPropertyDescriptor &&
                    columnPropertyDescriptor.PivotKey == null &&
                    columnPropertyDescriptor.DisplayColumn.ColumnDescriptor != null)
                {
                    NodeIndex = columnValueTree.AddNode(columnPropertyDescriptor.DisplayColumn.ColumnDescriptor);
                    DependsOnlyOnRowValue = columnPropertyDescriptor.DependsOnlyOnRowValue;
                }
                var valueType = PropertyDescriptor.DataSchema.GetWrappedValueType(PropertyDescriptor.PropertyType);

                // Check if this is a ListColumnValue<T>
                ListElementType = GetListColumnValueStorageType(valueType);
                if (ListElementType != null)
                {
                    // This is a list column
                    // The element storage type is nullable so individual list slots can hold nulls
                    StorageType = new StorageType(typeof(IEnumerable<>).MakeGenericType(ListElementType.Type));
                    var elementField = new DataField(@"element", ListElementType.Type,
                        isNullable: true, isArray: false);
                    SchemaField = new ListField(Name, elementField);
                    DataField = elementField;
                }
                else
                {
                    StorageType = new StorageType(DecideStorageType(valueType));
                    DataField = new DataField(Name, StorageType.Type, isNullable: true);
                    SchemaField = DataField;
                }
            }

            public string Name { get; }
            public DataPropertyDescriptor PropertyDescriptor { get; }
            /// <summary>
            /// Index of this column's node in the ColumnValueTree, or -1 if the column is not a plain
            /// ColumnPropertyDescriptor and has to be evaluated on its own with <see cref="GetStorageValue"/>.
            /// </summary>
            public int NodeIndex { get; }
            /// <summary>
            /// True if rows which share the same <see cref="RowItem.Value"/> have the same value in this column.
            /// Only known for columns in the ColumnValueTree; any other column is assumed to vary per row.
            /// </summary>
            public bool DependsOnlyOnRowValue { get; }
            /// <summary>
            /// The type of the values in the array that holds a chunk of this column.
            /// For list columns this is an IEnumerable of <see cref="ListElementType"/>.
            /// </summary>
            public StorageType StorageType { get; }
            /// <summary>
            /// For list columns, the storage type of each list element. Otherwise null.
            /// </summary>
            public StorageType ListElementType { get; }
            public Field SchemaField { get; }
            public DataField DataField { get; }

            /// <summary>
            /// Creates a buffer which holds this column for a chunk of up to <paramref name="capacity"/> rows.
            /// The buffer's element type is the one Parquet.Net stores the field's values as, which for
            /// a string column is <see cref="ReadOnlyMemory{Char}"/>.
            /// </summary>
            public ColumnBuffer CreateBuffer(int capacity)
            {
                var bufferType = ListElementType != null ? typeof(ListColumnBuffer<>) : typeof(ColumnBuffer<>);
                return (ColumnBuffer) Activator.CreateInstance(bufferType.MakeGenericType(DataField.ClrType),
                    DataField, capacity);
            }

            /// <summary>
            /// Returns the column's value for the row, converted to something that can be stored
            /// in a <see cref="ColumnBuffer"/>, or null.
            /// </summary>
            public object GetStorageValue(RowItem rowItem)
            {
                return ConvertToStorageValue(PropertyDescriptor.GetValue(rowItem));
            }

            /// <summary>
            /// Converts a value which the column's property descriptor returned to something
            /// that can be stored in a <see cref="ColumnBuffer"/>, or null. For a list column
            /// that is an array of <see cref="ListElementType"/>.
            /// </summary>
            public object ConvertToStorageValue(object value)
            {
                value = PropertyDescriptor.DataSchema.UnwrapValue(value);
                if (value == null)
                {
                    return null;
                }

                if (ListElementType != null)
                {
                    // Extract the list from ListColumnValue<T>
                    return ConvertListColumnValue(value);
                }
                return StorageType.ConvertValue(value);
            }

            private Array ConvertListColumnValue(object listColumnValue)
            {
                // Get the underlying list as an array
                Array array = (listColumnValue as IListColumnValue)?.ToArray();
                if (array == null)
                {
                    return null;
                }

                if (array.GetType().GetElementType() == ListElementType.Type)
                {
                    return array;
                }

                var convertedArray = Array.CreateInstance(ListElementType.Type, array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    var value = ListElementType.ConvertValue(array.GetValue(i));
                    if (value != null)
                    {
                        convertedArray.SetValue(value, i);
                    }
                }

                return convertedArray;
            }
        }
        /// <summary>
        /// Holds one column of a chunk while the values are calculated, and writes it as one column of
        /// a row group. A buffer is reused for chunk after chunk so that the large arrays which hold a
        /// chunk are not allocated, and collected, once per chunk.
        /// </summary>
        private abstract class ColumnBuffer
        {
            /// <summary>
            /// Makes the buffer ready for a chunk with the given number of rows, all of them null.
            /// </summary>
            public abstract void Reset(int rowCount);
            /// <summary>
            /// Stores a value which is not null and has already been converted to the column's storage type.
            /// Different rows may be stored from different threads.
            /// </summary>
            public abstract void Store(int rowIndex, object value);
            /// <summary>
            /// Arranges the chunk's values the way Parquet wants them written, once every row has been stored:
            /// the values which are not null packed together, with definition levels saying where the nulls were.
            /// </summary>
            public abstract void Pack();
            /// <summary>
            /// Writes the packed chunk as the next column of the row group.
            /// </summary>
            public abstract Task WriteAsync(ParquetRowGroupWriter groupWriter, CancellationToken cancellationToken);

            /// <summary>
            /// Converts a stored value to the type Parquet.Net stores the field's values as. A value from
            /// <see cref="StorageType.ConvertValue"/> is already that type, except a string, which is
            /// stored as <see cref="ReadOnlyMemory{Char}"/>.
            /// </summary>
            protected static T ToStorage<T>(object value) where T : struct
            {
                if (typeof(T) == typeof(ReadOnlyMemory<char>))
                {
                    return (T) (object) ((string) value).AsMemory();
                }
                return (T) value;
            }
        }

        private class ColumnBuffer<T> : ColumnBuffer where T : struct
        {
            private readonly DataField _field;
            private readonly T[] _values;
            /// <summary>
            /// The Parquet definition level of each row: 1 where the row has a value, 0 where it is null.
            /// </summary>
            private readonly int[] _definitionLevels;
            private int _rowCount;
            /// <summary>
            /// After <see cref="Pack"/>, the number of values at the start of <see cref="_values"/>.
            /// </summary>
            private int _valueCount;

            public ColumnBuffer(DataField field, int capacity)
            {
                _field = field;
                _values = new T[capacity];
                _definitionLevels = new int[capacity];
            }

            public override void Reset(int rowCount)
            {
                if (rowCount > _values.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(rowCount));
                }
                _rowCount = rowCount;
                Array.Clear(_definitionLevels, 0, rowCount);
            }

            public override void Store(int rowIndex, object value)
            {
                _values[rowIndex] = ToStorage<T>(value);
                _definitionLevels[rowIndex] = 1;
            }

            public override void Pack()
            {
                // The values which are not null are moved towards the start of the array, in place:
                // a value never moves past a row which has not been looked at yet
                _valueCount = 0;
                for (int i = 0; i < _rowCount; i++)
                {
                    if (_definitionLevels[i] != 0)
                    {
                        _values[_valueCount++] = _values[i];
                    }
                }
            }

            public override Task WriteAsync(ParquetRowGroupWriter groupWriter, CancellationToken cancellationToken)
            {
                return groupWriter.WriteAllPartsAsync(_field, new ReadOnlyMemory<T>(_values, 0, _valueCount),
                    new ReadOnlyMemory<int>(_definitionLevels, 0, _rowCount), null, cancellationToken);
            }
        }

        /// <summary>
        /// A list column keeps one array per row of a chunk holding that row's list, and packs them into
        /// one flat array of the elements with the repetition levels which say where each list starts and
        /// the definition levels which say whether a row's list is null, empty, or has a null element.
        /// </summary>
        private class ListColumnBuffer<T> : ColumnBuffer where T : struct
        {
            private readonly DataField _elementField;
            private readonly Array[] _lists;
            private int _rowCount;
            private T[] _values = Array.Empty<T>();
            private int[] _definitionLevels = Array.Empty<int>();
            private int[] _repetitionLevels = Array.Empty<int>();
            private int _valueCount;
            /// <summary>
            /// The number of definition and repetition levels, one per list element, or one for a row
            /// whose list is null or empty.
            /// </summary>
            private int _levelCount;

            public ListColumnBuffer(DataField elementField, int capacity)
            {
                _elementField = elementField;
                _lists = new Array[capacity];
            }

            public override void Reset(int rowCount)
            {
                if (rowCount > _lists.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(rowCount));
                }
                _rowCount = rowCount;
                Array.Clear(_lists, 0, rowCount);
            }

            public override void Store(int rowIndex, object value)
            {
                _lists[rowIndex] = (Array) value;
            }

            public override void Pack()
            {
                int levelCount = 0;
                for (int rowIndex = 0; rowIndex < _rowCount; rowIndex++)
                {
                    levelCount += Math.Max(1, _lists[rowIndex]?.Length ?? 0);
                }
                if (levelCount > _definitionLevels.Length)
                {
                    _values = new T[levelCount];
                    _definitionLevels = new int[levelCount];
                    _repetitionLevels = new int[levelCount];
                }
                // The element's definition level counts the levels of nesting which are present: the
                // list, the list's repeated group (so at least one element), and the element's value
                int valueLevel = _elementField.MaxDefinitionLevel;
                int nullElementLevel = valueLevel - 1;
                int emptyListLevel = valueLevel - 2;
                int nullListLevel = valueLevel - 3;
                _valueCount = 0;
                _levelCount = 0;
                for (int rowIndex = 0; rowIndex < _rowCount; rowIndex++)
                {
                    var list = _lists[rowIndex];
                    if (list == null || list.Length == 0)
                    {
                        _repetitionLevels[_levelCount] = 0;
                        _definitionLevels[_levelCount++] = list == null ? nullListLevel : emptyListLevel;
                        continue;
                    }
                    for (int i = 0; i < list.Length; i++)
                    {
                        var element = list.GetValue(i);
                        // 0 starts a new list, 1 continues the previous element's list
                        _repetitionLevels[_levelCount] = i == 0 ? 0 : 1;
                        if (element == null)
                        {
                            _definitionLevels[_levelCount++] = nullElementLevel;
                        }
                        else
                        {
                            _definitionLevels[_levelCount++] = valueLevel;
                            _values[_valueCount++] = ToStorage<T>(element);
                        }
                    }
                }
            }

            public override Task WriteAsync(ParquetRowGroupWriter groupWriter, CancellationToken cancellationToken)
            {
                return groupWriter.WriteAllPartsAsync(_elementField, new ReadOnlyMemory<T>(_values, 0, _valueCount),
                    new ReadOnlyMemory<int>(_definitionLevels, 0, _levelCount),
                    new ReadOnlyMemory<int>(_repetitionLevels, 0, _levelCount), cancellationToken);
            }
        }

        private static StorageType GetListColumnValueStorageType(Type type)
        {
            var elementType = ListColumnValue.GetElementType(type);
            if (elementType == null)
            {
                return null;
            }

            return new StorageType(DecideStorageType(elementType));
        }

        private static Dictionary<Type, Type> _storageTypes = new Dictionary<Type, Type>
        {
            { typeof(int), typeof(int?) },
            { typeof(long), typeof(long?) },
            { typeof(double), typeof(double?) },
            { typeof(float), typeof(float?) },
            { typeof(bool), typeof(bool?) },
            { typeof(decimal), typeof(decimal?) },
            // Old call sites wrapped DateTime as DateTimeOffset with a local-Kind
            // assumption that wasn't actually valid; store DateTime? directly so
            // the value goes through unchanged.
            { typeof(DateTime), typeof(DateTime?) }
        };

        static ParquetReportExporter()
        {
            foreach (var value in _storageTypes.Values.ToArray())
            {
                _storageTypes[value] = value;
            }
        }

        public static Type DecideStorageType(Type type)
        {
            if (_storageTypes.TryGetValue(type, out var storageType))
            {
                return storageType;
            }
            return typeof(string);
        }

        /// <summary>
        /// The .NET type that a column's values are held in before being written to Parquet.
        /// The nullable underlying type is computed once in the constructor so that converting
        /// each value does not have to call <see cref="Nullable.GetUnderlyingType"/>, which is slow.
        /// </summary>
        public class StorageType
        {
            public StorageType(Type type)
            {
                Type = type;
                NullableUnderlyingType = Nullable.GetUnderlyingType(type);
            }

            public Type Type { get; }
            /// <summary>
            /// If <see cref="Type"/> is a <see cref="Nullable{T}"/>, then T. Otherwise null.
            /// </summary>
            public Type NullableUnderlyingType { get; }

            /// <summary>
            /// Converts a value to something which can be stored in an array whose element type is <see cref="Type"/>.
            /// Note that a boxed <see cref="Nullable{T}"/> with a value is a boxed T, so for a nullable
            /// storage type the returned value is a boxed T.
            /// </summary>
            public object ConvertValue(object value)
            {
                if (value == null)
                {
                    return null;
                }

                var valueType = value.GetType();
                if (valueType == Type)
                {
                    return value;
                }

                if (NullableUnderlyingType != null)
                {
                    if (valueType == NullableUnderlyingType)
                    {
                        return value;
                    }
                    return Convert.ChangeType(value, NullableUnderlyingType);
                }
                if (Type == typeof(string))
                {
                    return value.ToString();
                }
                return Convert.ChangeType(value, Type);
            }
        }

        /// <summary>
        /// Returns the number of rows that should be in each row group based on the
        /// set of columns that are going to be written.
        /// 
        /// </summary>
        public static int DecideRowCountPerGroup(ItemProperties itemProperties)
        {
            var targetGroupSize = 1 << 28; // Try to have row groups that are 256MB on disk
            int rowCount = targetGroupSize / EstimateRowByteCount(itemProperties);
            return Math.Max(rowCount, 1000);
        }

        /// <summary>
        /// Returns a loose estimate of the number of bytes each row will take up.
        /// </summary>
        public static int EstimateRowByteCount(IEnumerable<DataPropertyDescriptor> propertyDescriptors)
        {
            int columnCount = 0;
            int maxSublistDepth = 0;
            int leafColumnByteCount = 0;
            foreach (var propertyDescriptor in propertyDescriptors)
            {
                columnCount++;
                int sublistDepth = 0;
                if (propertyDescriptor is ColumnPropertyDescriptor columnPropertyDescriptor)
                {
                    var propertyPath = columnPropertyDescriptor.PropertyPath;
                    for (; false == propertyPath?.IsRoot; propertyPath = propertyPath.Parent)
                    {
                        if (propertyPath.IsUnboundLookup)
                        {
                            sublistDepth++;
                        }
                    }
                }

                int columnSize = EstimateColumnSize(propertyDescriptor);
                // Only include the column byte counts for the columns from the deepest depth of sublist.
                // Other columns are assumed to have a lot of duplication
                if (sublistDepth > maxSublistDepth)
                {
                    maxSublistDepth = sublistDepth;
                    leafColumnByteCount = columnSize;
                }
                else if (sublistDepth == maxSublistDepth)
                {
                    leafColumnByteCount += columnSize;
                }
            }

            return 1 + columnCount + leafColumnByteCount;
        }

        public static int EstimateColumnSize(DataPropertyDescriptor propertyDescriptor)
        {
            var columnType = propertyDescriptor.DataSchema.GetWrappedValueType(propertyDescriptor.PropertyType);
            if (columnType.IsPrimitive)
            {
                return Marshal.SizeOf(columnType);
            }
            if (GetListColumnValueStorageType(columnType) != null)
            {
                return 64;
            }

            return 8;
        }
    }
}
