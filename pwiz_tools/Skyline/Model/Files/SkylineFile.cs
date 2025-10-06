/*
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Files
{
    public class SkylineFile : FileNode
    {
        public static SkylineFile Create(SrmDocument document, string documentFilePath) 
        {
            string name, fileName, filePath;

            if (IsDocumentSaved(documentFilePath))
            {
                var justFileName = Path.GetFileName(documentFilePath);

                name = justFileName;
                fileName = justFileName;
                filePath = documentFilePath;
            }
            else
            {
                name = FileResources.FileModel_NewDocument;
                filePath = null;
                fileName = null;
            }

            var files = BuildFromDocument(document, documentFilePath);

            return new SkylineFile(documentFilePath, name, fileName, filePath, files);
        }

        private SkylineFile(string documentFilePath, string name, string fileName, string filePath, IList<FileNode> files) :
            base(documentFilePath, IdentityPath.ROOT)
        {
            Name = name;
            FileName = fileName;
            FilePath = filePath;
            Files = files;
        }

        public override bool IsBackedByFile => true;
        public override bool RequiresSavedDocument => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }
        public override IList<FileNode> Files { get; }
        public override ImageId ImageAvailable => ImageId.skyline;

        public FileNode Folder<T>() where T : FileNode
        {
            return Files.OfType<T>().FirstOrDefault();
        }

        private static IList<FileNode> BuildFromDocument(SrmDocument document, string documentFilePath)
        {
            var list = new List<FileNode>();

            if (document.Settings.DataSettings.IsAuditLoggingEnabled)
            {
                var log = SkylineAuditLog.Create(document, documentFilePath);
                list.Add(log);
            }

            if (IsDocumentSaved(documentFilePath))
            {
                var view = SkylineViewFile.Create(document, documentFilePath);
                list.Add(view);
            }

            // TODO: adding Chromatograms to FilesTree causes drag-and-drop tests to fail. Cause unknown - maybe moving
            //       nodes further puts them outside the visible frame and causes DnD issues?
            // CONSIDER: is this correct? See more where Cache files are created in MeasuredResults @ line 1640
            // { // Chromatogram Cache (.skyd)
            //     var cachePaths = document.Settings.MeasuredResults?.CachePaths;
            //     if (cachePaths != null)
            //     {
            //         foreach (var _ in cachePaths)
            //         {
            //             var name = FileResources.FileModel_ChromatogramCache;
            //             var filePath = ChromatogramCache.FinalPathForName(documentFilePath, null);
            //
            //             list.Add(SkylineChromatogramCache.Create(documentFilePath, name, filePath));
            //         }
            //     }
            // }

            { // Replicates
                var measuredResults = document.MeasuredResults;
                if (document.Settings.HasResults)
                {
                    var files = measuredResults.Chromatograms.Select(chromatogramSet => Replicate.Create(documentFilePath, chromatogramSet)).ToList();
                    var folder = new ReplicatesFolder(documentFilePath, files);

                    list.Add(folder);
                }
            }

            { // SpectralLibraries
                var peptideSettings = document.Settings.PeptideSettings;
                if (peptideSettings is { HasLibraries: true })
                {
                    var files = peptideSettings.Libraries.LibrarySpecs.Select(library => SpectralLibrary.Create(documentFilePath, library)).ToList();
                    var folder = new SpectralLibrariesFolder(documentFilePath, files);

                    list.Add(folder);
                }
            }

            if (document.Settings.PeptideSettings is { HasBackgroundProteome: true })
            {
                var file = BackgroundProteome.Create(documentFilePath, document.Settings.PeptideSettings.BackgroundProteome);
                var folder = new BackgroundProteomeFolder(documentFilePath, file);

                list.Add(folder);
            }

            if (document.Settings.PeptideSettings is { HasRTCalcPersisted: true })
            {
                var file = RTCalc.Create(documentFilePath, document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
                var folder = new RTCalcFolder(documentFilePath, file);

                list.Add(folder);
            }

            if (document.Settings.TransitionSettings is { HasIonMobilityLibraryPersisted: true })
            {
                var file = IonMobilityLibrary.Create(documentFilePath, document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary);
                var folder = new IonMobilityLibraryFolder(documentFilePath, file);

                list.Add(folder);
            }

            if (document.Settings.TransitionSettings is { HasOptimizationLibraryPersisted: true })
            {
                var file = OptimizationLibrary.Create(documentFilePath, document.Settings.TransitionSettings.Prediction.OptimizedLibrary);
                var folder = new OptimizationLibraryFolder(documentFilePath, file);

                list.Add(folder);
            }

            return ImmutableList.ValueOf(list);
        }
    }
}