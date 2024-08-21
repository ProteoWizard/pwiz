/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Caches lists of losses for each ion type and cleavage offset for a particular peptide sequence.
    /// </summary>
    public class TransitionLossMap
    {
        private readonly Dictionary<(IonType ionType, int cleavageOffset), ImmutableList<TransitionLosses>> _lossLists =
            new Dictionary<(IonType ionType, int cleavageOffset), ImmutableList<TransitionLosses>>();

        public static TransitionLossMap ForTarget(Target target, PeptideModifications peptideModifications,
            ExplicitMods explicitMods, MassType massType)
        {
            return new TransitionLossMap(TransitionGroup.CalcPotentialLosses(
                target, peptideModifications, explicitMods, massType), massType);
        }
        public TransitionLossMap(IList<IList<ExplicitLoss>> potentialLosses, MassType massType)
        {
            PotentialLosses = potentialLosses ?? Array.Empty<IList<ExplicitLoss>>();
            MassType = massType;
        }

        public IList<IList<ExplicitLoss>> PotentialLosses { get; }
        public MassType MassType { get; }

        public IEnumerable<TransitionLosses> CalcTransitionLosses(IonType ionType, int cleavageOffset)
        {
            var key = (ionType, cleavageOffset);
            if (!_lossLists.TryGetValue(key, out var list))
            {
                list = ImmutableList.ValueOf(
                    TransitionGroup.CalcTransitionLosses(ionType, cleavageOffset, MassType, PotentialLosses));
                _lossLists[key] = list;
            }

            return list;
        }
    }
}
