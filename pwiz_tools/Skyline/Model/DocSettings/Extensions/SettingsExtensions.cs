/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.DocSettings.Extensions
{
    /// <summary>
    /// Helper functions for working with <see cref="SrmSettings"/> in a test
    /// environment.
    /// </summary>
    public static class SettingsExtensions
    {
        public static SrmSettings ChangePeptideSettings(this SrmSettings settings,
            Func<PeptideSettings, PeptideSettings> change)
        {
            return settings.ChangePeptideSettings(change(settings.PeptideSettings));
        }

        public static SrmSettings ChangePeptidePrediction(this SrmSettings settings,
            Func<PeptidePrediction, PeptidePrediction> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangePrediction(change(setP.Prediction)));
        }

        public static SrmSettings ChangePeptideFilter(this SrmSettings settings,
            Func<PeptideFilter, PeptideFilter> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeFilter(change(setP.Filter)));
        }

        public static SrmSettings ChangePeptideLibraries(this SrmSettings settings,
            Func<PeptideLibraries, PeptideLibraries> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeLibraries(change(setP.Libraries)));
        }

        public static SrmSettings ChangePeptideModifications(this SrmSettings settings,
            Func<PeptideModifications, PeptideModifications> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeModifications(change(setP.Modifications)));
        }

        public static SrmSettings ChangeTransitionSettings(this SrmSettings settings,
            Func<TransitionSettings, TransitionSettings> change)
        {
            return settings.ChangeTransitionSettings(change(settings.TransitionSettings));
        }

        public static SrmSettings ChangeTransitionPrediction(this SrmSettings settings,
            Func<TransitionPrediction, TransitionPrediction> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangePrediction(change(setT.Prediction)));
        }

        public static SrmSettings ChangeTransitionFilter(this SrmSettings settings,
            Func<TransitionFilter, TransitionFilter> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeFilter(change(setT.Filter)));
        }

        public static SrmSettings ChangeTransitionLibraries(this SrmSettings settings,
            Func<TransitionLibraries, TransitionLibraries> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeLibraries(change(setT.Libraries)));
        }

        public static SrmSettings ChangeTransitionInstrument(this SrmSettings settings,
            Func<TransitionInstrument, TransitionInstrument> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeInstrument(change(setT.Instrument)));
        }
    }
}
