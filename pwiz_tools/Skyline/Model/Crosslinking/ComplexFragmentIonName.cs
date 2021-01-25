/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using NHibernate.Proxy;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents the parts of a <see cref="LegacyComplexFragmentIon"/> separated from the actual Transition and TransitionGroup objects.
    /// </summary>
    public class LegacyComplexFragmentIonName
    {
        public LegacyComplexFragmentIonName(ModificationSite site, FragmentIonType ionType)
        {
            Site = site;
            IonType = ionType;
            Children = new List<LegacyComplexFragmentIonName>();
        }

        public ModificationSite Site { get; }
        public FragmentIonType IonType { get; }
        public List<LegacyComplexFragmentIonName> Children { get; private set; }

        public void FillInIons(IList<ImmutableList<ModificationSite>> sitePaths, FragmentIonType[] array)
        {
            var queue = new List<Tuple<ImmutableList<ModificationSite>, LegacyComplexFragmentIonName>>
                {Tuple.Create(ImmutableList<ModificationSite>.EMPTY, this)};
            while (queue.Count > 0)
            {
                var tuple = queue[0];
                queue.RemoveAt(0);
                var fragmentIon = tuple.Item2;
                var sitePath = ImmutableList.ValueOf(tuple.Item1.Append(fragmentIon.Site));
                int index = sitePaths.IndexOf(sitePath);
                string sitePathString = string.Join(@"/", sitePath);
                if (index < 0)
                {
                    throw new InvalidDataException(string.Format(@"Unable to find site {0}",
                        sitePathString));
                }

                if (!array[index].IsEmpty)
                {
                    throw new InvalidDataException(string.Format(@"Duplicate ion at  {0}", sitePathString));
                }

                array[index] = fragmentIon.IonType;
                queue.AddRange(fragmentIon.Children.Select(child => Tuple.Create(sitePath, child)));
            }
        }

        public static IonChain ToIonChain(IList<ImmutableList<ModificationSite>> sitePaths, IEnumerable<LegacyComplexFragmentIonName> ionNames)
        {
            var array = new FragmentIonType[sitePaths.Count];
            foreach (var ionName in ionNames)
            {
                ionName.FillInIons(sitePaths, array);
            }
            return IonChain.FromIons(array);
        }

        public static LegacyComplexFragmentIonName FromLinkedIonProto(SkylineDocumentProto.Types.LinkedIon linkedIon)
        {
            var result = new LegacyComplexFragmentIonName(new ModificationSite(linkedIon.ModificationIndex, linkedIon.ModificationName), 
                new FragmentIonType(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal));
            result.Children.AddRange(linkedIon.Children.Select(FromLinkedIonProto));
            return result;
        }
    }
}
