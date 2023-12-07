using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class MatchExpressionListDlg : FormEx
    {
        private CreateMatchExpressionDlg _createProteinExpressionMatchExpressionDlg;
        private string _oldRegex;
        public MatchExpressionListDlg(CreateMatchExpressionDlg createMatchExpressionDlg)
        {
            InitializeComponent();
            _createProteinExpressionMatchExpressionDlg = createMatchExpressionDlg;
            _oldRegex = _createProteinExpressionMatchExpressionDlg.Expression;
            label1.Text = string.Format(Resources.MatchExpressionListDlg_MatchExpressionListDlg_Enter_a_list_of__0__on_separate_lines_, _createProteinExpressionMatchExpressionDlg.MatchSelectedItem.DisplayString);

        }
        private void proteinsTextBox_textChanged(object sender, EventArgs e)
        {
            ParseToRegex();
        }

        private void ParseToRegex()
        {
            var userText = proteinsTextBox.Text;
            if (string.IsNullOrEmpty(userText))
            {
                return;
            }
            var reader = new StringReader(userText);
            string line;
            var proteins = new HashSet<string>();
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                proteins.Add(line);
            }
            var sb = new StringBuilder();
            var proteinList = proteins.ToList();
            sb.Append('^');
            for (var i = 0; i < proteinList.Count - 1; i++)
            {
                var protein = proteinList[i];
                sb.Append(protein);
                sb.Append('$');
                sb.Append('|');
                sb.Append('^');
            }

            sb.Append(proteinList.Last());
            sb.Append('$');
            SetRegexText(sb.ToString());
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            // Upon cancellation, reset the REGEX to whatever was there before opening this dialog
            SetRegexText(_oldRegex);
        }

        private void SetRegexText(string regex)
        {
            _createProteinExpressionMatchExpressionDlg.SetRegexText(regex);
        }
    }
}
