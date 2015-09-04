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

using System.Collections.Generic;
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
            return new IPooledStream[0];
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return !ReferenceEquals(container.Document, tag);
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            SrmDocument docNew, docOrig;
            do
            {
                docOrig = container.Document;
                var loadMonitor = new LoadMonitor(this, container, docOrig);
                docNew = DocumentRetentionTimes.RecalculateAlignments(docOrig, loadMonitor);
                if (null == docNew)
                {
                    EndProcessing(docOrig);
                    return false;
                }
            }
            while (!CompleteProcessing(container, docNew, docOrig));
            return true;
        }
    }
}
