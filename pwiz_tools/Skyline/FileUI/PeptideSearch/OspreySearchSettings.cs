/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.FileUI.PeptideSearch
{
    /// <summary>
    /// The settings an Osprey search ran with, for the document's audit log. They are recorded
    /// inside the peptide search's entry
    /// (<see cref="PeptideSearchWizardDlg.ImportPeptideSearchSettings"/>), as a DDA search
    /// engine's settings are. Each tracked property is named by its PropertyNames resource.
    /// </summary>
    public class OspreySearchSettings
    {
        public OspreySearchSettings(OspreyConfig config)
        {
            Resolution = config.Resolution;
            FragmentTolerance = config.FragmentTolerance;
            FragmentUnit = config.FragmentUnit;
            RunFdr = config.RunFdr;
            ExperimentFdr = config.ExperimentFdr;
            ProteinFdr = config.ProteinFdr;
            Threads = config.Threads;
            NonDefaultAdditionalSettings = GetNonDefaultAdditionalSettings(config);
        }

        [Track]
        public string Resolution { get; }
        [Track]
        public double FragmentTolerance { get; }
        [Track]
        public string FragmentUnit { get; }
        [Track]
        public double RunFdr { get; }
        [Track]
        public double ExperimentFdr { get; }
        [Track]
        public double ProteinFdr { get; }
        [Track]
        public int Threads { get; }

        /// <summary>
        /// The additional settings the run changed from their defaults, among those in the run's
        /// scopes (the only ones its command line carried); null when there are none.
        /// </summary>
        [Track(defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public IEnumerable<AbstractDdaSearchEngine.Setting> NonDefaultAdditionalSettings { get; }

        private static IList<AbstractDdaSearchEngine.Setting> GetNonDefaultAdditionalSettings(OspreyConfig config)
        {
            var additionalSettings = config.AdditionalSettings;
            if (additionalSettings == null)
                return null;
            var scopes = OspreyAdditionalSettings.ScopesFor(config);
            var changedInScope = additionalSettings.ChangedSettings
                .Where(s => scopes.Contains(additionalSettings.GetProperty(s).Scope)).ToList();
            return changedInScope.Count > 0 ? changedInScope : null;
        }
    }
}
