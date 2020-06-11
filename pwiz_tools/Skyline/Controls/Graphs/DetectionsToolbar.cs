using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using MathNet.Numerics.Properties;
using NHibernate.Mapping.ByCode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using Resources = pwiz.Skyline.Properties.Resources;
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
            _timer.Tick += new EventHandler(Timer_OnTick);

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
            Settings.TargetType = IntLabeledValue.GetValue<DetectionsPlotPane.TargetType>(cbLevel, Settings.TargetType);
            _timer.Start();
        }
    }
}
