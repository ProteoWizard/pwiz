/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding.RowActions
{
    /// <summary>
    /// Actions which remove peaks belonging to the items in the selected rows in the Document Grid.
    /// </summary>
    public abstract class RemovePeaksAction : RowAction
    {
        public static readonly RemovePeaksAction Peptides = new RemovePeptides();

        public static readonly RemovePeaksAction Precursors = new RemovePrecursors();

        public static readonly RemovePeaksAction Transitions = new RemoveTransitions();

        public static IEnumerable<RemovePeaksAction> All
        {
            get
            {
                yield return Peptides;
                yield return Precursors;
                // Don't include "Remove Transitions" since removing a single transition's peak from a single replicate
                // invalidates comparing total areas between replicates.
                // yield return Transitions;
            }
        }

        public void RemovePeaks(SrmDocument.DOCUMENT_TYPE docType, BoundDataGridView dataGridView)
        {
            var parent = FormUtil.FindTopLevelOwner(dataGridView);
            var selectedPeaks = GetSelectedPeaks(dataGridView).Distinct().ToArray();
            if (selectedPeaks.Length == 0)
            {
                MessageDlg.Show(parent, Resources.RemovePeaksAction_RemovePeaks_No_peaks_are_selected);
                return;
            }

            var lookup = selectedPeaks.ToLookup(tuple => tuple.Item1, tuple => tuple.Item2).ToArray();
            string message = GetConfirmRemoveMessage(docType, selectedPeaks.Length, lookup.Length);

            if (MultiButtonMsgDlg.Show(parent, message, MultiButtonMsgDlg.BUTTON_OK) != DialogResult.OK)
            {
                return;
            }

            var skylineWindow = GetSkylineWindow(dataGridView);
            lock (skylineWindow.GetDocumentChangeLock())
            {
                skylineWindow.ModifyDocument(Resources.RemovePeaksAction_RemovePeaks_Remove_peaks,
                    doc =>
                    {
                        var longOperationRunner = new LongOperationRunner
                        {
                            ParentControl = parent,
                            JobTitle = Resources.RemovePeaksAction_RemovePeaks_Removing_Peaks
                        };
                        SrmDocument resultDocument = doc;
                        doc = doc.BeginDeferSettingsChanges();
                        longOperationRunner.Run(broker =>
                        {
                            for (int iGroup = 0; iGroup < lookup.Length; iGroup++)
                            {
                                broker.ProgressValue = iGroup * 100 / lookup.Length;
                                if (broker.IsCanceled)
                                {
                                    return;
                                }
                                doc = RemovePeaks(doc, lookup[iGroup]);
                            }

                            resultDocument = doc.EndDeferSettingsChanges(resultDocument, null);
                        });
                        return resultDocument;
                    },
                    docPair => AuditLogEntry.CreateSingleMessageEntry(
                        new MessageInfo(MessageType.removed_peaks, docPair.NewDocumentType, selectedPeaks.Length, lookup.Length)));
            }
        }

        protected abstract SrmDocument RemovePeaks(SrmDocument document, IGrouping<IdentityPath, ResultFileKey> peaks);

        protected abstract string GetConfirmRemoveMessage(SrmDocument.DOCUMENT_TYPE docType, int peakCount, int nodeCount);
        public abstract IEnumerable<Result> GetSelectedResults(BoundDataGridView dataGridView);

        public IEnumerable<Tuple<IdentityPath, ResultFileKey>> GetSelectedPeaks(BoundDataGridView dataGridView)
        {
            foreach (var result in GetSelectedResults(dataGridView))
            {
                if (result.IsEmpty())
                {
                    continue;
                }
                var resultFile = result.GetResultFile();
                var resultFileKey = new ResultFileKey(resultFile.Replicate.ReplicateIndex,
                    resultFile.ChromFileInfoId, 0);
                var tuple = Tuple.Create(result.GetSkylineDocNode().IdentityPath, resultFileKey);
                yield return tuple;
            }
        }

        public override ToolStripMenuItem CreateMenuItem(SrmDocument.DOCUMENT_TYPE docType, BoundDataGridView dataGridView)
        {
            var toolStripMenuItem =
                new ToolStripMenuItem(GetMenuItemText(docType), null, (sender, args) => RemovePeaks(docType, dataGridView));
            if (!GetSelectedResults(dataGridView).Any())
            {
                toolStripMenuItem.Enabled = false;
            }
            return toolStripMenuItem;
        }

        private static SrmDocument RemovePrecursorPeak(SrmDocument document, IdentityPath groupPath, string replicateName, MsDataFileUri filePath)
        {
            return document.ChangePeak(groupPath, replicateName, filePath, null, 0, 0, UserSet.TRUE, PeakIdentification.FALSE, false);
        }

        private static bool GetReplicateName(SrmDocument document, ResultFileKey resultFileKey, out string replicateName, out MsDataFileUri filePath)
        {
            replicateName = null;
            filePath = null;
            if (!document.Settings.HasResults || document.Settings.MeasuredResults.Chromatograms.Count <= resultFileKey.ReplicateIndex)
            {
                return false;
            }

            var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[resultFileKey.ReplicateIndex];
            var chromFileInfo = chromatogramSet.GetFileInfo(resultFileKey.ChromFileInfoId);
            if (chromFileInfo == null)
            {
                return false;
            }

            replicateName = chromatogramSet.Name;
            filePath = chromFileInfo.FilePath;
            return true;
        }

        class RemovePeptides : RemovePeaksAction
        {
            protected override string GetConfirmRemoveMessage(SrmDocument.DOCUMENT_TYPE docType, int peakCount, int nodeCount)
            {
                bool proteomic = docType == SrmDocument.DOCUMENT_TYPE.proteomic;
                if (peakCount == 1)
                {
                    return proteomic 
                            ? Resources.RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_this_peptide_peak_
                            : Resources.RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_this_molecule_peak_;
                }

                if (nodeCount == 1)
                {
                    return string.Format(proteomic
                        ? Resources.RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from_one_peptide_
                        : Resources.RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from_one_molecule_, peakCount);
                }

                return string.Format(proteomic
                    ? Resources
                        .RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from__1__peptides_
                    : Resources.RemovePeptides_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from__1__molecules_, peakCount, nodeCount);
            }

            public override IEnumerable<Result> GetSelectedResults(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(PeptideResult), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var result in rowItemValues.GetRowValues(rowItem).Cast<Result>())
                    {
                        yield return result;
                    }
                }

                foreach (var precursorResult in Precursors.GetSelectedResults(dataGridView))
                {
                    yield return ((PrecursorResult) precursorResult).PeptideResult;
                }
            }

            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return docType == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? Resources.RemovePeptides_MenuItemText_Remove_Peptide_Peaks___
                    : Resources.RemovePeptides_MenuItemText_Remove_Molecule_Peaks___;
            }

            protected override SrmDocument RemovePeaks(SrmDocument document, IGrouping<IdentityPath, ResultFileKey> peaks)
            {
                var peptideDocNode = (PeptideDocNode)document.FindNode(peaks.Key);

                foreach (var resultFileKey in peaks)
                {
                    string replicateName;
                    MsDataFileUri filePath;
                    if (!GetReplicateName(document, resultFileKey, out replicateName, out filePath))
                    {
                        continue;
                    }
                    foreach (TransitionGroupDocNode transitionGroup in peptideDocNode.TransitionGroups)
                    {
                        var groupPath = new IdentityPath(peaks.Key, transitionGroup.Id);
                        document = RemovePrecursorPeak(document, groupPath, replicateName, filePath);
                    }
                }

                return document;
            }
        }

        class RemoveTransitions : RemovePeaksAction
        {
            protected override string GetConfirmRemoveMessage(SrmDocument.DOCUMENT_TYPE docType, int peakCount, int nodeCount)
            {
                if (peakCount == 1)
                {
                    return Resources.RemoveTransitions_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_this_transition_peak_;
                }

                if (nodeCount == 1)
                {
                    return string.Format(Resources.RemoveTransitions_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from_one_transition_, peakCount);
                }
                return string.Format(Resources.RemoveTransitions_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from__1__transitions_, peakCount, nodeCount);
            }

            public override IEnumerable<Result> GetSelectedResults(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(TransitionResult), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var result in rowItemValues.GetRowValues(rowItem).Cast<Result>())
                    {
                        yield return result;
                    }
                }
            }

            protected override SrmDocument RemovePeaks(SrmDocument document, IGrouping<IdentityPath, ResultFileKey> peaks)
            {
                var groupPath = peaks.Key.Parent;
                foreach (var resultFileKey in peaks)
                {
                    string replicateName;
                    MsDataFileUri filePath;
                    if (!GetReplicateName(document, resultFileKey, out replicateName, out filePath))
                    {
                        continue;
                    }
                    document = document.ChangePeak(groupPath, replicateName, filePath, (Transition)peaks.Key.Child, 0, 0, UserSet.TRUE,
                        PeakIdentification.FALSE, false);
                }

                return document;
            }

            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return Resources.RemoveTransitions_MenuItemText_Remove_Transition_Peaks___;
            }
        }

        class RemovePrecursors : RemovePeaksAction
        {
            protected override string GetConfirmRemoveMessage(SrmDocument.DOCUMENT_TYPE docType, int peakCount, int nodeCount)
            {
                if (peakCount == 1)
                {
                    return Resources.RemovePrecursors_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_this_precursor_peak_;
                }

                if (nodeCount == 1)
                {
                    return string.Format(Resources.RemovePrecursors_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from_one_precursor_, peakCount);
                }

                return string.Format(Resources.RemovePrecursors_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from__1__precursors_, peakCount, nodeCount);
            }

            public override IEnumerable<Result> GetSelectedResults(BoundDataGridView dataGridView)
            {
                var rowItemValues = RowItemValues.FromDataGridView(typeof(PrecursorResult), dataGridView);
                foreach (var rowItem in rowItemValues.GetSelectedRowItems(dataGridView))
                {
                    foreach (var result in rowItemValues.GetRowValues(rowItem).Cast<Result>())
                    {
                        yield return result;
                    }
                }

                foreach (var transitionResult in Transitions.GetSelectedResults(dataGridView))
                {
                    yield return ((TransitionResult) transitionResult).PrecursorResult;
                }
            }

            protected override SrmDocument RemovePeaks(SrmDocument document, IGrouping<IdentityPath, ResultFileKey> peaks)
            {
                foreach (var resultFileKey in peaks)
                {
                    string replicateName;
                    MsDataFileUri filePath;
                    if (!GetReplicateName(document, resultFileKey, out replicateName, out filePath))
                    {
                        continue;
                    }

                    document = RemovePrecursorPeak(document, peaks.Key, replicateName, filePath);
                }

                return document;
            }

            public override string GetMenuItemText(SrmDocument.DOCUMENT_TYPE docType)
            {
                return Resources.RemovePrecursors_MenuItemText_Remove_Precursor_Peaks___;
            }
        }
    }
}
