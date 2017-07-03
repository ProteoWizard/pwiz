//
// $Id: GraphForm.Designer.cs 1721 2010-01-23 04:18:45Z nickshulman $
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

namespace seems
{
    partial class HeatmapForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            pwiz.MSGraph.MSGraphPane msGraphPane1 = new pwiz.MSGraph.MSGraphPane();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager( typeof( GraphForm ) );
            this.msGraphControl = new pwiz.MSGraph.MSGraphControl();
            this.SuspendLayout();
            // 
            // msGraphControl
            // 
            this.msGraphControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.msGraphControl.EditButtons = System.Windows.Forms.MouseButtons.Left;
            this.msGraphControl.EditModifierKeys = System.Windows.Forms.Keys.None;
            msGraphPane1.BaseDimension = 8F;
            msGraphPane1.IsAlignGrids = false;
            msGraphPane1.IsBoundedRanges = false;
            msGraphPane1.IsFontsScaled = true;
            msGraphPane1.IsIgnoreInitial = false;
            msGraphPane1.IsIgnoreMissing = false;
            msGraphPane1.IsPenWidthScaled = false;
            msGraphPane1.LineType = ZedGraph.LineType.Normal;
            msGraphPane1.Tag = null;
            msGraphPane1.TitleGap = 0.5F;
            this.msGraphControl.GraphPane = msGraphPane1;
            this.msGraphControl.IsEnableVZoom = false;
            this.msGraphControl.Location = new System.Drawing.Point( 0, 0 );
            this.msGraphControl.Name = "msGraphControl";
            this.msGraphControl.PanButtons2 = System.Windows.Forms.MouseButtons.None;
            this.msGraphControl.ScrollGrace = 0;
            this.msGraphControl.ScrollMaxX = 0;
            this.msGraphControl.ScrollMaxY = 0;
            this.msGraphControl.ScrollMaxY2 = 0;
            this.msGraphControl.ScrollMinX = 0;
            this.msGraphControl.ScrollMinY = 0;
            this.msGraphControl.ScrollMinY2 = 0;
            this.msGraphControl.Size = new System.Drawing.Size( 464, 413 );
            this.msGraphControl.TabIndex = 0;
            // 
            // GraphForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size( 464, 413 );
            this.Controls.Add( this.msGraphControl );
            this.MinimumSize = new System.Drawing.Size( 100, 50 );
            this.Name = "GraphForm";
            this.TabText = "GraphForm";
            this.Text = "GraphForm";
            this.ResizeBegin += new System.EventHandler( this.GraphForm_ResizeBegin );
            this.ResizeEnd += new System.EventHandler( this.GraphForm_ResizeEnd );
            this.ResumeLayout( false );

		}

		#endregion

        private pwiz.MSGraph.MSGraphControl msGraphControl;


    }
}