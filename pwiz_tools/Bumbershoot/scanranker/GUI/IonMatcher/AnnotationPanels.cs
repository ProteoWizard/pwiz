//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Forms.Controls
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
