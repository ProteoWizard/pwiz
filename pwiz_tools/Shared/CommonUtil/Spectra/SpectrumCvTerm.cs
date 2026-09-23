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

using System.Collections.Generic;

namespace pwiz.Common.Spectra
{
    /// <summary>
    /// A controlled-vocabulary term that can appear on a spectrum, as the ontology describes it
    /// rather than as some file reported it. A catalog entry says what a term means and what kind
    /// of value it carries; a <see cref="SpectrumMetadataTerm"/> says what a particular spectrum
    /// had to say for it.
    /// </summary>
    public class SpectrumCvTerm
    {
        /// <summary>
        /// The XML Schema types that describe a number. A term declared with one of these can be
        /// filtered with numeric comparisons; anything else (xsd:string, xsd:boolean, xsd:anyURI,
        /// xsd:dateTime, ...) is compared as text.
        /// </summary>
        private static readonly ICollection<string> NUMERIC_VALUE_TYPES = new HashSet<string>
        {
            @"xsd:decimal", @"xsd:float", @"xsd:double", @"xsd:byte", @"xsd:short", @"xsd:int",
            @"xsd:long", @"xsd:integer", @"xsd:negativeInteger", @"xsd:nonNegativeInteger",
            @"xsd:nonPositiveInteger", @"xsd:positiveInteger", @"xsd:unsignedByte",
            @"xsd:unsignedShort", @"xsd:unsignedInt", @"xsd:unsignedLong"
        };

        public SpectrumCvTerm(string accession, string name, string definition, string valueType)
        {
            Accession = accession;
            Name = name;
            Definition = definition;
            ValueType = string.IsNullOrEmpty(valueType) ? null : valueType;
        }

        /// <summary>
        /// The CV accession, e.g. "MS:1000505".
        /// </summary>
        public string Accession { get; }

        /// <summary>
        /// Human-readable term name, e.g. "base peak intensity".
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The controlled-vocabulary definition of the term, shown as explanatory help text.
        /// </summary>
        public string Definition { get; }

        /// <summary>
        /// The type of value the term carries, as declared by the ontology's has_value_type
        /// relationship: usually an XML Schema type such as "xsd:float", but a CV accession for the
        /// few terms whose value is a list (e.g. "MS:1002712", list of integers). Null for the terms
        /// that carry no value at all, the pure flags such as "zoom scan".
        /// </summary>
        public string ValueType { get; }

        /// <summary>
        /// True when the term's declared value is a number, so a column for it can offer numeric
        /// comparisons rather than only text ones.
        /// </summary>
        public bool IsNumeric
        {
            get { return ValueType != null && NUMERIC_VALUE_TYPES.Contains(ValueType); }
        }

        public override string ToString()
        {
            return Name + @" (" + Accession + @")";
        }
    }
}
