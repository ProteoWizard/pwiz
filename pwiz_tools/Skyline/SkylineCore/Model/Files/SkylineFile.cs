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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Files
{
    public class SkylineFile : FileModel
    {
        /// <summary>
        /// Controls how file resources are displayed in the Files view.
        /// When true, shows file names (e.g., "Background Proteome - Rat_mini.protdb")
        /// When false, shows resource names (e.g., "Background Proteome - Rat mini")
        /// </summary>
        public static bool ShowFileNames { get; set; }

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

        private SkylineFile(string documentFilePath, string name, string fileName, string filePath, IList<FileModel> files) :
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
        protected override string FileTypeText => string.Empty; // Root .sky file has no type prefix
        public override IList<FileModel> Files { get; }
        public override ImageId ImageAvailable => ImageId.skyline;

        public FileModel Folder<T>() where T : FileModel
        {
            return Files.OfType<T>().FirstOrDefault();
        }

        private static IList<FileModel> BuildFromDocument(SrmDocument document, string documentFilePath)
        {
            var list = new List<FileModel>();

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

            if (document.Settings.PeptideSettings is { HasBackgroundProteome: true })
            {
                var file = BackgroundProteome.Create(documentFilePath, document.Settings.PeptideSettings.BackgroundProteome);
                list.Add(file);
            }

            if (document.Settings.PeptideSettings is { HasRTCalcPersisted: true })
            {
                var file = RTCalc.Create(documentFilePath, document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator);
                list.Add(file);
            }

            { // Spectral Libraries
                var peptideSettings = document.Settings.PeptideSettings;
                if (peptideSettings is { HasLibraries: true })
                {
                    var librarySpecs = peptideSettings.Libraries.LibrarySpecs.Where(s => s != null).ToList();
                    if (librarySpecs.Count == 1)
                    {
                        // Single library - show without folder, using same format as other library types
                        var file = SpectralLibrary.Create(documentFilePath, librarySpecs[0], includeTypePrefix: true);
                        list.Add(file);
                    }
                    else if (librarySpecs.Count > 1)
                    {
                        // Multiple libraries - show in folder
                        var files = librarySpecs.Select(library => SpectralLibrary.Create(documentFilePath, library)).ToList();
                        var folder = new SpectralLibrariesFolder(documentFilePath, files);
                        list.Add(folder);
                    }
                }
            }

            if (document.Settings.TransitionSettings is { HasOptimizationLibraryPersisted: true })
            {
                var file = OptimizationLibrary.Create(documentFilePath, document.Settings.TransitionSettings.Prediction.OptimizedLibrary);
                list.Add(file);
            }

            if (document.Settings.TransitionSettings is { HasIonMobilityLibraryPersisted: true })
            {
                var file = IonMobilityLibrary.Create(documentFilePath, document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary);
                list.Add(file);
            }

            { // Chromatogram Cache (.skyd) - only picks out the final cache and not smaller, temporary files
                // Show the chromatograms node if the document has results, even if the .skyd file hasn't been created yet
                // This ensures the node exists at the correct tree index when restoring view state
                if (document.Settings.HasResults)
                {
                    var skydFile = document.Settings.MeasuredResults?.CacheFinal;
                    if (skydFile != null)
                    {
                        list.Add(SkylineChromatogramCache.Create(documentFilePath, document.Settings.MeasuredResults.CacheFinal));
                    }
                    else if (IsDocumentSaved(documentFilePath))
                    {
                        // Document has results but no .skyd file yet - create a node showing the expected path
                        var expectedPath = ChromatogramCache.FinalPathForName(documentFilePath, null);
                        var id = new ChromatogramCacheId();
                        list.Add(new SkylineChromatogramCache(documentFilePath, id, expectedPath));
                    }
                }
            }

            { // Replicates
                var measuredResults = document.MeasuredResults;
                if (document.Settings.HasResults)
                {
                    var files = measuredResults.Chromatograms.Select(chromatogramSet => Replicate.Create(documentFilePath, chromatogramSet)).ToList();
                    var folder = new ReplicatesFolder(documentFilePath, files);

                    list.Add(folder);
                }
            }

            return ImmutableList.ValueOf(list);
        }

        public IList<FileModel> AsList()
        {
            return new SingletonList<FileModel>(this);
        }
    }
}
