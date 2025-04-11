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
using MathNet.Numerics.Statistics;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.RetentionTimes.PeakImputation;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
            var alignmentTarget = AlignmentTarget.GetAlignmentTarget(document);
            if (alignmentTarget == null)
            {
                return null;
            }
            var libraries = document.Settings.PeptideSettings.Libraries;
            var unloadedLibraries = IndexesOfUnalignedLibraries(libraries).Select(i => libraries.Libraries[i]).ToList();
            if (unloadedLibraries.Count == 0)
            {
                return null;
            }

            return nameof(RetentionTimeManager) + @": " +
                   TextUtil.SpaceSeparate(unloadedLibraries.Select(lib => lib.Name));
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
            var loadingTag = tag as LoadingTag;
            if (loadingTag == null)
            {
                return false;
            }

            var document = container.Document;
            var peptideLibraries = document.Settings.PeptideSettings.Libraries;
            if (!peptideLibraries.Libraries.Contains(loadingTag.Library))
            {
                return true;
            }

            if (!Equals(loadingTag.AlignmentTarget, AlignmentTarget.GetAlignmentTarget(document)))
            {
                return true;
            }

            if (null != peptideLibraries.GetLibraryAlignment(loadingTag.Library))
            {
                return true;
            }

            return false;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var alignmentTarget = AlignmentTarget.GetAlignmentTarget(docCurrent);
            if (alignmentTarget == null)
            {
                return false;
            }
            var peptideLibraries = docCurrent.Settings.PeptideSettings.Libraries;
            var indexUnaligned = IndexesOfUnalignedLibraries(peptideLibraries).Append(-1).FirstOrDefault();
            if (indexUnaligned < 0)
            {
                return false;
            }

            var library = peptideLibraries.Libraries[indexUnaligned];
            var loadMonitor = new LoadMonitor(this, container, new LoadingTag(alignmentTarget, library));
            IProgressStatus progressStatus = new ProgressStatus(string.Format("Performing alignment between library {0} and {1}", library.Name, alignmentTarget.Calculator.Name));
            LibraryAlignment libraryAlignment = null;
            try
            {
                loadMonitor.UpdateProgress(progressStatus);
                libraryAlignment = PerformAlignment(loadMonitor, ref progressStatus, alignmentTarget, library);

                loadMonitor.UpdateProgress(progressStatus.Complete());
            }
            catch (Exception e)
            {
                if (loadMonitor.IsCanceled)
                {
                    return false;
                }
                loadMonitor.UpdateProgress(progressStatus.ChangeErrorException(e));
                return false;
            }
            if (libraryAlignment == null)
            {
                return false;
            }

            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                if (!Equals(peptideLibraries, docCurrent.Settings.PeptideSettings.Libraries) || !Equals(alignmentTarget, AlignmentTarget.GetAlignmentTarget(docCurrent)))
                {
                    return false;
                }
                var alignments = peptideLibraries.LibraryAlignments.ToList();
                alignments[indexUnaligned] = libraryAlignment;
                peptideLibraries = peptideLibraries.ChangeLibraryAlignments(alignments);
                docNew = docCurrent;
                docNew = docNew.ChangeSettings(
                    docNew.Settings.ChangePeptideSettings(
                        docNew.Settings.PeptideSettings.ChangeLibraries(peptideLibraries)));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private IEnumerable<int> IndexesOfUnalignedLibraries(PeptideLibraries peptideLibraries)
        {
            return Enumerable.Range(0, peptideLibraries.Libraries.Count).Where(i =>
                true == peptideLibraries.Libraries[i]?.IsLoaded && null == peptideLibraries.LibraryAlignments[i]);
        }

        public static LibraryAlignment PerformAlignment(ILoadMonitor loadMonitor, ref IProgressStatus progressStatus, AlignmentTarget alignmentTarget, Library library)
        {
            var allRetentionTimes = library.GetAllRetentionTimes();
            if (allRetentionTimes == null)
            {
                return new LibraryAlignment(library, new Dictionary<Target, double>(), null,
                    new Dictionary<string, AlignmentFunction>());
            }

            var medianRetentionTimes = new Dictionary<Target, double>();
            foreach (var target in allRetentionTimes.SelectMany(dict => dict.Keys).Distinct())
            {
                if (loadMonitor.IsCanceled)
                {
                    return null;
                }

                var retentionTimes = new List<double>();
                foreach (var dict in allRetentionTimes)
                {
                    if (dict.TryGetValue(target, out var rt))
                    {
                        retentionTimes.Add(rt);
                    }
                }

                if (retentionTimes.Count > 0)
                {
                    medianRetentionTimes.Add(target, retentionTimes.Median());
                }
            }

            using var pollingCancellationToken = new PollingCancellationToken(() => loadMonitor.IsCanceled);
            var alignmentFunctions = new AlignmentFunction[allRetentionTimes.Length];
            int completedCount = 0;
            IProgressStatus localProgressStatus = progressStatus;
            ParallelEx.For(0, alignmentFunctions.Length, iFile =>
            {
                var alignment =
                    alignmentTarget.PerformAlignment(allRetentionTimes[iFile], pollingCancellationToken.Token);
                if (loadMonitor.IsCanceled)
                {
                    return;
                }
                lock (alignmentFunctions)
                {
                    alignmentFunctions[iFile] = alignment;
                    loadMonitor.UpdateProgress(localProgressStatus =
                        localProgressStatus.ChangePercentComplete(100 * completedCount++ / alignmentFunctions.Length));
                }
            });
            progressStatus = localProgressStatus;
            return new LibraryAlignment(library, medianRetentionTimes, alignmentTarget, library.LibraryFiles.FilePaths
                .Zip(alignmentFunctions,
                    Tuple.Create).ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2));
        }

        class LoadingTag
        {
            public LoadingTag(AlignmentTarget alignmentTarget, Library library)
            {
                AlignmentTarget = alignmentTarget;
                Library = library;
            }


            public AlignmentTarget AlignmentTarget { get; }
            public Library Library { get; }
        }
    }
}
