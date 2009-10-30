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
using System.Configuration;
using System.Collections.Specialized;

namespace seems
{
	using LabelToAliasAndColorPair = Map<string, Pair<string, Color>>.MapPair;

	public partial class ChromatogramAnnotationSettingsForm : Form
	{
		internal class LabelToAliasAndColorListItem
		{
			internal LabelToAliasAndColorPair mapPair;
			internal LabelToAliasAndColorListItem( LabelToAliasAndColorPair mapPair )
			{
				this.mapPair = mapPair;
			}

			internal string ListBoxView
			{
				get
				{
					return String.Format( "{0,-13}{1,-9}", mapPair.Key.Substring( 0, Math.Min( 6, mapPair.Key.Length ) ), mapPair.Value.first.Substring( 0, Math.Min( 4, mapPair.Value.first.Length ) ) );
				}
			}
		}

		private List<string> matchToleranceUnitList = new List<string>();
        private AnnotationSettings settings;

        public ChromatogramAnnotationSettingsForm( AnnotationSettings settings )
		{
			InitializeComponent();

			matchToleranceUnitList.Add( "sec." );
			//matchToleranceUnitList.Add( "min." );
			//matchToleranceUnitList.Add( "hours" );

            this.settings = settings;

			matchToleranceUnitsComboBox.Items.AddRange( (object[]) matchToleranceUnitList.ToArray() );
			matchToleranceUnitsComboBox.SelectedIndex = (int) settings.MatchToleranceUnit;
			matchToleranceCheckbox.Checked = settings.MatchToleranceOverride;
			matchToleranceTextBox.Text = settings.MatchTolerance.ToString();
			showTimeLabelsCheckbox.Checked = settings.ShowXValues;
			showTotalIntensityLabelsCheckbox.Checked = settings.ShowYValues;

			if( !matchToleranceCheckbox.Checked )
			{
				matchToleranceTextBox.Enabled = false;
				matchToleranceUnitsComboBox.Enabled = false;
			}

			showMatchedAnnotationsCheckbox.Checked = settings.ShowMatchedAnnotations;
			showUnmatchedAnnotationsCheckbox.Checked = settings.ShowUnmatchedAnnotations;

			foreach( LabelToAliasAndColorPair itr in settings.LabelToAliasAndColorMap )
			{
				aliasAndColorMappingListBox.Items.Add( new LabelToAliasAndColorListItem( itr ) );
			}
		}

		private void okButton_Click( object sender, EventArgs e )
		{
			this.DialogResult = DialogResult.OK;

			Properties.Settings.Default.TimeMatchToleranceUnit = (int) ( settings.MatchToleranceUnit = (MatchToleranceUnits) matchToleranceUnitsComboBox.SelectedIndex );
			Properties.Settings.Default.ChromatogramMatchToleranceOverride = settings.MatchToleranceOverride = matchToleranceCheckbox.Checked;
			Properties.Settings.Default.TimeMatchTolerance = settings.MatchTolerance = Convert.ToDouble( matchToleranceTextBox.Text );
            Properties.Settings.Default.ShowChromatogramTimeLabels = settings.ShowXValues = showTimeLabelsCheckbox.Checked;
            Properties.Settings.Default.ShowChromatogramIntensityLabels = settings.ShowYValues = showTotalIntensityLabelsCheckbox.Checked;
			Properties.Settings.Default.ShowChromatogramMatchedAnnotations = settings.ShowMatchedAnnotations = showMatchedAnnotationsCheckbox.Checked;
			Properties.Settings.Default.ShowChromatogramUnmatchedAnnotations = settings.ShowUnmatchedAnnotations = showUnmatchedAnnotationsCheckbox.Checked;

			settings.LabelToAliasAndColorMap.Clear();
			foreach( object itr in aliasAndColorMappingListBox.Items )
			{
				LabelToAliasAndColorPair item = ( itr as LabelToAliasAndColorListItem ).mapPair;
				settings.LabelToAliasAndColorMap[item.Key] = new Pair<string, Color>( item.Value.first, item.Value.second );
			}

			Properties.Settings.Default.Save();
			//ownerGraphForm.updateGraph();
		}

		private void cancelButton_Click( object sender, EventArgs e )
		{
			this.DialogResult = DialogResult.Cancel;
		}

		private void aliasAndColorMappingListBox_DrawItem( object sender, DrawItemEventArgs e )
		{
			e.DrawBackground();
			LabelToAliasAndColorListItem item = (LabelToAliasAndColorListItem) aliasAndColorMappingListBox.Items[e.Index];
			e.Graphics.DrawString( item.ListBoxView,
				aliasAndColorMappingListBox.Font,
				new SolidBrush( aliasAndColorMappingListBox.ForeColor ),
				new PointF( (float) e.Bounds.X, (float) e.Bounds.Y ) );
			Rectangle colorSampleBox = new Rectangle( e.Bounds.Location, e.Bounds.Size );
			colorSampleBox.X = e.Bounds.Right - e.Bounds.Height * 2;
			colorSampleBox.Location.Offset( -5, 0 );
            e.Graphics.FillRectangle( new SolidBrush( Color.White ), colorSampleBox );
			int middle = colorSampleBox.Y + colorSampleBox.Height / 2;
			e.Graphics.DrawLine( new Pen( item.mapPair.Value.second, 2 ), colorSampleBox.Left, middle, colorSampleBox.Right, middle );
			e.DrawFocusRectangle();
		}

		private void addAliasAndColorMappingButton_Click( object sender, EventArgs e )
		{
            AnnotationSettingsAddEditDialog dialog = new AnnotationSettingsAddEditDialog( Color.White );
			if( dialog.ShowDialog() == DialogResult.OK )
			{
				aliasAndColorMappingListBox.Items.Add(
					new LabelToAliasAndColorListItem(
						new Map<string, Pair<string, Color>>.MapPair( dialog.label,
							new Pair<string, Color>( dialog.alias, dialog.color ) ) ) );
			}
		}

		private void editAliasAndColorMappingButton_Click( object sender, EventArgs e )
		{
			LabelToAliasAndColorListItem item = (LabelToAliasAndColorListItem) aliasAndColorMappingListBox.SelectedItem;
            AnnotationSettingsAddEditDialog dialog = new AnnotationSettingsAddEditDialog( Color.White, item.mapPair.Key, item.mapPair.Value.first, item.mapPair.Value.second );
			if( dialog.ShowDialog() == DialogResult.OK )
			{
				if( item.mapPair.Key == dialog.label )
				{
					item.mapPair.Value.first = dialog.alias;
					item.mapPair.Value.second = dialog.color;
				} else
				{
					aliasAndColorMappingListBox.Items.RemoveAt( aliasAndColorMappingListBox.SelectedIndex );
					aliasAndColorMappingListBox.Items.Add(
					new LabelToAliasAndColorListItem(
						new Map<string, Pair<string, Color>>.MapPair( dialog.label,
							new Pair<string, Color>( dialog.alias, dialog.color ) ) ) );
				}
				aliasAndColorMappingListBox.Refresh();
			}
		}

		private void removeAliasAndColorMappingButton_Click( object sender, EventArgs e )
		{
			if( aliasAndColorMappingListBox.SelectedItem != null )
				aliasAndColorMappingListBox.Items.RemoveAt( aliasAndColorMappingListBox.SelectedIndex );
		}

		private void clearAliasAndColorMappingButton_Click( object sender, EventArgs e )
		{
			aliasAndColorMappingListBox.Items.Clear();
		}

		private void matchToleranceTextBox_TextChanged( object sender, EventArgs e )
		{
			if( matchToleranceTextBox.Text == "." )
				matchToleranceTextBox.Text = "0.";
			else
			{
				double result;
				if( !Double.TryParse( matchToleranceTextBox.Text, out result ) )
				{
					matchToleranceTextBox.Text = settings.MatchTolerance.ToString();
				}
			}
		}

		private void matchToleranceCheckbox_CheckedChanged( object sender, EventArgs e )
		{
			if( !matchToleranceCheckbox.Checked )
			{
				matchToleranceTextBox.Enabled = false;
				matchToleranceUnitsComboBox.Enabled = false;
				matchToleranceUnitsComboBox.SelectedIndex = (int) settings.MatchToleranceUnit;
				matchToleranceTextBox.Text = settings.MatchTolerance.ToString();
			} else
			{
				matchToleranceTextBox.Enabled = true;
				matchToleranceUnitsComboBox.Enabled = true;
			}
		}
	}
}