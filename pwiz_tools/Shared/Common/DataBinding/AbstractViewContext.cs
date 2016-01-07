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
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Base implementation of the <see cref="IViewContext"/> interface
    /// </summary>
    public abstract class AbstractViewContext : IViewContext
    {
        
        public const string DefaultViewName = "default"; // Not L10N
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
            return viewInfo.ParentColumn.PropertyType.Name + (currentViewName == GetDefaultViewName() ? string.Empty : currentViewName);
        }
        public abstract bool RunLongJob(Control owner, Action<IProgressMonitor> job);
        public DataSchema DataSchema { get; private set; }
        public IEnumerable<ViewSpec> BuiltInViews
        {
            get { return _rowSources.SelectMany(rowSourceInfo=>rowSourceInfo.Views.Select(view=>view.ViewSpec.SetRowSource(rowSourceInfo.Name))); }
        }

        public abstract IEnumerable<ViewGroup> ViewGroups { get; }

        public ViewGroup FindGroup(ViewGroupId id)
        {
            if (Equals(id, ViewGroup.BUILT_IN.Id))
            {
                return ViewGroup.BUILT_IN;
            }
            return ViewGroups.FirstOrDefault(group => Equals(group.Id, id));
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

        public virtual ViewSpecList GetViewSpecList(ViewGroupId viewGroup)
        {
            if (Equals(viewGroup, ViewGroup.BUILT_IN.Id))
            {
                return new ViewSpecList(BuiltInViews);
            }
            return null;
        }

        public abstract ViewGroup DefaultViewGroup { get; }

        public virtual bool TryRenameView(ViewGroupId groupId, string oldName, string newName)
        {
            var viewSpecList = GetViewSpecList(groupId) ?? ViewSpecList.EMPTY;
            if (null != viewSpecList.GetView(newName))
            {
                return false;
            }
            SaveViewSpecList(groupId, viewSpecList.RenameView(oldName, newName));
            return true;
        }

        public virtual void AddOrReplaceViews(ViewGroupId groupId, IEnumerable<ViewSpec> viewSpecs)
        {
            var viewSpecList = GetViewSpecList(groupId) ?? ViewSpecList.EMPTY;
            viewSpecList = viewSpecList.AddOrReplaceViews(viewSpecs);
            SaveViewSpecList(groupId, viewSpecList);
        }

        public virtual void DeleteViews(ViewGroupId groupId, IEnumerable<string> viewNames)
        {
            var viewSpecList = GetViewSpecList(groupId);
            viewSpecList = viewSpecList.DeleteViews(viewNames);
            SaveViewSpecList(groupId, viewSpecList);
        }

        protected abstract void SaveViewSpecList(ViewGroupId viewGroupId, ViewSpecList viewSpecList);

        protected RowSourceInfo FindRowSourceInfo(ViewInfo viewInfo)
        {
            return FindRowSourceInfo(viewInfo.ParentColumn.PropertyType);
        }

        public ViewInfo GetViewInfo(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var rowSourceInfo = GetRowSourceInfo(viewSpec);
            if (null == rowSourceInfo)
            {
                return null;
            }
            return new ViewInfo(DataSchema, rowSourceInfo.RowType, viewSpec).ChangeViewGroup(viewGroup);
        }

        public ViewInfo GetViewInfo(ViewName? viewName)
        {
            if (!viewName.HasValue)
            {
                return null;
            }
            var viewSpec = GetViewSpecList(viewName.Value.GroupId).GetView(viewName.Value.Name);
            if (null == viewSpec)
            {
                return null;
            }
            return GetViewInfo(FindGroup(viewName.Value.GroupId), viewSpec);
        }

        protected RowSourceInfo GetRowSourceInfo(ViewSpec viewSpec)
        {
            return RowSources.FirstOrDefault(rowSource => rowSource.Name == viewSpec.RowSource);
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

        protected virtual void WriteData(IProgressMonitor progressMonitor, TextWriter writer,
            BindingListSource bindingListSource, DsvWriter dsvWriter)
        {
            ProgressStatus status = new ProgressStatus(string.Format(Resources.AbstractViewContext_WriteData_Writing__0__rows, bindingListSource.Count));
            WriteDataWithStatus(progressMonitor, ref status, writer, bindingListSource, dsvWriter);
        }

        protected virtual void WriteDataWithStatus(IProgressMonitor progressMonitor, ref ProgressStatus status, TextWriter writer, BindingListSource bindingListSource, DsvWriter dsvWriter)
        {
            IList<RowItem> rows = Array.AsReadOnly(bindingListSource.Cast<RowItem>().ToArray());
            IList<PropertyDescriptor> properties = bindingListSource.GetItemProperties(new PropertyDescriptor[0]).Cast<PropertyDescriptor>().ToArray();
            dsvWriter.WriteHeaderRow(writer, properties);
            var rowCount = rows.Count;
            int startPercent = status.PercentComplete;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                int percentComplete = startPercent + (rowIndex*(100 - startPercent)/rowCount);
                if (percentComplete > status.PercentComplete)
                {
                    status = status.ChangeMessage(string.Format(Resources.AbstractViewContext_WriteData_Writing_row__0___1_, (rowIndex + 1), rowCount))
                        .ChangePercentComplete(percentComplete);
                    progressMonitor.UpdateProgress(status);
                }
                dsvWriter.WriteDataRow(writer, rows[rowIndex], properties);
            }
        }

        public void Export(Control owner, BindingListSource bindingListSource)
        {
            try
            {
                var dataFormats = new[] {DataFormats.CSV, DataFormats.TSV};
                string fileFilter = string.Join("|", dataFormats.Select(format => format.FileFilter).ToArray()); // Not L10N
                using (var saveFileDialog = new SaveFileDialog
                {
                    Filter = fileFilter,
                    InitialDirectory = GetExportDirectory(),
                    FileName = GetDefaultExportFilename(bindingListSource.ViewInfo),
                })
                {
                    if (saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner)) == DialogResult.Cancel)
                    {
                        return;
                    }
                    var dataFormat = dataFormats[saveFileDialog.FilterIndex - 1];
                    SafeWriteToFile(owner, saveFileDialog.FileName, stream =>
                    {
                        var writer = new StreamWriter(stream, new UTF8Encoding(false));
                        var cloneableRowSource = bindingListSource.RowSource as ICloneableList;
                        bool finished = false;
                        if (null == cloneableRowSource)
                        {
                            var progressMonitor = new UncancellableProgressMonitor();
                            WriteData(progressMonitor, writer, bindingListSource, dataFormat.GetDsvWriter());
                            finished = true;
                        }
                        else
                        {
                            var clonedList = cloneableRowSource.DeepClone();
                            RunLongJob(owner, progressMonitor =>
                            {
                                using (var clonedBindingList = new BindingListSource())
                                {
                                    SetViewFrom(bindingListSource, clonedList, clonedBindingList);
                                    WriteData(progressMonitor, writer, clonedBindingList, dataFormat.GetDsvWriter());
                                    finished = !progressMonitor.IsCanceled;
                                }
                            });
                        }
                        if (finished)
                        {
                            writer.Flush();
                        }
                        return finished;
                    });
                    SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
                }
            }
            catch (Exception exception)
            {
                ShowMessageBox(owner, Resources.AbstractViewContext_Export_There_was_an_error_writing_to_the_file__ + exception.Message,
                    MessageBoxButtons.OK);
            }
        }

        /// <summary>
        /// Open a file stream, and call the provided function.
        /// If that function returns true, then commit the file stream.
        /// The default implementation of this method always commits the file stream, but this method
        /// can be overridden by other ViewContext's that need transactional filesystem support.
        /// </summary>
        protected virtual bool SafeWriteToFile(Control owner, string fileName, Func<Stream, bool> writeFunc)
        {
            using (var stream = File.OpenWrite(fileName))
            {
                return writeFunc(stream);
            }
        }

        public void CopyAll(Control owner, BindingListSource bindingListSource)
        {
            try
            {
                StringWriter tsvWriter = new StringWriter();
                var cloneableRowSource = bindingListSource.RowSource as ICloneableList;
                if (null == cloneableRowSource)
                {
                    var progressMonitor = new UncancellableProgressMonitor();
                    WriteData(progressMonitor, tsvWriter, bindingListSource, DataFormats.TSV.GetDsvWriter());
                }
                else
                {
                    var clonedList = cloneableRowSource.DeepClone();
                    if (!RunLongJob(owner, progressMonitor =>
                    {
                        using (var clonedBindingList = new BindingListSource())
                        {
                            SetViewFrom(bindingListSource, clonedList, clonedBindingList);
                            WriteData(progressMonitor, tsvWriter, clonedBindingList, DataFormats.TSV.GetDsvWriter());
                            progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).Complete());
                        }
                    }))
                    {
                        return;
                    }
                }
                DataObject dataObject = new DataObject();
                dataObject.SetText(tsvWriter.ToString());
                Clipboard.SetDataObject(dataObject);
            }
            catch (Exception exception)
            {
                ShowMessageBox(owner, 
                    Resources.AbstractViewContext_CopyAll_There_was_an_error_copying_the_data_to_the_clipboard__ + exception.Message, 
                    MessageBoxButtons.OK);
            }
        }

        protected virtual ViewEditor CreateViewEditor(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            return new ViewEditor(this, GetViewInfo(viewGroup, viewSpec));
        }
        
        public virtual ViewSpec CustomizeView(Control owner, ViewSpec viewSpec, ViewGroup viewPath)
        {
            using (var customizeViewForm = CreateViewEditor(viewPath, viewSpec))
            {
                if (FormUtil.ShowDialog(owner, customizeViewForm) == DialogResult.Cancel)
                {
                    return null;
                }
                // Consider: if save fails, reshow CustomizeViewForm?
                ViewInfo viewInfo = customizeViewForm.ViewInfo;
                viewInfo = new ViewInfo(viewInfo.ParentColumn, viewInfo.GetViewSpec().SetName(customizeViewForm.ViewName));
                SaveView(viewPath.Id, viewInfo.GetViewSpec(), viewSpec.Name);
                return viewInfo.GetViewSpec();
            }
        }

        public ViewSpec NewView(Control owner, ViewGroup viewPath)
        {
            return CustomizeView(owner, GetBlankView(), viewPath);
        }

        protected virtual ViewSpec GetBlankView()
        {
            return new ViewSpec().SetRowType(RowSources.First().RowType);
        }

        public void ManageViews(Control owner)
        {
            using (var manageViewsForm = new ManageViewsForm(this))
            {
                FormUtil.ShowDialog(owner, manageViewsForm);
            }
        }

        protected virtual void SaveView(ViewGroupId groupId, ViewSpec viewSpec, string originalName)
        {
            var viewSpecList = GetViewSpecList(groupId) ?? ViewSpecList.EMPTY;
            viewSpecList = viewSpecList.ReplaceView(originalName, viewSpec);
            SaveViewSpecList(groupId, viewSpecList);
        }

        public abstract void ExportViews(Control owner, ViewSpecList views);
        public abstract void ExportViewsToFile(Control owner, ViewSpecList views, string fileName);
        public abstract void ImportViews(Control owner, ViewGroup viewGroup);
        public abstract void ImportViewsFromFile(Control control, ViewGroup viewGroup, string fileName);

        public virtual void CopyViewsToGroup(Control control, ViewGroup viewGroup, ViewSpecList viewSpecList)
        {
            var currentViews = GetViewSpecList(viewGroup.Id);
            var conflicts = new HashSet<string>();
            foreach (var view in viewSpecList.ViewSpecs)
            {
                var existing = currentViews.GetView(view.Name);
                if (existing != null && !Equals(existing, view))
                {
                    conflicts.Add(view.Name);
                }
            }
            if (conflicts.Count > 0)
            {
                string message;
                if (conflicts.Count == 1)
                {
                    message = Resources.AbstractViewContext_CopyViewsToGroup_The_name___0___already_exists__Do_you_want_to_replace_it_;
                }
                else
                {
                    var messageLines = new List<String>();
                    messageLines.Add(Resources.AbstractViewContext_CopyViewsToGroup_The_following_names_already_exist_);
                    messageLines.AddRange(conflicts);
                    messageLines.Add(Resources.AbstractViewContext_CopyViewsToGroup_Do_you_want_to_replace_them_);
                    message = string.Join(Environment.NewLine, messageLines);
                }
                var result = ShowMessageBox(control, message, MessageBoxButtons.YesNoCancel);
                switch (result)
                {
                    case DialogResult.Cancel:
                        return;
                    case DialogResult.Yes:
                        currentViews = new ViewSpecList(currentViews.ViewSpecs.Where(view => !conflicts.Contains(view.Name)));
                        break;
                }
            }
            foreach (var view in viewSpecList.ViewSpecs)
            {
                if (null == currentViews.GetView(view.Name))
                {
                    currentViews = currentViews.ReplaceView(null, view);
                }
            }
            SaveViewSpecList(viewGroup.Id, currentViews);
        }

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
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                return constructor != null ? (DataGridViewColumn) constructor.Invoke(new object[0]) : null;
                // ReSharper restore ConditionIsAlwaysTrueOrFalse
            }
            catch (Exception exception)
            {
                Trace.TraceError("Exception constructing column of type {0}:{1}", columnTypeAttribute.ColumnType, exception); // Not L10N
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
            column.DefaultCellStyle.FormatProvider = DataSchema.DataSchemaLocalizer.FormatProvider;
            if (propertyDescriptor.IsReadOnly)
            {
                column.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245); // Lighter than Color.LightGray, which is still pretty dark actually
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
                    message = Resources.AbstractViewContext_OnDataError_An_unknown_error_occurred_updating_the_value_;
                }
                else
                {
                    message = string.Format(Resources.AbstractViewContext_OnDataError_,
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

        public virtual Image[] GetImageList()
        {
            return new Image[0];
        }

        public virtual int GetImageIndex(ViewSpec viewItem)
        {
            return -1;
        }

        protected void SetViewFrom(BindingListSource sourceBindingList, IEnumerable newRowSource,
            BindingListSource targetBindingList)
        {
            targetBindingList.SetView(sourceBindingList.ViewInfo, newRowSource);
            targetBindingList.RowFilter = sourceBindingList.RowFilter;
            if (sourceBindingList.SortDescriptions != null)
            {
                targetBindingList.ApplySort(sourceBindingList.SortDescriptions);
            }
        }

        protected class UncancellableProgressMonitor : IProgressMonitor
        {
            public bool IsCanceled
            {
                get { return false; }
            }

            public UpdateProgressResponse UpdateProgress(ProgressStatus status)
            {
                return UpdateProgressResponse.normal;
            }

            public bool HasUI
            {
                get { return false; }
            }
        }

        public virtual event Action ViewsChanged;
    }
}