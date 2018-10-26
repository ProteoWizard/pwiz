namespace pwiz.Skyline.Controls.Startup
{
    partial class ActionBoxControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.labelCaption = new System.Windows.Forms.Label();
            this.labelDescription = new System.Windows.Forms.Label();
            this.iconPictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // labelCaption
            // 
            this.labelCaption.AutoSize = true;
            this.labelCaption.Location = new System.Drawing.Point(5, 155);
            this.labelCaption.Name = "labelCaption";
            this.labelCaption.Size = new System.Drawing.Size(0, 15);
            this.labelCaption.TabIndex = 0;
            this.labelCaption.Click += new System.EventHandler(this.ControlClick);
            this.labelCaption.MouseEnter += new System.EventHandler(this.labelCaption_MouseEnter);
            this.labelCaption.MouseLeave += new System.EventHandler(this.labelCaption_MouseLeave);
            // 
            // labelDescription
            // 
            this.labelDescription.AccessibleName = "labelDescription";
            this.labelDescription.AutoEllipsis = true;
            this.labelDescription.Location = new System.Drawing.Point(10, 10);
            this.labelDescription.Name = "labelDescription";
            this.labelDescription.Size = new System.Drawing.Size(160, 138);
            this.labelDescription.TabIndex = 1;
            this.labelDescription.Visible = false;
            this.labelDescription.Click += new System.EventHandler(this.ControlClick);
            this.labelDescription.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.labelDescription.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            // 
            // iconPictureBox
            // 
            this.iconPictureBox.AccessibleName = "Icon";
            this.iconPictureBox.Location = new System.Drawing.Point(10, 10);
            this.iconPictureBox.Name = "iconPictureBox";
            this.iconPictureBox.Size = new System.Drawing.Size(160, 138);
            this.iconPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.iconPictureBox.TabIndex = 2;
            this.iconPictureBox.TabStop = false;
            this.iconPictureBox.Click += new System.EventHandler(this.ControlClick);
            this.iconPictureBox.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.iconPictureBox.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            // 
            // ActionBoxControl
            // 
            this.AccessibleName = "ActionBoxControl";
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.iconPictureBox);
            this.Controls.Add(this.labelDescription);
            this.Controls.Add(this.labelCaption);
            this.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "ActionBoxControl";
            this.Padding = new System.Windows.Forms.Padding(6, 5, 6, 5);
            this.Size = new System.Drawing.Size(182, 182);
            this.Click += new System.EventHandler(this.ControlClick);
            this.MouseEnter += new System.EventHandler(this.ControlMouseEnter);
            this.MouseLeave += new System.EventHandler(this.ControlMouseLeave);
            ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelCaption;
        private System.Windows.Forms.Label labelDescription;
        private System.Windows.Forms.PictureBox iconPictureBox;

    }
}
