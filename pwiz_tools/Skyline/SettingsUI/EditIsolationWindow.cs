/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public class EditIsolationWindow
    {
        public double? Start { get; set; }
        public double? End { get; set; }
        public double? Target { get; set; }
        public double? StartMargin { get; set; }
        public double? EndMargin { get; set; }
        public double? CERange { get; set; }

        public void Validate()
        {
            // Construct IsolationWindow to perform validation.
// ReSharper disable ObjectCreationAsStatement
            new IsolationWindow(this);
// ReSharper restore ObjectCreationAsStatement
        }
    }
}
