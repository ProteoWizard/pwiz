/*
 * Original author: Aaron Banse <acbanse .at. icloud.com>,
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

using pwiz.Skyline.Util;
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public partial class PropertiesForm : DockableFormEx
    {
        private PropertiesForm()
        {
            InitializeComponent();
            HideOnClose = true; // Hide the form when closed, but do not dispose it
        }

        public PropertiesForm(SkylineWindow skylineWindow) : this()
        {
            SkylineWindow = skylineWindow;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PropertyGrid PropertyGrid => propertyGrid;
    }
}
