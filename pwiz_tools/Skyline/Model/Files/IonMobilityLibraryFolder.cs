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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    // .imsdb
    public class IonMobilityLibraryFolder : FileNode
    {
        private static readonly Identity IMSDB_FOLDER = new StaticFolderId();

        public IonMobilityLibraryFolder(IDocumentContainer documentContainer) : 
            base(documentContainer, new IdentityPath(IMSDB_FOLDER), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document.Settings.TransitionSettings;
        public override string Name => SkylineResources.SkylineWindow_FindIonMobilityLibrary_Ion_Mobility_Library;
        public override string FilePath => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.TransitionSettings is { HasIonMobilityLibraryPersisted: true })
                {
                    var model = new IonMobilityLibrary(DocumentContainer, Document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.Id);
                    return ImmutableList<FileNode>.Singleton(model);
                }
                else 
                {
                    return ImmutableList.Empty<FileNode>();
                }
            }
        }
    }
}
