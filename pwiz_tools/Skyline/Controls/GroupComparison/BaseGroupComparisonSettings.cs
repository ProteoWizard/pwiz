/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public class BaseGroupComparisonSettings : FormEx
    {
        protected bool _inChangeSettings;

        public BaseGroupComparisonSettings()
        {
            
        }

        protected BaseGroupComparisonSettings(GroupComparisonModel groupComparisonModel)
        {
            GroupComparisonModel = groupComparisonModel;
            GroupComparisonModel.AddModelChanged(this, OnModelChanged);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual GroupComparisonDef GroupComparisonDef
        {
            get { return GroupComparisonModel.GroupComparisonDef; }
            set { throw new InvalidOperationException(); }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GroupComparisonModel GroupComparisonModel { get; private set; }

        private void OnModelChanged(GroupComparisonModel model)
        {
            if (_inChangeSettings)
            {
                return;
            }
            _inChangeSettings = true;
            try
            {
                UpdateSettings();
            }
            finally
            {
                _inChangeSettings = false;
            }
        }

        protected virtual void UpdateSettings()
        {
            
        }

        protected void BindComboBox<T>(ComboBox comboBox, Func<IEnumerable<T>> listItems,
            Func<GroupComparisonDef, T> getter, Func<GroupComparisonDef, T, GroupComparisonDef> setter)
        {
            
        }

        protected class ComboBinding<T>
        {
            public ComboBinding(ComboBox comboBox, Func<IEnumerable<T>> listItems, Func<GroupComparisonDef, T> getter,
                Func<GroupComparisonDef, T, GroupComparisonDef> setter)
            {
                
            }
        }

        protected void comboControlAnnotation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            var comboControlAnnotation = (ComboBox) sender;
            GroupComparisonDef =
                GroupComparisonDef.ChangeControlAnnotation(
                    comboControlAnnotation.SelectedItem as string);
        }

        protected void comboControlValue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            var comboControlValue = (ComboBox) sender;
            GroupComparisonDef = GroupComparisonDef.ChangeControlValue(comboControlValue.SelectedItem as string);
        }

        protected void comboNormalizationMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            var comboNormalizationMethod = (ComboBox) sender;
            var normalizationMethod = comboNormalizationMethod.SelectedItem as NormalizationMethod ??
                                      NormalizationMethod.NONE;
            GroupComparisonDef = GroupComparisonDef
                .ChangeNormalizationMethod(normalizationMethod);
        }

        protected void comboCaseValue_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            var comboCaseValue = (ComboBox) sender;
            string caseValue = comboCaseValue.SelectedItem as string;
            if (string.IsNullOrEmpty(caseValue))
            {
                caseValue = null;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeCaseValue(caseValue);
        }

        protected void comboIdentityAnnotation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            var comboIdentityAnnotation = (ComboBox) sender;
            string identityAnnotation = comboIdentityAnnotation.SelectedItem as string;
            if (string.IsNullOrEmpty(identityAnnotation))
            {
                GroupComparisonDef = GroupComparisonDef
                    .ChangeIdentityAnnotation(null)
                    .ChangeAverageTechnicalReplicates(false);
            }
            else
            {
                GroupComparisonDef = GroupComparisonDef
                    .ChangeIdentityAnnotation(identityAnnotation)
                    .ChangeAverageTechnicalReplicates(true);
            }
        }

        protected void tbxConfidenceLevel_TextChanged(object sender, EventArgs e)
        {
            if (_inChangeSettings)
            {
                return;
            }
            double confidenceLevel;
            var tbxConfidenceLevel = (TextBox) sender;
            MessageBoxHelper helper = new MessageBoxHelper(this, false);

            if (!helper.ValidateDecimalTextBox(tbxConfidenceLevel, 0, 100, out confidenceLevel))
            {
                return;
            }
            GroupComparisonDef = GroupComparisonDef.ChangeConfidenceLevelTimes100(confidenceLevel);
        }

        protected IEnumerable<string> ListReplicateAnnotations()
        {
            return GroupComparisonModel.Document.Settings.DataSettings.AnnotationDefs
                .Where(def => def.AnnotationTargets.Contains(AnnotationDef.AnnotationTarget.replicate))
                .Select(def => def.Name);
        }

        protected string[] ListControlValues()
        {
            var newSettings = GroupComparisonModel.Document.Settings;
            var annotationDef = newSettings.DataSettings.AnnotationDefs.FirstOrDefault(
                def => def.Name == GroupComparisonDef.ControlAnnotation);
            if (null != annotationDef && newSettings.HasResults)
            {
                string[] controlValues = newSettings.MeasuredResults.Chromatograms.Select(
                    chromatogram => chromatogram.Annotations.GetAnnotation(annotationDef.Name) ?? string.Empty)
                    .Distinct()
                    .ToArray();
                Array.Sort(controlValues);
                return controlValues;
            }
            return new string[0];
        }

        protected void ReplaceComboItems<T>(ComboBox comboBox, IEnumerable<T> items, T selectedItem)
        {
            var itemObjects = items.Cast<object>().ToArray();
            int newSelectedIndex = -1;
            for (int i = 0; i < itemObjects.Length; i++)
            {
                if (Equals(selectedItem, itemObjects[i]))
                {
                    newSelectedIndex = i;
                    break;
                }
            }
            if (newSelectedIndex == comboBox.SelectedIndex && ArrayUtil.EqualsDeep(itemObjects, comboBox.Items.Cast<object>().ToArray()))
            {
                return;
            }
            comboBox.Items.Clear();
            comboBox.Items.AddRange(itemObjects);
            comboBox.SelectedIndex = newSelectedIndex;
        }

        public virtual ComboBox ComboControlAnnotation { get { throw new InvalidOperationException();} }
        public virtual ComboBox ComboControlValue { get { throw new InvalidOperationException();} }
        public virtual ComboBox ComboNormalizationMethod { get {  throw new InvalidOperationException();} }
        public virtual ComboBox ComboCaseValue { get {  throw new InvalidOperationException();} }
        public virtual ComboBox ComboIdentityAnnotation { get {  throw new InvalidOperationException();} }
        public virtual TextBox TextBoxConfidenceLevel { get { throw new InvalidOperationException();} }
        public virtual RadioButton RadioScopePerProtein { get { throw new InvalidOperationException();} }
        public virtual RadioButton RadioScopePerPeptide { get { throw new InvalidOperationException();} }
    }
}
