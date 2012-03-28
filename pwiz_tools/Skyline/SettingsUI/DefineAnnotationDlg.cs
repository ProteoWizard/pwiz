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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Dialog for defining new annotations.
    /// </summary>
    public partial class DefineAnnotationDlg : FormEx
    {
        private readonly IEnumerable<AnnotationDef> _existing;
        private AnnotationDef _annotationDef;

        public DefineAnnotationDlg(IEnumerable<AnnotationDef> existing)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _existing = existing;
        }

        public void SetAnnotationDef(AnnotationDef annotationDef)
        {
            _annotationDef = annotationDef;
            if (annotationDef == null)
            {
                AnnotationName = "";
                AnnotationType = AnnotationDef.AnnotationType.text;
                AnnotationTargets = 0;
                Items = new string[0];
            }
            else
            {
                AnnotationName = annotationDef.Name;
                AnnotationType = annotationDef.Type;
                AnnotationTargets = annotationDef.AnnotationTargets;
                Items = annotationDef.Items;
            }
        }

        public String AnnotationName
        {
            get
            {
                return tbxName.Text;
            }
            set
            {
                tbxName.Text = value;
            }
        }

        public AnnotationDef.AnnotationType AnnotationType
        {
            get
            {
                return (AnnotationDef.AnnotationType) comboType.SelectedIndex;
            }
            set
            {
                comboType.SelectedIndex = (int) value;
            }
        }

        public AnnotationDef.AnnotationTarget AnnotationTargets
        {
            get
            {
                AnnotationDef.AnnotationTarget targets = 0;
                for (int i = 0; i < checkedListBoxAppliesTo.Items.Count; i++)
                {
                    if (checkedListBoxAppliesTo.GetItemChecked(i))
                    {
                        targets |= (AnnotationDef.AnnotationTarget)(1 << i);
                    }
                }
                return targets;
            }
            set
            {
                for (int i = 0; i < checkedListBoxAppliesTo.Items.Count; i++)
                {
                    checkedListBoxAppliesTo.SetItemChecked(i, ((int)value & (1 << i)) != 0);
                }
            }
        }

        public IList<String> Items {
            get
            {
                if (string.IsNullOrEmpty(tbxValues.Text))
                {
                    return new string[0];
                }
                return tbxValues.Text.Split(new[] {"\r\n"}, StringSplitOptions.None);
            }
            set
            {
                tbxValues.Text = string.Join("\r\n", value.ToArray());
            }
        }

        public AnnotationDef GetAnnotationDef()
        {
            return new AnnotationDef(AnnotationName, AnnotationTargets, AnnotationType, Items);
        }

        private void comboType_SelectedIndexChanged(object sender, EventArgs e)
        {
            tbxValues.Enabled = AnnotationType == AnnotationDef.AnnotationType.value_list;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog() {
            var messageBoxHelper = new MessageBoxHelper(this);
            var cancelEventArgs = new CancelEventArgs();
            string name;
            if (!messageBoxHelper.ValidateNameTextBox(cancelEventArgs, tbxName, out name))
            {
                return;
            }
            if (_annotationDef == null || name != _annotationDef.Name)
            {
                foreach (var annotationDef in _existing)
                {
                    if (annotationDef.Name == name)
                    {
                        messageBoxHelper.ShowTextBoxError(tbxName, "There is already an annotation defined named '{0}'.", name);
                        return;
                    }
                }
            }
            if (checkedListBoxAppliesTo.CheckedItems.Count == 0)
            {
                MessageBox.Show(this, "Choose at least one type for this annotation to apply to.", Program.Name);
                checkedListBoxAppliesTo.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
