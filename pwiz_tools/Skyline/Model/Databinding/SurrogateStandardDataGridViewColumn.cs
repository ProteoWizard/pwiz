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
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// A combo box column with a list of the surrogate standard molecules in the document.
    /// </summary>
    public class SurrogateStandardDataGridViewColumn : BoundComboBoxColumn
    {
        protected override object[] GetDropdownItems()
        {
            var document = SkylineDataSchema.Document;
            var items = new List<object> { string.Empty };
            items.AddRange(document.Settings.GetPeptideStandards(StandardType.SURROGATE_STANDARD).Select(peptide=>peptide.PeptideDocNode.ModifiedTarget.InvariantName).Distinct());
            return items.ToArray();
        }
    }
}
