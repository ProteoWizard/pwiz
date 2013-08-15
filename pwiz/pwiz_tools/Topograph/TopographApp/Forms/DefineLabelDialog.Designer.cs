namespace pwiz.Topograph.ui.Forms
{
    partial class DefineLabelDialog
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DefineLabelDialog));
            this.label1 = new System.Windows.Forms.Label();
            this.tbxTracerSymbol = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tbxMassDifference = new System.Windows.Forms.TextBox();
            this.cbxEluteEarlier = new System.Windows.Forms.CheckBox();
            this.cbxEluteLater = new System.Windows.Forms.CheckBox();
            this.btn15N = new System.Windows.Forms.Button();
            this.btnD3Leu = new System.Windows.Forms.Button();
            this.btnSaveAndClose = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.tbxInitialApe = new System.Windows.Forms.TextBox();
            this.tbxFinalApe = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxAtomCount = new System.Windows.Forms.TextBox();
            this.tbxAtomicPercentEnrichment = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.tbxTracerName = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 115);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Tracer Symbol";
            // 
            // tbxTracerSymbol
            // 
            this.tbxTracerSymbol.Location = new System.Drawing.Point(185, 112);
            this.tbxTracerSymbol.Name = "tbxTracerSymbol";
            this.tbxTracerSymbol.Size = new System.Drawing.Size(100, 20);
            this.tbxTracerSymbol.TabIndex = 1;
            this.tbxTracerSymbol.Leave += new System.EventHandler(this.TbxEnrichedSymbolOnLeave);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(7, 144);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(118, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Tracer Mass Difference";
            // 
            // tbxMassDifference
            // 
            this.tbxMassDifference.Location = new System.Drawing.Point(185, 144);
            this.tbxMassDifference.Name = "tbxMassDifference";
            this.tbxMassDifference.Size = new System.Drawing.Size(100, 20);
            this.tbxMassDifference.TabIndex = 2;
            // 
            // cbxEluteEarlier
            // 
            this.cbxEluteEarlier.AutoSize = true;
            this.cbxEluteEarlier.Location = new System.Drawing.Point(405, 106);
            this.cbxEluteEarlier.Name = "cbxEluteEarlier";
            this.cbxEluteEarlier.Size = new System.Drawing.Size(119, 17);
            this.cbxEluteEarlier.TabIndex = 5;
            this.cbxEluteEarlier.Text = "Tracers elute earlier";
            this.cbxEluteEarlier.UseVisualStyleBackColor = true;
            // 
            // cbxEluteLater
            // 
            this.cbxEluteLater.AutoSize = true;
            this.cbxEluteLater.Location = new System.Drawing.Point(405, 129);
            this.cbxEluteLater.Name = "cbxEluteLater";
            this.cbxEluteLater.Size = new System.Drawing.Size(111, 17);
            this.cbxEluteLater.TabIndex = 6;
            this.cbxEluteLater.Text = "Tracers elute later";
            this.cbxEluteLater.UseVisualStyleBackColor = true;
            // 
            // btn15N
            // 
            this.btn15N.Location = new System.Drawing.Point(10, 361);
            this.btn15N.Name = "btn15N";
            this.btn15N.Size = new System.Drawing.Size(75, 23);
            this.btn15N.TabIndex = 11;
            this.btn15N.Text = "15N";
            this.btn15N.UseVisualStyleBackColor = true;
            this.btn15N.Click += new System.EventHandler(this.Btn15NOnClick);
            // 
            // btnD3Leu
            // 
            this.btnD3Leu.Location = new System.Drawing.Point(122, 361);
            this.btnD3Leu.Name = "btnD3Leu";
            this.btnD3Leu.Size = new System.Drawing.Size(75, 23);
            this.btnD3Leu.TabIndex = 12;
            this.btnD3Leu.Text = "D3 Leu";
            this.btnD3Leu.UseVisualStyleBackColor = true;
            this.btnD3Leu.Click += new System.EventHandler(this.BtnD3LeuOnClick);
            // 
            // btnSaveAndClose
            // 
            this.btnSaveAndClose.Location = new System.Drawing.Point(472, 358);
            this.btnSaveAndClose.Name = "btnSaveAndClose";
            this.btnSaveAndClose.Size = new System.Drawing.Size(68, 23);
            this.btnSaveAndClose.TabIndex = 9;
            this.btnSaveAndClose.Text = "OK";
            this.btnSaveAndClose.UseVisualStyleBackColor = true;
            this.btnSaveAndClose.Click += new System.EventHandler(this.BtnSaveAndCloseOnClick);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(399, 235);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(127, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Initial Percent Enrichment";
            // 
            // tbxInitialApe
            // 
            this.tbxInitialApe.Location = new System.Drawing.Point(532, 232);
            this.tbxInitialApe.Name = "tbxInitialApe";
            this.tbxInitialApe.Size = new System.Drawing.Size(100, 20);
            this.tbxInitialApe.TabIndex = 7;
            this.tbxInitialApe.Text = "0";
            // 
            // tbxFinalApe
            // 
            this.tbxFinalApe.Location = new System.Drawing.Point(532, 301);
            this.tbxFinalApe.Name = "tbxFinalApe";
            this.tbxFinalApe.Size = new System.Drawing.Size(100, 20);
            this.tbxFinalApe.TabIndex = 8;
            this.tbxFinalApe.Text = "100";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(402, 304);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(125, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Final Percent Enrichment";
            // 
            // tbxAtomCount
            // 
            this.tbxAtomCount.Location = new System.Drawing.Point(149, 257);
            this.tbxAtomCount.Name = "tbxAtomCount";
            this.tbxAtomCount.Size = new System.Drawing.Size(100, 20);
            this.tbxAtomCount.TabIndex = 3;
            // 
            // tbxAtomicPercentEnrichment
            // 
            this.tbxAtomicPercentEnrichment.Location = new System.Drawing.Point(150, 284);
            this.tbxAtomicPercentEnrichment.Name = "tbxAtomicPercentEnrichment";
            this.tbxAtomicPercentEnrichment.Size = new System.Drawing.Size(100, 20);
            this.tbxAtomicPercentEnrichment.TabIndex = 4;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(22, 260);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(62, 13);
            this.label5.TabIndex = 15;
            this.label5.Text = "Atom Count";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 287);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(135, 13);
            this.label6.TabIndex = 16;
            this.label6.Text = "Atomic Percent Enrichment";
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(3, 74);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(359, 35);
            this.label7.TabIndex = 17;
            this.label7.Text = "Specify the three letter amino acid, or the atomic element symbol of the thing th" +
                "at was used as the tracer in this experiment:";
            // 
            // label8
            // 
            this.label8.Location = new System.Drawing.Point(7, 186);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(337, 68);
            this.label8.TabIndex = 18;
            this.label8.Text = resources.GetString("label8.Text");
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(402, 75);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(243, 28);
            this.label9.TabIndex = 19;
            this.label9.Text = "Specify whether the tracers potentially have a different retention time than the " +
                "tracee.";
            // 
            // label10
            // 
            this.label10.Location = new System.Drawing.Point(402, 164);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(230, 65);
            this.label10.TabIndex = 20;
            this.label10.Text = "Specify the percentage enrichment at the start of the experiment, before the orga" +
                "nism\'s diet was changed (that is, the percent abundance of the tracer above its " +
                "natural abundance):";
            // 
            // label11
            // 
            this.label11.Location = new System.Drawing.Point(399, 260);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(227, 27);
            this.label11.TabIndex = 21;
            this.label11.Text = "Specify the enrichment of the organism\'s new diet at the start of the experiment:" +
                "";
            // 
            // label12
            // 
            this.label12.Location = new System.Drawing.Point(10, 325);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(217, 33);
            this.label12.TabIndex = 22;
            this.label12.Text = "Use these buttons to fill in this form with pre-set values:";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(12, 49);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(72, 13);
            this.label14.TabIndex = 25;
            this.label14.Text = "Tracer Name:";
            // 
            // tbxTracerName
            // 
            this.tbxTracerName.Location = new System.Drawing.Point(120, 47);
            this.tbxTracerName.Name = "tbxTracerName";
            this.tbxTracerName.Size = new System.Drawing.Size(122, 20);
            this.tbxTracerName.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(557, 358);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.BtnCancelOnClick);
            // 
            // label13
            // 
            this.label13.Location = new System.Drawing.Point(7, 5);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(638, 39);
            this.label13.TabIndex = 26;
            this.label13.Text = resources.GetString("label13.Text");
            // 
            // DefineLabelDialog
            // 
            this.AcceptButton = this.btnSaveAndClose;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(650, 393);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbxTracerName);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.tbxAtomicPercentEnrichment);
            this.Controls.Add(this.tbxAtomCount);
            this.Controls.Add(this.tbxFinalApe);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.tbxInitialApe);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnSaveAndClose);
            this.Controls.Add(this.btnD3Leu);
            this.Controls.Add(this.btn15N);
            this.Controls.Add(this.cbxEluteLater);
            this.Controls.Add(this.cbxEluteEarlier);
            this.Controls.Add(this.tbxMassDifference);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.tbxTracerSymbol);
            this.Controls.Add(this.label1);
            this.Name = "DefineLabelDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TabText = "Define Isotope Label";
            this.Text = "Define Isotope Label";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxTracerSymbol;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tbxMassDifference;
        private System.Windows.Forms.CheckBox cbxEluteEarlier;
        private System.Windows.Forms.CheckBox cbxEluteLater;
        private System.Windows.Forms.Button btn15N;
        private System.Windows.Forms.Button btnD3Leu;
        private System.Windows.Forms.Button btnSaveAndClose;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tbxInitialApe;
        private System.Windows.Forms.TextBox tbxFinalApe;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxAtomCount;
        private System.Windows.Forms.TextBox tbxAtomicPercentEnrichment;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox tbxTracerName;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label label13;

    }
}