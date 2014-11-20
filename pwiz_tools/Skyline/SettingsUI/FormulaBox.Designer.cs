namespace pwiz.Skyline.SettingsUI
{
    partial class FormulaBox
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
            this.components = new System.ComponentModel.Container();
            this.labelFormula = new System.Windows.Forms.Label();
            this.labelAverage = new System.Windows.Forms.Label();
            this.labelMono = new System.Windows.Forms.Label();
            this.textFormula = new System.Windows.Forms.TextBox();
            this.textAverage = new System.Windows.Forms.TextBox();
            this.textMono = new System.Windows.Forms.TextBox();
            this.btnFormula = new System.Windows.Forms.Button();
            this.contextFormula = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.hToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.h2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.c13ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.n15ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.oToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.o18ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.sToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextFormula.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelFormula
            // 
            this.labelFormula.AutoSize = true;
            this.labelFormula.Location = new System.Drawing.Point(-3, 0);
            this.labelFormula.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFormula.Name = "labelFormula";
            this.labelFormula.Size = new System.Drawing.Size(98, 17);
            this.labelFormula.TabIndex = 0;
            this.labelFormula.Text = "<placeholder>";
            // 
            // labelAverage
            // 
            this.labelAverage.AutoSize = true;
            this.labelAverage.Location = new System.Drawing.Point(169, 66);
            this.labelAverage.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelAverage.Name = "labelAverage";
            this.labelAverage.Size = new System.Drawing.Size(98, 17);
            this.labelAverage.TabIndex = 5;
            this.labelAverage.Text = "<placeholder>";
            // 
            // labelMono
            // 
            this.labelMono.AutoSize = true;
            this.labelMono.Location = new System.Drawing.Point(-3, 66);
            this.labelMono.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelMono.Name = "labelMono";
            this.labelMono.Size = new System.Drawing.Size(98, 17);
            this.labelMono.TabIndex = 3;
            this.labelMono.Text = "<placeholder>";
            // 
            // textFormula
            // 
            this.textFormula.Location = new System.Drawing.Point(0, 21);
            this.textFormula.Margin = new System.Windows.Forms.Padding(4);
            this.textFormula.Name = "textFormula";
            this.textFormula.Size = new System.Drawing.Size(239, 22);
            this.textFormula.TabIndex = 1;
            this.textFormula.TextChanged += new System.EventHandler(this.textFormula_TextChanged);
            // 
            // textAverage
            // 
            this.textAverage.Location = new System.Drawing.Point(172, 86);
            this.textAverage.Margin = new System.Windows.Forms.Padding(4);
            this.textAverage.Name = "textAverage";
            this.textAverage.Size = new System.Drawing.Size(132, 22);
            this.textAverage.TextChanged += new System.EventHandler(this.textAverage_TextChanged);
            this.textAverage.TabIndex = 6;
            // 
            // textMono
            // 
            this.textMono.Location = new System.Drawing.Point(0, 86);
            this.textMono.Margin = new System.Windows.Forms.Padding(4);
            this.textMono.Name = "textMono";
            this.textMono.Size = new System.Drawing.Size(132, 22);
            this.textMono.TextChanged += new System.EventHandler(this.textMono_TextChanged);
            this.textMono.TabIndex = 4;
            // 
            // btnFormula
            // 
            this.btnFormula.Location = new System.Drawing.Point(248, 17);
            this.btnFormula.Margin = new System.Windows.Forms.Padding(4);
            this.btnFormula.Name = "btnFormula";
            this.btnFormula.Size = new System.Drawing.Size(31, 28);
            this.btnFormula.TabIndex = 2;
            this.btnFormula.UseVisualStyleBackColor = true;
            this.btnFormula.Click += new System.EventHandler(this.btnFormula_Click);
            // 
            // contextFormula
            // 
            this.contextFormula.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hToolStripMenuItem,
            this.h2ToolStripMenuItem,
            this.cToolStripMenuItem,
            this.c13ToolStripMenuItem,
            this.nToolStripMenuItem,
            this.n15ToolStripMenuItem,
            this.oToolStripMenuItem,
            this.o18ToolStripMenuItem,
            this.pToolStripMenuItem,
            this.sToolStripMenuItem});
            this.contextFormula.Name = "contextFormula";
            this.contextFormula.Size = new System.Drawing.Size(106, 244);
            // 
            // hToolStripMenuItem
            // 
            this.hToolStripMenuItem.Name = "hToolStripMenuItem";
            this.hToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.hToolStripMenuItem.Text = "H";
            this.hToolStripMenuItem.Click += new System.EventHandler(this.hToolStripMenuItem_Click);
            // 
            // h2ToolStripMenuItem
            // 
            this.h2ToolStripMenuItem.Name = "h2ToolStripMenuItem";
            this.h2ToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.h2ToolStripMenuItem.Text = "2H";
            this.h2ToolStripMenuItem.Click += new System.EventHandler(this.h2ToolStripMenuItem_Click);
            // 
            // cToolStripMenuItem
            // 
            this.cToolStripMenuItem.Name = "cToolStripMenuItem";
            this.cToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.cToolStripMenuItem.Text = "C";
            this.cToolStripMenuItem.Click += new System.EventHandler(this.cToolStripMenuItem_Click);
            // 
            // c13ToolStripMenuItem
            // 
            this.c13ToolStripMenuItem.Name = "c13ToolStripMenuItem";
            this.c13ToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.c13ToolStripMenuItem.Text = "13C";
            this.c13ToolStripMenuItem.Click += new System.EventHandler(this.c13ToolStripMenuItem_Click);
            // 
            // nToolStripMenuItem
            // 
            this.nToolStripMenuItem.Name = "nToolStripMenuItem";
            this.nToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.nToolStripMenuItem.Text = "N";
            this.nToolStripMenuItem.Click += new System.EventHandler(this.nToolStripMenuItem_Click);
            // 
            // n15ToolStripMenuItem
            // 
            this.n15ToolStripMenuItem.Name = "n15ToolStripMenuItem";
            this.n15ToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.n15ToolStripMenuItem.Text = "15N";
            this.n15ToolStripMenuItem.Click += new System.EventHandler(this.n15ToolStripMenuItem_Click);
            // 
            // oToolStripMenuItem
            // 
            this.oToolStripMenuItem.Name = "oToolStripMenuItem";
            this.oToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.oToolStripMenuItem.Text = "O";
            this.oToolStripMenuItem.Click += new System.EventHandler(this.oToolStripMenuItem_Click);
            // 
            // o18ToolStripMenuItem
            // 
            this.o18ToolStripMenuItem.Name = "o18ToolStripMenuItem";
            this.o18ToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.o18ToolStripMenuItem.Text = "18O";
            this.o18ToolStripMenuItem.Click += new System.EventHandler(this.o18ToolStripMenuItem_Click);
            // 
            // pToolStripMenuItem
            // 
            this.pToolStripMenuItem.Name = "pToolStripMenuItem";
            this.pToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.pToolStripMenuItem.Text = "P";
            this.pToolStripMenuItem.Click += new System.EventHandler(this.pToolStripMenuItem_Click);
            // 
            // sToolStripMenuItem
            // 
            this.sToolStripMenuItem.Name = "sToolStripMenuItem";
            this.sToolStripMenuItem.Size = new System.Drawing.Size(105, 24);
            this.sToolStripMenuItem.Text = "S";
            this.sToolStripMenuItem.Click += new System.EventHandler(this.sToolStripMenuItem_Click);
            // 
            // FormulaBox
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnFormula);
            this.Controls.Add(this.textMono);
            this.Controls.Add(this.textAverage);
            this.Controls.Add(this.textFormula);
            this.Controls.Add(this.labelMono);
            this.Controls.Add(this.labelAverage);
            this.Controls.Add(this.labelFormula);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "FormulaBox";
            this.Size = new System.Drawing.Size(307, 113);
            this.contextFormula.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelFormula;
        private System.Windows.Forms.Label labelAverage;
        private System.Windows.Forms.Label labelMono;
        private System.Windows.Forms.TextBox textFormula;
        private System.Windows.Forms.TextBox textAverage;
        private System.Windows.Forms.TextBox textMono;
        private System.Windows.Forms.Button btnFormula;
        private System.Windows.Forms.ContextMenuStrip contextFormula;
        private System.Windows.Forms.ToolStripMenuItem hToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem h2ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem c13ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem n15ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem oToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem o18ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem sToolStripMenuItem;
    }
}
