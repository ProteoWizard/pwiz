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

using System.Collections.Generic;

namespace pwiz.Skyline.Model.DocSettings.Extensions
{
    /// <summary>
    /// Helper functions for working with <see cref="SrmSettings"/> in a test
    /// environment.
    /// </summary>
    public static class SettingsExtensions
    {
        public delegate TObj ChangeFunc<TObj>(TObj value);

        public static SrmSettings ChangePeptideSettings(this SrmSettings settings,
            ChangeFunc<PeptideSettings> change)
        {
            return settings.ChangePeptideSettings(change(settings.PeptideSettings));
        }

        public static SrmSettings ChangePeptidePrediction(this SrmSettings settings,
            ChangeFunc<PeptidePrediction> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangePrediction(change(setP.Prediction)));
        }

        public static SrmSettings ChangePeptideFilter(this SrmSettings settings,
            ChangeFunc<PeptideFilter> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeFilter(change(setP.Filter)));
        }

        public static SrmSettings ChangePeptideLibraries(this SrmSettings settings,
            ChangeFunc<PeptideLibraries> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeLibraries(change(setP.Libraries)));
        }

        public static SrmSettings ChangePeptideModifications(this SrmSettings settings,
            ChangeFunc<PeptideModifications> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeModifications(change(setP.Modifications)));
        }

        public static SrmSettings ChangePeptideIntegration(this SrmSettings settings,
            ChangeFunc<PeptideIntegration> change)
        {
            return settings.ChangePeptideSettings(setP => setP.ChangeIntegration(change(setP.Integration)));
        }

        public static SrmSettings ChangeTransitionSettings(this SrmSettings settings,
            ChangeFunc<TransitionSettings> change)
        {
            return settings.ChangeTransitionSettings(change(settings.TransitionSettings));
        }

        public static SrmSettings ChangeTransitionPrediction(this SrmSettings settings,
            ChangeFunc<TransitionPrediction> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangePrediction(change(setT.Prediction)));
        }

        public static SrmSettings ChangeTransitionFilter(this SrmSettings settings,
            ChangeFunc<TransitionFilter> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeFilter(change(setT.Filter)));
        }

        public static SrmSettings ChangeTransitionLibraries(this SrmSettings settings,
            ChangeFunc<TransitionLibraries> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeLibraries(change(setT.Libraries)));
        }

        public static SrmSettings ChangeTransitionIntegration(this SrmSettings settings,
            ChangeFunc<TransitionIntegration> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeIntegration(change(setT.Integration)));
        }

        public static SrmSettings ChangeTransitionInstrument(this SrmSettings settings,
            ChangeFunc<TransitionInstrument> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeInstrument(change(setT.Instrument)));
        }

        public static SrmSettings ChangeTransitionFullScan(this SrmSettings settings,
            ChangeFunc<TransitionFullScan> change)
        {
            return settings.ChangeTransitionSettings(setT => setT.ChangeFullScan(change(setT.FullScan)));
        }

        public static SrmSettings ChangeDataSettings(this SrmSettings settings,
            ChangeFunc<DataSettings> change)
        {
            return settings.ChangeDataSettings(change(settings.DataSettings));
        }

        public static SrmSettings ChangeAnnotationDefs(this SrmSettings settings,
            ChangeFunc<IList<AnnotationDef>> change)
        {
            return settings.ChangeDataSettings(setD => setD.ChangeAnnotationDefs(change(setD.AnnotationDefs)));
        }
    }
}
