/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public partial class ColumnChooser : FormEx
    {
        public ColumnChooser()
        {
            InitializeComponent();

            Icon = Resources.Skyline;
        }

        public void SetColumns(IList<string> nameList, IList<bool> checkedList)
        {
            checkedListBox1.Items.Clear();
            checkedListBox1.Items.AddRange(nameList.Cast<object>().ToArray());
            for (int i = 0; i < checkedList.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, checkedList[i]);
            }
        }

        public void SetChecked(IDictionary<string, bool> dictNameChecked)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                bool isChecked;
                if (dictNameChecked.TryGetValue((string) checkedListBox1.Items[i], out isChecked))
                    checkedListBox1.SetItemChecked(i, isChecked);
            }            
        }

        public IList<bool> GetCheckedList()
        {
            var result = new bool[checkedListBox1.Items.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = checkedListBox1.GetItemChecked(i);
            }
            return result;
        }
        /// <summary>
        /// Used for testing
        /// </summary>
        public CheckedListBox CheckedListBox { get { return checkedListBox1; } }
    }
}
