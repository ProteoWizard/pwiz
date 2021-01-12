using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Skyline.EditUI
{
    partial class PermuteIsotopeModificationsDlg
    {
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PermuteIsotopeModificationsDlg));
            this.label1 = new System.Windows.Forms.Label();
            this.comboIsotopeModification = new System.Windows.Forms.ComboBox();
            this.groupBoxPermutationStyle = new System.Windows.Forms.GroupBox();
            this.radioButtonComplexPermutation = new System.Windows.Forms.RadioButton();
            this.radioButtonSimplePermutation = new System.Windows.Forms.RadioButton();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.groupBoxPermutationStyle.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // comboIsotopeModification
            // 
            this.comboIsotopeModification.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboIsotopeModification.FormattingEnabled = true;
            resources.ApplyResources(this.comboIsotopeModification, "comboIsotopeModification");
            this.comboIsotopeModification.Name = "comboIsotopeModification";
            this.comboIsotopeModification.SelectedIndexChanged += new System.EventHandler(this.comboIsotopeModification_SelectedIndexChanged);
            // 
            // groupBoxPermutationStyle
            // 
            resources.ApplyResources(this.groupBoxPermutationStyle, "groupBoxPermutationStyle");
            this.groupBoxPermutationStyle.Controls.Add(this.radioButtonComplexPermutation);
            this.groupBoxPermutationStyle.Controls.Add(this.radioButtonSimplePermutation);
            this.groupBoxPermutationStyle.Name = "groupBoxPermutationStyle";
            this.groupBoxPermutationStyle.TabStop = false;
            // 
            // radioButtonComplexPermutation
            // 
            resources.ApplyResources(this.radioButtonComplexPermutation, "radioButtonComplexPermutation");
            this.radioButtonComplexPermutation.Name = "radioButtonComplexPermutation";
            this.toolTip1.SetToolTip(this.radioButtonComplexPermutation, resources.GetString("radioButtonComplexPermutation.ToolTip"));
            this.radioButtonComplexPermutation.UseVisualStyleBackColor = true;
            // 
            // radioButtonSimplePermutation
            // 
            resources.ApplyResources(this.radioButtonSimplePermutation, "radioButtonSimplePermutation");
            this.radioButtonSimplePermutation.Checked = true;
            this.radioButtonSimplePermutation.Name = "radioButtonSimplePermutation";
            this.radioButtonSimplePermutation.TabStop = true;
            this.toolTip1.SetToolTip(this.radioButtonSimplePermutation, resources.GetString("radioButtonSimplePermutation.ToolTip"));
            this.radioButtonSimplePermutation.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            resources.ApplyResources(this.btnOK, "btnOK");
            this.btnOK.Name = "btnOK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // PermuteIsotopeModificationsDlg
            // 
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.groupBoxPermutationStyle);
            this.Controls.Add(this.comboIsotopeModification);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PermuteIsotopeModificationsDlg";
            this.ShowIcon = false;
            this.groupBoxPermutationStyle.ResumeLayout(false);
            this.groupBoxPermutationStyle.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboIsotopeModification;
        private System.Windows.Forms.GroupBox groupBoxPermutationStyle;
        private System.Windows.Forms.RadioButton radioButtonComplexPermutation;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.RadioButton radioButtonSimplePermutation;



    }
}
