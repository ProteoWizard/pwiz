using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace IDPicker.Forms
{
    public partial class TextInputBox : Form
    {
        private readonly Regex _inputFormat;

        public TextInputBox()
        {
            InitializeComponent();
            _inputFormat = new Regex(@"\w| ");
        }

        public TextInputBox(string regEx)
        {
            InitializeComponent();
            try
            {
                _inputFormat = new Regex(regEx);
            }
            catch (Exception)
            {
                _inputFormat = new Regex(@"\w| ");
            }
        }

        private void inputTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!_inputFormat.IsMatch(e.KeyChar.ToString()))
                e.Handled = true;
        }

        private void TextInputBox_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (string.IsNullOrEmpty(inputTextBox.Text) && okButton.Focused)
                e.Cancel = true;
        }
    }
}
