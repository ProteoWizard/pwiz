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

using System;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.FilesView
{
    // .imsdb
    public class IonMobilityLibrary : FileNode
    {
        private readonly Lazy<IonMobility.IonMobilityLibrary> _lazy;
        public IonMobilityLibrary(SrmDocument document, string documentPath, Identity id) : base(document, documentPath, new IdentityPath(id))
        {
            _lazy = new Lazy<IonMobility.IonMobilityLibrary>(FindIonMobilityLibrary);
        }

        public override Immutable Immutable => _lazy.Value;

        public override string Name => _lazy.Value.Name;
        public override string FilePath => _lazy.Value.FilePath;

        public override bool IsBackedByFile => true;

        private IonMobility.IonMobilityLibrary FindIonMobilityLibrary()
        {
            return Document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary;
        }
    }
}