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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Base implementation of the <see cref="IViewContext"/> interface
    /// </summary>
    public abstract class AbstractViewContext : IViewContext
    {
        protected AbstractViewContext(ColumnDescriptor parentColumn)
        {
            ParentColumn = parentColumn;
            BuiltInViewSpecs = new ViewSpec[0];
        }

        public abstract string GetExportDirectory();
        public abstract void SetExportDirectory(string value);
        public abstract DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons);
        public virtual string GetDefaultExportFilename(BindingListView bindingListView)
        {
            string currentViewName = bindingListView.ViewInfo.Name;
            return ParentColumn.PropertyType.Name + currentViewName == GetDefaultViewName() ? "" : currentViewName;
        }
        public abstract void RunLongJob(Control owner, Action<IProgressMonitor> job);
        public ColumnDescriptor ParentColumn { get; protected set; }
        public IEnumerable<ViewSpec> BuiltInViewSpecs
        {
            get; protected set;
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
            return "default";
        }

        protected virtual void WriteData(IProgressMonitor progressMonitor, TextWriter writer, BindingListView bindingListView, IDataFormat dataFormat)
        {
            var tempBindingListView = new BindingListView(new ViewInfo(ParentColumn, bindingListView.GetViewSpec()), bindingListView.ToArray());
            var tempDataGridView = new BoundDataGridView
                                       {
                                           DataSource = new BindingSource(tempBindingListView, ""),
                                           SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                                           ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText,
                                           RowHeadersVisible = false,
                                           Visible = false,
                                           BindingContext = new BindingContext(),
                                           AllowUserToAddRows = false,
                                       };
            using (tempDataGridView)
            {
                var status = new ProgressStatus("Writing " + tempDataGridView.Rows.Count + " rows");
                dataFormat.WriteRow(writer, tempDataGridView.Columns.Cast<DataGridViewColumn>().Select(column=>column.HeaderCell.EditedFormattedValue));
                var rowCount = tempDataGridView.Rows.Count;
                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    if (progressMonitor.IsCanceled)
                    {
                        return;
                    }
                    status = status.ChangeMessage("Writing row " + (rowIndex + 1) + "/" + rowCount)
                        .ChangePercentComplete(rowIndex*100/rowCount);
                    progressMonitor.UpdateProgress(status);
                    var row = tempDataGridView.Rows.SharedRow(rowIndex);
                    dataFormat.WriteRow(writer, 
                                        row.Cells.Cast<DataGridViewCell>()
                                            .Select(cell => cell.GetEditedFormattedValue(rowIndex, DataGridViewDataErrorContexts.Formatting | DataGridViewDataErrorContexts.ClipboardContent)));
                }
            }
        }

        public static string Escape(object value)
        {
            if (value == null)
            {
                return "";
            }
            var strValue = value.ToString();
            var result = new StringBuilder("\"", strValue.Length + 2);
            foreach (var ch in strValue)
            {
                if (ch == '"')
                {
                    result.Append('"');
                }
                result.Append(ch);
            }
            result.Append('"');
            return result.ToString();
        }

        public void Export(Control owner, BindingListView bindingListView)
        {
            var dataFormats = new[] { DataFormats.Csv, DataFormats.Tsv };
            string fileFilter = string.Join("|", dataFormats.Select(format => format.FileFilter).ToArray());
            using (var saveFileDialog = new SaveFileDialog()
            {
                Filter = fileFilter,
                InitialDirectory = GetExportDirectory(),
                FileName = GetDefaultExportFilename(bindingListView),
            })
            {
                if (saveFileDialog.ShowDialog(owner.TopLevelControl) == DialogResult.Cancel)
                {
                    return;
                }
                var dataFormat = dataFormats[saveFileDialog.FilterIndex - 1];
                RunLongJob(owner, progressMonitor =>
                {
                    using (var writer = new StreamWriter(File.OpenWrite(saveFileDialog.FileName), new UTF8Encoding(false)))
                    {
                        WriteData(progressMonitor, writer, bindingListView, dataFormat);
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
    }
}