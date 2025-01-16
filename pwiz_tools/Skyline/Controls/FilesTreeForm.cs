﻿/*
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class FilesTreeForm : DockableFormEx
    {
        public FilesTreeForm(IDocumentUIContainer documentContainer)
        {
            InitializeComponent();

            filesTree.InitializeTree(documentContainer);
        }

        public FilesTree FilesTree { get { return filesTree; } }

        protected override string GetPersistentString()
        {
            return base.GetPersistentString() + @"|" + FilesTree.GetPersistentString();
        }
    }
}