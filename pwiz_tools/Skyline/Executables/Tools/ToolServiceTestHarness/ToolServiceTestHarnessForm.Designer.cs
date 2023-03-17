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
            lblMethod = new Label();
            lblArgument1 = new Label();
            tbxArgument1 = new TextBox();
            lblArgument2 = new Label();
            tbxArgument2 = new TextBox();
            btnInvokeMethod = new Button();
            lblResult = new Label();
            tbxResult = new TextBox();
            SuspendLayout();
            // 
            // comboMethod
            // 
            comboMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            comboMethod.FormattingEnabled = true;
            resources.ApplyResources(comboMethod, "comboMethod");
            comboMethod.Name = "comboMethod";
            comboMethod.SelectedIndexChanged += comboMethod_SelectedIndexChanged;
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
            // lblMethod
            // 
            resources.ApplyResources(lblMethod, "lblMethod");
            lblMethod.Name = "lblMethod";
            // 
            // lblArgument1
            // 
            resources.ApplyResources(lblArgument1, "lblArgument1");
            lblArgument1.Name = "lblArgument1";
            // 
            // tbxArgument1
            // 
            resources.ApplyResources(tbxArgument1, "tbxArgument1");
            tbxArgument1.Name = "tbxArgument1";
            // 
            // lblArgument2
            // 
            resources.ApplyResources(lblArgument2, "lblArgument2");
            lblArgument2.Name = "lblArgument2";
            // 
            // tbxArgument2
            // 
            resources.ApplyResources(tbxArgument2, "tbxArgument2");
            tbxArgument2.Name = "tbxArgument2";
            // 
            // btnInvokeMethod
            // 
            resources.ApplyResources(btnInvokeMethod, "btnInvokeMethod");
            btnInvokeMethod.Name = "btnInvokeMethod";
            btnInvokeMethod.UseVisualStyleBackColor = true;
            btnInvokeMethod.Click += btnInvokeMethod_Click;
            // 
            // lblResult
            // 
            resources.ApplyResources(lblResult, "lblResult");
            lblResult.Name = "lblResult";
            // 
            // tbxResult
            // 
            resources.ApplyResources(tbxResult, "tbxResult");
            tbxResult.Name = "tbxResult";
            // 
            // ToolServiceTestHarnessForm
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(tbxResult);
            Controls.Add(lblResult);
            Controls.Add(btnInvokeMethod);
            Controls.Add(tbxArgument2);
            Controls.Add(lblArgument2);
            Controls.Add(tbxArgument1);
            Controls.Add(lblArgument1);
            Controls.Add(lblMethod);
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
        private Label lblMethod;
        private Label lblArgument1;
        private TextBox tbxArgument1;
        private Label lblArgument2;
        private TextBox tbxArgument2;
        private Button btnInvokeMethod;
        private Label lblResult;
        private TextBox tbxResult;
    }
}