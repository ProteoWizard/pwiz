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
	public partial class AnnotationEditAddEditDialog : Form
	{
		public SeemsPointAnnotation annotation;
		private Color bgColor;

		public AnnotationEditAddEditDialog( Color bgColor )
		{
			InitializeComponent();

			Text = "Add manual annotation";
			annotation = new SeemsPointAnnotation();
			annotation.Color = Color.Gray;
			annotation.Width = 1;
			this.bgColor = bgColor;
		}

		public AnnotationEditAddEditDialog( Color bgColor, SeemsPointAnnotation annotation )
		{
			InitializeComponent();

			Text = "Edit manual annotation";
			this.annotation = annotation;
			pointTextBox.Text = annotation.Point.ToString();
			labelTextBox.Text = annotation.Label;
			colorDialog1.Color = annotation.Color;
			widthUpDown.Value = annotation.Width;
			this.bgColor = bgColor;
		}

		private void button1_Click( object sender, EventArgs e )
		{
			annotation.Point = Convert.ToDouble( pointTextBox.Text );
			annotation.Label = labelTextBox.Text;
			annotation.Color = colorDialog1.Color;
			annotation.Width = (int) widthUpDown.Value;
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
			e.Graphics.DrawLine( new Pen( colorDialog1.Color, (float) widthUpDown.Value ), e.ClipRectangle.Left, middle, e.ClipRectangle.Right, middle );
		}

		private void colorBox_Click( object sender, EventArgs e )
		{
			if( colorDialog1.ShowDialog() == DialogResult.OK )
				colorBox.Refresh();
		}

		private void pointTextBox_KeyDown( object sender, KeyEventArgs e )
		{
			// Determine whether the keystroke is a number from the top of the keyboard.
			if( e.KeyCode < Keys.D0 || e.KeyCode > Keys.D9 )
			{
				// Determine whether the keystroke is a number from the keypad.
				if( e.KeyCode < Keys.NumPad0 || e.KeyCode > Keys.NumPad9 )
				{
					// Determine whether the keystroke is a backspace.
					if( e.KeyCode != Keys.Back &&
						e.KeyCode != Keys.Delete &&
						e.KeyCode != Keys.Enter &&
						e.KeyCode != Keys.Decimal &&
						e.KeyCode != Keys.OemPeriod &&
						e.KeyCode != Keys.Left &&
						e.KeyCode != Keys.Right &&
						e.KeyCode != Keys.Tab &&
						e.KeyCode != Keys.Home &&
						e.KeyCode != Keys.End )
					{
						e.SuppressKeyPress = true;
					}
				}
			}
		}

		private void widthUpDown_ValueChanged( object sender, EventArgs e )
		{
			colorBox.Refresh();
		}
	}
}