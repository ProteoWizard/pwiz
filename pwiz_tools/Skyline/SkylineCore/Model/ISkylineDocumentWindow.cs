/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// What the document grid's data (SkylineDataSchema and its entities) needs from the Skyline
    /// main window: to modify the document with undo, and to navigate to what a grid link points at.
    /// Implemented by SkylineWindow; null when the grid is not in a Skyline window.
    /// </summary>
    public interface ISkylineDocumentWindow
    {
        SrmDocument.DOCUMENT_TYPE ModeUI { get; }

        string DocumentFilePath { get; }

        /// <summary>
        /// False while the window is still being built, as when a .view file is restored.
        /// </summary>
        bool HasSequenceTree { get; }

        void ModifyDocument(string description, Func<SrmDocument, SrmDocument> act,
            Func<SrmDocumentPair, AuditLogEntry> logFunc);

        void ModifyDocument(string description, IUndoState undoState, Func<SrmDocument, SrmDocument> act,
            Action onModifying, Action onModified, Func<SrmDocumentPair, AuditLogEntry> logFunc);

        IdentityPath SelectedPath { get; set; }

        int SelectedResultsIndex { get; set; }

        /// <summary>
        /// Shows the calibration curve, switching it to a single batch when asked and it is not already.
        /// </summary>
        void ShowCalibrationForm(bool singleBatch);

        /// <summary>
        /// Zooms the chromatogram graph of a replicate, if it is showing, to a peak.
        /// </summary>
        void ZoomChromatogramToPeak(string replicateName, double startTime, double endTime);

        void ShowGraphFullScan(IScanProvider scanProvider, int transitionIndex, int scanIndex, int? optStep);
    }
}
