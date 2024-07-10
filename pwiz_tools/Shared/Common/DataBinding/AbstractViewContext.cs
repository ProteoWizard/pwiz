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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.Properties;
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
            return viewInfo.ParentColumn.PropertyType.Name + (currentViewName == GetDefaultViewName() ? string.Empty : currentViewName);
        }
        public abstract bool RunLongJob(Control owner, Action<CancellationToken, IProgressMonitor> job);

        public virtual bool RunOnThisThread(Control owner, Action<CancellationToken, IProgressMonitor> job)
        {
            job(CancellationToken.None, new SilentProgressMonitor());
            return true;
        }
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

        public virtual void AddOrReplaceViews(ViewGroupId groupId, IEnumerable<ViewSpecLayout> viewSpecs)
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

        public ViewInfo GetViewInfo(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var rowSourceInfo = GetRowSourceInfo(viewSpec);
            if (null == rowSourceInfo)
            {
                return null;
            }
            return new ViewInfo(DataSchema, rowSourceInfo.RowType, viewSpec).ChangeViewGroup(viewGroup);
        }

        public virtual ViewInfo GetViewInfo(ViewName? viewName)
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

        public RowSourceInfo GetRowSourceInfo(ViewSpec viewSpec)
        {
            return RowSources.FirstOrDefault(rowSource => rowSource.Name == viewSpec.RowSource);
        }

        protected virtual string GetDefaultViewName()
        {
            return DefaultViewName;
        }

        public virtual IRowSource GetRowSource(ViewInfo viewInfo)
        {
            var rowSource = _rowSources.FirstOrDefault(rowSourceInfo => rowSourceInfo.Name == viewInfo.RowSourceName);
            if (rowSource == null)
            {
                return StaticRowSource.EMPTY;
            }
            return rowSource.Rows;
        } 

        public Icon ApplicationIcon { get; protected set; }

        protected virtual void WriteData(IProgressMonitor progressMonitor, TextWriter writer,
            BindingListSource bindingListSource, char separator)
        {
            IProgressStatus status = new ProgressStatus(string.Format(Resources.AbstractViewContext_WriteData_Writing__0__rows, bindingListSource.Count));
            WriteDataWithStatus(progressMonitor, ref status, writer, RowItemEnumerator.FromBindingListSource(bindingListSource), separator);
        }

        protected virtual void WriteDataWithStatus(IProgressMonitor progressMonitor, ref IProgressStatus status, TextWriter writer, RowItemEnumerator rowItemEnumerator, char separator)
        {
            var dsvWriter = CreateDsvWriter(separator, rowItemEnumerator.ColumnFormats);
            dsvWriter.WriteHeaderRow(writer, rowItemEnumerator.ItemProperties);
            var rowCount = rowItemEnumerator.Count;
            int startPercent = status.PercentComplete;
            int rowIndex = 0;
            while (rowItemEnumerator.MoveNext())
            {
                if (progressMonitor.IsCanceled)
                {
                    return;
                }
                int percentComplete = startPercent + (rowIndex * (100 - startPercent) / rowCount);
                if (percentComplete > status.PercentComplete)
                {
                    status = status.ChangeMessage(string.Format(Resources.AbstractViewContext_WriteData_Writing_row__0___1_, (rowIndex + 1), rowCount))
                        .ChangePercentComplete(percentComplete);
                    progressMonitor.UpdateProgress(status);
                }
                dsvWriter.WriteDataRow(writer, rowItemEnumerator.Current, rowItemEnumerator.ItemProperties);
                rowIndex++;
            }
        }

        /// <summary>
        /// Returns the list of options to show in the Save File dialog which comes up when the user exports a report.
        /// </summary>
        protected virtual IEnumerable<TabularFileFormat> ListAvailableExportFormats()
        {
            // These strings do not need to be localized, since they are only seen if this method has not been overridden.
            // SkylineViewContext overrides this method and uses the appropriately localized strings.
            yield return new TabularFileFormat(',', @"Comma Separated Values(*.csv)|*.csv");
            yield return new TabularFileFormat('\t', @"Tab Separated Values(*.tsv)|*.tsv");
        }

        public void Export(Control owner, BindingListSource bindingListSource)
        {
            try
            {
                var dataFormats = ListAvailableExportFormats().ToArray();
                string fileFilter = string.Join(@"|", dataFormats.Select(format => format.FileFilter).ToArray());
                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = fileFilter;
                    saveFileDialog.InitialDirectory = GetExportDirectory();
                    saveFileDialog.FileName = GetDefaultExportFilename(bindingListSource.ViewInfo);
                    if (saveFileDialog.ShowDialog(FormUtil.FindTopLevelOwner(owner)) == DialogResult.Cancel)
                    {
                        return;
                    }
                    var dataFormat = dataFormats[saveFileDialog.FilterIndex - 1];
                    ExportToFile(owner, bindingListSource, saveFileDialog.FileName, dataFormat.Separator);
                    SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
                }
            }
            catch (Exception exception)
            {
                ShowMessageBox(owner, Resources.AbstractViewContext_Export_There_was_an_error_writing_to_the_file__ + exception.Message,
                    MessageBoxButtons.OK);
            }
        }

        public virtual DsvWriter CreateDsvWriter(char separator, ColumnFormats columnFormats)
        {
            return new DsvWriter(DataSchema.DataSchemaLocalizer.FormatProvider, DataSchema.DataSchemaLocalizer.Language,
                separator)
            {
                ColumnFormats = columnFormats
            };
        }

        public void ExportToFile(Control owner, BindingListSource bindingListSource, String filename,
            char separator)
        {
            SafeWriteToFile(owner, filename, stream =>
            {
                var writer = new StreamWriter(stream, new UTF8Encoding(false));
                bool finished = false;
                RunOnThisThread(owner, (cancellationToken, progressMonitor) =>
                {
                    WriteData(progressMonitor, writer, bindingListSource, separator);
                    finished = !progressMonitor.IsCanceled;
                });
                if (finished)
                {
                    writer.Flush();
                }
                return finished;
            });
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
                if (!RunOnThisThread(owner, (cancellationToken, progressMonitor) =>
                    {
                        WriteData(progressMonitor, tsvWriter, bindingListSource, '\t');
                        progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).Complete());
                    }))
                {
                    return;
                }

                SetClipboardText(owner, tsvWriter.ToString());
            }
            catch (Exception exception)
            {
                ShowMessageBox(owner, 
                    Resources.AbstractViewContext_CopyAll_There_was_an_error_copying_the_data_to_the_clipboard__ + exception.Message, 
                    MessageBoxButtons.OK);
            }
        }

        protected virtual void SetClipboardText(Control owner, string text)
        {
            DataObject dataObject = new DataObject();
            dataObject.SetText(text);
            Clipboard.SetDataObject(dataObject);
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

        public virtual void SaveView(ViewGroupId groupId, ViewSpec viewSpec, string originalName)
        {
            var viewSpecList = GetViewSpecList(groupId) ?? ViewSpecList.EMPTY;
            viewSpecList = viewSpecList.ReplaceView(originalName, new ViewSpecLayout(viewSpec, viewSpecList.GetViewLayouts(viewSpec.Name)));
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
            foreach (var view in viewSpecList.ViewSpecLayouts)
            {
                var existing = currentViews.GetViewSpecLayout(view.Name);
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
                    message = string.Format(Resources.AbstractViewContext_CopyViewsToGroup_The_name___0___already_exists__Do_you_want_to_replace_it_, conflicts.First());
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
                        currentViews = currentViews.Filter(view => !conflicts.Contains(view.Name));
                        break;
                }
            }
            foreach (var view in viewSpecList.ViewSpecLayouts)
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
                var constructor = columnTypeAttribute.ColumnType.GetConstructor(Array.Empty<Type>());
                Debug.Assert(null != constructor);
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                // ReSharper disable ConstantConditionalAccessQualifier
                return (DataGridViewColumn) constructor?.Invoke(Array.Empty<object>());
                // ReSharper restore ConstantConditionalAccessQualifier
                // ReSharper restore ConditionIsAlwaysTrueOrFalse
            }
            catch (Exception exception)
            {
                Trace.TraceError(@"Exception constructing column of type {0}:{1}", columnTypeAttribute.ColumnType, exception);
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
            var dataPropertyDescriptor = propertyDescriptor as DataPropertyDescriptor;
            if (dataPropertyDescriptor != null)
            {
                var format = (FormatAttribute) dataPropertyDescriptor.Attributes[typeof(FormatAttribute)];
                if (format != null)
                {
                    if (null != format.Format)
                    {
                        column.DefaultCellStyle.Format = format.Format;
                    }
                    if (null != format.NullValue && propertyDescriptor.IsReadOnly)
                    {
                        column.DefaultCellStyle.NullValue = format.NullValue;
                    }
                    if (format.Width != 0)
                    {
                        column.Width = format.Width;
                    }
                }
            }
            column.DefaultCellStyle.FormatProvider = DataSchema.DataSchemaLocalizer.FormatProvider;
            if (propertyDescriptor.IsReadOnly)
            {
                column.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245); // Lighter than Color.LightGray, which is still pretty dark actually
            }
            if (!string.IsNullOrEmpty(propertyDescriptor.Description))
            {
                column.ToolTipText = propertyDescriptor.Description;
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
            var viewSpec = new ViewSpec().SetName(DefaultViewName).SetRowType(parentColumn.PropertyType).SetUiMode(parentColumn.UiMode);
            var columns = new List<ColumnSpec>();
            foreach (var column in parentColumn.GetChildColumns())
            {
                if (null != column.DataSchema.GetCollectionInfo(column.PropertyType))
                {
                    continue;
                }
                if (column.DataSchema.IsHidden(column))
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
            return Array.Empty<Image>();
        }

        public virtual int GetImageIndex(ViewSpec viewItem)
        {
            return -1;
        }

        protected void SetViewFrom(BindingListSource sourceBindingList, IRowSource newRowSource,
            BindingListSource targetBindingList)
        {
            targetBindingList.SetView(sourceBindingList.ViewInfo, newRowSource);
            targetBindingList.RowFilter = sourceBindingList.RowFilter;
            if (sourceBindingList.SortDescriptions != null)
            {
                targetBindingList.ApplySort(sourceBindingList.SortDescriptions);
            }
        }

        public ViewLayoutList GetViewLayoutList(ViewName viewName)
        {
            return GetViewSpecList(viewName.GroupId).GetViewLayouts(viewName.Name);
        }

        public void SetViewLayoutList(ViewGroupId viewGroup, ViewLayoutList list)
        {
            var viewSpecList = GetViewSpecList(viewGroup);
            viewSpecList = viewSpecList.SaveViewLayouts(list);
            SaveViewSpecList(viewGroup, viewSpecList);
        }

        public virtual bool HasRowActions
        {
            get { return false; }
        }
        public virtual void RowActionsDropDownOpening(ToolStripItemCollection dropDownItems)
        {
            dropDownItems.Clear();
        }

        public virtual IEnumerable<IUiModeInfo> AvailableUiModes
        {
            get { yield break; }
        }

        public virtual void ToggleClustering(BindingListSource bindingListSource, bool turnClusteringOn)
        {
            if (null == bindingListSource.ClusteringSpec)
            {
                bindingListSource.ClusteringSpec = ClusteringSpec.DEFAULT;
            }
            else
            {
                if (!bindingListSource.IsComplete && !(bindingListSource.ReportResults is ClusteredReportResults))
                {
                    return;
                }
                bindingListSource.ClusteringSpec = null;
            }

        }

        // Default implementation of ViewsChanged which never fires.
        // SkylineViewContext overrides and uses this event 
#pragma warning disable 67
        public virtual event Action ViewsChanged;

        public virtual bool CanDisplayView(ViewSpec viewSpec)
        {
            return null != GetRowSourceInfo(viewSpec);
        }
    }
}
