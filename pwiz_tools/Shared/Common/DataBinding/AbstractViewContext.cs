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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Base implementation of the <see cref="IViewContext"/> interface
    /// </summary>
    public abstract class AbstractViewContext : IViewContext
    {
        public const string DefaultViewName = "default";
        private IList<ViewSpec> _builtInViewSpecs;
        protected AbstractViewContext(ColumnDescriptor parentColumn)
        {
            ParentColumn = parentColumn;
            BuiltInViewSpecs = new[] {GetDefaultViewSpec(parentColumn)};
        }
        protected AbstractViewContext(ColumnDescriptor parentColumn, IEnumerable<ViewSpec> builtInViewSpecs)
        {
            ParentColumn = parentColumn;
            BuiltInViewSpecs = builtInViewSpecs;
        }

        public abstract string GetExportDirectory();
        public abstract void SetExportDirectory(string value);
        public abstract DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons);
        public virtual string GetDefaultExportFilename(ViewSpec viewSpec)
        {
            string currentViewName = viewSpec.Name;
            return ParentColumn.PropertyType.Name + (currentViewName == GetDefaultViewName() ? "" : currentViewName);
        }
        public abstract void RunLongJob(Control owner, Action<IProgressMonitor> job);
        public ColumnDescriptor ParentColumn { get; protected set; }
        public IEnumerable<ViewSpec> BuiltInViewSpecs
        {
            get { return _builtInViewSpecs; }
            set { _builtInViewSpecs = ImmutableList.ValueOf(value); }
        }
        public abstract IEnumerable<ViewSpec> CustomViewSpecs { get;}
        protected abstract void SetCustomViewSpecs(IEnumerable<ViewSpec> values);
        public virtual string GetNewViewName()
        {
            var takenNames = new HashSet<string>(BuiltInViewSpecs.Select(viewSpec => viewSpec.Name));
            takenNames.UnionWith(CustomViewSpecs.Select(viewSpec=>viewSpec.Name));
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

        public Icon ApplicationIcon { get; protected set; }

        protected virtual void WriteData(IProgressMonitor progressMonitor, TextWriter writer, BindingListSource bindingListSource, IDataFormat dataFormat)
        {
            IList<RowItem> rows = Array.AsReadOnly(bindingListSource.Cast<RowItem>().ToArray());
            IList<PropertyDescriptor> properties = bindingListSource.GetItemProperties(new PropertyDescriptor[0]).Cast<PropertyDescriptor>().ToArray();
            var status = new ProgressStatus("Writing " + rows.Count + " rows");
            dataFormat.WriteRow(writer, properties.Select(pd=>pd.DisplayName));
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
                var row = rows[rowIndex];
                // TODO(nicksh): Format the column values.
                dataFormat.WriteRow(writer, properties.Select(pd =>
                    {
                        var columnPropertyDescriptor = pd as ColumnPropertyDescriptor;
                        if (null == columnPropertyDescriptor)
                        {
                            return pd.GetValue(row);
                        }
                        return columnPropertyDescriptor.DisplayColumn.GetValue(row, columnPropertyDescriptor.PivotKey, false);
                    }));
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
                FileName = GetDefaultExportFilename(bindingListSource.ViewSpec),
            })
            {
                if (saveFileDialog.ShowDialog(owner.TopLevelControl) == DialogResult.Cancel)
                {
                    return;
                }
                var dataFormat = dataFormats[saveFileDialog.FilterIndex - 1];
                RunLongJob(owner, progressMonitor =>
                {
// ReSharper disable AccessToDisposedClosure
                    using (var writer = new StreamWriter(File.OpenWrite(saveFileDialog.FileName), new UTF8Encoding(false)))
// ReSharper restore AccessToDisposedClosure
                    {
                        WriteData(progressMonitor, writer, bindingListSource, dataFormat);
                    }
                });
                SetExportDirectory(Path.GetDirectoryName(saveFileDialog.FileName));
            }
        }

        protected virtual bool IsReadOnly(ViewSpec viewSpec)
        {
            return BuiltInViewSpecs.Any(builtInViewSpec => viewSpec.Name == builtInViewSpec.Name);
        }

        public virtual ViewSpec MakeEditable(ViewSpec viewSpec)
        {
            if (!IsReadOnly(viewSpec) && !string.IsNullOrEmpty(viewSpec.Name))
            {
                return viewSpec;
            }
            var viewNames = new HashSet<string>(BuiltInViewSpecs.Select(builtInViewSpec => builtInViewSpec.Name));
            viewNames.UnionWith(CustomViewSpecs.Select(customViewSpec=>customViewSpec.Name));
            for (int index = 1; ;index++ )
            {
                string name = "CustomView" + index;
                if (!viewNames.Contains(name))
                {
                    return viewSpec.SetName(name);
                }
            }
        }

        public ViewSpec CustomizeView(Control owner, ViewSpec viewSpec)
        {
            viewSpec = MakeEditable(viewSpec);
            using (var customizeViewForm = new CustomizeViewForm(this, viewSpec))
            {
                if (customizeViewForm.ShowDialog(owner.TopLevelControl) == DialogResult.Cancel)
                {
                    return null;
                }
                // Consider: if save fails, reshow CustomizeViewForm?
                return SaveView(customizeViewForm.ViewSpec);
            }
        }

        public void ManageViews(Control owner)
        {
            using (var manageViewsForm = new ManageViewsForm(this))
            {
                manageViewsForm.ShowDialog(owner.TopLevelControl);
            }
        }

        public virtual ViewSpec GetBlankView()
        {
            return MakeEditable(new ViewSpec());
        }

        public virtual ViewSpec SaveView(ViewSpec newViewSpec)
        {
            var viewSpecs = new List<ViewSpec>(CustomViewSpecs);
            var existingIndex = viewSpecs.FindIndex(vs => vs.Name == newViewSpec.Name);
            if (existingIndex >= 0)
            {
                viewSpecs[existingIndex] = newViewSpec;
            }
            else
            {
                viewSpecs.Add(newViewSpec);
            }
            SetCustomViewSpecs(viewSpecs);
            return newViewSpec;
        }

        public void DeleteViews(IEnumerable<ViewSpec> viewSpecs)
        {
            var deletedViews = new HashSet<ViewSpec>(viewSpecs);
            SetCustomViewSpecs(CustomViewSpecs.Where(viewSpec=>!deletedViews.Contains(viewSpec)));
        }

        public abstract IViewContext GetViewContext(ColumnDescriptor column);

        public virtual DataGridViewColumn CreateGridViewColumn(PropertyDescriptor propertyDescriptor)
        {
            if (!propertyDescriptor.IsReadOnly)
            {
                var valueList = GetValueList(propertyDescriptor);
                if (valueList != null)
                {
                    var result = new DataGridViewComboBoxColumn
                        {
                        Name = propertyDescriptor.Name,
                        DataPropertyName = propertyDescriptor.Name,
                        HeaderText = propertyDescriptor.DisplayName,
                    };
                    foreach (var value in valueList)
                    {
                        result.Items.Add(value);
                    }
                    return result;
                }
            }
            if (propertyDescriptor.PropertyType == typeof(bool))
            {
                return new DataGridViewCheckBoxColumn
                    {
                               Name = propertyDescriptor.Name,
                               DataPropertyName = propertyDescriptor.Name,
                               HeaderText = propertyDescriptor.DisplayName,
                           };
            }
            var columnPropertyDescriptor = propertyDescriptor as ColumnPropertyDescriptor;
            if (columnPropertyDescriptor == null)
            {
                return new DataGridViewTextBoxColumn
                    {
                               Name = propertyDescriptor.Name,
                               DataPropertyName = propertyDescriptor.Name,
                               HeaderText = propertyDescriptor.DisplayName
                           };
            }
            return new ViewColumn(this, columnPropertyDescriptor);
        }
        public virtual IEnumerable GetValueList(PropertyDescriptor propertyDescriptor)
        {
            if (propertyDescriptor.PropertyType.IsEnum)
            {
                return Enum.GetValues(propertyDescriptor.PropertyType);
            }
            if (propertyDescriptor.PropertyType.IsGenericType && propertyDescriptor.PropertyType.GetGenericTypeDefinition() == typeof(Nullable))
            {
                return new object[] {null}.Concat(Enum.GetValues(propertyDescriptor.PropertyType).Cast<object>());
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

        public static ViewSpec GetDefaultViewSpec(ColumnDescriptor parentColumn)
        {
            return new ViewSpec().SetName(DefaultViewName).SetColumns(parentColumn.GetChildColumns()
                .Select(c => new ColumnSpec(PropertyPath.Root.Property(c.Name))));
        }
    }
}