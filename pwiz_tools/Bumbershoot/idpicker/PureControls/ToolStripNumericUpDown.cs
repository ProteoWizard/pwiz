//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace IDPicker.Controls
{
    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.ToolStrip)]
    public class ToolStripNumericUpDown : ToolStripControlHost
    {
        public ToolStripNumericUpDown()
            : base(new NumericUpDown())
        {
        }

        public NumericUpDown NumericUpDownControl { get { return Control as NumericUpDown; } }

        public decimal Value { get { return NumericUpDownControl.Value; } set { NumericUpDownControl.Value = value; } }
        public decimal Maximum { get { return NumericUpDownControl.Maximum; } set { NumericUpDownControl.Maximum = value; } }
        public decimal Minimum { get { return NumericUpDownControl.Minimum; } set { NumericUpDownControl.Minimum = value; } }
        public decimal Increment { get { return NumericUpDownControl.Increment; } set { NumericUpDownControl.Increment = value; } }
        public int DecimalPlaces { get { return NumericUpDownControl.DecimalPlaces; } set { NumericUpDownControl.DecimalPlaces = value; } }

        protected override void OnSubscribeControlEvents(Control c)
        {
            base.OnSubscribeControlEvents(c);
            NumericUpDown mumControl = (NumericUpDown)c;
            mumControl.ValueChanged += OnValueChanged;
        }

        protected override void OnUnsubscribeControlEvents(Control c)
        {
            base.OnUnsubscribeControlEvents(c);
            NumericUpDown mumControl = (NumericUpDown)c;
            mumControl.ValueChanged -= OnValueChanged;
        }

        public event EventHandler ValueChanged;

        private void OnValueChanged(object sender, EventArgs e)
        {
            if (ValueChanged != null)
                ValueChanged(this, e);
        }
    }
}