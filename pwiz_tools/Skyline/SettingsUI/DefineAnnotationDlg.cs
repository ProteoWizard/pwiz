﻿/*
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
            checkedListBoxAppliesTo.Items.Clear();
            foreach (var annotationTarget in new[]
                                                 {
                                                     AnnotationDef.AnnotationTarget.protein,
                                                     AnnotationDef.AnnotationTarget.peptide,
                                                     AnnotationDef.AnnotationTarget.precursor,
                                                     AnnotationDef.AnnotationTarget.transition,
                                                     AnnotationDef.AnnotationTarget.replicate, 
                                                     AnnotationDef.AnnotationTarget.precursor_result,
                                                     AnnotationDef.AnnotationTarget.transition_result,
                                                 })
            {
                checkedListBoxAppliesTo.Items.Add(new AnnotationTargetItem(annotationTarget, ModeUIHelper.ModeUI));
            }
            comboType.Items.AddRange(ListPropertyType.ListPropertyTypes().ToArray());
            comboType.SelectedIndex = 0;
            ComboHelper.AutoSizeDropDown(comboType);
            _existing = existing;
        }

        public void SetAnnotationDef(AnnotationDef annotationDef)
        {
            _annotationDef = annotationDef;
            if (annotationDef == null)
            {
                AnnotationName = string.Empty;
                AnnotationType = AnnotationDef.AnnotationType.text;
                AnnotationTargets = AnnotationDef.AnnotationTargetSet.EMPTY;
                Items = new string[0];
            }
            else
            {
                AnnotationName = annotationDef.Name;
                ListPropertyType = annotationDef.ListPropertyType;
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
                return ListPropertyType == null ? AnnotationDef.AnnotationType.text : ListPropertyType.AnnotationType;
            }
            set
            {
                if (!Equals(value, AnnotationType))
                {
                    ListPropertyType = new ListPropertyType(value, null);
                }
            }
        }

        public ListPropertyType ListPropertyType
        {
            get { return (ListPropertyType) comboType.SelectedItem; }
            set { comboType.SelectedItem = value; }
        }

        public AnnotationDef.AnnotationTargetSet AnnotationTargets
        {
            get
            {
                var targets = new List<AnnotationDef.AnnotationTarget>();
                for (int i = 0; i < checkedListBoxAppliesTo.Items.Count; i++)
                {
                    if (checkedListBoxAppliesTo.GetItemChecked(i))
                    {
                        targets.Add(((AnnotationTargetItem)checkedListBoxAppliesTo.Items[i]).AnnotationTarget);
                    }
                }
                return AnnotationDef.AnnotationTargetSet.OfValues(targets);
            }
            set
            {
                for (int i = 0; i < checkedListBoxAppliesTo.Items.Count; i++)
                {
                    AnnotationDef.AnnotationTarget annotationTarget =
                        ((AnnotationTargetItem) checkedListBoxAppliesTo.Items[i]).AnnotationTarget;
                    checkedListBoxAppliesTo.SetItemChecked(i, value.Contains(annotationTarget));
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
                // ReSharper disable once LocalizableElement
                return tbxValues.Text.Split(new[] {"\r\n"}, StringSplitOptions.None);
            }
            set
            {
                tbxValues.Text = TextUtil.LineSeparate(value.ToArray()); 
            }
        }

        public AnnotationDef GetAnnotationDef()
        {
            return new AnnotationDef(AnnotationName, AnnotationTargets, ListPropertyType, Items);
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
            string name;
            if (!messageBoxHelper.ValidateNameTextBox(tbxName, out name))
            {
                return;
            }
            if (_annotationDef == null || name != _annotationDef.Name)
            {
                foreach (var annotationDef in _existing)
                {
                    if (annotationDef.Name == name)
                    {
                        messageBoxHelper.ShowTextBoxError(tbxName, Resources.DefineAnnotationDlg_OkDialog_There_is_already_an_annotation_defined_named__0__, name);
                        return;
                    }
                }
            }
            if (checkedListBoxAppliesTo.CheckedItems.Count == 0)
            {
                MessageBox.Show(this, Resources.DefineAnnotationDlg_OkDialog_Choose_at_least_one_type_for_this_annotation_to_apply_to, Program.Name);
                checkedListBoxAppliesTo.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        internal class AnnotationTargetItem
        {
            public AnnotationTargetItem(AnnotationDef.AnnotationTarget annotationTarget, SrmDocument.DOCUMENT_TYPE modeUI)
            {
                AnnotationTarget = annotationTarget;
                ModeUI = modeUI;
            }

            public AnnotationDef.AnnotationTarget AnnotationTarget { get; private set; }
            public SrmDocument.DOCUMENT_TYPE ModeUI { get; private set; }
            public override string ToString()
            {
                return Helpers.PeptideToMoleculeTextMapper.Translate(AnnotationDef.AnnotationTargetPluralName(AnnotationTarget), ModeUI);
            }
        }
    }
}
