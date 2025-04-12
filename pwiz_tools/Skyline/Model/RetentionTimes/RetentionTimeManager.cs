/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.RetentionTimes
{
    /// <summary>
    /// Manages reading retention times from libraries in the background, and performing
    /// retention time alignments for the data files that are in the document.
    /// </summary>
    public class RetentionTimeManager : BackgroundLoader
    {
        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return true;
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            return DocumentRetentionTimes.IsNotLoadedExplained(document);
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            return Array.Empty<IPooledStream>();
        }

        public override void ClearCache()
        {
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            var param = tag as DocumentRetentionTimes.LibraryAlignmentParam;
            if (param == null)
            {
                return false;
            }
            
            var document = container.Document;
            return !document.Settings.DocumentRetentionTimes.GetMissingAlignments(document.Settings).Contains(param);
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document,
            SrmDocument docCurrent)
        {
            bool result = false;
            try
            {
                result = PerformNextAlignment(container, document, docCurrent);
            }
            finally
            {
                if (!result)
                {
                    EndProcessing(document);
                }
            }

            return result;
        }
        private bool PerformNextAlignment(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var alignmentTarget = AlignmentTarget.GetAlignmentTarget(docCurrent);
            if (alignmentTarget == null)
            {
                return false;
            }

            var documentRetentionTimes = docCurrent.Settings.DocumentRetentionTimes;
            var newDocumentRetentionTimes = documentRetentionTimes.UpdateFromLoadedSettings(docCurrent.Settings);
            if (newDocumentRetentionTimes == null)
            {
                var alignmentParam = documentRetentionTimes.GetMissingAlignments(docCurrent.Settings).FirstOrDefault();
                if (alignmentParam == null)
                {
                    return false;
                }
                var loadMonitor = new LoadMonitor(this, container, alignmentParam);
                IProgressStatus progressStatus = new ProgressStatus(string.Format("Performing alignment between library {0} and predictor {1}", alignmentParam.Key, alignmentTarget.Calculator.Name));
                LibraryAlignments libraryAlignment;
                try
                {
                    loadMonitor.UpdateProgress(progressStatus);
                    libraryAlignment = DocumentRetentionTimes.
                        PerformAlignment(loadMonitor, ref progressStatus, alignmentParam);

                    loadMonitor.UpdateProgress(progressStatus.Complete());
                }
                catch (Exception e)
                {
                    if (loadMonitor.IsCanceled)
                    {
                        loadMonitor.UpdateProgress(progressStatus.Cancel());
                        return false;
                    }
                    loadMonitor.UpdateProgress(progressStatus.ChangeErrorException(e));
                    return false;
                }
                if (libraryAlignment == null)
                {
                    return false;
                }

                newDocumentRetentionTimes =
                    documentRetentionTimes.ChangeLibraryAlignments(alignmentParam, libraryAlignment);
            }

            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                if (!ReferenceEquals(docCurrent.Id, document.Id))
                {
                    return false;
                }
                if (!Equals(documentRetentionTimes, docCurrent.Settings.DocumentRetentionTimes) || !Equals(alignmentTarget, AlignmentTarget.GetAlignmentTarget(docCurrent)))
                {
                    return false;
                }

                docNew = docCurrent.ChangeSettings(
                    docCurrent.Settings.ChangeDocumentRetentionTimes(newDocumentRetentionTimes));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }
    }
}
