/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class MatchExpressionListDlg : ModeUIInvariantFormEx
    {
        private readonly CreateMatchExpressionDlg _createMatchExpressionDlg;
        private readonly string _oldRegex;
        public MatchExpressionListDlg(CreateMatchExpressionDlg createDlg)
        {
            InitializeComponent();
            _createMatchExpressionDlg = createDlg;
            _oldRegex = _createMatchExpressionDlg.Expression;

        }
        private void proteinsTextBox_textChanged(object sender, EventArgs e)
        {
            ParseToRegex();
        }

        /// <summary>
        /// Parse the contents of the text box into a regex where
        /// each line is one possible case-insensitive match
        /// e.g. "Cas9\nHsp70" to "(?i)^Cas9$|^Hsp70$"
        /// </summary>
        private void ParseToRegex()
        {
            var userText = proteinsTextBox.Text;
            if (string.IsNullOrEmpty(userText))
            {
                SetRegexText(string.Empty);
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

            if (proteins.IsNullOrEmpty())
            {
                SetRegexText(string.Empty);
                return;
            }
            var sb = new StringBuilder();
            var proteinList = proteins.ToList();
            sb.Append(@"(?i)");
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
            // Upon cancellation, reset the regex to whatever was there before opening this dialog
            SetRegexText(_oldRegex);
            Close();
        }

        private void SetRegexText(string regex)
        {
            _createMatchExpressionDlg.SetRegexText(regex);
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
