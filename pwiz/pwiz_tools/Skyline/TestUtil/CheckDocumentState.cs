/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using pwiz.Skyline;
using pwiz.Skyline.Model;

namespace pwiz.SkylineTestUtil
{

    /// <summary>
    /// Helper class to check document state with proper synchronization (wait for document change).
    /// Use this when the document is likely to have its nodes updated by the protein metadata background updater,
    /// or its parent CheckDocumentState if you don't necessarily need to wait for document stability.
    /// </summary>
    public class CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate : CheckDocumentState
    {
        public CheckDocumentStateWithPossibleProteinMetadataBackgroundUpdate(int groups, int peptides, int tranGroups, int transitions)
            : base(groups, peptides, tranGroups, transitions, null, true)
        {
        }
    }

    /// <summary>
    /// Helper class to check document state with proper synchronization (wait for document change).
    /// </summary>
    public class CheckDocumentState : System.IDisposable
    {
        private readonly SrmDocument _document;
        private readonly int _groups;
        private readonly int _peptides;
        private readonly int _tranGroups;
        private readonly int _transitions;
        private readonly int? _revisionIncrement;
        private readonly bool _waitForLoaded;

        public CheckDocumentState(int groups, int peptides, int tranGroups, int transitions, int? revisionIncrement = null, bool waitForLoaded = false)
        {
            _document = Program.MainWindow.Document;
            _groups = groups;
            _peptides = peptides;
            _tranGroups = tranGroups;
            _transitions = transitions;
            _revisionIncrement = revisionIncrement;
            _waitForLoaded = waitForLoaded;
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            int revision = _document.RevisionIndex + (_revisionIncrement ?? 1);
            var newDocument = _document;
            do
            {
                newDocument = _waitForLoaded
                    ? AbstractFunctionalTest.WaitForDocumentChangeLoaded(newDocument)
                    : AbstractFunctionalTest.WaitForDocumentChange(newDocument);
            }
            while (newDocument.RevisionIndex < revision);

            AssertEx.IsDocumentState(newDocument, (_revisionIncrement.HasValue ? revision : (int?) null), _groups, _peptides, _tranGroups, _transitions);
        }

        #endregion
    }
}
