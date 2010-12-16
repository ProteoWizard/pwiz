using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IonMatcher.Controls
{
    public partial class AnnotationPanels : UserControl
    {
        public AnnotationPanels()
        {
            InitializeComponent();
        }

        private void maxChargeUpDown_ValueChanged(object sender, EventArgs e)
        {
            minChargeUpDown.Value = Math.Min(maxChargeUpDown.Value, minChargeUpDown.Value);
        }

        private void minChargeUpDown_ValueChanged(object sender, EventArgs e)
        {
            maxChargeUpDown.Value = Math.Max(maxChargeUpDown.Value, minChargeUpDown.Value);
        }
    }
}
