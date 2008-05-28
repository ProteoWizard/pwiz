using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace seems
{
	public partial class AnnotationEditForm : Form
	{
		private GraphForm ownerGraphForm;

		public AnnotationEditForm( GraphForm graphForm )
		{
			InitializeComponent();

			ownerGraphForm = graphForm;

			PointDataMap<SeemsPointAnnotation> annotationMap = ownerGraphForm.CurrentPointAnnotations;
			foreach( SeemsPointAnnotation annotation in annotationMap.Values )
				annotationListBox.Items.Add( new AnnotationListItem( annotation ) );
		}

		private void okButton_Click( object sender, EventArgs e )
		{
			DialogResult = DialogResult.OK;

			PointDataMap<SeemsPointAnnotation> annotationMap = ownerGraphForm.CurrentPointAnnotations;
			annotationMap.Clear();
			foreach( object itr in annotationListBox.Items )
			{
				AnnotationListItem item = (AnnotationListItem) itr;
				annotationMap[item.annotation.Point] = item.annotation;
			}

			this.Close();
		}

		private void cancelButton_Click( object sender, EventArgs e )
		{
			DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void addAnnotationButton_Click( object sender, EventArgs e )
		{
			AnnotationEditAddEditDialog dialog = new AnnotationEditAddEditDialog( ownerGraphForm.ZedGraphControl.GraphPane.Chart.Fill.Color );
			if( dialog.ShowDialog() == DialogResult.OK )
			{
				annotationListBox.Items.Add( new AnnotationListItem( dialog.annotation ) );
			}
		}

		private void editAnnotationButton_Click( object sender, EventArgs e )
		{
			AnnotationListItem item = (AnnotationListItem) annotationListBox.SelectedItem;
			AnnotationEditAddEditDialog dialog = new AnnotationEditAddEditDialog( ownerGraphForm.ZedGraphControl.GraphPane.Chart.Fill.Color, item.annotation );
			if( dialog.ShowDialog() == DialogResult.OK )
			{
				item.annotation = dialog.annotation;
				annotationListBox.Refresh();
			}
		}

		private void removeAnnotationButton_Click( object sender, EventArgs e )
		{
			if( annotationListBox.SelectedItem != null )
				annotationListBox.Items.RemoveAt( annotationListBox.SelectedIndex );
		}

		private void clearAnnotationsButton_Click( object sender, EventArgs e )
		{
			annotationListBox.Items.Clear();
		}

		private void annotationListBox_DrawItem( object sender, DrawItemEventArgs e )
		{
			e.DrawBackground();
			AnnotationListItem item = (AnnotationListItem) annotationListBox.Items[e.Index];
			e.Graphics.DrawString( item.annotation.Point.ToString("f3"),
				annotationListBox.Font,
				new SolidBrush( annotationListBox.ForeColor ),
				new PointF( (float) e.Bounds.X, (float) e.Bounds.Y ) );

			string label;
			if( item.annotation.Label.Contains(" ") )
				label = "\"" + item.annotation.Label + "\"";
			else
				label = item.annotation.Label;
			e.Graphics.DrawString( label,
				annotationListBox.Font,
				new SolidBrush( annotationListBox.ForeColor ),
				new PointF( (float) e.Bounds.X + ( ( e.Bounds.Width / 2 ) - ( e.Bounds.Height * 2 ) ), (float) e.Bounds.Y ) );

			Rectangle colorSampleBox = new Rectangle( e.Bounds.Location, e.Bounds.Size );
			colorSampleBox.X = e.Bounds.Right - e.Bounds.Height * 2;
			colorSampleBox.Location.Offset( -5, 0 );
			e.Graphics.FillRectangle( new SolidBrush( ownerGraphForm.ZedGraphControl.GraphPane.Chart.Fill.Color ), colorSampleBox );
			int middle = colorSampleBox.Y + colorSampleBox.Height / 2;
			e.Graphics.DrawLine( new Pen( item.annotation.Color, item.annotation.Width ), colorSampleBox.Left, middle, colorSampleBox.Right, middle );
			e.DrawFocusRectangle();
		}

		internal class AnnotationListItem
		{
			internal SeemsPointAnnotation annotation;
			internal AnnotationListItem( SeemsPointAnnotation annotation )
			{
				this.annotation = annotation;
			}

			internal string ListBoxView
			{
				get
				{
					
					if( annotation.Label.Contains(" ") )
						return String.Format( "{0,-6}{1,-9}", annotation.Point, "\"" + annotation.Label + "\"" );
					else
						return String.Format( "{0,-6}{1,-9}", annotation.Point, annotation.Label );
				}
			}
		}
	}
}