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

using System;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Util;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public partial class PropertyGridForm : DockableFormEx
    {
        public PropertyGridForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            HideOnClose = true; // Hide the form when closed, but do not dispose it
            SkylineWindow = skylineWindow;
            ((IDocumentUIContainer)SkylineWindow).ListenUI(SkylineWindow_OnUiDocumentChanged);
        }

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

        private void SkylineWindow_OnUiDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            SkylineWindow.UpdatePropertyGrid();
        }

        #region Test Support

        public GridItem GetGridItemByPropName(string name)
        {
            if (propertyGrid == null || string.IsNullOrEmpty(name))
                return null;

            // Get the root grid item (the "Properties" category)
            var root = propertyGrid.SelectedGridItem;
            while (root?.Parent != null)
                root = root.Parent;

            if (root == null)
                return null;

            // Search all children for a property with the given name
            return (from GridItem category in root.GridItems from GridItem item in category.GridItems select item)
                .FirstOrDefault(item => item.PropertyDescriptor != null && item.PropertyDescriptor.Name == name);
        }

        #endregion
    }
}
