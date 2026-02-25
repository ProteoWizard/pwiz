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
using pwiz.Skyline.Model.DocSettings;
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
            if (document.Settings.GetAlignmentTargetSpec().IsChromatogramPeaks)
            {
                return true;
            }
            return !IsLoaded(document);
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
            if (tag is SrmDocument tagDocument)
            {
                return !ReferenceEquals(tagDocument, container.Document);
            }

            if (tag is DocumentRetentionTimes.LibraryAlignmentParam param)
            {
                var document = container.Document;
                return !document.Settings.DocumentRetentionTimes.GetMissingAlignments(document.Settings).Contains(param);
            }

            return false;
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
            if (!AlignmentTarget.TryGetCurrentAlignmentTarget(document, out var alignmentTarget))
            {
                return false;
            }
            var documentRetentionTimes = docCurrent.Settings.DocumentRetentionTimes;
            var newDocumentRetentionTimes = documentRetentionTimes.UpdateFromLoadedSettings(alignmentTarget, docCurrent.Settings);
            if (newDocumentRetentionTimes == null)
            {
                var alignmentParam = documentRetentionTimes.GetMissingAlignments(docCurrent.Settings).FirstOrDefault();
                if (alignmentParam != null)
                {
                    newDocumentRetentionTimes =
                        PerformLibraryAlignment(container, documentRetentionTimes, alignmentParam);
                }
                else
                {
                    newDocumentRetentionTimes = UpdateResultFileAlignments(alignmentTarget, container, docCurrent);
                }

                if (newDocumentRetentionTimes == null)
                {
                    return false;
                }

                if (ReferenceEquals(newDocumentRetentionTimes, documentRetentionTimes))
                {
                    return false;
                }
            }

            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                if (!ReferenceEquals(docCurrent.Id, document.Id))
                {
                    return false;
                }

                if (!Equals(documentRetentionTimes, docCurrent.Settings.DocumentRetentionTimes) 
                    || !AlignmentTarget.TryGetCurrentAlignmentTarget(docCurrent, out var currentAlignmentTarget) 
                    || !Equals(currentAlignmentTarget, alignmentTarget))
                {
                    return false;
                }

                try
                {
                    using var settingsChangeMonitor =
                        new SrmSettingsChangeMonitor(new LoadMonitor(this, container, docCurrent),
                            RetentionTimesResources
                                .RetentionTimeManager_PerformNextAlignment_Updating_retention_time_alignment);
                    docNew = docCurrent.ChangeSettings(
                        docCurrent.Settings.ChangeDocumentRetentionTimes(newDocumentRetentionTimes),
                        settingsChangeMonitor);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private DocumentRetentionTimes PerformLibraryAlignment(IDocumentContainer container,
            DocumentRetentionTimes documentRetentionTimes,
            DocumentRetentionTimes.LibraryAlignmentParam alignmentParam)
        {
            var loadMonitor = new LoadMonitor(this, container, alignmentParam);
            IProgressStatus progressStatus = new ProgressStatus(GetAlignmentDescription(alignmentParam.AlignmentTarget, alignmentParam.LibraryName));
            Alignments alignment;
            try
            {
                loadMonitor.UpdateProgress(progressStatus);
                alignment = DocumentRetentionTimes.
                    PerformAlignment(loadMonitor, ref progressStatus, alignmentParam);

                loadMonitor.UpdateProgress(progressStatus.Complete());
            }
            catch (Exception e)
            {
                if (loadMonitor.IsCanceled)
                {
                    loadMonitor.UpdateProgress(progressStatus.Cancel());
                    return null;
                }

                loadMonitor.UpdateProgress(progressStatus.ChangeErrorException(e));
                return null;
            }

            return documentRetentionTimes.ChangeLibraryAlignments(alignmentParam, alignment);
        }

        private DocumentRetentionTimes UpdateResultFileAlignments(AlignmentTarget alignmentTarget, IDocumentContainer container, SrmDocument docCurrent)
        {
            var loadMonitor = new LoadMonitor(this, container, docCurrent);
            var documentRetentionTimes = docCurrent.Settings.DocumentRetentionTimes;
            IProgressStatus progressStatus = new ProgressStatus(RetentionTimesResources.RetentionTimeManager_UpdateResultFileAlignments_Performing_replicate_retention_time_alignments);
            DocumentRetentionTimes result;
            try
            {
                loadMonitor.UpdateProgress(progressStatus);
                result = documentRetentionTimes.UpdateResultFileAlignments(alignmentTarget, loadMonitor, ref progressStatus, docCurrent);
                loadMonitor.UpdateProgress(progressStatus.Complete());
            }
            catch (Exception e)
            {
                if (loadMonitor.IsCanceled)
                {
                    loadMonitor.UpdateProgress(progressStatus.Cancel());
                    return null;
                }

                loadMonitor.UpdateProgress(progressStatus.ChangeErrorException(e));
                return null;
            }

            return result;
        }

        private static string GetAlignmentDescription(AlignmentTarget target, string libraryName)
        {
            if (target is AlignmentTarget.LibraryTarget libraryTarget)
            {
                if (libraryTarget.Library.Name == libraryName)
                {
                    return string.Format(RetentionTimesResources.RetentionTimeManager_GetAlignmentDescription_Performing_alignment_between_library__0__and_itself,
                        libraryName);
                }

                return string.Format(RetentionTimesResources.RetentionTimeManager_GetAlignmentDescription_Performing_alignment_between_libraries__0__and__1_, libraryName, libraryTarget.Library.Name);
            }

            if (target is AlignmentTarget.Irt irt)
            {
                return string.Format(RetentionTimesResources.RetentionTimeManager_GetAlignmentDescription_Performing_alignment_between_library__0__and_calculator__1_,
                    libraryName, irt.Calculator.Name);
            }

            return string.Format(RetentionTimesResources.RetentionTimeManager_GetAlignmentDescription_Performing_alignment_on_library__0_, libraryName);

        }
    }
}
