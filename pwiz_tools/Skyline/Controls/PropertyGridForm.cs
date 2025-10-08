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

using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;
using pwiz.Common.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public partial class PropertyGridForm : DockableFormEx
    {
        private PropertyGridForm()
        {
            InitializeComponent();
        }

        public PropertyGridForm(SkylineWindow skylineWindow) : this()
        {
            HideOnClose = true; // Hide the form when closed, but do not dispose it
            SkylineWindow = skylineWindow;

            SkylineWindow.Listen(SkylineWindow_OnDocumentChanged);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        public void SetPropertyObject(SkylineObject properties)
        {
            propertyGrid.SelectedObject = properties;
            propertyGrid.ExpandAllGridItems();
            propertyGrid.Refresh();
        }

        public SkylineObject GetPropertyObject()
        {
            return propertyGrid.SelectedObject as SkylineObject;
        }

        private void SkylineWindow_OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            Invoke(new MethodInvoker(() => SkylineWindow.UpdatePropertyGrid()));
        }

        #region Test Support

        public bool PropertyIsNullOrNotFound(string propName)
        {
            return GetPropertyObject() != null &&
                   GetPropertyObject().GetProperties()[propName] != null &&
                   ((string)GetPropertyObject().GetProperties()[propName].GetValue(GetPropertyObject())).IsNullOrEmpty();
        }

        #endregion
    }
}
