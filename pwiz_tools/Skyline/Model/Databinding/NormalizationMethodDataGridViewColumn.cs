/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.GroupComparison;

namespace pwiz.Skyline.Model.Databinding
{
    public class NormalizationMethodDataGridViewColumn : BoundComboBoxColumn
    {
        public NormalizationMethodDataGridViewColumn()
        {
            DisplayMember = @"Item1";
            ValueMember = @"Item2";
        }

        protected override object[] GetDropdownItems()
        {
            var document = SkylineDataSchema.Document;
            List<Tuple<string, NormalizationMethod>> normalizationMethods
                = new List<Tuple<string, NormalizationMethod>>
                {new Tuple<string, NormalizationMethod>(string.Empty, null)};

            normalizationMethods.AddRange(NormalizationMethod.ListNormalizationMethods(document).Select(ToDropdownItem));
            normalizationMethods.AddRange(NormalizationMethod.RatioToSurrogate.ListSurrogateNormalizationMethods(document).Select(ToDropdownItem));
            // If there are any molecules that have a normalization method that is not in the list, add it to the end.
            var normalizationMethodValues = new HashSet<NormalizationMethod>(normalizationMethods.Select(tuple => tuple.Item2)
                .Where(normalizationMethod=>null != normalizationMethod));
            foreach (var molecule in document.Molecules)
            {
                if (molecule.NormalizationMethod != null &&
                    normalizationMethodValues.Add(molecule.NormalizationMethod))
                {
                    normalizationMethods.Add(ToDropdownItem(molecule.NormalizationMethod));
                }
            }

            return normalizationMethods.Cast<object>().ToArray();
        }

        private static Tuple<string, NormalizationMethod> ToDropdownItem(NormalizationMethod normalizationMethod)
        {
            return Tuple.Create(normalizationMethod.ToString(), normalizationMethod);
        }
    }
}
