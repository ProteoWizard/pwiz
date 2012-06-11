/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("isolation_scheme")]
    public sealed class IsolationScheme : XmlNamedElement, IValidating
    {
        public const int MIN_MULTIPLEXED_ISOLATION_WINDOWS = 2;
        public const int MAX_MULTIPLEXED_ISOLATION_WINDOWS = 100;

        public static class SpecialHandlingType
        {
            public const string NONE = "None";
            public const string MULTIPLEXED = "Multiplexed";
            public const string MS_E = "MSe";

            public static void Validate(string specialHandling)
            {
                if (!Equals(specialHandling, NONE) &&
                    !Equals(specialHandling, MULTIPLEXED) &&
                    !Equals(specialHandling, MS_E))
                    throw new InvalidDataException(string.Format(
                        @"""{0}"" is not a valid setting for full scan special handling", specialHandling));
            }
        };

        public double? PrecursorFilter { get; private set; }
        public double? PrecursorRightFilter { get; private set; }
        private ReadOnlyCollection<IsolationWindow> _prespecifiedIsolationWindows;

        public string SpecialHandling { get; private set; }
        public int? WindowsPerScan { get; private set; }

        public IsolationScheme(string name, double? precursorFilter, double? precursorRightFilter = null)
            : base(name)
        {
            PrecursorFilter = precursorFilter;
            PrecursorRightFilter = precursorRightFilter;
            SpecialHandling = SpecialHandlingType.NONE;
            PrespecifiedIsolationWindows = new IsolationWindow[0];
            DoValidate();
        }

        public IsolationScheme(string name, IList<IsolationWindow> isolationWindows, string specialHandling = SpecialHandlingType.NONE, int? windowsPerScan = null)
            : base(name)
        {
            PrespecifiedIsolationWindows = isolationWindows;
            SpecialHandling = specialHandling;
            WindowsPerScan = windowsPerScan;
            DoValidate();
        }

        // Returns true if target is determined from results.
        public bool FromResults
        {
            get { return _prespecifiedIsolationWindows.Count == 0; }
        }


        public IList<IsolationWindow> PrespecifiedIsolationWindows
        {
            get { return _prespecifiedIsolationWindows; }
            private set { _prespecifiedIsolationWindows = MakeReadOnly(value); }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IsolationScheme()
        {
            PrespecifiedIsolationWindows = new IsolationWindow[0];
        }

        void IValidating.Validate()
        {
            DoValidate();
        }

        private void DoValidate()
        {
            SpecialHandlingType.Validate(SpecialHandling);

            if (PrecursorFilter != null)
            {
                if (PrespecifiedIsolationWindows.Count > 0)
                {
                    throw new InvalidDataException("Isolation scheme cannot have a filter and a prespecifed isolation window");
                }
                TransitionFullScan.ValidateRange(PrecursorFilter, TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER, TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                              "The precursor m/z filter must be between {0} and {1}");
                if (PrecursorRightFilter.HasValue)
                {
                    TransitionFullScan.ValidateRange(PrecursorRightFilter, TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER, TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                                  "The precursor m/z filter must be between {0} and {1}");
                }
                if (!Equals(SpecialHandling, SpecialHandlingType.NONE))
                {
                    throw new InvalidDataException("Special handling applies only to prespecified isolation windows");
                }
                if (WindowsPerScan.HasValue)
                {
                    throw new InvalidDataException("Isolation scheme can specify multiplexed windows only for prespecified isolation windows");
                }
            }

            else if (PrecursorRightFilter != null)
            {
                throw new InvalidDataException("Isolation scheme cannot have a right filter without a left filter");
            }

            else
            {
                if (PrespecifiedIsolationWindows.Count == 0)
                {
                    if (!Equals(SpecialHandling, SpecialHandlingType.MS_E))
                        throw new InvalidDataException("Isolation scheme must have a filter or a prespecifed isolation window");
                }
                else if (Equals(SpecialHandling, SpecialHandlingType.MS_E))
                {
                    throw new InvalidDataException("Isolation scheme for MSe cannot contain isolation windows");
                }

                if (Equals(SpecialHandling, SpecialHandlingType.MULTIPLEXED))
                {
                    if (!WindowsPerScan.HasValue || WindowsPerScan.Value < 1)
                    {
                        throw new InvalidDataException("Multiplexed windows require at least one window per scan");
                    }
                    if (PrespecifiedIsolationWindows.Count % WindowsPerScan.Value != 0)
                    {
                        throw new InvalidDataException("The number of prespecified isolation windows must be a multiple of the windows per scan in multiplexed sampling.");
                    }
                }
                else
                {
                    if (WindowsPerScan.HasValue)
                    {
                        throw new InvalidDataException("Windows per scan requires multiplexed isolation windows");
                    }
                }
            }
        }

        private enum ATTR
        {
            precursor_filter,
            precursor_left_filter,
            precursor_right_filter,
            special_handling,
            windows_per_scan
        }

        public static IsolationScheme Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IsolationScheme());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes.
            base.ReadXml(reader);

            PrecursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_filter);
            if (!PrecursorFilter.HasValue)
            {
                PrecursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_left_filter);
                PrecursorRightFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_right_filter);
            }

            SpecialHandling = reader.GetAttribute(ATTR.special_handling) ?? SpecialHandlingType.NONE;
            WindowsPerScan = reader.GetNullableIntAttribute(ATTR.windows_per_scan);

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                // Consume tag
                reader.ReadStartElement();

                var list = new List<IsolationWindow>();
                reader.ReadElements(list);
                PrespecifiedIsolationWindows = list;

                reader.ReadEndElement();
            }
            DoValidate();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes.
            base.WriteXml(writer);

            if (PrecursorRightFilter.HasValue)
            {
                writer.WriteAttributeNullable(ATTR.precursor_left_filter, PrecursorFilter);
                writer.WriteAttributeNullable(ATTR.precursor_right_filter, PrecursorRightFilter);
            }
            else
            {
                writer.WriteAttributeNullable(ATTR.precursor_filter, PrecursorFilter);
            }

            if (!Equals(SpecialHandling, SpecialHandlingType.NONE))
                writer.WriteAttribute(ATTR.special_handling, SpecialHandling);
            writer.WriteAttributeNullable(ATTR.windows_per_scan, WindowsPerScan);

            writer.WriteElements(_prespecifiedIsolationWindows);
        }

        #endregion

        #region object overrides

        public bool Equals(IsolationScheme other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   ArrayUtil.EqualsDeep(other._prespecifiedIsolationWindows, _prespecifiedIsolationWindows) &&
                   other.PrecursorFilter.Equals(PrecursorFilter) &&
                   other.PrecursorRightFilter.Equals(PrecursorRightFilter) &&
                   other.SpecialHandling.Equals(SpecialHandling) &&
                   other.WindowsPerScan.Equals(WindowsPerScan);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as IsolationScheme);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (_prespecifiedIsolationWindows != null ? _prespecifiedIsolationWindows.GetHashCodeDeep() : 0);
                result = (result*397) ^ (PrecursorFilter.HasValue ? PrecursorFilter.Value.GetHashCode() : 0);
                result = (result*397) ^ (PrecursorRightFilter.HasValue ? PrecursorRightFilter.Value.GetHashCode() : 0);
                result = (result*397) ^ (SpecialHandling.GetHashCode());
                result = (result*397) ^ (WindowsPerScan.HasValue ? WindowsPerScan.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}
