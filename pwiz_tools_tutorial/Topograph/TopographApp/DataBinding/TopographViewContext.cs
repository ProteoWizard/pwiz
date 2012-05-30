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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.ui.DataBinding;
using pwiz.Topograph.ui.Forms;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui
{
    public class TopographViewContext : AbstractViewContext
    {
        public TopographViewContext(Workspace workspace, Type rowType, IEnumerable<ViewSpec> builtInViews) : this(workspace, rowType)
        {
            Workspace = workspace;
            BuiltInViewSpecs = builtInViews;
        }

        public TopographViewContext(Workspace workspace, Type rowType) : base(new ColumnDescriptor(new TopographDataSchema(workspace), rowType))
        {
            var childColumns = ParentColumn.GetChildColumns().Select(c => new ColumnSpec(new IdentifierPath(IdentifierPath.Root, c.Name)));
            BuiltInViewSpecs = Array.AsReadOnly(new[] { new ViewSpec().SetName("default").SetColumns(childColumns)});
        }

        public Type RowType { get { return ParentColumn.PropertyType; } }
        public Workspace Workspace { get; private set; }
        public new IEnumerable<ViewSpec> BuiltInViewSpecs 
        {
            get
            {
                return base.BuiltInViewSpecs;
            }
            set
            {
                base.BuiltInViewSpecs = value;
            }
        }

        public override string GetExportDirectory()
        {
            Settings.Default.Reload();
            return Settings.Default.ExportResultsDirectory;
        }
        public override void SetExportDirectory(string directory)
        {
            if (directory == GetExportDirectory())
            {
                return;
            }
            Settings.Default.ExportResultsDirectory = directory;
            Settings.Default.Save();
        }

        public override string GetDefaultExportFilename(ViewSpec viewSpec)
        {
            return Path.GetFileNameWithoutExtension(Workspace.DatabasePath) + base.GetDefaultExportFilename(viewSpec);
        }

        private string ViewSpecKey
        {
            get
            {
                return RowType.FullName;
            }
        }
        public override IEnumerable<ViewSpec> CustomViewSpecs
        {
            get
            {
                Settings.Default.Reload();
                var viewSpecLists = Settings.Default.ViewSpecLists;
                if (viewSpecLists != null)
                {
                    var viewSpecList = viewSpecLists.FirstOrDefault(vsl => ViewSpecKey == vsl.Name);
                    if (viewSpecList != null)
                    {
                        return viewSpecList.ViewSpecs;
                    }
                }
                return new ViewSpec[0];
            }
        }
        protected override void SetCustomViewSpecs(IEnumerable<ViewSpec> value)
        {
            var newValue = value.ToArray();
            Array.Sort(newValue);
            if (CustomViewSpecs.SequenceEqual(newValue))
            {
                return;
            }
            var viewSpecLists = Settings.Default.ViewSpecLists;
            var newViewSpecList = newValue.Length == 0 ? null : new ViewSpecList
                                   {
                                       Name = ViewSpecKey,
                                       ViewSpecs = new ReadOnlyCollection<ViewSpec>(newValue),
                                   };
            var existingIndex = viewSpecLists.FindIndex(vsl => ViewSpecKey == vsl.Name);
            if (existingIndex >= 0)
            {
                if (newViewSpecList == null)
                {
                    viewSpecLists.RemoveAt(existingIndex);
                }
                else
                {
                    viewSpecLists[existingIndex] = newViewSpecList;
                }
            }
            else if (newViewSpecList != null)
            {
                viewSpecLists.Add(newViewSpecList);
            }
            Settings.Default.ViewSpecLists = viewSpecLists;
            Settings.Default.Save();
        }
        public override void RunLongJob(Control owner, Action<IProgressMonitor> job)
        {
            using (var longWaitDialog = new LongWaitDialog(owner.TopLevelControl, Program.AppName))
            {
                var longOperationBroker =
                    new LongOperationBroker(
                        broker =>
                        job.Invoke(ProgressMonitorImpl.NewProgressMonitorImpl(new ProgressStatus("Working"),
                                                                              iProgress =>
                                                                                  {
                                                                                      try
                                                                                      {
                                                                                          broker.
                                                                                              UpdateStatusMessage(
                                                                                                  iProgress + "% complete");
                                                                                          return true;
                                                                                      }
                                                                                      catch (JobCancelledException)
                                                                                      {
                                                                                          return false;
                                                                                      }
                                                                                  })), longWaitDialog);
                longOperationBroker.LaunchJob();
            }
        }
        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons)
        {
            return MessageBox.Show(owner.TopLevelControl, message, Program.AppName, messageBoxButtons);
        }

        public override IViewContext GetViewContext(ColumnDescriptor column)
        {
            return new TopographViewContext(Workspace, column.PropertyType);
        }
    }
}
