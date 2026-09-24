/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// A single dropdown entry that represents one of the four supported
    /// (peptide method, protein method) summarization combinations.
    /// </summary>
    public class SummarizationMethodOption
    {
        public static readonly SummarizationMethodOption SUM_SUM = new SummarizationMethodOption(
            SummarizationMethod.AVERAGING, SummarizationMethod.AVERAGING,
            () => SettingsUIResources.SummarizationMethodOption_Sum_transitions__Sum_peptides);
        public static readonly SummarizationMethodOption SUM_POLISH = new SummarizationMethodOption(
            SummarizationMethod.AVERAGING, SummarizationMethod.MEDIANPOLISH,
            () => SettingsUIResources.SummarizationMethodOption_Sum_transitions__Median_polish_peptides);
        public static readonly SummarizationMethodOption POLISH_SUM = new SummarizationMethodOption(
            SummarizationMethod.MEDIANPOLISH, SummarizationMethod.AVERAGING,
            () => SettingsUIResources.SummarizationMethodOption_Median_polish_transitions__Sum_peptides);
        public static readonly SummarizationMethodOption POLISH_POLISH = new SummarizationMethodOption(
            SummarizationMethod.MEDIANPOLISH, SummarizationMethod.MEDIANPOLISH,
            () => SettingsUIResources.SummarizationMethodOption_Median_polish_transitions__Median_polish_peptides);

        public static readonly IList<SummarizationMethodOption> ALL = ImmutableList.ValueOf(new[]
        {
            SUM_SUM, SUM_POLISH, POLISH_SUM, POLISH_POLISH
        });

        public static SummarizationMethodOption DEFAULT => SUM_SUM;

        private readonly Func<string> _getLabelFunc;

        private SummarizationMethodOption(SummarizationMethod peptideMethod, SummarizationMethod proteinMethod,
            Func<string> getLabelFunc)
        {
            PeptideMethod = peptideMethod;
            ProteinMethod = proteinMethod;
            _getLabelFunc = getLabelFunc;
        }

        public SummarizationMethod PeptideMethod { get; }
        public SummarizationMethod ProteinMethod { get; }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public static SummarizationMethodOption FindMatch(SummarizationMethod peptideMethod,
            SummarizationMethod proteinMethod)
        {
            return ALL.FirstOrDefault(opt =>
                       Equals(opt.PeptideMethod, peptideMethod) && Equals(opt.ProteinMethod, proteinMethod))
                   ?? DEFAULT;
        }
    }
}
