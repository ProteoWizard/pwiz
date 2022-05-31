/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline
{
    ///
    /// For testing and debugging Skyline command-line interface
    ///
    public class CommandLineUI
    {
        private readonly CommandArgs _commandArgs;
        private SkylineWindow SkylineWindow { get { return Program.MainWindow; } }

        public CommandLineUI(string[] args, CommandStatusWriter consoleOut)
        {
            _commandArgs = new CommandArgs(consoleOut, false);
            if (!_commandArgs.ParseArgs(args))
            {
                consoleOut.WriteLine(Resources.CommandLine_Run_Exiting___);
                return;
            }

            Program.UnitTest = Program.FunctionalTest = true;
            Program.TestExceptions = new List<Exception>();
            Program.NoSaveSettings = true;
            Program.DisableJoining = _commandArgs.ImportDisableJoining;
            Program.NoAllChromatogramsGraph = _commandArgs.NoAllChromatogramsGraph;
            Settings.Default.AutoShowAllChromatogramsGraph = !_commandArgs.HideAllChromatogramsGraph;
            LocalizationHelper.InitThread();

            // Run test in new thread (Skyline on main thread).
            Program.Init();
            Settings.Default.SrmSettingsList[0] = SrmSettingsList.GetDefault();
            // Reset defaults with names from resources for testing different languages
            Settings.Default.BackgroundProteomeList[0] = BackgroundProteomeList.GetDefault();
            Settings.Default.DeclusterPotentialList[0] = DeclusterPotentialList.GetDefault();
            Settings.Default.RetentionTimeList[0] = RetentionTimeList.GetDefault();
            Settings.Default.ShowStartupForm = false;

            var threadTest = new Thread(Run);
            LocalizationHelper.InitThread(threadTest);
            threadTest.Start();
            Program.Main();
            threadTest.Join();
        }

        private void Run()
        {
            WaitForSkyline();
            OpenDocument(_commandArgs.SkylineFile);
            SkylineWindow.DiscardChanges = true;
            RunUI(SkylineWindow.Close);
        }

        private void WaitForSkyline()
        {
            while (Program.MainWindow == null || !Program.MainWindow.IsHandleCreated)
            {
                Thread.Sleep(10);
            }
        }

        private void OpenDocument(string documentFile)
        {
            RunUI(() => SkylineWindow.OpenFile(documentFile));
            WaitForDocumentLoaded();
        }

        private void RunUI(Action action)
        {
            SkylineWindow.Invoke(action);
        }

        private void WaitForDocumentLoaded()
        {
            WaitForConditionUI(() => Program.MainWindow.Document != null && Program.MainWindow.Document.IsLoaded);
            WaitForProteinMetadataBackgroundLoaderCompleted();  // make sure document is stable
        }

        private void WaitForProteinMetadataBackgroundLoaderCompleted()
        {
            // In a functional test we expect the protein metadata search to at least pretend to have gone to the web
            WaitForConditionUI(() => ProteinMetadataManager.IsLoadedDocument(Program.MainWindow.Document));
        }


        // Import results from one or more data files.
        private void ImportResults(MsDataFileUri msDataFileUri)
        {
            RunDlg<ImportResultsDlg>(Program.MainWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.NamedPathSets =
                    importResultsDlg.GetDataSourcePathsFileReplicates(new[] { msDataFileUri });
                importResultsDlg.OkDialog();
            });

            WaitForConditionUI(() =>
            {
                var document = Program.MainWindow.DocumentUI;
                return document.Settings.HasResults && document.Settings.MeasuredResults.IsLoaded;
            });
        }

        private void WaitForConditionUI(Func<bool> func)
        {
            while (true)
            {
                if (Program.MainWindow != null)
                {
                    bool exit = false;
                    RunUI(() => exit = func());
                    if (exit)
                        break;
                }
                Thread.Sleep(100);
            }
        }

        private void RunDlg<TDlg>(Action show, Action<TDlg> act) where TDlg : Form
        {
            TDlg dlg = ShowDialog<TDlg>(show);
            RunUI(() =>
            {
                if (act != null)
                    act(dlg);
                else
                    dlg.CancelButton.PerformClick();
            });
            WaitForClosedForm(dlg);
        }

        private static TDlg ShowDialog<TDlg>(Action act) where TDlg : Form
        {
            Program.MainWindow.BeginInvoke(act);
            return WaitForOpenForm<TDlg>();
        }

        public void WaitForClosedForm(Form formClose)
        {
            while (true)
            {
                bool isOpen = true;
                RunUI(() => isOpen = IsFormOpen(formClose));
                if (!isOpen)
                    return;
                Thread.Sleep(50);
            }
        }

        private static bool IsFormOpen(Form form)
        {
            foreach (var formOpen in OpenForms)
            {
                if (ReferenceEquals(form, formOpen))
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<Form> OpenForms
        {
            get
            {
                return FormUtil.OpenForms;
            }
        }

        private static TDlg WaitForOpenForm<TDlg>() where TDlg : Form
        {
            while (true)
            {
                TDlg tForm = FindOpenForm<TDlg>();
                if (tForm != null)
                    return tForm;

                Thread.Sleep(50);
            }
        }

        private static TDlg FindOpenForm<TDlg>() where TDlg : Form
        {
            foreach (var form in OpenForms)
            {
                var tForm = form as TDlg;
                if (tForm != null && tForm.Created)
                {
                    return tForm;
                }
            }
            return null;
        }
    }
}