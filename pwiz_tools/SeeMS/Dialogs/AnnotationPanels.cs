using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace seems
{
    public partial class AnnotationPanels : UserControl
    {
        public AnnotationPanels()
        {
            InitializeComponent();
            this.noTopSeries.Select();
            this.noBottomSeries.Select();
        }

        private void maxChargeUpDown_ValueChanged( object sender, EventArgs e )
        {
            minChargeUpDown.Value = Math.Min( maxChargeUpDown.Value, minChargeUpDown.Value );
        }

        private void minChargeUpDown_ValueChanged( object sender, EventArgs e )
        {
            maxChargeUpDown.Value = Math.Max( maxChargeUpDown.Value, minChargeUpDown.Value );
        }
    }
}
