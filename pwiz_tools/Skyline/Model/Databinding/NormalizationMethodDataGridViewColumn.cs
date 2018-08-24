/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Databinding
{
    public class NormalizationMethodDataGridViewColumn : DataGridViewComboBoxColumn
    {
        private SkylineDataSchema _skylineDataSchema;
        private readonly IDocumentChangeListener _documentChangeListener;
        public NormalizationMethodDataGridViewColumn()
        {
            DisplayMember = @"Item1";
            ValueMember = @"Item2";

            FlatStyle = FlatStyle.Flat;
            _documentChangeListener = new DocumentChangeListener(this);
        }

        protected override void OnDataGridViewChanged()
        {
            base.OnDataGridViewChanged();
            SkylineDataSchema = FindSkylineDataSchema();
        }

        public SkylineDataSchema SkylineDataSchema
        {
            get { return _skylineDataSchema; }
            set
            {
                if (ReferenceEquals(SkylineDataSchema, value))
                {
                    return;
                }
                if (null != SkylineDataSchema)
                {
                    SkylineDataSchema.Unlisten(_documentChangeListener);
                }
                _skylineDataSchema = value;
                if (null != SkylineDataSchema)
                {
                    SkylineDataSchema.Listen(_documentChangeListener);
                }
                UpdateDropdownItems();
            }
        }

        private SkylineDataSchema FindSkylineDataSchema()
        {
            if (null == DataGridView || null == DataPropertyName)
            {
                return null;
            }
            var bindingSource = DataGridView.DataSource as BindingSource;
            if (null == bindingSource)
            {
                return null;
            }
            var columnPropertyDescriptor = bindingSource.GetItemProperties(null)[DataPropertyName] as ColumnPropertyDescriptor;
            if (null == columnPropertyDescriptor)
            {
                return null;
            }
            return columnPropertyDescriptor.DisplayColumn.DataSchema as SkylineDataSchema;
        }

        private void UpdateDropdownItems()
        {
            var newItems = GetDropdownItems(GetSrmDocument());
            if (!newItems.SequenceEqual(Items.Cast<object>()))
            {
                Items.Clear();
                Items.AddRange(newItems.Cast<object>().ToArray());
            }
        }

        private SrmDocument GetSrmDocument()
        {
            if (null == DataGridView || null == DataPropertyName)
            {
                return null;
            }
            var bindingSource = DataGridView.DataSource as BindingSource;
            if (null == bindingSource)
            {
                return null;
            }
            var columnPropertyDescriptor = bindingSource.GetItemProperties(null)[DataPropertyName] as ColumnPropertyDescriptor;
            if (null == columnPropertyDescriptor)
            {
                return null;
            }
            var skylineDataSchema = columnPropertyDescriptor.DisplayColumn.DataSchema as SkylineDataSchema;
            if (skylineDataSchema == null)
            {
                return null;
            }
            return skylineDataSchema.Document;
        }

        private IList<Tuple<string, NormalizationMethod>> GetDropdownItems(SrmDocument document)
        {
            List<Tuple<string, NormalizationMethod>> normalizationMethods
                = new List<Tuple<string, NormalizationMethod>>
                {new Tuple<string, NormalizationMethod>(string.Empty, null)};

            if (null != document)
            {
                normalizationMethods.AddRange(NormalizationMethod.ListNormalizationMethods(document).Select(ToDropdownItem));
                normalizationMethods.AddRange(NormalizationMethod.RatioToSurrogate.ListSurrogateNormalizationMethods(document).Select(ToDropdownItem));
                // If there are any molecules that have a normalization method that is not in the list, add it to the end.
                var normalizationMethodValues = new HashSet<NormalizationMethod>(normalizationMethods.Select(tuple => tuple.Item2)
                    .Where(normalizationMethod=>null != normalizationMethod));
                foreach (var molecule in document.Molecules)
                {
                    if (molecule.NormalizationMethod != null &&
                        normalizationMethodValues.Add(molecule.NormalizationMethod))
                    {
                        normalizationMethods.Add(ToDropdownItem(molecule.NormalizationMethod));
                    }
                }
            }

            return normalizationMethods;
        }

        private static Tuple<string, NormalizationMethod> ToDropdownItem(NormalizationMethod normalizationMethod)
        {
            return Tuple.Create(normalizationMethod.ToString(), normalizationMethod);
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly NormalizationMethodDataGridViewColumn _column;
            public DocumentChangeListener(NormalizationMethodDataGridViewColumn column)
            {
                _column = column;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _column.UpdateDropdownItems();
            }
        }
    }
}
