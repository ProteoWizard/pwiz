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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls.FilesTree
{
    // .imsdb
    public class IonMobilityLibraryFolder : FileNode
    {
        private static readonly Identity IMSDB_FOLDER = new StaticFolderId();

        public IonMobilityLibraryFolder(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(IMSDB_FOLDER), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document.Settings.TransitionSettings;

        public override string Name => SkylineResources.SkylineWindow_FindIonMobilityLibrary_Ion_Mobility_Library;
        public override string FilePath => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.TransitionSettings == null || !Document.Settings.TransitionSettings.HasIonMobilityLibraryPersisted)
                {
                    return Array.Empty<Replicate>().ToList<FileNode>();
                }

                return ImmutableList<FileNode>.Singleton(new IonMobilityLibrary(Document, DocumentPath, Document.Settings.TransitionSettings.IonMobilityFiltering.IonMobilityLibrary.Id));
            }
        }
    }
}
