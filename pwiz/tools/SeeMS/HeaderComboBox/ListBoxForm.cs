using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace HeaderComboBox
{
	public partial class ListBoxForm : Form
	{
		public ListBoxForm()
		{
			InitializeComponent();
		}

		public ListBox ListBox
		{
			get
			{
				return listBox1;
			}
		}
	}
}