using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.Settings;
using IntLabeledValue = pwiz.Skyline.Controls.Graphs.DetectionsGraphController.IntLabeledValue;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class DetectionsToolbar : GraphSummaryToolbar //UserControl GraphSummaryToolbar        
    {
        private Timer _timer;
        public bool TimerComplete { get; private set; }

        private static Bitmap _emptyBitmap = new Bitmap(1, 1);

        private Dictionary<ToolStripDropDown, ToolStripItem> _selectedItems =
            new Dictionary<ToolStripDropDown, ToolStripItem>(4);

        public override bool Visible => true;

        public DetectionsToolbar(GraphSummary graphSummary) : base(graphSummary)
        {
            InitializeComponent();
            _timer = new Timer { Interval = 100 };
            _timer.Tick += Timer_OnTick;
            TimerComplete = true;

            IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
        }

        private void Timer_OnTick(object sender, EventArgs e)
        {
            _graphSummary.UpdateUIWithoutToolbar();
            _timer.Stop();
            TimerComplete = true;
        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= Timer_OnTick;

            base.OnHandleDestroyed(e);
        }

        public override void UpdateUI()
        {
            IntLabeledValue.PopulateCombo(cbLevel, Settings.TargetType);
        }

        //marked public for testing purposes
        public void pbProperties_Click(object sender, EventArgs e)
        {
            using (var dlgProperties = new DetectionToolbarProperties(_graphSummary))
            {
                if (dlgProperties.ShowDialog(FormEx.GetParentForm(this)) == DialogResult.OK)
                {
                    this.UpdateUI();

                    TimerComplete = false;
                    _timer.Stop();
                    _timer.Start();
                }
            }
        }

        private void cbLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLevel.Items.Count == 2)
            {
                Settings.TargetType = IntLabeledValue.GetValue(cbLevel, Settings.TargetType);
                TimerComplete = false;
                _timer.Stop();
                _timer.Start();
            }
        }
    }
}
