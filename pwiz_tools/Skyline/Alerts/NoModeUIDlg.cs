/*
 * Original author: Brian Pratt<bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Drawing;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{

    public partial class NoModeUIDlg : FormEx
    {
        public SrmDocument.DOCUMENT_TYPE SelectedDocumentType { get; private set; }

        public NoModeUIDlg()
        {
            InitializeComponent();

            // N.B. the Imagelist we use here is all blanks, which we then replace here in the ctor. This is done
            // so that if we ever update the proteomic/molecules/mixed bitmaps we don't have to remake this ImageList.
            imageListBoxItemProteomics.ImageList.Images[imageListBoxItemProteomics.ImageIndex] = new Bitmap(Resources.UIModeProteomic);
            imageListBoxItemMolecule.ImageList.Images[imageListBoxItemMolecule.ImageIndex] = new Bitmap(Resources.UIModeSmallMolecules);
            imageListBoxItemMixed.ImageList.Images[imageListBoxItemMixed.ImageIndex] = new Bitmap(Resources.UIModeMixed);

            SelectModeUI(SrmDocument.DOCUMENT_TYPE.proteomic); // Make a guess at proteomic
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            SelectedDocumentType = (SrmDocument.DOCUMENT_TYPE) listBoxModeUI.SelectedIndex;
            Settings.Default.UIMode = UiModes.FromDocumentType(SelectedDocumentType);
            Close();
        }

        #region testing support
        public void SelectModeUI(SrmDocument.DOCUMENT_TYPE doctype)
        {
            listBoxModeUI.SelectedIndex = (int)doctype;
        }
        public void ClickOk()
        {
            buttonOK_Click(null, null);
        }
        #endregion

    }
}
