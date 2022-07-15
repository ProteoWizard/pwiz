/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2022
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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public class ProteinGroupMetadata : ProteinMetadata
    {
        const string GROUP_SEPARATOR = @"/";
        public new static readonly ProteinGroupMetadata EMPTY = new ProteinGroupMetadata();

        public ImmutableList<ProteinMetadata> ProteinMetadata { get; }

        [Track]
        public override string Name => ProteinMetadata.All(p => p.Name == null)
            ? null
            : string.Join(GROUP_SEPARATOR, ProteinMetadata.Select(p => p.Name));

        [Track]
        public override string Description => ProteinMetadata.All(p => p.Description == null)
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadata.Where(p => !string.IsNullOrWhiteSpace(p.Description)).Select(p => p.Description));

        [Track]
        public override string PreferredName => ProteinMetadata.All(p => p.PreferredName == null)
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadata.Where(p => !string.IsNullOrWhiteSpace(p.PreferredName)).Select(p => p.PreferredName));

        [Track]
        public override string Accession => ProteinMetadata.All(p => p.Accession == null)
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadata.Where(p => !string.IsNullOrWhiteSpace(p.Accession)).Select(p => p.Accession));

        [Track]
        public override string Gene => ProteinMetadata.All(p => p.Gene == null)
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadata.Where(p => !string.IsNullOrWhiteSpace(p.Gene)).Select(p => p.Gene));

        [Track]
        public override string Species => ProteinMetadata.All(p => p.Species == null)
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadata.Where(p => !string.IsNullOrWhiteSpace(p.Species)).Select(p => p.Species));
        [Track]
        public override WebSearchInfo WebSearchInfo => ProteinMetadata.Select(p => p.WebSearchInfo).First();

        public override ProteinMetadata ChangeName(string name)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangeDescription(string descr)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangePreferredName(string preferredname)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangeAccession(string accession)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangeGene(string gene)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangeSpecies(string species)
        {
            return new ProteinGroupMetadata(this);
        }

        public ProteinMetadata ChangeSingleProteinMetadata(ProteinMetadata singleProteinMetadata)
        {
            Assume.IsTrue(singleProteinMetadata != null && !(singleProteinMetadata is ProteinGroupMetadata));
            var proteinMetadataList = new List<ProteinMetadata>(ProteinMetadata);
            var matchingProteinIndex = proteinMetadataList.IndexOf(m => m.Name == singleProteinMetadata?.Name);
            Assume.IsTrue(matchingProteinIndex >= 0, $@"no matching protein in group {this} for protein {singleProteinMetadata}");
            proteinMetadataList[matchingProteinIndex] = singleProteinMetadata;
            return new ProteinGroupMetadata(Name, Description, proteinMetadataList);
        }

        public override ProteinMetadata ChangeWebSearchInfo(WebSearchInfo webSearchInfo)
        {
            return new ProteinGroupMetadata(this, webSearchInfo);
        }

        public override ProteinMetadata ClearWebSearchInfo()
        {
            // sometimes all you really want is to initialize
            return new ProteinGroupMetadata(this, WebSearchInfo.EMPTY);
        }

        public override ProteinMetadata SetWebSearchCompleted()
        {
            return new ProteinGroupMetadata(this, WebSearchInfo.SetSearchCompleted());
        }

        public override ProteinMetadata SetWebSearchTerm(WebSearchTerm search)
        {
            return new ProteinGroupMetadata(this, WebSearchInfo.SetSearchTerm(search));
        }

        /// <summary>
        /// returns a copy of this, with any blanks filled in
        /// using the members of source
        /// </summary>
        /// <param name="source">the source to merge from</param>
        /// <returns>a copy of this merged with source, with this winning when fields conflict</returns>
        public override ProteinMetadata Merge(ProteinMetadata source)
        {
            if (source == null)
                return this;
            if (source is ProteinGroupMetadata sourceGroup)
            {
                return new ProteinGroupMetadata(String.IsNullOrEmpty(Name) ? sourceGroup.Name : Name,
                    String.IsNullOrEmpty(Description) ? sourceGroup.Description : Description,
                    sourceGroup.ProteinMetadata);
            }

            return new ProteinGroupMetadata(String.IsNullOrEmpty(Name) ? source.Name : Name,
                String.IsNullOrEmpty(Description) ? source.Description : Description,
                ProteinMetadata);
            //throw new InvalidOperationException("cannot merge ProteinMetadata into ProteinGroupMetadata");
        }

        private ProteinGroupMetadata() : base(null, null)
        {
        }

        private ProteinGroupMetadata(ProteinGroupMetadata other, WebSearchInfo webSearchInfo = null) : base(other.Name, other.Description)
        {
            webSearchInfo ??= other.ProteinMetadata.First().WebSearchInfo;
            ProteinMetadata = ImmutableList<ProteinMetadata>.ValueOf(other.ProteinMetadata);
            //ProteinMetadata[0] = ProteinMetadata[0].ChangeWebSearchInfo(webSearchInfo);
            /*    Name = ProteinMetadata.All(p => p.Name == null)
                ? null
                : string.Join(GROUP_SEPARATOR, ProteinMetadata.OrderBy(p => p.).Select(p => p.Name));*/
        }

        public ProteinGroupMetadata(string name, string description, IList<ProteinMetadata> proteinMetadata) : base(name, description)
        {
            ProteinMetadata = ImmutableList<ProteinMetadata>.ValueOf(proteinMetadata);
        }

        public new object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }

        public bool Equals(ProteinGroupMetadata other)
        {
            if (other == null)
                return false;
            if (!string.Equals(Name, other.Name))
                return false;
            if (!ArrayUtil.EqualsDeep(ProteinMetadata, other.ProteinMetadata))
                return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ProteinGroupMetadata) obj);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                int result = (Name != null ? Name.GetHashCode() : 0);
                result = (result * 397) ^ ProteinMetadata.GetHashCodeDeep();
                return result;
            }
        }

        public override string ToString()
        {

            return String.Format(@"name='{0}' accession='{1}' preferredname='{2}' description='{3}' gene='{4}' species='{5}' websearch='{6}'",
                Name, Accession, PreferredName, Description, Gene, Species, WebSearchInfo);
        }
    }
}