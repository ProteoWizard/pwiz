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

using System;
using System.ComponentModel;
using System.Linq;
using System.Resources;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class SpectralLibrary : RootSkylineObject
    {
        private readonly SpectrumSourceFileDetails _fileDetails;
        private readonly LibrarySpec _librarySpec;

        public SpectralLibrary(SkylineDataSchema dataSchema, string filePath) : base(dataSchema)
        {
            _fileDetails = new SpectrumSourceFileDetails(filePath);
            _librarySpec = dataSchema.Document.Settings.PeptideSettings.Libraries.LibrarySpecs
                .FirstOrDefault(libSpec => libSpec.FilePath.Equals(filePath));
        }

        public string Name => _librarySpec.Name;
        public int SpectrumCount => _fileDetails.BestSpectrum;
        public int MatchedCount => _fileDetails.MatchedSpectrum;
        public string LibraryType => _librarySpec.GetLibraryTypeName();

        #region PropertyGrid Support

        public override ResourceManager GetResourceManager() => PropertyGridFileNodeResources.ResourceManager;

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var baseProps = TypeDescriptor.GetProperties(GetType());
            var processedProps = (
                from PropertyDescriptor prop in baseProps
                select PropertyTransform(prop)
            ).Cast<PropertyDescriptor>().ToList();

            return new PropertyDescriptorCollection(processedProps.ToArray());
        }

        #endregion
    }
}
