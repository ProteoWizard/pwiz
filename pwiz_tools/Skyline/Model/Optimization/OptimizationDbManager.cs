/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Optimization
{
    public sealed class OptimizationDbManager : BackgroundLoader
    {
        public static string IsNotLoadedDocumentExplained(SrmDocument document)
        {
            // Not loaded if the library is not usable
            var lib = GetOptimizationLibrary(document);
            if (lib == null || lib.IsUsable || lib.IsNone)
            {
                return null;
            }
            return "OptimizationDbManager: GetOptimizationLibrary(document) not usable and not none"; // Not L10N
        }

        private readonly Dictionary<string, OptimizationLibrary> _loadedLibraries =
            new Dictionary<string, OptimizationLibrary>();

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null)
            {
                return true;
            }
            return !ReferenceEquals(GetOptimizationLibrary(document), GetOptimizationLibrary(previous));
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedDocumentExplained(document);
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var lib = GetOptimizationLibrary(docCurrent);
            if (lib != null && !lib.IsNone && !lib.IsUsable)
                lib = LoadLibrary(container, lib);
            if (lib == null || !ReferenceEquals(document.Id, container.Document.Id))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }

            SrmDocument docNew;
            do
            {
                // Change the document to use the new library
                docCurrent = container.Document;
                if (!ReferenceEquals(GetOptimizationLibrary(docCurrent), GetOptimizationLibrary(container.Document)))
                    return false;
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangeTransitionPrediction(predict =>
                    predict.ChangeOptimizationLibrary(lib)));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private OptimizationLibrary LoadLibrary(IDocumentContainer container, OptimizationLibrary lib)
        {
            // TODO: Something better than locking for the entire load
            lock (_loadedLibraries)
            {
                OptimizationLibrary libResult;
                if (!_loadedLibraries.TryGetValue(lib.Name, out libResult))
                {
                    libResult = lib.Initialize(container.Document, new LoadMonitor(this, container, lib));
                    if (libResult != null)
                        _loadedLibraries.Add(libResult.Name, libResult);
                }
                return libResult;
            }
        }

        private static OptimizationLibrary GetOptimizationLibrary(SrmDocument document)
        {
            return document.Settings.TransitionSettings.Prediction.OptimizedLibrary;
        }
    }
}
