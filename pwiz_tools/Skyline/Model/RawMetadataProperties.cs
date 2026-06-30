/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Spectra;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// A read-only, PropertyGrid-friendly view over a spectrum's uninterpreted mzML
    /// CV/user parameters (<see cref="SpectrumMetadataTerm"/>). It appears in the
    /// full-scan properties sidebar as a single expandable "Raw metadata" node; each
    /// term becomes a child row showing the ontology term name and its value with unit.
    /// The term set is per-scan and not known at compile time, so this implements
    /// <see cref="ICustomTypeDescriptor"/> rather than exposing fixed properties.
    /// </summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class RawMetadataProperties : ICustomTypeDescriptor
    {
        private readonly IList<SpectrumMetadataTerm> _terms;

        public RawMetadataProperties(IEnumerable<SpectrumMetadataTerm> terms)
        {
            _terms = terms.ToArray();
        }

        public bool Any
        {
            get { return _terms.Count > 0; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, FullScanPropertiesRes.RawMetadataSummary, _terms.Count);
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(null);
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var descriptors = _terms
                .Select((term, index) => (PropertyDescriptor) new TermPropertyDescriptor(term, index))
                .ToArray();
            return new PropertyDescriptorCollection(descriptors);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return null;
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return null;
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return EventDescriptorCollection.Empty;
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return EventDescriptorCollection.Empty;
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        /// <summary>
        /// Exposes one <see cref="SpectrumMetadataTerm"/> as a read-only string-valued row.
        /// The descriptor name is index-qualified so it stays unique even if the same
        /// accession appears more than once; the displayed label is the ontology term name.
        /// </summary>
        private class TermPropertyDescriptor : PropertyDescriptor
        {
            private readonly SpectrumMetadataTerm _term;

            public TermPropertyDescriptor(SpectrumMetadataTerm term, int index)
                : base(string.Format(CultureInfo.InvariantCulture, @"{0}_{1}", index, term.Accession), null)
            {
                _term = term;
            }

            public override string DisplayName
            {
                get { return _term.Name ?? _term.Accession; }
            }

            public override string Description
            {
                get { return _term.Accession; }
            }

            public override object GetValue(object component)
            {
                if (string.IsNullOrEmpty(_term.Unit))
                {
                    return _term.Value;
                }
                return _term.Value + @" " + _term.Unit;
            }

            public override bool CanResetValue(object component)
            {
                return false;
            }

            public override bool ShouldSerializeValue(object component)
            {
                return false;
            }

            public override void ResetValue(object component)
            {
            }

            public override void SetValue(object component, object value)
            {
            }

            public override bool IsReadOnly
            {
                get { return true; }
            }

            public override Type ComponentType
            {
                get { return typeof(RawMetadataProperties); }
            }

            public override Type PropertyType
            {
                get { return typeof(string); }
            }
        }
    }
}
