﻿/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.IonMobility
{
    /// <summary>
    /// Manage the potentially length process of loading an ion mobility library from sqlite
    /// </summary>
    public sealed class IonMobilityLibraryManager : BackgroundLoader
    {
        public static string IsNotLoadedDocumentExplained(SrmDocument document)
        {
            // Not loaded if the library is not usable
            var calc = GetIonMobilityLibrary(document);
            if (calc == null || calc.IsNone || calc.IsUsable)
                return null;
            return @"IonMobilityLibraryManager : GetIonMobilityLibrary(document) not usable and not none";
        }

        private readonly Dictionary<string, IonMobilityLibrary> _loadedIonMobilityeLibraries =
            new Dictionary<string, IonMobilityLibrary>();

        // For use on container shutdown, clear caches to restore minimal memory footprint
        public override void ClearCache()
        {
            lock (_loadedIonMobilityeLibraries)
            {
                _loadedIonMobilityeLibraries.Clear();
            }
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return !ReferenceEquals(GetIonMobilityLibrary(document), GetIonMobilityLibrary(previous));
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
            var ionMobilityLibrary = GetIonMobilityLibrary(docCurrent);
            if (ionMobilityLibrary != null && !ionMobilityLibrary.IsUsable)
                ionMobilityLibrary = LoadIonMobilityLibrary(container, ionMobilityLibrary);
            if (ionMobilityLibrary == null || !ReferenceEquals(document.Id, container.Document.Id))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }
            var ionMobilityFiltering = docCurrent.Settings.TransitionSettings.IonMobilityFiltering;
            var ionMobilityFilteringNew = !ReferenceEquals(ionMobilityLibrary, ionMobilityFiltering.IonMobilityLibrary)
                ? ionMobilityFiltering.ChangeLibrary(ionMobilityLibrary)
                : ionMobilityFiltering;

            if (ionMobilityFilteringNew == null ||
                !ReferenceEquals(document.Id, container.Document.Id) ||
                (Equals(ionMobilityFiltering, ionMobilityFilteringNew)))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }
            SrmDocument docNew;
            do
            {
                // Change the document to use the new ion mobility filter.
                docCurrent = container.Document;
                if (!ReferenceEquals(ionMobilityFiltering, docCurrent.Settings.TransitionSettings.IonMobilityFiltering))
                    return false; // Loading was cancelled or document changed
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangeTransitionSettings(t => t.ChangeIonMobilityFiltering(ionMobilityFilteringNew)));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private IonMobilityLibrary LoadIonMobilityLibrary(IDocumentContainer container, IonMobilityLibrary dtLib)
        {
            // TODO: Something better than locking for the entire load
            lock (_loadedIonMobilityeLibraries)
            {
                IonMobilityLibrary libResult;
                if (!_loadedIonMobilityeLibraries.TryGetValue(dtLib.Name, out libResult))
                {
                    libResult = dtLib.Initialize(new LoadMonitor(this, container, dtLib));
                    if (libResult != null)
                        _loadedIonMobilityeLibraries.Add(libResult.Name, libResult);
                }
                return libResult;
            }
        }

        private static IonMobilityLibrary GetIonMobilityLibrary(SrmDocument document)
        {
            if (document == null)
                return null;
            var ionMobilityFiltering = document.Settings.TransitionSettings.IonMobilityFiltering;
            return ionMobilityFiltering?.IonMobilityLibrary;
        }

    }
}
