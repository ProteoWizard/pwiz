/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Form for choosing which annotation definitions should be included in the document
    /// </summary>
    public partial class ChooseAnnotationsDlg : FormEx
    {
        private readonly SkylineWindow _parent;
        private readonly SettingsListBoxDriver<AnnotationDef> _annotationsListBoxDriver;

        public ChooseAnnotationsDlg(SkylineWindow parent)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _parent = parent;
            _annotationsListBoxDriver = new SettingsListBoxDriver<AnnotationDef>(checkedListBoxAnnotations, Settings.Default.AnnotationDefList);
            _annotationsListBoxDriver.LoadList(null, parent.DocumentUI.Settings.DataSettings.AnnotationDefs);
        }

        private void btnEditAnnotationList_Click(object sender, EventArgs e)
        {
            EditList();
        }

        public void EditList()
        {
            CheckDisposed();
            _annotationsListBoxDriver.EditList();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            _parent.ModifyDocument(Resources.ChooseAnnotationsDlg_OkDialog_Change_Annotation_Settings, ChangeAnnotationDefs);
            Close();
        }

        /// <summary>
        /// Changes the set of annotation definitions in the document to the ones chosen
        /// in the checkedlistbox.  Also, walks the nodes in the document tree and removes any
        /// annotation values that is no longer defined in the document.
        /// </summary>
        private SrmDocument ChangeAnnotationDefs(SrmDocument srmDocument)
        {
            var dataSettings = srmDocument.Settings.DataSettings;
            dataSettings = dataSettings.ChangeAnnotationDefs(_annotationsListBoxDriver.Chosen);
            if (dataSettings.Equals(srmDocument.Settings.DataSettings))
            {
                return srmDocument;
            }
            srmDocument = srmDocument.ChangeSettings(srmDocument.Settings.ChangeDataSettings(dataSettings));
            var annotationNames = new HashSet<string>();
            foreach (var annotationDef in dataSettings.AnnotationDefs)
            {
                annotationNames.Add(annotationDef.Name);
            }
            srmDocument = (SrmDocument) srmDocument.StripAnnotationValues(annotationNames);
            return srmDocument;
        }

        public CheckedListBox AnnotationsCheckedListBox
        {
            get { return checkedListBoxAnnotations; }
        }
    }
}
