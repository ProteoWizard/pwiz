using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace HeaderComboBox
{
	public partial class HeaderComboBoxControl : UserControl
	{
		public HeaderComboBoxControl()
		{
			InitializeComponent();

			selectedIndex = -1;
			selectedItem = null;
			selectedValue = null;

			listBoxForm1 = new ListBoxForm();
			listHeaderForm1 = new ListBoxForm();
			//listHeaderForm1.ListBox.IntegralHeight = false;
			listHeaderForm1.ListBox.SelectionMode = SelectionMode.None;

			TextBox.PreviewKeyDown += new PreviewKeyDownEventHandler( this.control_PreviewKeyDown );
			ListBox.MouseDown += new MouseEventHandler( this.list_MouseClick );
		}

		#region Forward properties of child controls
		public TextBox TextBox
		{
			get
			{
				return textBox1;
			}
		}

		public Button Button
		{
			get
			{
				return button1;
			}
		}

		public ListBoxForm ListBoxForm
		{
			get { return listBoxForm1; }
		}

		public ListBox ListBox
		{
			get
			{
				return listBoxForm1.ListBox;
			}
		}

		public System.Windows.Forms.ListBox.ObjectCollection Items
		{
			get
			{
				return ListBox.Items;
			}
		}

		[DefaultValue( "" )]
		public override string Text
		{
			get
			{
				return TextBox.Text;
			}
		}

		[DefaultValue( "" )]
		public string ListHeaderText
		{
			get
			{
				return headerString;
			}

			set
			{
				headerString = value;
				listHeaderForm1.ListBox.Items.Clear();
				listHeaderForm1.ListBox.Items.Add( headerString );
			}
		}

		public int ItemHeight
		{
			get
			{
				return ListBox.ItemHeight;
			}

			set
			{
				ListBox.ItemHeight = value;
			}
		}

		public bool IntegralHeight
		{
			get
			{
				return ListBox.IntegralHeight;
			}

			set
			{
				ListBox.IntegralHeight = value;
			}
		}

		[DefaultValue( "" )]
		public string ListDisplayMember
		{
			get
			{
				return ListBox.DisplayMember;
			}

			set
			{
				ListBox.DisplayMember = value;
			}
		}

		[DefaultValue( "" )]
		public string TextDisplayMember
		{
			get
			{
				return textDisplayMemberName;
			}

			set
			{
				textDisplayMemberName = value;
			}
		}

		[DefaultValue( "" )]
		public string ValueMember
		{
			get
			{
				return ListBox.ValueMember;
			}

			set
			{
				ListBox.ValueMember = value;
			}
		}
		#endregion

		public event EventHandler SelectedIndexChanged;
		private int selectedIndex;
		public int SelectedIndex
		{
			get
			{
				return selectedIndex;
			}

			set
			{
				int oldIndex = selectedIndex;
				selectedIndex = ListBox.SelectedIndex = value;
				selectedItem = ListBox.SelectedItem;
				selectedValue = ListBox.SelectedValue;
				UpdateTextBox();

				if( selectedIndex != oldIndex && SelectedIndexChanged != null )
						SelectedIndexChanged( this, EventArgs.Empty );
			}
		}

		private object selectedItem;
		public object SelectedItem
		{
			get
			{
				return selectedItem;
			}

			set
			{
				ListBox.SelectedItem = value;
				SelectedIndex = ListBox.SelectedIndex;
			}
		}

		public event EventHandler SelectedValueChanged;
		private object selectedValue;
		public object SelectedValue
		{
			get
			{
				return selectedValue;
			}

			set
			{
				if( selectedValue != value )
				{
					ListBox.SelectedValue = value;
					SelectedIndex = ListBox.SelectedIndex;
					if( SelectedValueChanged != null )
						SelectedValueChanged( this, EventArgs.Empty );
				}
			}
		}

		public void BeginUpdate()
		{
			ListBox.BeginUpdate();
		}

		public void EndUpdate()
		{
			ListBox.EndUpdate();
		}

		private ListBoxForm listBoxForm1;
		private ListBoxForm listHeaderForm1;
		private string textDisplayMemberName = "";
		private string headerString = "";

		private void button1_Click( object sender, EventArgs e )
		{
			listBoxForm1.SuspendLayout();
			if( ListHeaderText.Length > 0 ) listHeaderForm1.SuspendLayout();

			ListBox.Height = this.TopLevelControl.Height / 2;

			Graphics g;
			int numVisibleItems = ListBox.Height / ListBox.ItemHeight;
			bool hasScrollBar = ( (double) Items.Count / (double) numVisibleItems ) > 1.0;
			int scrollBarWidth = hasScrollBar ? System.Windows.Forms.SystemInformation.VerticalScrollBarWidth : 0;
			if( Items.Count > 0 )
			{
				g = ListBox.CreateGraphics();
				string maxItemText = ListHeaderText;
				int bottomIndex = Math.Min( ListBox.TopIndex + numVisibleItems, Items.Count );
				for( int i = ListBox.TopIndex; i < bottomIndex; ++i )
				{
					string itemText;
					System.Reflection.PropertyInfo displayMember = Items[i].GetType().GetProperty( ListDisplayMember );
					if( displayMember != null )
						itemText = displayMember.GetValue( Items[i], null ).ToString();
					else
						itemText = Items[i].ToString();
					if( itemText.Length > maxItemText.Length )
						maxItemText = itemText;
				}
				ListBox.Width = g.MeasureString( maxItemText, ListBox.Font ).ToSize().Width + scrollBarWidth * 2;
			}

			Point listBoxLocation = new Point();
			listBoxLocation.X = this.Parent.PointToScreen( this.Location ).X - ListBox.Width + this.Width;
			listBoxLocation.X = Math.Max( Screen.FromPoint( this.Parent.PointToScreen( TextBox.Location ) ).Bounds.Left, listBoxLocation.X );
			listBoxLocation.Y = this.Parent.PointToScreen( this.Location ).Y + this.Height;
			if( ListHeaderText.Length > 0 )
			{
				listHeaderForm1.ListBox.IntegralHeight = false;
				listHeaderForm1.ListBox.Font = new Font(listBoxForm1.ListBox.Font, FontStyle.Bold);
				listHeaderForm1.ListBox.Height = listBoxForm1.ListBox.ItemHeight + listHeaderForm1.Margin.Vertical;
				listHeaderForm1.ListBox.Width = listBoxForm1.ListBox.Width;
				listHeaderForm1.Location = listBoxLocation;
				listBoxLocation.Y += listHeaderForm1.ListBox.Height;
			}
			listBoxForm1.Location = listBoxLocation;
			listBoxForm1.ResumeLayout( true );
			if( ListHeaderText.Length > 0 ) listHeaderForm1.ResumeLayout( true );
			listBoxForm1.BringToFront();
			if( ListHeaderText.Length > 0 ) listHeaderForm1.BringToFront();
			ListBox.Focus();
			Application.DoEvents();
			ListBox.LostFocus += new EventHandler( this.list_Close );
			if( ListHeaderText.Length > 0 ) listHeaderForm1.Show();
			listBoxForm1.Show();
		}

		public string SelectedItemText
		{
			get
			{
				if( ListBox.SelectedItem != null )
				{
					System.Reflection.PropertyInfo displayMember = SelectedItem.GetType().GetProperty( ListDisplayMember );
					if( displayMember != null )
						return displayMember.GetValue( SelectedItem, null ).ToString();
					else
						return SelectedItem.ToString();
				} else
					return "";
			}
		}

		public void UpdateTextBox()
		{
			if( SelectedItem != null )
			{
				System.Reflection.PropertyInfo displayMember = SelectedItem.GetType().GetProperty( textDisplayMemberName );
				if( displayMember != null )
					textBox1.Text = displayMember.GetValue( SelectedItem, null ).ToString();
				else
					textBox1.Text = SelectedItem.ToString();

				Graphics g = textBox1.CreateGraphics();
				int textWidth = g.MeasureString( textBox1.Text, TextBox.Font ).ToSize().Width;
				if( textBox1.Width < textWidth )
				{
					Width = textWidth + button1.Width + 10;
				}
			} else
				textBox1.Text = "";
		}

		private void list_Close( object sender, EventArgs e )
		{
			if( ListHeaderText.Length > 0 ) listHeaderForm1.Hide();
			listBoxForm1.Hide();
			ListBox.LostFocus -= new EventHandler( this.list_Close );
		}

		private void list_MouseClick( object sender, MouseEventArgs e )
		{
			if( e.Button == MouseButtons.Right )
			{
				ListBox.SelectedIndex = ListBox.IndexFromPoint( e.Location );
			} else
			{
				SelectedIndex = ListBox.SelectedIndex;
				list_Close( sender, (EventArgs) e );
			}
		}

		private void control_PreviewKeyDown( object sender, PreviewKeyDownEventArgs e )
		{
			int key = (int) e.KeyCode;
			if( TextBox.Focused && key == (int) Keys.Enter )
			{
				if( SelectedItem == null )
				{
					Text = "";
					return;
				}

				System.Reflection.PropertyInfo valueMember = SelectedItem.GetType().GetProperty( ValueMember );
				if( valueMember != null )
				{
					object newValue;
					try
					{
						newValue = Convert.ChangeType( Text, Type.GetTypeCode( valueMember.PropertyType ) );
					} catch
					{
						newValue = valueMember.GetValue( SelectedItem, null );
					}
					int newScanNumberIndex = -1;
					System.Collections.IEnumerator itr = Items.GetEnumerator();
					for( int i = 0; i < Items.Count; ++i )
					{
						object item = Items[i];
						object itemValue = valueMember.GetValue( item, null );
						if( Convert.ChangeType( itemValue, Type.GetTypeCode( valueMember.PropertyType ) ).Equals( newValue ) )
						{
							newScanNumberIndex = i;
							break;
						}
					}

					if( newScanNumberIndex > 0 )
					{
						SelectedIndex = newScanNumberIndex;
						return;
					}
				}
				UpdateTextBox();
			}
		}

		private void HeaderComboBoxControl_Layout( object sender, LayoutEventArgs e )
		{
			textBox1.Width = Width - button1.Width;
			textBox1.Location = new Point( 0, 0 );
			button1.Location = new Point( textBox1.Width, 0 );
		}
	}
}