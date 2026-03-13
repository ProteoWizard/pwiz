namespace pwiz.Skyline.Menus
{
    partial class RetentionTimesContextMenu
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(RetentionTimesContextMenu));
            this.timeGraphContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replicateComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePeptideComparisonContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.regressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scoreToRunToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runToRunToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.schedulingContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePlotContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeCorrelationContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeResidualsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePointsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeTargetsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.targetsAt1FDRToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeStandardsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeDecoysContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rtValueMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.allRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwhmRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fwbRTValueContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showRTLegendContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.refineRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.predictionRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setRTThresholdContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.setRegressionMethodContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.linearRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.kernelDensityEstimationContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loessContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator22 = new System.Windows.Forms.ToolStripSeparator();
            this.createRTRegressionContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.chooseCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.placeholderToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorCalculators = new System.Windows.Forms.ToolStripSeparator();
            this.addCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.updateCalculatorContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator23 = new System.Windows.Forms.ToolStripSeparator();
            this.removeRTOutliersContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeRTContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timePropsContextMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator38 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuRetentionTimes = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextMenuRetentionTimes.SuspendLayout();
            this.SuspendLayout();
            //
            // contextMenuRetentionTimes
            //
            this.contextMenuRetentionTimes.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeGraphContextMenuItem,
            this.timePlotContextMenuItem,
            this.timePointsContextMenuItem,
            this.rtValueMenuItem,
            this.showRTLegendContextMenuItem,
            this.refineRTContextMenuItem,
            this.predictionRTContextMenuItem,
            this.setRTThresholdContextMenuItem,
            this.setRegressionMethodContextMenuItem,
            this.toolStripSeparator22,
            this.createRTRegressionContextMenuItem,
            this.chooseCalculatorContextMenuItem,
            this.toolStripSeparator23,
            this.removeRTOutliersContextMenuItem,
            this.removeRTContextMenuItem,
            this.timePropsContextMenuItem,
            this.toolStripSeparator38});
            this.contextMenuRetentionTimes.Name = "contextMenuRetentionTimes";
            resources.ApplyResources(this.contextMenuRetentionTimes, "contextMenuRetentionTimes");
            //
            // timeGraphContextMenuItem
            //
            this.timeGraphContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.replicateComparisonContextMenuItem,
            this.timePeptideComparisonContextMenuItem,
            this.regressionContextMenuItem,
            this.schedulingContextMenuItem});
            this.timeGraphContextMenuItem.Name = "timeGraphContextMenuItem";
            resources.ApplyResources(this.timeGraphContextMenuItem, "timeGraphContextMenuItem");
            this.timeGraphContextMenuItem.DropDownOpening += new System.EventHandler(this.timeGraphMenuItem_DropDownOpening);
            //
            // replicateComparisonContextMenuItem
            //
            this.replicateComparisonContextMenuItem.CheckOnClick = true;
            this.replicateComparisonContextMenuItem.Name = "replicateComparisonContextMenuItem";
            resources.ApplyResources(this.replicateComparisonContextMenuItem, "replicateComparisonContextMenuItem");
            this.replicateComparisonContextMenuItem.Click += new System.EventHandler(this.replicateComparisonMenuItem_Click);
            //
            // timePeptideComparisonContextMenuItem
            //
            this.timePeptideComparisonContextMenuItem.Name = "timePeptideComparisonContextMenuItem";
            resources.ApplyResources(this.timePeptideComparisonContextMenuItem, "timePeptideComparisonContextMenuItem");
            this.timePeptideComparisonContextMenuItem.Click += new System.EventHandler(this.timePeptideComparisonMenuItem_Click);
            //
            // regressionContextMenuItem
            //
            this.regressionContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.scoreToRunToolStripMenuItem,
            this.runToRunToolStripMenuItem});
            this.regressionContextMenuItem.Name = "regressionContextMenuItem";
            resources.ApplyResources(this.regressionContextMenuItem, "regressionContextMenuItem");
            //
            // scoreToRunToolStripMenuItem
            //
            this.scoreToRunToolStripMenuItem.Name = "scoreToRunToolStripMenuItem";
            resources.ApplyResources(this.scoreToRunToolStripMenuItem, "scoreToRunToolStripMenuItem");
            this.scoreToRunToolStripMenuItem.Click += new System.EventHandler(this.regressionMenuItem_Click);
            //
            // runToRunToolStripMenuItem
            //
            this.runToRunToolStripMenuItem.Name = "runToRunToolStripMenuItem";
            resources.ApplyResources(this.runToRunToolStripMenuItem, "runToRunToolStripMenuItem");
            this.runToRunToolStripMenuItem.Click += new System.EventHandler(this.fullReplicateComparisonToolStripMenuItem_Click);
            //
            // schedulingContextMenuItem
            //
            this.schedulingContextMenuItem.Name = "schedulingContextMenuItem";
            resources.ApplyResources(this.schedulingContextMenuItem, "schedulingContextMenuItem");
            this.schedulingContextMenuItem.Click += new System.EventHandler(this.schedulingMenuItem_Click);
            //
            // timePlotContextMenuItem
            //
            this.timePlotContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeCorrelationContextMenuItem,
            this.timeResidualsContextMenuItem});
            this.timePlotContextMenuItem.Name = "timePlotContextMenuItem";
            resources.ApplyResources(this.timePlotContextMenuItem, "timePlotContextMenuItem");
            //
            // timeCorrelationContextMenuItem
            //
            this.timeCorrelationContextMenuItem.Name = "timeCorrelationContextMenuItem";
            resources.ApplyResources(this.timeCorrelationContextMenuItem, "timeCorrelationContextMenuItem");
            this.timeCorrelationContextMenuItem.Click += new System.EventHandler(this.timeCorrelationContextMenuItem_Click);
            //
            // timeResidualsContextMenuItem
            //
            this.timeResidualsContextMenuItem.Name = "timeResidualsContextMenuItem";
            resources.ApplyResources(this.timeResidualsContextMenuItem, "timeResidualsContextMenuItem");
            this.timeResidualsContextMenuItem.Click += new System.EventHandler(this.timeResidualsContextMenuItem_Click);
            //
            // timePointsContextMenuItem
            //
            this.timePointsContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.timeTargetsContextMenuItem,
            this.targetsAt1FDRToolStripMenuItem,
            this.timeStandardsContextMenuItem,
            this.timeDecoysContextMenuItem});
            this.timePointsContextMenuItem.Name = "timePointsContextMenuItem";
            resources.ApplyResources(this.timePointsContextMenuItem, "timePointsContextMenuItem");
            //
            // timeTargetsContextMenuItem
            //
            this.timeTargetsContextMenuItem.Name = "timeTargetsContextMenuItem";
            resources.ApplyResources(this.timeTargetsContextMenuItem, "timeTargetsContextMenuItem");
            this.timeTargetsContextMenuItem.Click += new System.EventHandler(this.timeTargetsContextMenuItem_Click);
            //
            // targetsAt1FDRToolStripMenuItem
            //
            this.targetsAt1FDRToolStripMenuItem.Name = "targetsAt1FDRToolStripMenuItem";
            resources.ApplyResources(this.targetsAt1FDRToolStripMenuItem, "targetsAt1FDRToolStripMenuItem");
            this.targetsAt1FDRToolStripMenuItem.Click += new System.EventHandler(this.targetsAt1FDRToolStripMenuItem_Click);
            //
            // timeStandardsContextMenuItem
            //
            this.timeStandardsContextMenuItem.Name = "timeStandardsContextMenuItem";
            resources.ApplyResources(this.timeStandardsContextMenuItem, "timeStandardsContextMenuItem");
            this.timeStandardsContextMenuItem.Click += new System.EventHandler(this.timeStandardsContextMenuItem_Click);
            //
            // timeDecoysContextMenuItem
            //
            this.timeDecoysContextMenuItem.Name = "timeDecoysContextMenuItem";
            resources.ApplyResources(this.timeDecoysContextMenuItem, "timeDecoysContextMenuItem");
            this.timeDecoysContextMenuItem.Click += new System.EventHandler(this.timeDecoysContextMenuItem_Click);
            //
            // rtValueMenuItem
            //
            this.rtValueMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allRTValueContextMenuItem,
            this.timeRTValueContextMenuItem,
            this.fwhmRTValueContextMenuItem,
            this.fwbRTValueContextMenuItem});
            this.rtValueMenuItem.Name = "rtValueMenuItem";
            resources.ApplyResources(this.rtValueMenuItem, "rtValueMenuItem");
            this.rtValueMenuItem.DropDownOpening += new System.EventHandler(this.peptideRTValueMenuItem_DropDownOpening);
            //
            // allRTValueContextMenuItem
            //
            this.allRTValueContextMenuItem.Name = "allRTValueContextMenuItem";
            resources.ApplyResources(this.allRTValueContextMenuItem, "allRTValueContextMenuItem");
            this.allRTValueContextMenuItem.Click += new System.EventHandler(this.allRTValueContextMenuItem_Click);
            //
            // timeRTValueContextMenuItem
            //
            this.timeRTValueContextMenuItem.Name = "timeRTValueContextMenuItem";
            resources.ApplyResources(this.timeRTValueContextMenuItem, "timeRTValueContextMenuItem");
            this.timeRTValueContextMenuItem.Click += new System.EventHandler(this.timeRTValueContextMenuItem_Click);
            //
            // fwhmRTValueContextMenuItem
            //
            this.fwhmRTValueContextMenuItem.Name = "fwhmRTValueContextMenuItem";
            resources.ApplyResources(this.fwhmRTValueContextMenuItem, "fwhmRTValueContextMenuItem");
            this.fwhmRTValueContextMenuItem.Click += new System.EventHandler(this.fwhmRTValueContextMenuItem_Click);
            //
            // fwbRTValueContextMenuItem
            //
            this.fwbRTValueContextMenuItem.Name = "fwbRTValueContextMenuItem";
            resources.ApplyResources(this.fwbRTValueContextMenuItem, "fwbRTValueContextMenuItem");
            this.fwbRTValueContextMenuItem.Click += new System.EventHandler(this.fwbRTValueContextMenuItem_Click);
            //
            // showRTLegendContextMenuItem
            //
            this.showRTLegendContextMenuItem.Name = "showRTLegendContextMenuItem";
            resources.ApplyResources(this.showRTLegendContextMenuItem, "showRTLegendContextMenuItem");
            this.showRTLegendContextMenuItem.Click += new System.EventHandler(this.showRTLegendContextMenuItem_Click);
            //
            // refineRTContextMenuItem
            //
            this.refineRTContextMenuItem.CheckOnClick = true;
            this.refineRTContextMenuItem.Name = "refineRTContextMenuItem";
            resources.ApplyResources(this.refineRTContextMenuItem, "refineRTContextMenuItem");
            this.refineRTContextMenuItem.Click += new System.EventHandler(this.refineRTContextMenuItem_Click);
            //
            // predictionRTContextMenuItem
            //
            this.predictionRTContextMenuItem.CheckOnClick = true;
            this.predictionRTContextMenuItem.Name = "predictionRTContextMenuItem";
            resources.ApplyResources(this.predictionRTContextMenuItem, "predictionRTContextMenuItem");
            this.predictionRTContextMenuItem.Click += new System.EventHandler(this.predictionRTContextMenuItem_Click);
            //
            // setRTThresholdContextMenuItem
            //
            this.setRTThresholdContextMenuItem.Name = "setRTThresholdContextMenuItem";
            resources.ApplyResources(this.setRTThresholdContextMenuItem, "setRTThresholdContextMenuItem");
            this.setRTThresholdContextMenuItem.Click += new System.EventHandler(this.setRTThresholdContextMenuItem_Click);
            //
            // setRegressionMethodContextMenuItem
            //
            this.setRegressionMethodContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.linearRegressionContextMenuItem,
            this.kernelDensityEstimationContextMenuItem,
            this.logRegressionContextMenuItem,
            this.loessContextMenuItem});
            this.setRegressionMethodContextMenuItem.Name = "setRegressionMethodContextMenuItem";
            resources.ApplyResources(this.setRegressionMethodContextMenuItem, "setRegressionMethodContextMenuItem");
            //
            // linearRegressionContextMenuItem
            //
            this.linearRegressionContextMenuItem.Name = "linearRegressionContextMenuItem";
            resources.ApplyResources(this.linearRegressionContextMenuItem, "linearRegressionContextMenuItem");
            this.linearRegressionContextMenuItem.Click += new System.EventHandler(this.linearRegressionContextMenuItem_Click);
            //
            // kernelDensityEstimationContextMenuItem
            //
            this.kernelDensityEstimationContextMenuItem.Name = "kernelDensityEstimationContextMenuItem";
            resources.ApplyResources(this.kernelDensityEstimationContextMenuItem, "kernelDensityEstimationContextMenuItem");
            this.kernelDensityEstimationContextMenuItem.Click += new System.EventHandler(this.kernelDensityEstimationContextMenuItem_Click);
            //
            // logRegressionContextMenuItem
            //
            this.logRegressionContextMenuItem.Name = "logRegressionContextMenuItem";
            resources.ApplyResources(this.logRegressionContextMenuItem, "logRegressionContextMenuItem");
            this.logRegressionContextMenuItem.Click += new System.EventHandler(this.logRegressionContextMenuItem_Click);
            //
            // loessContextMenuItem
            //
            this.loessContextMenuItem.Name = "loessContextMenuItem";
            resources.ApplyResources(this.loessContextMenuItem, "loessContextMenuItem");
            this.loessContextMenuItem.Click += new System.EventHandler(this.loessContextMenuItem_Click);
            //
            // toolStripSeparator22
            //
            this.toolStripSeparator22.Name = "toolStripSeparator22";
            resources.ApplyResources(this.toolStripSeparator22, "toolStripSeparator22");
            //
            // createRTRegressionContextMenuItem
            //
            this.createRTRegressionContextMenuItem.Name = "createRTRegressionContextMenuItem";
            resources.ApplyResources(this.createRTRegressionContextMenuItem, "createRTRegressionContextMenuItem");
            this.createRTRegressionContextMenuItem.Click += new System.EventHandler(this.createRTRegressionContextMenuItem_Click);
            //
            // chooseCalculatorContextMenuItem
            //
            this.chooseCalculatorContextMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.placeholderToolStripMenuItem1,
            this.toolStripSeparatorCalculators,
            this.addCalculatorContextMenuItem,
            this.updateCalculatorContextMenuItem});
            this.chooseCalculatorContextMenuItem.Name = "chooseCalculatorContextMenuItem";
            resources.ApplyResources(this.chooseCalculatorContextMenuItem, "chooseCalculatorContextMenuItem");
            this.chooseCalculatorContextMenuItem.DropDownOpening += new System.EventHandler(this.chooseCalculatorContextMenuItem_DropDownOpening);
            //
            // placeholderToolStripMenuItem1
            //
            this.placeholderToolStripMenuItem1.Name = "placeholderToolStripMenuItem1";
            resources.ApplyResources(this.placeholderToolStripMenuItem1, "placeholderToolStripMenuItem1");
            //
            // toolStripSeparatorCalculators
            //
            this.toolStripSeparatorCalculators.Name = "toolStripSeparatorCalculators";
            resources.ApplyResources(this.toolStripSeparatorCalculators, "toolStripSeparatorCalculators");
            //
            // addCalculatorContextMenuItem
            //
            this.addCalculatorContextMenuItem.Name = "addCalculatorContextMenuItem";
            resources.ApplyResources(this.addCalculatorContextMenuItem, "addCalculatorContextMenuItem");
            this.addCalculatorContextMenuItem.Click += new System.EventHandler(this.addCalculatorContextMenuItem_Click);
            //
            // updateCalculatorContextMenuItem
            //
            this.updateCalculatorContextMenuItem.Name = "updateCalculatorContextMenuItem";
            resources.ApplyResources(this.updateCalculatorContextMenuItem, "updateCalculatorContextMenuItem");
            this.updateCalculatorContextMenuItem.Click += new System.EventHandler(this.updateCalculatorContextMenuItem_Click);
            //
            // toolStripSeparator23
            //
            this.toolStripSeparator23.Name = "toolStripSeparator23";
            resources.ApplyResources(this.toolStripSeparator23, "toolStripSeparator23");
            //
            // removeRTOutliersContextMenuItem
            //
            this.removeRTOutliersContextMenuItem.Name = "removeRTOutliersContextMenuItem";
            resources.ApplyResources(this.removeRTOutliersContextMenuItem, "removeRTOutliersContextMenuItem");
            this.removeRTOutliersContextMenuItem.Click += new System.EventHandler(this.removeRTOutliersContextMenuItem_Click);
            //
            // removeRTContextMenuItem
            //
            this.removeRTContextMenuItem.Name = "removeRTContextMenuItem";
            resources.ApplyResources(this.removeRTContextMenuItem, "removeRTContextMenuItem");
            this.removeRTContextMenuItem.Click += new System.EventHandler(this.removeRTContextMenuItem_Click);
            //
            // timePropsContextMenuItem
            //
            this.timePropsContextMenuItem.Name = "timePropsContextMenuItem";
            resources.ApplyResources(this.timePropsContextMenuItem, "timePropsContextMenuItem");
            this.timePropsContextMenuItem.Click += new System.EventHandler(this.timePropsContextMenuItem_Click);
            //
            // toolStripSeparator38
            //
            this.toolStripSeparator38.Name = "toolStripSeparator38";
            resources.ApplyResources(this.toolStripSeparator38, "toolStripSeparator38");
            this.contextMenuRetentionTimes.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuRetentionTimes;
        private System.Windows.Forms.ToolStripMenuItem timeGraphContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replicateComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePeptideComparisonContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem regressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scoreToRunToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runToRunToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem schedulingContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePlotContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeCorrelationContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeResidualsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePointsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeTargetsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem targetsAt1FDRToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeStandardsContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeDecoysContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rtValueMenuItem;
        private System.Windows.Forms.ToolStripMenuItem allRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timeRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwhmRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fwbRTValueContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showRTLegendContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refineRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem predictionRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setRTThresholdContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem setRegressionMethodContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem linearRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem kernelDensityEstimationContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loessContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator22;
        private System.Windows.Forms.ToolStripMenuItem createRTRegressionContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem chooseCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem placeholderToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorCalculators;
        private System.Windows.Forms.ToolStripMenuItem addCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem updateCalculatorContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator23;
        private System.Windows.Forms.ToolStripMenuItem removeRTOutliersContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeRTContextMenuItem;
        private System.Windows.Forms.ToolStripMenuItem timePropsContextMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator38;
    }
}
