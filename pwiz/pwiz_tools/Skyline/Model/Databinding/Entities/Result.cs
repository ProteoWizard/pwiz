/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public abstract class Result : SkylineObject, ILinkValue
    {
        private readonly ResultFile _resultFile;
        protected Result(SkylineDocNode docNode, ResultFile resultFile) : base(docNode.DataSchema)
        {
            SkylineDocNode = docNode;
            _resultFile = resultFile;
        }
        [Browsable(false)]
        protected SkylineDocNode SkylineDocNode { get; private set; }

        public ResultFile GetResultFile()
        {
            return _resultFile;
        }

        public abstract override string ToString();
        EventHandler ILinkValue.ClickEventHandler
        {
            get { return LinkValueOnClick; }
        }
        object ILinkValue.Value { get { return this; } }
        public void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }
            skylineWindow.SelectedPath = SkylineDocNode.IdentityPath;
            skylineWindow.SelectedResultsIndex = GetResultFile().Replicate.ReplicateIndex;
        }
    }
}
