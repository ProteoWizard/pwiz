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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
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

            propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        public void SetPropertyObject(GlobalizedObject properties)
        {
            propertyGrid.SelectedObject = properties;
            propertyGrid.ExpandAllGridItems();
        }

        public GlobalizedObject GetPropertyObject()
        {
            return propertyGrid.SelectedObject as GlobalizedObject;
        }

        private void PropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) =>
            UpdateDocument(e.ChangedItem.PropertyDescriptor, e.ChangedItem.Value);

        private void UpdateDocument(PropertyDescriptor propertyDescriptor, object newValue)
        {
            Assume.IsTrue(propertyDescriptor is IModifiablePropertyDescriptor { IsReadOnly: false });
            var modifiablePropertyDescriptor = (IModifiablePropertyDescriptor)propertyDescriptor;

            // acquire the document change lock and modify the document by calling the GetModifiedDocument delegate of the property descriptor. 
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDoc = SkylineWindow.Document;
                ModifiedDocument modifiedDoc = null;

                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.PerformWork(this, 750, progressMonitor =>
                {
                    using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);

                    modifiedDoc = modifiablePropertyDescriptor.GetModifiedDocument(SkylineWindow.Document, monitor, newValue);
                });

                SkylineWindow.ModifyDocument(FilesTreeResources.Change_ReplicateName, DocumentModifier.FromResult(originalDoc, modifiedDoc));
            }
        }

        #region Test Support

        public void PropertyObjectValuesModifiedManually(PropertyDescriptor propertyDescriptor)
            => UpdateDocument(propertyDescriptor, propertyDescriptor.GetValue(GetPropertyObject()));

        #endregion
    }
}
