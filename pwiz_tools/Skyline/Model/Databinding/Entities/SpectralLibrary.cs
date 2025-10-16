/*
 * Original author: Aaron Banse <acbanse .at. icloud dot com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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

using System.ComponentModel;
using System.Linq;
using System.Resources;
using pwiz.Skyline.Model.Files;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class SpectralLibrary : RootSkylineObject
    {
        public SpectralLibrary(SkylineDataSchema dataSchema, string name, string filePath, string localFilePath) : base(dataSchema)
        {
            var library = dataSchema.Document.Settings.PeptideSettings.Libraries.GetLibrary(name);
            var libSpec = library.CreateSpec(filePath);
            var libDetails = library.LibraryDetails;
            var libDataFiles = libDetails.DataFiles.ToList();

            Name = name;
            LocalFilePath = localFilePath;
            LibraryType = libSpec.GetLibraryTypeName();
            FilePath = libSpec.FilePath;
            SpectrumCount = libDetails.SpectrumCount;
            TotalPsmCount = libDetails.TotalPsmCount;
            UniquePeptideCount = libDetails.UniquePeptideCount;
            Id = libDetails.Id;
            Revision = libDetails.Revision;
            Version = libDetails.Version;
            DataFiles = libDataFiles.Count;

            // if all data files have the same score type and threshold, use those values, otherwise null
            ScoreType = null;
            ScoreThreshold = null;
            foreach (var kvp in libDataFiles.Where(sourceFileDetail => sourceFileDetail.ScoreThresholds.Any())
                         .SelectMany(sourceFileDetail => sourceFileDetail.ScoreThresholds))
            {
                if (ScoreType == null)
                {
                    ScoreType = kvp.Key.ToString();
                    ScoreThreshold = kvp.Value;
                }
                else if (ScoreType != kvp.Key.ToString() || ScoreThreshold != kvp.Value)
                {
                    ScoreType = null;
                    ScoreThreshold = null;
                    break;
                }
            }
        }

        public string Name { get; }
        public int DataFiles { get; }
        public string LibraryType { get; }
        public string FilePath { get; }
        public string LocalFilePath { get; }
        public int SpectrumCount { get; }
        public int TotalPsmCount { get; }
        public int UniquePeptideCount { get; }
        public string Id { get; }
        public string Revision { get; }
        public string Version { get; }
        public string ScoreType { get; }
        public double? ScoreThreshold { get; }

        public override ResourceManager GetResourceManager() => PropertyGridFileNodeResources.ResourceManager;

        public override PropertyDescriptorCollection GetProperties() => GetPropertiesReflective();

        protected override bool PropertyFilter(PropertyDescriptor prop)
        {
            return base.PropertyFilter(prop) && prop.GetValue(this) != null;
        }
    }
}
