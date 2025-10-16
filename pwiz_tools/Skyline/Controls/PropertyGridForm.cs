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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Files;
using Replicate = pwiz.Skyline.Model.Databinding.Entities.Replicate;

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
            propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;
        }

        private SkylineWindow SkylineWindow { get; }

        public void SetPropertyObject(RootSkylineObject properties)
        {
            propertyGrid.SelectedObject = properties;
            propertyGrid.ExpandAllGridItems();
            propertyGrid.Refresh();
        }

        public RootSkylineObject GetPropertyObject()
        {
            return propertyGrid.SelectedObject as RootSkylineObject;
        }

        private void SkylineWindow_OnUiDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            SkylineWindow.UpdatePropertyGrid();
        }

        private void PropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            var descriptor = e.ChangedItem.PropertyDescriptor;
            // If a replicate Name property was set to be the same as some other replicate, this is invalid.
            if (propertyGrid.SelectedObject is Replicate replicate && descriptor?.Name == nameof(Replicate.Name))
            {
                var newName = descriptor.GetValue(replicate);
                var currentIndex = replicate.ReplicateIndex;

                // Check if any other replicate has this name
                if (SkylineWindow.Document.Settings.MeasuredResults.Chromatograms
                    .Select((chromSet, index) => new { chromSet, index })
                    .Any(x => x.chromSet.Name == (string)newName && x.index != currentIndex))
                {
                    MessageDlg.Show(this, string.Format(PropertyGridFileNodeResources.ReplicateName_There_is_already_a_replicate_named___0___, newName));
                    // Revert to old value
                    Assume.IsTrue(descriptor is PropertyGridPropertyDescriptor);
                    ((PropertyGridPropertyDescriptor)descriptor).SetDisplayValue(propertyGrid.SelectedObject, e.OldValue);
                    propertyGrid.Refresh();
                }
            }
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
