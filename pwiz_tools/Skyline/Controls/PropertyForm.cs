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

using DigitalRune.Windows.Docking;
using pwiz.Skyline.Controls.FilesTree;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.PropertySheets;
using pwiz.Skyline.Util;
using pwiz.Common.SystemUtil;
using System.ComponentModel;
using System.Windows.Forms;

namespace pwiz.Skyline.Controls
{
    public partial class PropertyForm : DockableFormEx
    {
        private PropertyForm()
        {
            InitializeComponent();
        }
        
        public PropertyForm(SkylineWindow skylineWindow) : this()
        {
            HideOnClose = true; // Hide the form when closed, but do not dispose it
            SkylineWindow = skylineWindow;

            PropertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SkylineWindow SkylineWindow { get; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PropertyGrid PropertyGrid => propertyGrid;

        public void GetProperties(IDockableForm form)
        {
            if (form is IPropertyProvider propertyProviderForm)
            {
                PropertyGrid.SelectedObject = propertyProviderForm.GetSelectedObjectProperties();
                // TODO: Fix bug where PropertyGrid does not expand all nested properties when the form is first shown.
                // Highly possible it has to do with multiple expandable properties having the same display name.
                PropertyGrid.ExpandAllGridItems();
            }
            else
            {
                PropertyGrid.SelectedObject = null;
            }
        }

        private void PropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            Assume.IsTrue(e.ChangedItem.PropertyDescriptor is IModifiablePropertyDescriptor { IsReadOnly: false });
            var globalizedPropertyDescriptor = (IModifiablePropertyDescriptor)e.ChangedItem.PropertyDescriptor;

            Assume.IsTrue(e.ChangedItem.Value is string);
            var newValue = (string)e.ChangedItem.Value;

            // If the property is editable, acquire the document change lock and modify the document,
            // by calling the GetModifiedDocument delegate of the property descriptor.
            lock (SkylineWindow.GetDocumentChangeLock())
            {
                var originalDoc = SkylineWindow.Document;
                ModifiedDocument modifiedDoc = null;

                using var longWaitDlg = new LongWaitDlg();
                longWaitDlg.PerformWork(this, 750, progressMonitor =>
                {
                    using var monitor = new SrmSettingsChangeMonitor(progressMonitor, longWaitDlg.Text, SkylineWindow);

                    modifiedDoc = globalizedPropertyDescriptor.GetModifiedDocument(SkylineWindow.Document, monitor, newValue);
                });

                SkylineWindow.ModifyDocument(FilesTreeResources.Change_ReplicateName, DocumentModifier.FromResult(originalDoc, modifiedDoc));
            }
        }
    }
}
