/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model.AuditLog;
using System;
using System.Collections.Concurrent;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

//
// Helper class for explaining Skyline's filtering actions to users
// 

namespace pwiz.Skyline.Model.DocSettings
{
    public class FilterReason
    {
        private const string SEPARATOR = @" | ";

        public static readonly string TRANSITION_SETTINGS_FILTER_PEPTIDE_ION_TYPES =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilterAndLibrariesSettings_PeptideIonTypes;

        public static readonly string TRANSITION_SETTINGS_FILTER_SMALL_MOL_ION_TYPES =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilterAndLibrariesSettings_SmallMoleculeIonTypes;

        public static readonly string TRANSITION_SETTINGS_INSTRUMENT_MZ_RANGE =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Instrument + SEPARATOR +
            PropertyNames.TransitionInstrument_MinMz + @"," + PropertyNames.TransitionInstrument_MaxMz;

        public static readonly string TRANSITION_SETTINGS_INSTRUMENT_PRECURSOR_WINDOW =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilter_PrecursorMzWindow;

        public static readonly string TRANSITION_SETTINGS_FULL_SCAN =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_FullScan;

        public static readonly string TRANSITION_SETTINGS_FILTER_PEPTIDE_PRECURSOR_CHARGES =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilterAndLibrariesSettings_PeptidePrecursorCharges;

        public static readonly string TRANSITION_SETTINGS_FILTER_PEPTIDE_FRAGMENT_CHARGES =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilterAndLibrariesSettings_PeptideIonCharges;

        public static readonly string TRANSITION_SETTINGS_FILTER_SMALL_MOL_PRECURSOR_ADDUCTS =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilter_SmallMoleculePrecursorAdductsString;

        public static readonly string TRANSITION_SETTINGS_FILTER_SMALL_MOL_FRAGMENT_ADDUCTS =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Filter + SEPARATOR +
            PropertyNames.TransitionFilter_SmallMoleculeFragmentAdductsString;

        public static readonly string TRANSITION_SETTINGS_FULL_SCAN_PRECURSOR_ISOTOPES =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_FullScan + SEPARATOR +
            PropertyNames.TransitionFullScan_PrecursorIsotopes;

        public static readonly string TRANSITION_SETTINGS_FULL_SCAN_ISOLATION_SCHEME =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_FullScan + SEPARATOR +
            PropertyNames.TransitionFullScan_IsolationScheme;

        public static readonly string PEPTIDE_SETTINGS_LIBRARY_PEPTIDE_COUNT =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Libraries + SEPARATOR +
            PropertyNames.PeptideLibraries_PeptideCount;

        public static readonly string TRANSITION_SETTINGS_LIBRARY_MIN_ION_COUNT =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Libraries + SEPARATOR +
            PropertyNames.TransitionLibraries_MinIonCount;

        public static readonly string TRANSITION_SETTINGS_LIBRARY_PICK =
            PropertyNames.SrmSettings_TransitionSettings + SEPARATOR +
            PropertyNames.TransitionSettings_Libraries + SEPARATOR +
            PropertyNames.TransitionLibraries_Pick;

        public static readonly string PEPTIDE_SETTINGS_FILTER_MIN_PEPTIDE_LENGTH =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Filter + SEPARATOR +
            PropertyNames.PeptideFilter_MinPeptideLength;

        public static readonly string PEPTIDE_SETTINGS_FILTER_MAX_PEPTIDE_LENGTH =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Filter + SEPARATOR +
            PropertyNames.PeptideFilter_MaxPeptideLength;

        public static readonly string PEPTIDE_SETTINGS_FILTER_EXCLUDE_NTERM_AAS =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Filter + SEPARATOR +
            PropertyNames.PeptideFilter_ExcludeNTermAAs;

        public static readonly string PEPTIDE_SETTINGS_FILTER_EXCLUSIONS =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Filter + SEPARATOR +
            PropertyNames.PeptideFilter_Exclusions;

        public static readonly string PEPTIDE_SETTINGS_FILTER_PEPTIDE_UNIQUENESS =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Filter + SEPARATOR +
            PropertyNames.PeptideFilter_PeptideUniqueness;

        public static readonly string PEPTIDE_SETTINGS_MODIFICATIONS =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_Modifications;

        public static readonly string PEPTIDE_SETTINGS_DIGEST_MAX_MISSED_CLEAVAGES =
            PropertyNames.SrmSettings_PeptideSettings + SEPARATOR +
            PropertyNames.PeptideSettings_DigestSettings + SEPARATOR +
            PropertyNames.DigestSettings_MaxMissedCleavages;
    }

    public class FilterReasonsSet 
    {
        private readonly ConcurrentDictionary<string, string> _reasons = new ConcurrentDictionary<string, string>(); // N.B. I'd use ConcurrentHashSet if it existed

        public FilterReasonsSet()
        {
        }

        public FilterReasonsSet(string reasonText)
        {
            _reasons.TryAdd(reasonText, reasonText);
        }

        public void AddReason(string reasonText)
        {
            if (!string.IsNullOrEmpty(reasonText))
            {
                _reasons.TryAdd(reasonText, reasonText);
            }
        }

        public void RemoveReason(string reasonText)
        {
            if (!string.IsNullOrEmpty(reasonText))
            {
                _reasons.TryRemove(reasonText, out _);
            }
        }

        public void Clear()
        {
            _reasons.Clear();
        }

        public void UnionWith(FilterReasonsSet other)
        {
            if (other == null)
                return;
            foreach (var item in other._reasons)
            {
                _reasons.TryAdd(item.Key, item.Value);
            }
        }

        public int Count => _reasons?.Count ?? 0;

        public string DisplayString()
        {
            return _reasons?.Count == 0 ?
                string.Empty :
                Environment.NewLine + Environment.NewLine +
                string.Format(Resources.SkylineWindow_HandleSmallMoleculeAutomanage_Check_these_settings_for_details___0_,
                    Environment.NewLine + CommonTextUtil.LineSeparate(_reasons?.Keys));
        }


        public override string ToString()
        {
            return _reasons?.Count == 0 ? string.Empty : CommonTextUtil.LineSeparate(_reasons?.Keys);
        }
    }
}
