/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Find;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Themes;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// A server that implements functionality for interactive Skyline tools.
    /// </summary>
    public class ToolService : RemoteService, IToolService
    {
        private  readonly Dictionary<string, DocumentChangeSender> _documentChangeSenders = 
            new Dictionary<string,DocumentChangeSender>();

        private readonly SkylineWindow _skylineWindow;

        public ToolService(string serviceName, SkylineWindow skylineWindow) : base(serviceName)
        {
            _skylineWindow = skylineWindow;
        }

        /// <summary>
        /// Get a named report.
        /// </summary>
        /// <param name="toolName">Tool name.</param>
        /// <param name="reportName">Report name.</param>
        /// <returns>Report as a string.</returns>
        public string GetReport(string toolName, string reportName)
        {
            var report = new StringWriter();
            ToolDescriptionHelpers.GetReport(Program.MainWindow.Document, reportName, toolName, Program.MainWindow, report);
            return report.ToString();
        }

        public string GetReportFromDefinition(string reportDefinition)
        {
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(reportDefinition));
            var reportOrViewSpecList = ReportSharing.DeserializeReportList(memoryStream);
            if (reportOrViewSpecList.Count == 0)
            {
                throw new ArgumentException(@"No report definition found");
            }
            if (reportOrViewSpecList.Count > 1)
            {
                throw new ArgumentException(@"Too many report definitions");
            }
            var reportOrViewSpec = reportOrViewSpecList.First();
            if (null == reportOrViewSpec.ViewSpecLayout)
            {
                throw new ArgumentException(@"The report definition uses the old format.");
            }
            return GetReportRows(Program.MainWindow.Document, reportOrViewSpec.ViewSpecLayout, Program.MainWindow);
        }

        private string GetReportRows(SrmDocument document, ViewSpecLayout viewSpec, IProgressMonitor progressMonitor)
        {
            var container = new MemoryDocumentContainer();
            container.SetDocument(document, container.Document);
            var dataSchema = new SkylineDataSchema(container, DataSchemaLocalizer.INVARIANT);
            var viewContext = new Skyline.Controls.Databinding.DocumentGridViewContext(dataSchema);
            IProgressStatus status = new ProgressStatus(string.Format(Resources.ReportSpec_ReportToCsvString_Exporting__0__report,
                viewSpec.Name));
            var writer = new StringWriter();
            if (viewContext.Export(CancellationToken.None, progressMonitor, ref status, viewContext.GetViewInfo(null, viewSpec.ViewSpec), viewSpec.DefaultViewLayout, writer,
                    TextUtil.SEPARATOR_CSV))
            {
                return writer.ToString();
            }
            return null;
        }

        [Obsolete]
        public DocumentLocation GetDocumentLocation()
        {
            DocumentLocation documentLocation = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                if (!_skylineWindow.SelectedPath.Equals(new IdentityPath(Controls.SequenceTree.NODE_INSERT_ID)))
                {
                    documentLocation = new DocumentLocation(_skylineWindow.SequenceTree.SelectedPath.ToGlobalIndexList());
                    if (_skylineWindow.Document.Settings.HasResults)
                    {
                        var chromatogramSet =
                            _skylineWindow.Document.Settings.MeasuredResults.Chromatograms[
                                _skylineWindow.SelectedResultsIndex];
                        documentLocation = documentLocation.SetChromFileId(
                            chromatogramSet.MSDataFileInfos.First().FileId.GlobalIndex);
                    }
                }
            }));
            return documentLocation;
        }

        /// <summary>
        /// Select a document location in Skyline's tree view.
        /// </summary>
        /// <param name="documentLocation">Which location to select (null for insert node).</param>
        [Obsolete]
        public void SetDocumentLocation(DocumentLocation documentLocation)
        {
            Program.MainWindow.Invoke(new Action(() =>
            {
                if (documentLocation == null)
                    Program.MainWindow.SelectPath(new IdentityPath(Controls.SequenceTree.NODE_INSERT_ID));
                else
                {
                    Bookmark bookmark = Bookmark.ToBookmark(documentLocation, Program.MainWindow.DocumentUI);
                    Program.MainWindow.NavigateToBookmark(bookmark);
                }
            }));
        }

        public string GetDocumentLocationName()
        {
            string name = null;
            Program.MainWindow.Invoke(new Action(() => name = _skylineWindow.SequenceTree.SelectedNode.Text));
            return name;
        }

        public string GetReplicateName()
        {
            string name = null;
            Program.MainWindow.Invoke(new Action(() => name = _skylineWindow.SelectedGraphChromName));
            return name;
        }

        [Obsolete]
        public Chromatogram[] GetChromatograms(DocumentLocation documentLocation)
        {
            if (documentLocation == null)
                return new Chromatogram[0];
            var result = new List<Chromatogram>();
            SrmDocument document = Program.MainWindow.Document;
            Bookmark bookmark = Bookmark.ToBookmark(documentLocation, document);
            IdentityPath identityPath = bookmark.IdentityPath;
            var nodeList = GetDocNodes(identityPath, document);
            TransitionDocNode transitionDocNode = null;
            if (nodeList.Count > 3)
            {
                transitionDocNode = (TransitionDocNode) nodeList[3];
            }
            var measuredResults = document.Settings.MeasuredResults;
            var nodePep = (PeptideDocNode)(nodeList.Count > 1 ? nodeList[1] : null);
            if (null == nodePep)
            {
                return result.ToArray();
            }

            int iColor = 0;

            foreach (var chromatogramSet in measuredResults.Chromatograms)
            {
                foreach (var msDataFileInfo in chromatogramSet.MSDataFileInfos)
                {
                    if (bookmark.ChromFileInfoId != null && !ReferenceEquals(msDataFileInfo.FileId, bookmark.ChromFileInfoId))
                    {
                        continue;
                    }

                    foreach (var nodeGroup in nodePep.TransitionGroups)
                    {
                        if (nodeList.Count > 2 && !Equals(nodeGroup, nodeList[2]))
                        {
                            continue;
                        }
                        ChromatogramGroupInfo[] arrayChromInfo;
                        measuredResults.TryLoadChromatogram(
                            chromatogramSet,
                            nodePep,
                            nodeGroup,
                            (float) document.Settings.TransitionSettings.Instrument.MzMatchTolerance,
                            out arrayChromInfo);
                        foreach (var transition in nodeGroup.Transitions)
                        {
                            if (transitionDocNode != null && !Equals(transitionDocNode, transition))
                            {
                                continue;
                            }
                            foreach (var chromatogramGroup in arrayChromInfo)
                            {
                                for (int iTransition = 0; iTransition < chromatogramGroup.NumTransitions; iTransition++)
                                {
                                    ChromatogramInfo transitionInfo = chromatogramGroup.GetTransitionInfo(iTransition);
                                    if (Math.Abs(transitionInfo.ProductMz - transition.Mz) >
                                        document.Settings.TransitionSettings.Instrument.MzMatchTolerance)
                                    {
                                        continue;
                                    }
                                    Color color = ColorScheme.CurrentColorScheme.TransitionColors[iColor % ColorScheme.CurrentColorScheme.TransitionColors.Count];
                                    iColor++;
                                    result.Add(new Chromatogram
                                    {
                                        Intensities = transitionInfo.Intensities.ToArray(),
                                        ProductMz = transitionInfo.ProductMz.RawValue,  // For negative ion mode data this will be a negative value
                                        PrecursorMz = chromatogramGroup.PrecursorMz.RawValue,  // For negative ion mode data this will be a negative value
                                        Times = transitionInfo.Times.ToArray(),
                                        Color = color
                                    });
                                }
                            }
                        }
                    }
                }
            }

            if (result.Count == 1)
                result[0].Color = ColorScheme.ChromGraphItemSelected;

            return result.ToArray();
        }

        private List<DocNode> GetDocNodes(IdentityPath identityPath, SrmDocument document)
        {
            var result = new List<DocNode>();
            while (!identityPath.IsRoot)
            {
                result.Insert(0, document.FindNode(identityPath));
                identityPath = identityPath.Parent;
            }
            return result;
        }

        /// <summary>
        /// Return the file path of the current document.
        /// </summary>
        /// <returns>Path to document file.</returns>
        public string GetDocumentPath()
        {
            return Program.MainWindow.DocumentFilePath;
        }

        /// <summary>
        /// Return the version of Skyline that is running.
        /// </summary>
        /// <returns></returns>
        public SkylineTool.Version GetVersion()
        {
            var version = new SkylineTool.Version();
            try
            {
                version.Major = Install.MajorVersion;
                version.Minor = Install.MinorVersion;
                version.Build = Install.Build;
                version.Revision = Install.Revision;
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
            return version;
        }

        public void ImportFasta(string textFasta)
        {
            Program.MainWindow.Invoke(new Action(() =>
            {
                _skylineWindow.ImportFasta(new StringReader(textFasta), Helpers.CountLinesInString(textFasta),
                    false, Resources.ToolService_ImportFasta_Insert_proteins, new SkylineWindow.ImportFastaInfo(false, textFasta));
            }));
        }

        public void InsertSmallMoleculeTransitionList(string textCSV)
        {
            Program.MainWindow.Invoke(new Action(() =>
            {
                _skylineWindow.InsertSmallMoleculeTransitionList(textCSV,
                    Resources.ToolService_InsertSmallMoleculeTransitionList_Insert_Small_Molecule_Transition_List);
            }));
        }

        public void AddSpectralLibrary(string libraryName, string libraryPath)
        {
            var librarySpec = LibrarySpec.CreateFromPath(libraryName, libraryPath);
            if (librarySpec == null)
            {
                // ReSharper disable once LocalizableElement
                throw new ArgumentException(Resources.LibrarySpec_CreateFromPath_Unrecognized_library_type_at__0_, libraryPath);
            }

            // CONSIDER: Add this Library Spec to Settings.Default.SpectralLibraryList?
            Program.MainWindow.Invoke(new Action(() =>
            {
                _skylineWindow.ModifyDocument(Resources.LibrarySpec_Add_spectral_library, doc =>
                    doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(
                        lib.LibrarySpecs.Union(new[] { librarySpec }).ToArray()))), AuditLogEntry.SettingsLogFunction);
                Settings.Default.SpectralLibraryList.Add(librarySpec);
            }));
        }

        private readonly object _documentChangeSendersLock = new object();


        /// <summary>
        /// Add receiver for document change notifications.
        /// </summary>
        /// <param name="receiverName"></param>
        /// <param name="name"></param>
        public void AddDocumentChangeReceiver(string receiverName, string name)
        {
            lock (_documentChangeSendersLock)
            {
                _documentChangeSenders.Add(receiverName, new DocumentChangeSender(receiverName, name));
            }
        }

        /// <summary>
        /// Remove a document change receiver.
        /// </summary>
        /// <param name="receiverName"></param>
        public void RemoveDocumentChangeReceiver(string receiverName)
        {
            lock (_documentChangeSendersLock)
            {
                _documentChangeSenders.Remove(receiverName);
            }
        }

        /// <summary>
        /// Send a notification to all registered receivers.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="arg"></param>
        private void SendChange(Action<DocumentChangeSender, string> action, string arg = null)
        {
            const int MAX_TIMEOUT_COUNT = 10;
            // We have to send document changes off the UI thread, because the client
            // may respond by requesting further information on the UI thread, which
            // would cause a deadlock.
            var sendThread = new Thread(() =>
            {
                var deadSenders = new List<string>();
                KeyValuePair<string, DocumentChangeSender>[] senders;
                lock (_documentChangeSendersLock)
                {
                    senders = _documentChangeSenders.ToArray();
                }
                foreach (var documentChangeSender in senders)
                {
                    try
                    {
                        action(documentChangeSender.Value, arg);
                        documentChangeSender.Value.ResetTimeouts();
                    }
                    catch (TimeoutException)
                    {
                        var error = @"No response from " + documentChangeSender.Value.Name; 
                        _skylineWindow.BeginInvoke(new Action(() =>
                        {
                            _skylineWindow.ShowImmediateWindow();
                            _skylineWindow.ImmediateWindow.WriteLine(error);
                        }));
                        if (!documentChangeSender.Value.CountTimeout(MAX_TIMEOUT_COUNT))
                        {
                            deadSenders.Add(documentChangeSender.Key);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine(exception);
                    }
                }
                lock (_documentChangeSendersLock)
                {
                    foreach (var deadSender in deadSenders)
                    {
                        _documentChangeSenders.Remove(deadSender);
                    }
                }
            });
            sendThread.Start();
        }

        /// <summary>
        /// Send a document change event to all registed receivers.
        /// </summary>
        public void SendDocumentChange()
        {
            SendChange((sender, arg) => sender.DocumentChanged());
        }

        /// <summary>
        /// Send a selection change event to all registed receivers.
        /// </summary>
        public void SendSelectionChange()
        {
            SendChange((sender, arg) => sender.SelectionChanged());
        }

        private class DocumentChangeSender : IDocumentChangeReceiver
        {
            private RemoteClient _remoteClient;
            private int _timeoutCount;

            public string Name { get; private set; }

            public DocumentChangeSender(string connectionName, string name)
            {
                _remoteClient = new RemoteClient(connectionName)
                {
                    Timeout = 10
                };
                Name = name;
            }

            public void DocumentChanged()
            {
                _remoteClient.RemoteCall(DocumentChanged);
            }

            public void SelectionChanged()
            {
                _remoteClient.RemoteCall(SelectionChanged);
            }

            public void ResetTimeouts()
            {
                _timeoutCount = 0;
            }

            public bool CountTimeout(int maxCount)
            {
                return Interlocked.Increment(ref _timeoutCount) < maxCount;
            }
        }

        public int GetProcessId()
        {
            return Process.GetCurrentProcess().Id;
        }

        public void DeleteElements(string[] elementLocatorStrings)
        {
            var elementLocators = elementLocatorStrings.Select(ElementLocator.Parse).ToList();
            _skylineWindow.Invoke(new Action(() =>
            {
                DeleteElementsNow(elementLocators);
            }));
        }

        private void DeleteElementsNow(IEnumerable<ElementLocator> elementLocators)
        {
            lock (_skylineWindow.GetDocumentChangeLock())
            {
                var originalDocument = _skylineWindow.Document;
                var document = originalDocument;
                var nodeRefs = new List<NodeRef>();
                foreach (var elementLocator in elementLocators)
                {
                    var elementRef = ElementRefs.FromObjectReference(elementLocator);
                    if (elementRef is NodeRef nodeRef)
                    {
                        nodeRefs.Add(nodeRef);
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(Resources.ToolService_DeleteElementsNow_Unsupported_element__0_, elementLocator));
                    }
                }

                var identityPathsToDelete = NodeRef.GetIdentityPaths(document, nodeRefs).ToHashSet();

                if (!identityPathsToDelete.Any())
                {
                    return;
                }

                Controls.Databinding.RowActions.DeleteNodesAction.DeleteIdentityPaths(_skylineWindow, identityPathsToDelete);
            }
        }

        public void ImportProperties(string csvText)
        {
            _skylineWindow.Invoke(new Action(() =>
            {
                _skylineWindow.ImportAnnotations(new StringReader(csvText),
                    new MessageInfo(MessageType.imported_annotations, _skylineWindow.Document.DocumentType,
                        Resources.ToolService_ImportProperties_Import_Properties_from_external_tool));
            }));
        }

        public void ImportPeakBoundaries(string csvText)
        {
            _skylineWindow.Invoke(new Action(() =>
            {
                lock (_skylineWindow.GetDocumentChangeLock())
                {
                    var originalDocument = _skylineWindow.DocumentUI;
                    var document = originalDocument;
                    using (var longWaitDlg = new Controls.LongWaitDlg())
                    {
                        longWaitDlg.PerformWork(_skylineWindow, 1000, progressMonitor =>
                        {
                            document = LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () =>
                            {
                                var peakBoundaryImporter = new PeakBoundaryImporter(originalDocument);
                                return peakBoundaryImporter.Import(new StringReader(csvText), progressMonitor,
                                    Helpers.CountLinesInString(csvText), true);
                            });
                        });
                        if (longWaitDlg.IsCanceled)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    _skylineWindow.ModifyDocument(
                        Resources.ToolService_ImportPeakBoundaries_Import_peak_boundaries_from_external_tool,
                        doc =>
                        {
                            if (!ReferenceEquals(doc, originalDocument))
                            {
                                // Should not be possible because of the lock on GetDocumentChangeLock
                                throw new InvalidOperationException(Resources
                                    .SkylineDataSchema_VerifyDocumentCurrent_The_document_was_modified_in_the_middle_of_the_operation_);
                            }
                            return document;
                        }, docPair =>
                            AuditLogEntry.CreateSingleMessageEntry(new MessageInfo(MessageType.imported_peak_boundaries,
                                _skylineWindow.DocumentUI.DocumentType,
                                Resources.ToolService_ImportPeakBoundaries_Import_peak_boundaries_from_external_tool)));
                }
            }));
        }

        public string GetSelectedElementLocator(string elementType)
        {
            ElementRef result = null;
            Exception exception = null;
            _skylineWindow.Invoke(new Action(() => 
            {
                try
                {
                    result = GetSelectedElementRefNow(elementType);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }));
            if (exception != null)
            {
                throw new TargetInvocationException(exception);
            }
            return result?.ToString();
        }

        private ElementRef GetSelectedElementRefNow(string elementType)
        {
            var document = _skylineWindow.DocumentUI;

            SrmDocument.Level nodeLevel;
            if (elementType == ReplicateRef.PROTOTYPE.ElementType)
            {
                if (!document.Settings.HasResults)
                {
                    return null;
                }

                return ReplicateRef.FromChromatogramSet(document.Settings.MeasuredResults
                    .Chromatograms[_skylineWindow.ComboResults.SelectedIndex]);
            }

            if (elementType == TransitionRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.Transitions;
            }
            else if (elementType == PrecursorRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.TransitionGroups;
            }
            else if (elementType == MoleculeRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.Molecules;
            }
            else if (elementType == MoleculeGroupRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.MoleculeGroups;
            }
            else
            {
                throw new ArgumentException(string.Format(Resources.ToolService_GetSelectedElementRefNow_Unsupported_element_type___0__, elementType));
            }

            var selectedPath = _skylineWindow.SelectedPath;
            if (selectedPath.Length <= (int)nodeLevel)
            {
                return null;
            }
            var elementRefs = new ElementRefs(document);
            return elementRefs.GetNodeRef(selectedPath.GetPathTo((int)nodeLevel));
        }
    }
}
