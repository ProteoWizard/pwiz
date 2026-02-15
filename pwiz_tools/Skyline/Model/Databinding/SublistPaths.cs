/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Databinding
{
    public static class SublistPaths
    {
        public static PropertyPath GetReplicateSublist(Type rowType)
        {
            if (rowType == typeof(SkylineDocument))
            {
                return PropertyPath.Root.Property(nameof(SkylineDocument.Replicates)).LookupAllItems();
            }
            if (rowType == typeof(Replicate))
            {
                return PropertyPath.Root.Property(nameof(Replicate.Files)).LookupAllItems();
            }
            if (rowType == typeof(Protein))
            {
                return PropertyPath.Root.Property(nameof(Protein.Results)).DictionaryValues()
                    .Property(nameof(Replicate.Files));
            }

            if (typeof(SkylineDocNode).IsAssignableFrom(rowType))
            {
                return PropertyPath.Root.Property(@"Results").LookupAllItems();
            }
            return PropertyPath.Root;
        }
    }
}
