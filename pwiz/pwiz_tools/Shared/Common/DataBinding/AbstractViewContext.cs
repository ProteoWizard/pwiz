/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.DataBinding.RowSources;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Base implementation of the <see cref="IViewContext"/> interface
    /// </summary>
    public abstract class AbstractViewContext : IViewContext
    {
        public const string DefaultViewName = "default";
        private IList<RowSourceInfo> _rowSources;

        protected AbstractViewContext(DataSchema dataSchema, IEnumerable<RowSourceInfo> rowSources)
        {
            DataSchema = dataSchema;
            RowSources = rowSources;
        }

        public abstract string GetExportDirectory();
        public abstract void SetExportDirectory(string value);
        public abstract DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons);
        protected virtual string GetDefaultExportFilename(ViewInfo viewInfo)
        {
            string currentViewName = viewInfo.Name;
            return viewInfo.ParentColumn.PropertyType.Name + (currentViewName == GetDefaultViewName() ? "" : currentViewName);
        }
        public abstract bool RunLongJob(Control owner, Action<IProgressMonitor> job);
        public DataSchema DataSchema { get; private set; }
        public IEnumerable<ViewSpec> BuiltInViews
        {
            get { return _rowSources.SelectMany(rowSourceInfo=>rowSourceInfo.Views.Select(view=>view.ViewSpec.SetRowSource(rowSourceInfo.Name))); }
        }

        protected IEnumerable<RowSourceInfo> RowSources
        {
            get
            {
                return _rowSources;
            }
            set
            {
                _rowSources = ImmutableList.ValueOf(value);
            }
        }

        protected RowSourceInfo FindRowSourceInfo(Type rowType)
        {
            return _rowSources.FirstOrDefault(rowSource => rowSource.RowType == rowType);
        }

        protected RowSourceInfo FindRowSourceInfo(ViewInfo viewInfo)
        {
            return FindRowSourceInfo(viewInfo.ParentColumn.PropertyType);
        }
        protected abstract ViewSpecList GetViewSpecList();
        protected abstract void SaveViewSpecList(ViewSpecList viewSpecList);

        public virtual IEnumerable<ViewSpec> CustomViews
        {
            get
            {
                var viewSpecList = GetViewSpecList() ?? ViewSpecList.EMPTY;
                var views = viewSpecList.ViewSpecs.Where(viewSpec => null != GetRowSourceInfo(viewSpec)).ToArray();
                return views;
            }
            set
            {
                SetCustomViews(value);
            }
        }

        public ViewInfo GetViewInfo(ViewSpec viewSpec)
        {
            var rowSourceInfo = GetRowSourceInfo(viewSpec);
            if (null == rowSourceInfo)
            {
                return null;
            }
            return new ViewInfo(DataSchema, rowSourceInfo.RowType, viewSpec);
        }

        protected RowSourceInfo GetRowSourceInfo(ViewSpec viewSpec)
        {
            return RowSources.FirstOrDefault(rowSource => rowSource.Name == viewSpec.RowSource);
        }

        protected virtual void SetCustomViews(IEnumerable<ViewSpec> customViewSpecs)
        {
            var oldViewSpecList = GetViewSpecList().ViewSpecs.ToList();
            int indexInsert = oldViewSpecList.FindIndex(viewSpec => null != GetRowSourceInfo(viewSpec));
            if (indexInsert < 0)
            {
                indexInsert = oldViewSpecList.Count;
            }
            var otherViews = oldViewSpecList.Where(view => null == GetRowSourceInfo(view)).ToArray();
            var combinedList = otherViews.Take(indexInsert).Concat(customViewSpecs).Concat(otherViews.Skip(indexInsert));
            SaveViewSpecList(new ViewSpecList(combinedList));
        }

        public virtual string GetNewViewName()
        {
            var takenNames = new HashSet<string>(BuiltInViews.Select(viewSpec => viewSpec.Name));
            takenNames.UnionWith(CustomViews.Select(viewSpec=>viewSpec.Name));
            const string baseName = "CustomView";
            for (int index = 1;;index++)
            {
                string name = baseName + index;
                if (!takenNames.Contains(name))
                {
                    return name;
                }
            }
        }

        protected virtual string GetDefaultViewName()
        {
            return DefaultViewName;
        }

        public virtual IEnumerable GetRowSource(ViewInfo viewInfo)
        {
            var rowSource = _rowSources.FirstOrDefault(rowSourceInfo => rowSourceInfo.Name == viewInfo.RowSourceName);
            if (rowSource == null)
            {
                return Array.CreateInstance(viewInfo.ParentColumn.PropertyType, 0);
            }
            return rowSource.Rows;
        } 

        public Icon ApplicationIcon { get; protected set; }

        protected virtual void WriteData(IProgressMonitor progressMonitor, TextWriter writer, BindingListSource bindingListSource, DsvWriter dsvWriter)
        {
            IList<RowItem> rows = Array.AsReadOnly(bindingListSource.Cast<RowItem>().ToArray());
            IList<PropertyDescriptor> properties = bindingListSource.GetItemProperties(new PropertyDescriptor[0]).Cast<PropertyDescriptor>().ToArray();
            var status = new ProgressStatus("Writing " + rows.Count + " rows");
            dsvWriter.WriteHeaderRow(writer, properties);
            var rowCount = rows.Count;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                status = status.ChangeMessage("Writing row " + (rowIndex + 1) + "/" + rowCount)
                    .ChangePercentComplete(rowIndex*100/rowCount);
                progressMonitor.UpdateProgress(status);
                dsvWriter.WriteDataRow(writer, rows[rowIndex], properties);
            }
        }

        public void Export(Control owner, BindingListSource bindingListSource)
        {
            var dataFormats = new[] { DataFormats.CSV, DataFormats.TSV };
            string fileFilter = string.Join("|", dataFormats.Select(format => format.FileFilter).ToArray());
            using (var saveFileDialog = new SaveFileDialog
                {
                Filter = fileFilter,
                InitialDirectory = GetExportDirectory(),
                FileName = GetDefaultExportFilename(bindingListSource.ViewInfo),
            })
            {
                if (saveFileDialog.ShowDialog(owner) == DialogResult.Cancel)
                {
                    return;
                }
                var dataFormat = dataFormats[saveFileDialog.FilterIndex - 1];
                using (var writer = new StreamWriter(File.OpenWrite(saveFileDialog.FileName),
                    new UTF8Encoding(false)))
                {
                    var cloneableRowSource = bindingListSource.RowSource as ICloneableList;
                    if (null == cloneableRowSource)
                    {
                        var progressMonitor = new UncancellableProgressMonitor();
                        WriteData(progressMonitor, writer, bindingListSource, dataFormat.GetDsvWriter());
                    }
                    else
                    {
                        var clonedList = cloneableRowSource.DeepClone();
                        RunLongJob(owner, progressMonitor =>
                        {
                            using (var clonedBindingList = new BindingListSource())
                            {
                                clonedBindingList.SetView(bindingListSource.ViewInfo, clonedList);
                                WriteData(progressMonitor, writer, clonedBindingList, dataFormat.GetDsvWriter());
                            }
                        });
                    }
                }
                SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
            }
        }

        public BindingListSource ExecuteQuery(Control owner, ViewSpec viewSpec)
        {
            var viewInfo = GetViewInfo(viewSpec);
            var rowSource = GetRowSource(viewInfo);
            BindingListSource bindingListSource = new BindingListSource();
            var cloneableList = rowSource as ICloneableList;
            if (null != cloneableList)
            {
                var clonedValues = ((ICloneableList) rowSource).DeepClone();
                RunLongJob(owner, progressMonitor =>
                {
                    progressMonitor.UpdateProgress(new ProgressStatus("Executing query"));
                    bindingListSource.SetView(viewInfo, clonedValues);
                });
            }
            else
            {
                bindingListSource.SetView(viewInfo, rowSource);
            }
            return bindingListSource;
        }

        protected virtual bool IsReadOnly(ViewInfo viewInfo)
        {
            return BuiltInViews.Any(builtInView => viewInfo.Name == builtInView.Name);
        }

        public virtual ViewSpec MakeEditable(ViewSpec viewSpec, string originalName)
        {
            if (string.IsNullOrEmpty(viewSpec.Name))
            {
                return viewSpec;
            }
            var viewNames = new HashSet<string>(BuiltInViews.Select(builtInViewSpec => builtInViewSpec.Name));
            if (originalName == viewSpec.Name && !string.IsNullOrEmpty(originalName))
            {
                if (!viewNames.Contains(viewSpec.Name))
                {
                    return viewSpec;
                }
            }
            viewNames.UnionWith(CustomViews.Select(customViewSpec=>customViewSpec.Name));
            return viewSpec.SetName(FindUniqueName(viewNames, viewSpec.Name));
        }

        protected string FindUniqueName(ISet<string> existingNames, string startingName)
        {
            if (!string.IsNullOrEmpty(startingName) && !existingNames.Contains(startingName))
            {
                return startingName;
            }
            string baseName = startingName ?? string.Empty;
            int lastDigit = baseName.Length;
            while (lastDigit > 0 && char.IsDigit(baseName[lastDigit - 1]))
            {
                lastDigit--;
            }
            baseName = baseName.Substring(0, lastDigit);
            if (baseName.Length == 0)
            {
                baseName = "CustomView";
            }
            for (int uniquifier = 1;; uniquifier++)
            {
                string name = baseName + uniquifier;
                if (!existingNames.Contains(name))
                {
                    return name;
                }
            }
        }

        public ViewSpec CustomizeView(Control owner, ViewSpec viewSpec)
        {
            return CustomizeOrCreateView(owner, viewSpec, viewSpec.Name);
        }

        protected virtual ViewEditor CreateViewEditor(ViewSpec viewSpec)
        {
            return new ViewEditor(this, GetViewInfo(viewSpec));
        }
        
        protected virtual ViewSpec CustomizeOrCreateView(Control owner, ViewSpec viewSpec, string originalName)
        {
            viewSpec = MakeEditable(viewSpec, originalName);
            using (var customizeViewForm = CreateViewEditor(viewSpec))
            {
                if (customizeViewForm.ShowDialog(owner.TopLevelControl) == DialogResult.Cancel)
                {
                    return null;
                }
                // Consider: if save fails, reshow CustomizeViewForm?
                ViewInfo viewInfo = customizeViewForm.ViewInfo;
                viewInfo = new ViewInfo(viewInfo.ParentColumn, viewInfo.GetViewSpec().SetName(customizeViewForm.ViewName));
                return SaveView(viewInfo, originalName).ViewSpec;
            }
        }

        public ViewSpec NewView(Control owner)
        {
            return CustomizeOrCreateView(owner, new ViewSpec().SetRowSource(RowSources.First().Name), null);
        }

        public ViewSpec CopyView(Control owner, ViewSpec currentView)
        {
            if (null == currentView)
            {
                currentView = BuiltInViews.FirstOrDefault();
            }
            if (null == currentView)
            {
                currentView = new ViewSpec().SetRowSource(RowSources.First().Name);
            }
            return CustomizeOrCreateView(owner, currentView, null);
        }

        public void ManageViews(Control owner)
        {
            using (var manageViewsForm = new ManageViewsForm(this)
            {
                ExportDataButtonVisible = false,
            })
            {
                manageViewsForm.ShowDialog(owner.TopLevelControl);
            }
        }

        protected virtual ViewInfo SaveView(ViewInfo viewInfo, string originalName)
        {
            var viewSpecs = CustomViews.ToList();
            var existingIndex = viewSpecs.FindIndex(vs => vs.Name == originalName);
            if (existingIndex >= 0)
            {
                viewSpecs[existingIndex] = viewInfo.ViewSpec;
            }
            else
            {
                viewSpecs.Add(viewInfo.ViewSpec);
            }
            SetCustomViews(viewSpecs);
            return viewInfo;
        }

        public void DeleteViews(IEnumerable<ViewSpec> views)
        {
            var deletedViewNames = new HashSet<string>(views.Select(view=>view.Name));
            SetCustomViews(CustomViews.Where(view=>!deletedViewNames.Contains(view.Name)));
        }

        public abstract void ExportViews(Control owner, IEnumerable<ViewSpec> views);
        public abstract void ImportViews(Control owner);

        public virtual DataGridViewColumn CreateGridViewColumn(PropertyDescriptor propertyDescriptor)
        {
            DataGridViewColumn column = CreateCustomColumn(propertyDescriptor);
            // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
            if (null == column)
            {
                column = CreateLinkColumn(propertyDescriptor);
            }
            if (null == column)
            {
                column = CreateComboBoxColumn(propertyDescriptor);
            }
            if (null == column)
            {
                column = CreateDefaultColumn(propertyDescriptor);
            }
            column = InitializeColumn(column, propertyDescriptor);
            return column;
        }

        protected virtual DataGridViewComboBoxColumn CreateComboBoxColumn(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.IsReadOnly)
            {
                return null;
            }
            var valueList = GetValueList(propertyDescriptor);
            if (valueList == null)
            {
                return null;
            }
            DataGridViewComboBoxColumn comboBoxColumn = new DataGridViewComboBoxColumn();
            foreach (var value in valueList)
            {
                comboBoxColumn.Items.Add(value);
            }
            return comboBoxColumn;
        }

        protected virtual DataGridViewColumn CreateCustomColumn(PropertyDescriptor propertyDescriptor)
        {
            var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
            if (null == columnPropertyDescriptor)
            {
                return null;
            }
            var columnDescriptor = columnPropertyDescriptor.DisplayColumn.ColumnDescriptor;
            if (null == columnDescriptor)
            {
                return null;
            }
            var columnTypeAttribute = columnDescriptor.GetAttributes().OfType<DataGridViewColumnTypeAttribute>().FirstOrDefault();

            if (columnTypeAttribute == null || columnTypeAttribute.ColumnType == null)
            {
                return null;
            }
            try
            {
                var constructor = columnTypeAttribute.ColumnType.GetConstructor(new Type[0]);
                Debug.Assert(null != constructor);
                return constructor != null ? (DataGridViewColumn) constructor.Invoke(new object[0]) : null;
            }
            catch (Exception exception)
            {
                Trace.TraceError("Exception constructing column of type {0}:{1}", columnTypeAttribute.ColumnType, exception);
                return null;
            }
        }

        protected DataGridViewLinkColumn CreateLinkColumn(PropertyDescriptor propertyDescriptor)
        {
            if (!typeof (ILinkValue).IsAssignableFrom(propertyDescriptor.PropertyType))
            {
                return null;
            }
            return new DataGridViewLinkColumn {TrackVisitedState = false};
        }

        protected virtual TColumn InitializeColumn<TColumn>(TColumn column, PropertyDescriptor propertyDescriptor) 
            where TColumn : DataGridViewColumn
        {
            column.DataPropertyName = propertyDescriptor.Name;
            column.HeaderText = propertyDescriptor.DisplayName;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            column.FillWeight = 1;
            var attributes = GetAttributeCollection(propertyDescriptor);
            var formatAttribute = attributes[typeof (FormatAttribute)] as FormatAttribute;
            if (null != formatAttribute)
            {
                if (null != formatAttribute.Format)
                {
                    column.DefaultCellStyle.Format = formatAttribute.Format;
                }
                if (null != formatAttribute.NullValue && propertyDescriptor.IsReadOnly)
                {
                    column.DefaultCellStyle.NullValue = formatAttribute.NullValue;
                }
            }
            return column;
        }

        protected virtual AttributeCollection GetAttributeCollection(PropertyDescriptor propertyDescriptor)
        {
            return propertyDescriptor.Attributes;
        }


        protected virtual IEnumerable GetValueList(PropertyDescriptor propertyDescriptor)
        {
            var propertyType = propertyDescriptor.PropertyType;
            if (propertyType.IsEnum)
            {
                return Enum.GetValues(propertyType);
            }
            if (propertyType.IsGenericType 
                && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var innerType = propertyType.GetGenericArguments()[0];
                if (innerType.IsEnum)
                {
                    return new object[] { null }.Concat(Enum.GetValues(innerType).Cast<object>());
                }
            }
            return null;
        }

        public virtual void OnDataError(object sender, DataGridViewDataErrorEventArgs dataGridViewDataErrorEventArgs)
        {
            if (0 != (dataGridViewDataErrorEventArgs.Context & DataGridViewDataErrorContexts.Commit))
            {
                var dataGridView = sender as DataGridView;
                string message;
                if (dataGridViewDataErrorEventArgs.Exception == null)
                {
                    message = "An unknown error occurred updating the value.";
                }
                else
                {
                    message = string.Format("There was an error updating the value:\r\n{0}",
                                            dataGridViewDataErrorEventArgs.Exception.Message);
                }
                if (dataGridView != null && dataGridView.IsCurrentCellInEditMode)
                {
                    if (ShowMessageBox(dataGridView, message, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                    {
                        dataGridView.CancelEdit();
                    }
                }
                else
                {
                    ShowMessageBox(sender as Control, message, MessageBoxButtons.OK);
                }
            }
        }

        public virtual bool DeleteEnabled
        {
            get { return false; }
            
        }
        public virtual void Delete()
        {
        }

        protected static ViewSpec GetDefaultViewSpec(ColumnDescriptor parentColumn)
        {
            var viewSpec = new ViewSpec().SetName(DefaultViewName).SetRowType(parentColumn.PropertyType);
            var columns = new List<ColumnSpec>();
            foreach (var column in parentColumn.GetChildColumns())
            {
                if (null != column.DataSchema.GetCollectionInfo(column.PropertyType))
                {
                    continue;
                }
                if (column.DataSchema.IsAdvanced(column))
                {
                    continue;
                }
                columns.Add(new ColumnSpec(PropertyPath.Root.Property(column.Name)));
            }
            viewSpec = viewSpec.SetColumns(columns);
            return viewSpec;
        }

        protected virtual DataGridViewColumn CreateDefaultColumn(PropertyDescriptor propertyDescriptor)
        {
            DataGridViewColumn dataGridViewColumn;
            var type = propertyDescriptor.PropertyType;
            if (type == typeof(bool) || type == typeof(CheckState))
            {
                dataGridViewColumn = new DataGridViewCheckBoxColumn(type == typeof(CheckState));
            }
            else 
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(Image));
                if (typeof (Image).IsAssignableFrom(type) || converter.CanConvertFrom(type))
                {
                    dataGridViewColumn = new DataGridViewImageColumn();
                }
                else
                {
                    dataGridViewColumn = new DataGridViewTextBoxColumn();
                }
            }
            return dataGridViewColumn;
        }

        public virtual void Preview(Control owner, ViewInfo viewInfo)
        {
        }

        private class UncancellableProgressMonitor : IProgressMonitor
        {
            public bool IsCanceled
            {
                get { return false; }
            }

            public void UpdateProgress(ProgressStatus status)
            {
            }

            public bool HasUI
            {
                get { return false; }
            }
        }
    }
}