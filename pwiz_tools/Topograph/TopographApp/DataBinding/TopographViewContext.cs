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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.ui.Forms;
using pwiz.Topograph.ui.Properties;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Util;

namespace pwiz.Topograph.ui.DataBinding
{
    public class TopographViewContext : AbstractViewContext
    {
        public TopographViewContext(Workspace workspace, Type rowType, IEnumerable rows, params ViewSpec[] builtInViews) 
            : base(new TopographDataSchema(workspace), new[]
            {
                new RowSourceInfo(rowType, rows, builtInViews.Select(
                    viewSpec=>new ViewInfo(new TopographDataSchema(workspace), rowType, viewSpec)))
            })
        {
            Workspace = workspace;
        }

        public TopographViewContext(Workspace workspace, Type rowType, IEnumerable rows)
            : this(workspace, rowType, rows, GetDefaultViewSpec(workspace, rowType))
        {
        }

        public Type RowType { get { return RowSources.First().RowType; } }

        public override IEnumerable<ViewGroup> ViewGroups
        {
            get { return new[] {new TopographViewGroup(RowType)}; }
        }

        public Workspace Workspace { get; private set; }
        public DeleteHandler DeleteHandler { get; set; }

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

        public override ViewGroup DefaultViewGroup
        {
            get { return ViewGroups.First(); }
        }

        protected override void SaveViewSpecList(ViewGroupId viewGroupId, ViewSpecList viewSpecList)
        {
            Settings.Default.Reload();
            Settings.Default.TopographViewGroups.SetViewSpecList(viewGroupId, viewSpecList);
            Settings.Default.Save();
        }

        public override ViewSpecList GetViewSpecList(ViewGroupId viewGroup)
        {
            return base.GetViewSpecList(viewGroup) 
                ?? Settings.Default.TopographViewGroups.GetViewSpecList(viewGroup) 
                ?? ViewSpecList.EMPTY;
        }

        public override void ExportViewsToFile(Control owner, ViewSpecList views, string fileName)
        {
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            using (FileStream stream = File.OpenWrite(fileName))
            {
                xmlSerializer.Serialize(stream, views);
                stream.Close();
            }
        }

        public override void ImportViewsFromFile(Control control, ViewGroup viewGroup, string fileName)
        {
            ViewSpec[] views;
            try
            {
                views = LoadViews(fileName).ToArray();
            }
            catch (Exception x)
            {
                ShowMessageBox(control, string.Format("Failure loading {0}:\n{1}", fileName, x.InnerException), MessageBoxButtons.OK);
                return;
            }
            if (views.Length == 0)
            {
                ShowMessageBox(control, "No views were found in that file.", MessageBoxButtons.OK);
                return;
            }
            CopyViewsToGroup(control, viewGroup, new ViewSpecList(views));
        }
        
        private const string ViewFileFilter = "tpgview Files (*.tpgview)|*.tpgview";

        public override void ExportViews(Control owner, ViewSpecList viewSpecList)
        {
            using (var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = Settings.Default.ExportResultsDirectory,
                CheckPathExists = true,
                Filter = ViewFileFilter,
            })
            {
                saveFileDialog.ShowDialog(owner);
                if (!string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    ExportViews(owner, viewSpecList);
                }
            }
        }

        public override void ImportViews(Control owner, ViewGroup viewGroup)
        {
            using (var importDialog = new OpenFileDialog
            {
                InitialDirectory = Settings.Default.ExportResultsDirectory,
                CheckPathExists = true,
                Filter = ViewFileFilter,
            })
            {
                importDialog.ShowDialog(owner);
                if (string.IsNullOrEmpty(importDialog.FileName))
                {
                    return;
                }
                ImportViewsFromFile(owner, viewGroup, importDialog.FileName);
            }
        }
        protected IEnumerable<ViewSpec> LoadViews(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var viewSerializer = new XmlSerializer(typeof(ViewSpecList));
                ViewSpecList viewSpecList = (ViewSpecList)viewSerializer.Deserialize(stream);
                return viewSpecList.ViewSpecs;
            }
        }



        public override bool RunLongJob(Control owner, Action<IProgressMonitor> job)
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
                return !longOperationBroker.WasCancelled;
            }
        }
        public override DialogResult ShowMessageBox(Control owner, string message, MessageBoxButtons messageBoxButtons)
        {
            return MessageBox.Show(owner.TopLevelControl, message, Program.AppName, messageBoxButtons);
        }

        public override bool DeleteEnabled
        {
            get
            {
                return null != DeleteHandler && DeleteHandler.DeleteEnabled;
            }
        }

        public override void Delete()
        {
            if (null != DeleteHandler)
            {
                DeleteHandler.Delete();
            }
        }



        protected static ViewSpec GetDefaultViewSpec(Workspace workspace, Type type)
        {
            var parentColumn = ColumnDescriptor.RootColumn(new TopographDataSchema(workspace), type);
            var childColumns = parentColumn.GetChildColumns().Select(c => new ColumnSpec(PropertyPath.Root.Property(c.Name)));
            return new ViewSpec().SetName("default").SetColumns(childColumns);
        }
    }
}
