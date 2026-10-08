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
using System.Windows.Forms;
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

            // The owner-drawn row height and the 16x16 icons are 96-DPI designs that auto-scaling
            // does not touch, so the scaled text would spill out of the rows (issue #4599). Keep
            // the designed three rows visible: IntegralHeight snaps the list to whole rows later.
            var imageList = listBoxModeUI.ImageList;
            if (DpiUtil.GetFactor(this) > 1)
            {
                imageList.ColorDepth = ColorDepth.Depth32Bit;
                imageList.ImageSize = DpiUtil.ScaleSize(this, imageList.ImageSize);
                listBoxModeUI.ItemHeight = DpiUtil.Scale(this, listBoxModeUI.ItemHeight);
                int rowsVisible = (int) Math.Round((double) listBoxModeUI.ClientSize.Height / listBoxModeUI.ItemHeight);
                listBoxModeUI.Height = rowsVisible * listBoxModeUI.ItemHeight +
                                       listBoxModeUI.Height - listBoxModeUI.ClientSize.Height;
            }

            // N.B. the Imagelist we use here is all blanks, which we then replace here in the ctor. This is done
            // so that if we ever update the proteomic/molecules/mixed bitmaps we don't have to remake this ImageList.
            // Changing ImageSize or ColorDepth above empties the list, so rebuild it in index order.
            var images = new Image[3];
            images[imageListBoxItemProteomics.ImageIndex] = DpiUtil.ScaleImageForList(this, new Bitmap(Resources.UIModeProteomic));
            images[imageListBoxItemMolecule.ImageIndex] = DpiUtil.ScaleImageForList(this, new Bitmap(Resources.UIModeSmallMolecules));
            images[imageListBoxItemMixed.ImageIndex] = DpiUtil.ScaleImageForList(this, new Bitmap(Resources.UIModeMixed));
            imageList.Images.Clear();
            foreach (var image in images)
                imageList.Images.Add(image);

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
