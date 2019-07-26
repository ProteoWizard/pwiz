/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lists;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Lists
{
    public partial class ListDesigner : FormEx
    {
        private readonly BindingList<ListProperty> _listProperties;
        private ListData _listDefOriginal;
        private ImmutableList<ListDef> _existing;

        public ListDesigner(ListData listData, IEnumerable<ListData> existing)
        {
            InitializeComponent();
            _listDefOriginal = listData ?? ListData.EMPTY;
            _existing = ImmutableList.ValueOfOrEmpty(existing.Select(l=>l.ListDef));
            _listProperties = new BindingList<ListProperty>();
            bindingSourceListProperties.DataSource = _listProperties;
            colPropertyType.ValueType = typeof(ListPropertyType);
            colPropertyType.ValueMember = @"Self";
            colPropertyType.DisplayMember = @"Label";
            SetListDef(_listDefOriginal.ListDef);
            _listProperties.ListChanged += (sender,args)=>OnListPropertiesChanged();
        }

        private void SetListDef(ListDef listDef)
        {
            tbxListName.Text = listDef.Name;
            if (!string.IsNullOrEmpty(listDef.Name))
            {
                tbxListName.ReadOnly = true;
            }
            foreach (var property in listDef.Properties)
            {
                _listProperties.Add(new ListProperty(property));
            }
            colPropertyType.Items.Clear();
            foreach (var propertyType in ListPropertyType.ListPropertyTypes(_existing))
            {
                if (propertyType.AnnotationType == AnnotationDef.AnnotationType.value_list)
                {
                    continue;
                }
                colPropertyType.Items.Add(propertyType);
            }
            PopulatePropertyDropdown(comboIdProperty, listDef.IdProperty);
            PopulatePropertyDropdown(comboDisplayProperty, listDef.DisplayProperty);
        }

        private void OnListPropertiesChanged()
        {
            PopulatePropertyDropdown(comboIdProperty, comboIdProperty.SelectedItem as string);
            PopulatePropertyDropdown(comboDisplayProperty, comboDisplayProperty.SelectedItem as string);
        }

        private void PopulatePropertyDropdown(ComboBox comboBox, string currentValue)
        {
            comboBox.Items.Clear();
            comboBox.Items.Add(string.Empty);
            var possibleValues = _listProperties
                .Select(prop => prop.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct();
            bool found = false;
            foreach (string propertyName in possibleValues)
            {
                comboBox.Items.Add(propertyName);
                if (propertyName == currentValue)
                {
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    found = true;
                }
            }
            if (!found)
            {
                if (string.IsNullOrEmpty(currentValue))
                {
                    comboBox.SelectedIndex = 0;
                }
                else
                {
                    comboBox.Items.Add(currentValue);
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }
            }
        }

        public ListData GetListDef()
        {
            
            var listDef = new ListDef(tbxListName.Text);
            listDef = listDef.ChangeProperties(_listProperties.Select(prop => prop.AnnotationDef));
            listDef = listDef.ChangeIdProperty(comboIdProperty.SelectedItem as string);
            listDef = listDef.ChangeDisplayProperty(comboDisplayProperty.SelectedItem as string);
            if (_listDefOriginal == null || _listDefOriginal.RowCount == 0)
            {
                return new ListData(listDef);
            }
            var columNameMap = _listProperties.Where(prop=>!string.IsNullOrEmpty(prop.OriginalName))
                .ToDictionary(prop => prop.Name, prop => prop.OriginalName);
            return _listDefOriginal.ChangeListDef(listDef, columNameMap);
        }

        public class ListProperty
        {
            public ListProperty() : this(AnnotationDef.EMPTY)
            {
            }
            public ListProperty(AnnotationDef annotationDef)
            {
                AnnotationDef = annotationDef;
                OriginalName = annotationDef.Name;
            }

            public string Name { get { return AnnotationDef.Name; } set
                {
                    AnnotationDef = (AnnotationDef) AnnotationDef.ChangeName(value);
                }
            }

            public ListPropertyType ListPropertyType
            {
                get { return new ListPropertyType(AnnotationDef.Type, AnnotationDef.Lookup); }
                set { AnnotationDef = AnnotationDef.ChangeType(value.AnnotationType).ChangeLookup(value.Lookup); }

            }

            public AnnotationDef AnnotationDef { get; set; }
            public string OriginalName { get; set; }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this, true);
            string name;
            if (!helper.ValidateNameTextBox(tbxListName, out name))
            {
                return;
            }
            if (name != _listDefOriginal.ListName && _existing.Any(listDef => listDef.Name == name))
            {
                helper.ShowTextBoxError(tbxListName, string.Format(Resources.ListDesigner_OkDialog_There_is_already_a_list_named___0___, name));
                return;
            }
            var propertyNames = new HashSet<string>();
            for (int i = 0; i < _listProperties.Count; i++)
            {
                if (!propertyNames.Add(_listProperties[i].Name))
                {
                    MessageDlg.Show(this, Resources.ListDesigner_OkDialog_Duplicate_property_name);
                    dataGridViewProperties.CurrentCell = dataGridViewProperties.Rows[i].Cells[colPropertyName.Index];
                    return;
                }
            }
            string idProperty = comboIdProperty.SelectedItem as string;
            if (!string.IsNullOrEmpty(idProperty) && !propertyNames.Contains(idProperty))
            {
                MessageDlg.Show(this, Resources.ListDesigner_OkDialog_No_such_property);
                comboIdProperty.Focus();
                return;
            }
            string displayProperty = comboDisplayProperty.SelectedItem as string;
            if (!string.IsNullOrEmpty(displayProperty) && !propertyNames.Contains(displayProperty))
            {
                MessageDlg.Show(this, Resources.ListDesigner_OkDialog_No_such_property);
                comboDisplayProperty.Focus();
                return;
            }
            try
            {
                GetListDef();
            }
            catch (Exception e)
            {
                MessageDlg.ShowWithException(this, TextUtil.LineSeparate(Resources.ListDesigner_OkDialog_There_was_an_error_trying_to_apply_this_list_definition_to_the_original_data_, e.Message), e);
                return;
            }
            
            DialogResult = DialogResult.OK;
        }
#region For test automation
        public string ListName
        {
            get { return tbxListName.Text; }
            set
            {
                if (value != tbxListName.Text)
                {
                    if (tbxListName.ReadOnly)
                    {
                        throw new InvalidOperationException();
                    }
                    tbxListName.Text = value;
                }
            }
        }

        public DataGridView ListPropertiesGrid { get { return dataGridViewProperties; } }
        public string IdProperty { get { return comboIdProperty.Text; } set { comboIdProperty.Text = value; } }

        public string DisplayProperty
        {
            get { return comboDisplayProperty.Text; }
            set { comboDisplayProperty.Text = value; }
        }
#endregion
    }
}
