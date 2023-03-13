namespace ToolServiceTestHarness
{
    partial class ToolServiceTestHarnessForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolServiceTestHarnessForm));
            comboMethod = new ComboBox();
            lblConnection = new Label();
            tbxConnection = new TextBox();
            SuspendLayout();
            // 
            // comboMethod
            // 
            comboMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            comboMethod.FormattingEnabled = true;
            resources.ApplyResources(comboMethod, "comboMethod");
            comboMethod.Name = "comboMethod";
            // 
            // lblConnection
            // 
            resources.ApplyResources(lblConnection, "lblConnection");
            lblConnection.Name = "lblConnection";
            // 
            // tbxConnection
            // 
            resources.ApplyResources(tbxConnection, "tbxConnection");
            tbxConnection.Name = "tbxConnection";
            // 
            // ToolServiceTestHarnessForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tbxConnection);
            Controls.Add(lblConnection);
            Controls.Add(comboMethod);
            Name = "ToolServiceTestHarnessForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox comboMethod;
        private Label lblConnection;
        private TextBox tbxConnection;
    }
}