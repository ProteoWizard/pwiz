using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Collections
{
    public abstract class Transposition : Immutable
    {
        private ImmutableList<ColumnData> _columns;
        
        public Transposition ChangeColumns(IEnumerable<ColumnData> columns)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im._columns = ImmutableList.ValueOf(columns);
            });
        }

        public Transposition ChangeColumnAt(int columnIndex, ColumnData column)
        {
            ColumnData currentColumn = default;
            if (columnIndex < _columns.Count)
            {
                currentColumn = _columns[columnIndex];
            }

            if (Equals(currentColumn, column))
            {
                return this;
            }

            ColumnData[] newColumns = new ColumnData[Math.Max(_columns.Count, columnIndex + 1)];
            _columns.CopyTo(newColumns, 0);
            newColumns[columnIndex] = column;
            int nonEmptyCount = newColumns.Length;
            while (nonEmptyCount > 0 && newColumns[nonEmptyCount - 1].IsEmpty)
            {
                nonEmptyCount--;
            }

            ImmutableList<ColumnData> immutableList;
            if (nonEmptyCount == newColumns.Length)
            {
                immutableList = ImmutableList.ValueOf(newColumns);
            }
            else
            {
                immutableList = ImmutableList.ValueOf(newColumns.Take(nonEmptyCount));
            }
            return ChangeProp(ImClone(this), im => im._columns = immutableList);
        }

        public IEnumerable<ColumnData> ColumnDatas
        {
            get
            {
                return _columns;
            }
        }

        public ColumnData GetColumnValues(int columnIndex)
        {
            if (columnIndex < _columns.Count)
            {
                return _columns[columnIndex];
            }

            return default;
        }

        public abstract class ColumnDef : Immutable
        {
            public abstract void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions, int columnIndex) where T : Transposition;
        }

        public struct ColumnData
        {
            private IEnumerable _data;

            public static ColumnData Constant<T>(T value)
            {
                return new ColumnData(new ConstantValue<T>(value));
            }
            public ColumnData(IEnumerable list)
            {
                _data = list;
            }

            public bool IsEmpty
            {
                get { return _data == null; }
            }

            public bool IsConstant<T>()
            {
                return _data is ConstantValue<T>;
            }

            public T GetValueAt<T>(int index)
            {
                if (_data == null)
                {
                    return default;
                }

                if (_data is ConstantValue<T> constant)
                {
                    return constant.Value;
                }

                var readOnlyList = (IReadOnlyList<T>)_data;
                if (index < 0 || index >= readOnlyList.Count)
                {
                    return default;
                }
                return readOnlyList[index];
            }

            public ImmutableList<T> ToImmutableList<T>()
            {
                return ImmutableList.ValueOf(_data as IReadOnlyList<T>);
            }

            public bool IsImmutableList<T>()
            {
                return _data is ImmutableList<T>;
            }

            private class ConstantValue<T> : IEnumerable
            {
                public ConstantValue(T value)
                {
                    Value = value;
                }
                public T Value { get; }
                public IEnumerator GetEnumerator()
                {
                    yield break;
                }

                protected bool Equals(ConstantValue<T> other)
                {
                    return EqualityComparer<T>.Default.Equals(Value, other.Value);
                }

                public override bool Equals(object obj)
                {
                    if (ReferenceEquals(null, obj)) return false;
                    if (ReferenceEquals(this, obj)) return true;
                    if (obj.GetType() != GetType()) return false;
                    return Equals((ConstantValue<T>)obj);
                }

                public override int GetHashCode()
                {
                    return EqualityComparer<T>.Default.GetHashCode(Value);
                }
            }

            public bool Equals(ColumnData other)
            {
                return Equals(_data, other._data);
            }

            public override bool Equals(object obj)
            {
                return obj is ColumnData other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _data?.GetHashCode() ?? 0;
            }
        }
    }

    public abstract class Transposition<TRow> : Transposition
    {
        public abstract Transposer GetTransposer();

        public TRow[] ToRows(int start, int count)
        {
            return GetTransposer().ToRows(ColumnDatas, start, count);
        }

        public TRow GetRow(int index)
        {
            return ToRows(index, 1)[0];
        }

        public Transposition ChangeRows(ICollection<TRow> rows)
        {
            return ChangeColumns(GetTransposer().ToColumns(rows));
        }

        public new abstract class ColumnDef : Transposition.ColumnDef
        {
            public abstract Array GetValues(IEnumerable<TRow> rows);
            public abstract void SetValues(IEnumerable<TRow> rows, ColumnData column, int start);
        }

        public sealed class ColumnDef<TCol> : ColumnDef
        {
            private Func<TRow, TCol> _getter;
            private Action<TRow, TCol> _setter;
            public ColumnDef(Func<TRow, TCol> getter, Action<TRow, TCol> setter, bool useValueCache = false)
            {
                _getter = getter;
                _setter = setter;
                UseValueCache = useValueCache;
            }

            public Type ValueType
            {
                get { return typeof(TCol); }
            }
            
            public bool UseValueCache { get; private set; }

            public override Array GetValues(IEnumerable<TRow> rows)
            {
                return rows.Select(_getter).ToArray();
            }

            public override void SetValues(IEnumerable<TRow> rows, ColumnData column, int start)
            {
                int iRow = 0;
                foreach (var row in rows)
                {
                    _setter(row, column.GetValueAt<TCol>(iRow + start));
                    iRow++;
                }
            }
            
            public override void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions, int columnIndex)
            {
                int iTransposition = 0;
                foreach (var newList in StoreLists(UseValueCache ? valueCache : null, transpositions.Select(t => t.GetColumnValues(columnIndex))))
                {
                    var transposition = transpositions[iTransposition];
                    transposition = (T)transposition.ChangeColumnAt(columnIndex, newList);
                    transpositions[iTransposition] = transposition;
                    iTransposition++;
                }
            }

            private static int ItemSize
            {
                get
                {
                    return Marshal.ReadInt32(typeof(TCol).TypeHandle.Value, 4);
                }
            }

            public static IEnumerable<ColumnData> StoreLists(ValueCache valueCache, IEnumerable<ColumnData> lists)
            {
                var localValueCache = new ValueCache();
                var columnInfos = new List<ColumnDataInfo>();
                foreach (var list in lists)
                {
                    var immutableList = list.ToImmutableList<TCol>();
                    if (immutableList == null)
                    {
                        columnInfos.Add(new ColumnDataInfo(list, null));
                    }
                    else if (true == valueCache?.TryGetCachedValue(ref immutableList))
                    {
                        columnInfos.Add(new ColumnDataInfo(new ColumnData(immutableList), null));
                    }
                    else
                    {
                        columnInfos.Add(new ColumnDataInfo(list, localValueCache.CacheValue(HashedObject.ValueOf(immutableList))));
                    }
                }
                var storedLists = StoreUniqueLists(columnInfos.Where(col => col.ImmutableList != null).Select(col => col.ImmutableList).Distinct())
                    .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
                foreach (var columnInfo in columnInfos)
                {
                    if (columnInfo.ImmutableList == null || !storedLists.TryGetValue(columnInfo.ImmutableList, out var storedList))
                    {
                        yield return columnInfo.OriginalColumnData;
                    }
                    else
                    {
                        if (valueCache != null && storedList.IsImmutableList<TCol>() )
                        {
                            yield return new ColumnData(valueCache.CacheValue(storedList.ToImmutableList<TCol>()));
                        }
                        else
                        {
                            yield return storedList;
                        }
                    }
                }
            }

            public static IEnumerable<Tuple<HashedObject<ImmutableList<TCol>>, ColumnData>> StoreUniqueLists(IEnumerable<HashedObject<ImmutableList<TCol>>> lists)
            {
                var remainingLists = new List<ListInfo>();
                foreach (var list in lists)
                {
                    if (list == null)
                    {
                        continue;
                    }

                    var listInfo = new ListInfo(list);
                    if (listInfo.UniqueValues.Count == 1)
                    {
                        if (Equals(list.Value[0], default(TCol)))
                        {
                            yield return Tuple.Create(list, default(ColumnData));
                        }
                        else if (list.Value.Count > 1)
                        {
                            yield return Tuple.Create(list, ColumnData.Constant(list.Value[0]));
                        }
                        continue;
                    }
                    remainingLists.Add(listInfo);
                }

                if (remainingLists.Count == 0)
                {
                    yield break;
                }

                if (ItemSize <= 1)
                {
                    yield break;
                }

                var mostUniqueItems = remainingLists.Max(list => list.UniqueValues.Count);
                if (mostUniqueItems >= byte.MaxValue)
                {
                    yield break;
                }

                var allUniqueItems = remainingLists.SelectMany(listInfo => listInfo.UniqueValues)
                    .Where(v => !Equals(v, default(TCol))).ToList();
                if (allUniqueItems.Count >= byte.MaxValue)
                {
                    yield break;
                }

                int totalItemCount = remainingLists.Sum(list => list.ImmutableList.Value.Count);
                var potentialSavings = (totalItemCount - allUniqueItems.Count) * (ItemSize - 1) - IntPtr.Size * remainingLists.Count;
                if (potentialSavings <= 0)
                {
                    yield break;
                }

                var factorListBuilder = new FactorList<TCol>.Builder(remainingLists.SelectMany(listInfo => listInfo.UniqueValues));
                foreach (var listInfo in remainingLists)
                {
                    yield return Tuple.Create(listInfo.ImmutableList,
                        new ColumnData(factorListBuilder.MakeFactorList(listInfo.ImmutableList.Value)));
                }
            }

            class ColumnDataInfo
            {
                public ColumnDataInfo(ColumnData columnData, HashedObject<ImmutableList<TCol>> immutableList)
                {
                    OriginalColumnData = columnData;
                    ImmutableList = immutableList;
                }

                public ColumnData OriginalColumnData { get; }

                public HashedObject<ImmutableList<TCol>> ImmutableList { get; }
            }

            class ListInfo
            {
                public ListInfo(HashedObject<ImmutableList<TCol>> list)
                {
                    ImmutableList = list;
                    UniqueValues = ImmutableList.Value.Distinct().ToList();
                }

                public HashedObject<ImmutableList<TCol>> ImmutableList { get; }
                public List<TCol> UniqueValues { get; }
            }

        }
        public abstract class Transposer
        {
            private List<ColumnDef> _columnDefs = new List<ColumnDef>();

            protected void DefineColumn<TCol>(Func<TRow, TCol> getter, Action<TRow, TCol> setter, bool useValueCache = false)
            {
                _columnDefs.Add(new ColumnDef<TCol>(getter, setter));
            }

            public IEnumerable<ColumnData> ToColumns(ICollection<TRow> rows)
            {
                return _columnDefs.Select(col => new ColumnData(col.GetValues(rows)));
            }

            public abstract Transposition<TRow> Transpose(ICollection<TRow> rows);

            protected abstract TRow[] CreateRows(int rowCount);
            public TRow[] ToRows(IEnumerable<ColumnData> columns, int start, int count)
            {
                var rows = CreateRows(count);
                int iColumn = 0;
                foreach (var column in columns)
                {
                    _columnDefs[iColumn++].SetValues(rows, column, start);
                }

                return rows;
            }

            public void EfficientlyStore<T>(ValueCache valueCache, IList<T> transpositions) where T : Transposition
            {
                for (int iCol = 0; iCol < _columnDefs.Count; iCol++)
                {
                    _columnDefs[iCol].EfficientlyStore(valueCache, transpositions, iCol);
                }
            }
        }
    }
}
