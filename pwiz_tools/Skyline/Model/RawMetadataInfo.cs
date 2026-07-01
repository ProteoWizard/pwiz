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
    /// Expandable holder for a scan's uninterpreted mzML CV/user parameters
    /// (<see cref="SpectrumMetadataTerm"/>). It appears as an expandable "Raw Metadata" node in the
    /// full-scan properties sidebar; each term becomes a read-only child row whose value carries its
    /// unit and whose help text is the controlled-vocabulary definition. The term set is per-scan and
    /// not known at compile time, so this implements <see cref="ICustomTypeDescriptor"/> rather than
    /// exposing fixed properties. Using a single expandable node (rather than injecting the terms as
    /// top-level properties) is deliberate: the WinForms PropertyGrid corrupts its expand/collapse
    /// state restore when custom descriptors form a dynamic top-level category.
    /// </summary>
    public class RawMetadataInfo : ICustomTypeDescriptor
    {
        private readonly IList<SpectrumMetadataTerm> _terms;

        public RawMetadataInfo(IEnumerable<SpectrumMetadataTerm> terms)
        {
            _terms = terms.ToArray();
        }

        public IList<SpectrumMetadataTerm> Terms
        {
            get { return _terms; }
        }

        public bool Any
        {
            get { return _terms.Count > 0; }
        }

        public override string ToString()
        {
            // The collapsed node carries no inline value; its children hold the terms.
            return string.Empty;
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
        /// One uninterpreted mzML CV/user parameter as a read-only child row. The label is the
        /// ontology term name; the value shows the term value with its unit (a value-less flag term,
        /// e.g. "MS1 spectrum", shows just its name); the help text is the CV definition. Display
        /// name and description are supplied as attributes because that is where the PropertyGrid
        /// help pane reads them. The descriptor name is index-qualified so it stays unique even if an
        /// accession repeats.
        /// </summary>
        private class TermPropertyDescriptor : PropertyDescriptor
        {
            private readonly SpectrumMetadataTerm _term;

            public TermPropertyDescriptor(SpectrumMetadataTerm term, int index)
                : base(string.Format(CultureInfo.InvariantCulture, @"{0}_{1}", index, term.Accession),
                    new Attribute[]
                    {
                        new DisplayNameAttribute(term.Name ?? term.Accession),
                        new DescriptionAttribute(string.IsNullOrEmpty(term.Definition) ? term.Accession : term.Definition)
                    })
            {
                _term = term;
            }

            public override object GetValue(object component)
            {
                if (string.IsNullOrEmpty(_term.Value))
                {
                    return string.Empty;
                }
                return string.IsNullOrEmpty(_term.Unit) ? _term.Value : _term.Value + @" " + _term.Unit;
            }

            public override bool CanResetValue(object component) => false;
            public override bool ShouldSerializeValue(object component) => false;
            public override void ResetValue(object component) { }
            public override void SetValue(object component, object value) { }
            public override bool IsReadOnly => true;
            public override Type ComponentType => typeof(RawMetadataInfo);
            public override Type PropertyType => typeof(string);
        }
    }
}
