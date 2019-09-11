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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
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
        private SkylineDataSchema _dataSchema;

        public DefineAnnotationDlg(IEnumerable<AnnotationDef> existing)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            checkedListBoxAppliesTo.Items.Clear();
            comboAppliesTo.Items.Clear();
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
                var item = new AnnotationTargetItem(annotationTarget, ModeUI);
                checkedListBoxAppliesTo.Items.Add(item);
                comboAppliesTo.Items.Add(item);
            }
            comboType.Items.AddRange(ListPropertyType.ListPropertyTypes().ToArray());
            comboType.SelectedIndex = 0;
            ComboHelper.AutoSizeDropDown(comboType);
            _existing = existing;
            _dataSchema = BrowsingDataSchema.GetBrowsingDataSchema();
            availableFieldsTree1.RootColumn = NullRootColumn;
        }

        private ColumnDescriptor NullRootColumn { get { return ColumnDescriptor.RootColumn(_dataSchema, typeof(object));} }

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
                if (annotationDef.Expression != null)
                {
                    var expression = annotationDef.Expression;
                    tabControl1.SelectedTab = tabPageCalculated;
                    availableFieldsTree1.SelectColumn(expression.Column);
                    if (null != expression.AggregateOperation 
                        && comboAppliesTo.Items.Contains(expression.AggregateOperation))
                    {
                        comboAppliesTo.SelectedItem = expression.AggregateOperation;
                    }
                }
                else
                {
                    tabControl1.SelectedTab = tabPageEditable;
                }
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
                if (Equals(AnnotationTargets, value))
                {
                    return;
                }
                for (int i = 0; i < checkedListBoxAppliesTo.Items.Count; i++)
                {
                    AnnotationDef.AnnotationTarget annotationTarget =
                        ((AnnotationTargetItem) checkedListBoxAppliesTo.Items[i]).AnnotationTarget;
                    checkedListBoxAppliesTo.SetItemChecked(i, value.Contains(annotationTarget));
                }

                if (value.Count == 1)
                {
                    for (int i = 0; i < comboAppliesTo.Items.Count; i++)
                    {
                        var item = comboAppliesTo.Items[i] as AnnotationTargetItem;
                        if (item != null && item.AnnotationTarget == value.First())
                        {
                            comboAppliesTo.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    comboAppliesTo.SelectedIndex = -1;
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
            if (!IsCalculated || null == GetSelectedColumnDescriptor())
            {
                return new AnnotationDef(AnnotationName, AnnotationTargets, ListPropertyType, Items);
            }

            var selectedColumnDescriptor = GetSelectedColumnDescriptor();
            var annotationDef = new AnnotationDef(AnnotationName, AnnotationTargets,
                GetListPropertyType(selectedColumnDescriptor, AggregateOperation), null);
            var expression = new AnnotationExpression(selectedColumnDescriptor.PropertyPath);
            if (selectedColumnDescriptor.CollectionAncestor() != null)
            {
                expression = expression.ChangeAggregateOperation(AggregateOperation);
            }
            annotationDef = annotationDef.ChangeExpression(expression);
            return annotationDef;
        }

        public void SelectPropertyPath(PropertyPath propertyPath)
        {
            availableFieldsTree1.SelectColumn(propertyPath);
        }

        public bool IsCalculated
        {
            get { return tabControl1.SelectedTab == tabPageCalculated; }
            set { tabControl1.SelectedTab = value ? tabPageCalculated : tabPageEditable; }
        }

        public AggregateOperation AggregateOperation
        {
            get
            {
                return comboAggregateOperation.SelectedItem as AggregateOperation;
            }
            set { comboAggregateOperation.SelectedItem = value; }
        }

        private ColumnDescriptor GetSelectedColumnDescriptor()
        {
            var selectedNode = availableFieldsTree1.SelectedNode;
            if (selectedNode == null)
            {
                return null;
            }

            return availableFieldsTree1.GetValueColumn(selectedNode);
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

            if (tabControl1.SelectedTab == tabPageEditable)
            {
                if (checkedListBoxAppliesTo.CheckedItems.Count == 0)
                {
                    messageBoxHelper.ShowTextBoxError(checkedListBoxAppliesTo, Resources.DefineAnnotationDlg_OkDialog_Choose_at_least_one_type_for_this_annotation_to_apply_to);
                    return;
                }
            }
            else
            {
                if (comboAppliesTo.SelectedIndex < 0)
                {
                    messageBoxHelper.ShowTextBoxError(comboAppliesTo, Resources.DefineAnnotationDlg_OkDialog_Choose_a_type_for_this_annotation_to_apply_to_);
                    return;
                }
                if (GetSelectedColumnDescriptor() == null)
                {
                    messageBoxHelper.ShowTextBoxError(availableFieldsTree1, Resources.DefineAnnotationDlg_OkDialog_Choose_a_value_for_this_annotation_);
                    return;
                }
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

        private void comboAppliesTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboAppliesTo.SelectedIndex < 0)
            {
                availableFieldsTree1.RootColumn = NullRootColumn;
            }


            var annotationTargetItem = (AnnotationTargetItem) comboAppliesTo.Items[comboAppliesTo.SelectedIndex];
            AnnotationTargets = AnnotationDef.AnnotationTargetSet.Singleton(annotationTargetItem.AnnotationTarget);
            availableFieldsTree1.RootColumn = ColumnDescriptor.RootColumn(_dataSchema, AnnotationCalculator.RowTypeFromAnnotationTarget(annotationTargetItem.AnnotationTarget), UiModes.FromDocumentType(ModeUI));
        }

        private void availableFieldsTree1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var columnDescriptor = GetSelectedColumnDescriptor();

            if (columnDescriptor == null || null == columnDescriptor.CollectionAncestor())
            {
                comboAggregateOperation.Enabled = false;
                AggregateOperation = null;
            }
            else
            {
                comboAggregateOperation.Enabled = true;
                var aggregateChoices = AggregateOperation.ALL
                    .Where(op => op.IsValidForType(_dataSchema, columnDescriptor.PropertyType)).ToArray();
                if (!aggregateChoices.SequenceEqual(comboAggregateOperation.Items.Cast<object>()))
                {
                    var oldValue = comboAggregateOperation.SelectedItem;
                    comboAggregateOperation.Items.Clear();
                    comboAggregateOperation.Items.AddRange(aggregateChoices);
                    comboAggregateOperation.SelectedIndex = Math.Max(0, Array.IndexOf(aggregateChoices, oldValue));
                }
            }
            if (columnDescriptor != null)
            {
                comboType.SelectedItem = GetListPropertyType(columnDescriptor, AggregateOperation);
            }
        }

        public static ListPropertyType GetListPropertyType(ColumnDescriptor columnDescriptor, AggregateOperation aggregateOperation)
        {
            var propertyType = columnDescriptor.DataSchema.GetWrappedValueType(columnDescriptor.PropertyType);
            if (aggregateOperation != null)
            {
                propertyType = aggregateOperation.GetPropertyType(propertyType);
            }
            if (propertyType == typeof(bool))
            {
                return ListPropertyType.TRUE_FALSE;
            }

            if (propertyType.IsPrimitive)
            {
                return ListPropertyType.NUMBER;
            }

            return ListPropertyType.TEXT;
        }

        private class BrowsingDataSchema : SkylineDataSchema
        {
            public static BrowsingDataSchema GetBrowsingDataSchema()
            {
                var memoryDocumentContainer = new MemoryDocumentContainer();
                var document = new SrmDocument(SrmSettingsList.GetDefault());
                memoryDocumentContainer.SetDocument(document, memoryDocumentContainer.Document);
                return new BrowsingDataSchema(memoryDocumentContainer, GetLocalizedSchemaLocalizer());
            }
            private BrowsingDataSchema(IDocumentContainer documentContainer, DataSchemaLocalizer dataSchemaLocalizer) :
                base(documentContainer, dataSchemaLocalizer)
            {

            }

            public override bool IsRootTypeSelectable(Type type)
            {
                return false;
            }
        }
    }
}
