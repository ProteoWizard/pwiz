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

using System.Drawing;
using System.Windows.Forms;

namespace pwiz.Skyline.Alerts
{

    partial class NoModeUIDlg
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // N.B. the Imagelist we use here is all blanks, which we then replace in the ctor. This is done
        // so that if we ever update the proteomic/molecules/mixed bitmaps we don't have to remake this ImageList.

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NoModeUIDlg));
            this.imageListModeUI = new System.Windows.Forms.ImageList(this.components);
            this.listBoxModeUI = new pwiz.Skyline.Controls.ImageListBox();
            this.imageListBoxItemProteomics = new pwiz.Skyline.Controls.ImageListBoxItem();
            this.imageListBoxItemMolecule = new pwiz.Skyline.Controls.ImageListBoxItem();
            this.imageListBoxItemMixed = new pwiz.Skyline.Controls.ImageListBoxItem();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            this.SuspendLayout();
            // 
            // imageListModeUI
            // 
            this.imageListModeUI.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageListModeUI.ImageStream")));
            this.imageListModeUI.TransparentColor = System.Drawing.Color.Transparent;
            this.modeUIHandler.SetUIMode(this.imageListModeUI, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.invariant);
            this.imageListModeUI.Images.SetKeyName(0, "Proteomic");
            this.imageListModeUI.Images.SetKeyName(1, "Molecules");
            this.imageListModeUI.Images.SetKeyName(2, "Mixed");
            // 
            // listBoxModeUI
            // 
            this.listBoxModeUI.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            resources.ApplyResources(this.listBoxModeUI, "listBoxModeUI");
            this.listBoxModeUI.ImageList = this.imageListModeUI;
            this.listBoxModeUI.Items.AddRange(new pwiz.Skyline.Controls.ImageListBoxItem[] {
            this.imageListBoxItemProteomics,
            this.imageListBoxItemMolecule,
            this.imageListBoxItemMixed});
            this.listBoxModeUI.Name = "listBoxModeUI";
            this.modeUIHandler.SetUIMode(this.listBoxModeUI, pwiz.Skyline.Util.Helpers.ModeUIExtender.MODE_UI_HANDLING_TYPE.invariant);
            // 
            // imageListBoxItemProteomics
            // 
            resources.ApplyResources(this.imageListBoxItemProteomics, "imageListBoxItemProteomics");
            // 
            // imageListBoxItemMolecule
            // 
            resources.ApplyResources(this.imageListBoxItemMolecule, "imageListBoxItemMolecule");
            // 
            // imageListBoxItemMixed
            // 
            resources.ApplyResources(this.imageListBoxItemMixed, "imageListBoxItemMixed");
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // buttonOK
            // 
            resources.ApplyResources(this.buttonOK, "buttonOK");
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // NoModeUIDlg
            // 
            this.AcceptButton = this.buttonOK;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ControlBox = false;
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.listBoxModeUI);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NoModeUIDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private pwiz.Skyline.Controls.ImageListBox listBoxModeUI;
        private System.Windows.Forms.ImageList imageListModeUI;
        private Controls.ImageListBoxItem imageListBoxItemProteomics;
        private Controls.ImageListBoxItem imageListBoxItemMolecule;
        private Controls.ImageListBoxItem imageListBoxItemMixed;
        private Label label2;
        private Label label1;
        private Button buttonOK;
    }

}