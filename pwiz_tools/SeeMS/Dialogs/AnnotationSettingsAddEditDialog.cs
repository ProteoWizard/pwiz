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
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace seems
{
	public partial class AnnotationSettingsAddEditDialog : Form
	{
		public string label;
		public string alias;
		public Color color;
		private Color bgColor;

		public AnnotationSettingsAddEditDialog( Color bgColor )
		{
			InitializeComponent();

			Text = "Add alias/color mapping";
			color = Color.Gray;
			this.bgColor = bgColor;
		}

		public AnnotationSettingsAddEditDialog( Color bgColor, string label, string alias, Color color )
		{
			InitializeComponent();

			Text = "Edit alias/color mapping";
			labelTextBox.Text = label;
			aliasTextBox.Text = alias;
			colorDialog1.Color = color;
			this.bgColor = bgColor;
		}

		private void button1_Click( object sender, EventArgs e )
		{
			label = labelTextBox.Text;
			alias = aliasTextBox.Text;
			color = colorDialog1.Color;
			DialogResult = DialogResult.OK;
			Close();
		}

		private void button2_Click( object sender, EventArgs e )
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void colorBox_Paint( object sender, PaintEventArgs e )
		{
			e.Graphics.FillRectangle( new SolidBrush( bgColor ), e.ClipRectangle );
			int middle = e.ClipRectangle.Y + e.ClipRectangle.Height / 2;
			e.Graphics.DrawLine( new Pen( colorDialog1.Color, 2 ), e.ClipRectangle.Left, middle, e.ClipRectangle.Right, middle );
		}

		private void colorBox_Click( object sender, EventArgs e )
		{
			if( colorDialog1.ShowDialog() == DialogResult.OK )
				colorBox.Refresh();
		}
	}
}