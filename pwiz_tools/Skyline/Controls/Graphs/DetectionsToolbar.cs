using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.Settings;
using IntLabeledValue = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.IntLabeledValue;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class DetectionsToolbar :  GraphSummaryToolbar        //UserControl 
    {
        private Timer _timer;
        private static Bitmap _emptyBitmap = new Bitmap(1, 1);

        private Dictionary<ToolStripDropDown, ToolStripItem> _selectedItems =
            new Dictionary<ToolStripDropDown, ToolStripItem>(4);

        public override bool Visible => true;

        public DetectionsToolbar(GraphSummary graphSummary) : base(graphSummary)
        {
            InitializeComponent();
            _timer = new Timer { Interval = 100 };
            _timer.Tick += Timer_OnTick;

            IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
        }

        private void Timer_OnTick(object sender, EventArgs e)
        {
            _graphSummary.UpdateUIWithoutToolbar();
            _timer.Stop();
        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
        }

        public override void UpdateUI()
        {
            IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
        }

        private void pbProperties_Click(object sender, EventArgs e)
        {
            using (var dlgProperties = new DetectionToolbarProperties(_graphSummary))
            {
                if (dlgProperties.ShowDialog(FormEx.GetParentForm(this)) == DialogResult.OK)
                    this.UpdateUI();
                    _timer.Start();
            }
        }

        private void cbLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.TargetType = IntLabeledValue.GetValue(cbLevel, Settings.TargetType);
            _timer.Start();
        }
    }
}
