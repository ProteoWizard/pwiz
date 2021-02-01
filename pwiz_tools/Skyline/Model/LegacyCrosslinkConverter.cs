/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class LegacyCrosslinkConverter
    {
        public LegacyCrosslinkConverter(SrmSettings settings, ExplicitMods explicitMods)
        {
            SrmSettings = settings;
            ExplicitMods = explicitMods;
            PeptideStructure = ExplicitMods.GetPeptideStructure();
        }

        public SrmSettings SrmSettings { get; }
        public ExplicitMods ExplicitMods { get; }
        public PeptideStructure PeptideStructure { get; }

        public ExplicitMods ConvertToLegacyFormat(Dictionary<int, ImmutableList<ModificationSite>> sitePathMap)
        {
            if (ExplicitMods.CrosslinkStructure.Crosslinks.Any(link => link.Sites.Count > 2))
            {
                throw new NotSupportedException();
            }
            var newCrosslinkModifications = new List<ExplicitMod>();
            var remainingCrosslinks =
                ExplicitMods.CrosslinkStructure.Crosslinks.Where(crosslink =>
                    !crosslink.Sites.PeptideIndexes.Contains(0)).ToList();
            sitePathMap.Add(0, ImmutableList<ModificationSite>.EMPTY);
            foreach (var crosslink in ExplicitMods.CrosslinkStructure.Crosslinks.Where(crosslink=>crosslink.Sites.PeptideIndexes.Contains(0)))
            {
                if (crosslink.Sites.Count > 2)
                {
                    throw new NotSupportedException();
                }

                var linkedPeptide = MakeLinkedPeptide(crosslink, 0, remainingCrosslinks, sitePathMap);
                newCrosslinkModifications.Add(new ExplicitMod(crosslink.Sites.Sites[0].AaIndex, crosslink.Crosslinker).ChangeLinkedPeptide(linkedPeptide));
            }

            return AddStaticModifications(ExplicitMods.Peptide, ExplicitMods, newCrosslinkModifications)
                .ChangeCrosslinkStructure(null);
        }

        public LegacyLinkedPeptide MakeLinkedPeptide(Crosslink crosslink, int parentPeptideIndex,
            List<Crosslink> remainingCrosslinks, Dictionary<int, ImmutableList<ModificationSite>> sitePathMap)
        {
            var peptideIndexes = crosslink.Sites.PeptideIndexes.ToList();
            Assume.IsTrue(peptideIndexes.Contains(parentPeptideIndex));
            if (peptideIndexes.Count == 1)
            {
                return new LegacyLinkedPeptide(null, crosslink.Sites.Sites[1].AaIndex, null);
            }

            var parentSitePath = sitePathMap[parentPeptideIndex];
            var parentSite = crosslink.Sites.First(site => site.PeptideIndex == parentPeptideIndex);
            var linkedSite = crosslink.Sites.First(site => site.PeptideIndex != parentPeptideIndex);
            var thisSitePath = ImmutableList.ValueOf(parentSitePath.Append(
                new ModificationSite(parentSite.AaIndex,
                    crosslink.Crosslinker.Name)));
            sitePathMap.Add(linkedSite.PeptideIndex, thisSitePath);
                

            var queue = new List<Crosslink>();
            for (int i = remainingCrosslinks.Count - 1; i >= 0; i--)
            {
                var nextCrosslink = remainingCrosslinks[i];
                if (nextCrosslink.Sites.PeptideIndexes.Contains(linkedSite.PeptideIndex))
                {
                    queue.Add(nextCrosslink);
                }
                else
                {
                    remainingCrosslinks.RemoveAt(i);
                }
            }

            var peptide = PeptideStructure.Peptides[linkedSite.PeptideIndex];
            var newCrosslinkModifications = new List<ExplicitMod>();
            foreach (var nextCrosslink in queue)
            {
                var thisSite = nextCrosslink.Sites.First(site => site.PeptideIndex == linkedSite.PeptideIndex);
                var otherSite = nextCrosslink.Sites.First(site => site.PeptideIndex != linkedSite.PeptideIndex);
                if (sitePathMap.ContainsKey(otherSite.PeptideIndex))
                {
                    throw new NotSupportedException(@"Crosslinks are not in tree structure");
                }
                var linkedPeptide = MakeLinkedPeptide(nextCrosslink, otherSite.PeptideIndex, remainingCrosslinks,
                    sitePathMap);
                newCrosslinkModifications.Add(new ExplicitMod(thisSite.AaIndex, nextCrosslink.Crosslinker).ChangeLinkedPeptide(linkedPeptide));
            }


            var explicitMods = AddStaticModifications(peptide, PeptideStructure.ExplicitModList[parentPeptideIndex],
                newCrosslinkModifications);
            return new LegacyLinkedPeptide(peptide,
                crosslink.Sites.First(site => site.PeptideIndex != parentPeptideIndex).AaIndex, explicitMods);
        }

        private ExplicitMods AddStaticModifications(Peptide peptide, ExplicitMods explicitMods,
            IEnumerable<ExplicitMod> newModifications)
        {
            var newStaticModifications = new List<ExplicitMod>();
            if (explicitMods?.StaticModifications != null)
            {
                newStaticModifications.AddRange(explicitMods.StaticModifications);
            }
            newStaticModifications.AddRange(newModifications);
            return new ExplicitMods(peptide, newStaticModifications, explicitMods?.GetHeavyModifications());
        }
    }
}
