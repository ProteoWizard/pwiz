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

namespace pwiz.Skyline.Model.Files
{
    public class BackgroundProteome : FileNode
    {
        private readonly Lazy<Model.Proteome.BackgroundProteome> _backgroundProteome;

        public BackgroundProteome(SrmDocument document, string documentPath, Identity backgroundProteomeId) : 
            base(document, documentPath, new IdentityPath(backgroundProteomeId))
        {
            _backgroundProteome = new Lazy<Model.Proteome.BackgroundProteome>(FindBackgroundProteome);
        }

        public override Immutable Immutable { get => _backgroundProteome.Value; }

        public override string Name => _backgroundProteome.Value.Name;
        public override string FilePath => _backgroundProteome.Value.FilePath;
        public override string FileName => _backgroundProteome.Value.FileName;

        public override bool IsBackedByFile => true;

        private Model.Proteome.BackgroundProteome FindBackgroundProteome()
        {
            return Document.Settings.PeptideSettings.BackgroundProteome;
        }
    }
}