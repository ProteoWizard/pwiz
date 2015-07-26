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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
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
            public static string NONE { get { return "None"; } }    // Not L10N : Used only in XML and in memory
            public const string MULTIPLEXED = "Multiplexed";  // Not L10N : Used only in XML and in memory
            public const string MS_E = "MSe"; // Not L10N : This is a Waters trademark, and probably not localizable
            public const string ALL_IONS = "All Ions";    // Not L10N?
            public const string OVERLAP = "Overlap"; // Not L10N?
            public const string OVERLAP_MULTIPLEXED = "Overlap Multiplexed"; // Not L10N?

            public static void Validate(string specialHandling)
            {
                if (!Equals(specialHandling, NONE) &&
                    !Equals(specialHandling, MULTIPLEXED) &&
                    !Equals(specialHandling, MS_E) &&
                    !Equals(specialHandling, ALL_IONS) &&
                    !Equals(specialHandling, OVERLAP) &&
                    !Equals(specialHandling, OVERLAP_MULTIPLEXED))
                {
                    throw new InvalidDataException(string.Format(
                        Resources.SpecialHandlingType_Validate___0___is_not_a_valid_setting_for_full_scan_special_handling, specialHandling));
                }                    
            }

            public static bool IsAllIons(string specialHandling)
            {
                return Equals(specialHandling, MS_E) || Equals(specialHandling, ALL_IONS);
            }
        };

        public double? PrecursorFilter { get; private set; }
        public double? PrecursorRightFilter { get; private set; }
        private ImmutableList<IsolationWindow> _prespecifiedIsolationWindows;

        /// <summary>
        /// Minimized set of disjoint m/z ranges covered to make checking if something
        /// is within the m/z range(s) covered by the isolation scheme faster.
        /// </summary>
        private ImmutableList<IsolationWindow> _prespecifiedDisjointWindows;

        public string SpecialHandling { get; private set; }
        public int? WindowsPerScan { get; private set; }

        public IsolationScheme(string name, string specialHandling, double? precursorFilter, double? precursorRightFilter = null)
            : base(name)
        {
            PrecursorFilter = precursorFilter;
            PrecursorRightFilter = precursorRightFilter;
            SpecialHandling = specialHandling;
            PrespecifiedIsolationWindows = ImmutableList<IsolationWindow>.EMPTY;
            DoValidate();
        }

        public IsolationScheme(string name, double? precursorFilter, double? precursorRightFilter = null)
            : this(name,SpecialHandlingType.NONE,precursorFilter, precursorRightFilter)
        {
        }

        public IsolationScheme(string name, IList<IsolationWindow> isolationWindows)
            : this(name, isolationWindows, SpecialHandlingType.NONE)
        {            
        }

        public IsolationScheme(string name, IList<IsolationWindow> isolationWindows, string specialHandling, int? windowsPerScan = null)
            : base(SpecialHandlingType.IsAllIons(specialHandling) ? SpecialHandlingType.ALL_IONS : name)
        {
            PrespecifiedIsolationWindows = isolationWindows;
            SpecialHandling = specialHandling;
            WindowsPerScan = windowsPerScan;
            DoValidate();
        }

        /// <summary>
        /// Returns true if the m/z value fall into at least one window
        /// </summary>
        public bool IsInRangeMz(double mz)
        {
            return FromResults || _prespecifiedDisjointWindows.Any(window => window.Contains(mz));
        }

        /// <summary>
        /// Returns true if both m/z values fall into the same window in at least one configuration
        /// TODO : should we return if FromResults is true (e.g. no pre-determined windows?)
        /// </summary>
        public bool MayFallIntoSameWindow(double mz1, double mz2)
        {
            return FromResults || PrespecifiedIsolationWindows.Any(window => window.Contains(mz1) && window.Contains(mz2));
        }

        /// <summary>
        /// Returns true if target is determined from results.
        /// </summary>
        public bool FromResults
        {
            get { return _prespecifiedIsolationWindows.Count == 0; }
        }

        public bool IsAllIons
        {
            get { return SpecialHandlingType.IsAllIons(SpecialHandling); }
        }

        public IList<IsolationWindow> PrespecifiedIsolationWindows
        {
            get { return _prespecifiedIsolationWindows; }
            private set
            {
                _prespecifiedIsolationWindows = MakeReadOnly(value);
                _prespecifiedDisjointWindows = MakeReadOnly(GetDisjointRanges(value));
            }
        }

        private IEnumerable<IsolationWindow> GetDisjointRanges(IList<IsolationWindow> isolationWindows)
        {
            var listIsolationWindows = new List<IsolationWindow>(isolationWindows);
            listIsolationWindows.Sort((w1, w2) => Comparer<double>.Default.Compare(w1.Start, w2.Start));
            int iDisjoint = 0;
            for (int i = 1; i < listIsolationWindows.Count; i++)
            {
                var disjointWindow = listIsolationWindows[iDisjoint];
                var nextWindow = listIsolationWindows[i];
                if (disjointWindow.End >= nextWindow.Start)
                {
                    listIsolationWindows[iDisjoint] = new IsolationWindow(disjointWindow.Start, nextWindow.End);
                }
                else
                {
                    iDisjoint++;
                    listIsolationWindows[iDisjoint] = nextWindow;
                }
            }
            return listIsolationWindows.Take(iDisjoint + 1);
        }

        public IsolationWindow GetIsolationWindow(double targetMz, double matchTolerance)
        {
            IsolationWindow isolationWindow = null;

            if (!FromResults)
            {
                // Match pre-specified targets.
                if (PrespecifiedIsolationWindows[0].Target.HasValue)
                {
                    foreach (var window in PrespecifiedIsolationWindows)
                    {
                        if (!window.TargetMatches(targetMz, matchTolerance)) continue;
                        if (isolationWindow != null)
                        {
                            throw new InvalidDataException(
                                string.Format(Resources.SpectrumFilter_FindFilterPairs_Two_isolation_windows_contain_targets_which_match_the_isolation_target__0__,
                                    targetMz));
                        }
                        isolationWindow = window;
                    }
                }
                // Find containing window.
                else
                {
                    double? bestDeltaMz = null;
                    // find the window with center closest to the target m/z
                    foreach (var window in PrespecifiedIsolationWindows)
                    {
                        if (!window.Contains(targetMz)) continue;
                        var winCenter = (window.IsolationStart + window.IsolationEnd) / 2.0;
                        var deltaMz = Math.Abs(winCenter - targetMz);
                        if (isolationWindow == null || deltaMz < bestDeltaMz)
                        {
                            isolationWindow = window;
                            bestDeltaMz = deltaMz;
                        }
                    }
                }
            }
            return isolationWindow;
        }

        public IEnumerable<IsolationWindow> GetIsolationWindowsContaining(double mz)
        {
            if (!FromResults)
            {
                foreach (var window in PrespecifiedIsolationWindows.Where(w => w.Contains(mz)))
                    yield return window;
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IsolationScheme()
        {
            PrespecifiedIsolationWindows = ImmutableList<IsolationWindow>.EMPTY;
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
                    throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Isolation_scheme_cannot_have_a_filter_and_a_prespecifed_isolation_window);
                }
                TransitionFullScan.ValidateRange(PrecursorFilter,
                                                 TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER,
                                                 TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                                                 Resources.IsolationScheme_DoValidate_The_precursor_m_z_filter_must_be_between__0__and__1_);
                if (PrecursorRightFilter.HasValue)
                {
                    TransitionFullScan.ValidateRange(PrecursorRightFilter,
                                                     TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER,
                                                     TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                                                     Resources.IsolationScheme_DoValidate_The_precursor_m_z_filter_must_be_between__0__and__1_);
                }
                if (WindowsPerScan.HasValue)
                {
                    throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Isolation_scheme_can_specify_multiplexed_windows_only_for_prespecified_isolation_windows);
                }
            }

            else if (PrecursorRightFilter != null)
            {
                throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Isolation_scheme_cannot_have_a_right_filter_without_a_left_filter);
            }

            else
            {
                if (PrespecifiedIsolationWindows.Count == 0)
                {
                    if (!IsAllIons)
                    {
                        throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Isolation_scheme_must_have_a_filter_or_a_prespecifed_isolation_window);
                    }
                }
                else if (IsAllIons)
                {
                    throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Isolation_scheme_for_all_ions_cannot_contain_isolation_windows);
                }

                if (Equals(SpecialHandling, SpecialHandlingType.MULTIPLEXED))
                {
                    if (!WindowsPerScan.HasValue || WindowsPerScan.Value < 1)
                    {
                        throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Multiplexed_windows_require_at_least_one_window_per_scan);
                    }
                    if (PrespecifiedIsolationWindows.Count % WindowsPerScan.Value != 0)
                    {
                        throw new InvalidDataException(Resources.IsolationScheme_DoValidate_The_number_of_prespecified_isolation_windows_must_be_a_multiple_of_the_windows_per_scan_in_multiplexed_sampling);
                    }
                }
                else
                {
                    if (WindowsPerScan.HasValue)
                    {
                        throw new InvalidDataException(Resources.IsolationScheme_DoValidate_Windows_per_scan_requires_multiplexed_isolation_windows);
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
            SpecialHandling = reader.GetAttribute(ATTR.special_handling) ?? SpecialHandlingType.NONE;
            // Backward compatibility with v1.3: force all ions name to all ions (may be MSe)
            if (!SpecialHandlingType.IsAllIons(SpecialHandling))
                base.ReadXml(reader);
            else
            {
                ReadXmlName(SpecialHandlingType.ALL_IONS);
                SpecialHandling = SpecialHandlingType.ALL_IONS;
            }

            PrecursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_filter);
            if (!PrecursorFilter.HasValue)
            {
                PrecursorFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_left_filter);
                PrecursorRightFilter = reader.GetNullableDoubleAttribute(ATTR.precursor_right_filter);
            }

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
