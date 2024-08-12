/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding;
using SymbolType=ZedGraph.SymbolType;

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    public sealed class SampleType : LabeledValues<string>
    {
        public static readonly SampleType UNKNOWN = new SampleType(@"unknown",
            () => QuantificationStrings.SampleType_UNKNOWN_Unknown, Color.Black, SymbolType.XCross);
        public static readonly SampleType STANDARD = new SampleType(@"standard",
            () => QuantificationStrings.SampleType_STANDARD_Standard, Color.Gray, SymbolType.Square);
        public static readonly SampleType QC = new SampleType(@"qc",
            ()=>QuantificationStrings.SampleType_QC_Quality_Control, Color.Green, SymbolType.Diamond);
        public static readonly SampleType SOLVENT = new SampleType(@"solvent",
            () => QuantificationStrings.SampleType_SOLVENT_Solvent, Color.BlueViolet, SymbolType.Circle);
        public static readonly SampleType BLANK = new SampleType(@"blank",
            () => QuantificationStrings.SampleType_BLANK_Blank, Color.Blue, SymbolType.Triangle);
        public static readonly SampleType DOUBLE_BLANK = new SampleType(@"double_blank",
            () => QuantificationStrings.SampleType_DOUBLE_BLANK_Double_Blank, Color.LightBlue, SymbolType.TriangleDown);
        public static readonly SampleType DEFAULT = UNKNOWN;

        private SampleType(string name, Func<string> getLabelFunc, Color color, SymbolType symbolType) : base(name, getLabelFunc)
        {
            Color = color;
            SymbolType = symbolType;
        }

        public static readonly ImmutableList<SampleType> ALL = ImmutableList.ValueOf(new[]
        {
            UNKNOWN,
            STANDARD,
            QC,
            SOLVENT,
            BLANK,
            DOUBLE_BLANK
        });

        public static IList<SampleType> ListSampleTypes()
        {
            return ALL;
        }

        public static SampleType FromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DEFAULT;
            }
            return ListSampleTypes().FirstOrDefault(sampleType => sampleType.Name == name);
        }

        public override string ToString()
        {
            return Label;
        }

        public Color Color { get; private set; }
        public SymbolType SymbolType { get; private set; }
        public bool AllowExclude { get { return Equals(STANDARD); } }

        private bool Equals(SampleType other)
        {
            return Name.Equals(other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SampleType) obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public class PropertyFormatter : IPropertyFormatter
        {
            public string FormatValue(CultureInfo cultureInfo, object value)
            {
                if (Equals(value, DEFAULT))
                {
                    return string.Empty;
                }
                return ((SampleType) value).Name;
            }

            public object ParseValue(CultureInfo cultureInfo, string text)
            {
                return FromName(text);
            }
        }
    }
}
