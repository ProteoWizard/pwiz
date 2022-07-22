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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public class ProteinGroupMetadata : ProteinMetadata
    {
        public const string GROUP_SEPARATOR = @" / ";
        public new static readonly ProteinGroupMetadata EMPTY = new ProteinGroupMetadata();

        private ProteinGroupMetadata() : base(null, null)
        {
        }

        private ProteinGroupMetadata(ProteinGroupMetadata other, WebSearchInfo webSearchInfo = null) : base(other.Name, other.Description)
        {
            webSearchInfo ??= other.ProteinMetadataList.First().WebSearchInfo;
            ProteinMetadataList = ImmutableList<ProteinMetadata>.ValueOf(other.ProteinMetadataList);
        }

        public ProteinGroupMetadata(IList<ProteinMetadata> proteinMetadata) : this()
        {
            ProteinMetadataList = ImmutableList<ProteinMetadata>.ValueOf(proteinMetadata);
        }

        public override ImmutableList<ProteinMetadata> ProteinMetadataList { get; }

        [Track]
        public override string Name => ProteinMetadataList.All(p => string.IsNullOrWhiteSpace(p.Name))
            ? null
            : string.Join(GROUP_SEPARATOR, ProteinMetadataList.Select(p => p.Name));

        [Track]
        public override string Description => ProteinMetadataList.All(p => string.IsNullOrWhiteSpace(p.Description))
            ? null
            : string.Join(Environment.NewLine,
                ProteinMetadataList.Select(p => string.IsNullOrWhiteSpace(p.Description) ? EnumNames.SampleType_unknown : p.Description));

        [Track]
        public override string PreferredName => ProteinMetadataList.All(p => string.IsNullOrWhiteSpace(p.PreferredName))
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadataList.Select(p => string.IsNullOrWhiteSpace(p.PreferredName) ? EnumNames.SampleType_unknown : p.PreferredName));

        [Track]
        public override string Accession => ProteinMetadataList.All(p => string.IsNullOrWhiteSpace(p.Accession))
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadataList.Select(p => string.IsNullOrWhiteSpace(p.Accession) ? EnumNames.SampleType_unknown : p.Accession));

        [Track]
        public override string Gene => ProteinMetadataList.All(p => string.IsNullOrWhiteSpace(p.Gene))
            ? null
            : string.Join(GROUP_SEPARATOR,
                ProteinMetadataList.Select(p => string.IsNullOrWhiteSpace(p.Gene) ? EnumNames.SampleType_unknown : p.Gene));

        [Track]
        public override string Species
        {
            get
            {
                var allSpecies = ProteinMetadataList.Select(p => p.Species).ToList();
                if (allSpecies.All(string.IsNullOrWhiteSpace))
                    return null;
                if (allSpecies.Distinct().Count() == 1)
                    return allSpecies.First();
                return string.Join(GROUP_SEPARATOR, allSpecies.Select(s => string.IsNullOrWhiteSpace(s) ? EnumNames.SampleType_unknown : s));
            }
        }

        public override WebSearchInfo WebSearchInfo => ProteinMetadataList.Select(p => p.WebSearchInfo).First();

        /// <summary>
        /// The Change* functions do nothing for a protein group. Protein groups' properties are always computed from their constituents' properties.
        /// </summary>
        public override ProteinMetadata ChangeName(string name)
        {
            return new ProteinGroupMetadata(this);
        }

        /// <summary><see cref="ChangeName"/></summary>
        public override ProteinMetadata ChangeDescription(string descr)
        {
            return new ProteinGroupMetadata(this);
        }

        /// <summary><see cref="ChangeName"/></summary>
        public override ProteinMetadata ChangePreferredName(string preferredname)
        {
            return new ProteinGroupMetadata(this);
        }

        /// <summary><see cref="ChangeName"/></summary>
        public override ProteinMetadata ChangeAccession(string accession)
        {
            return new ProteinGroupMetadata(this);
        }

        /// <summary><see cref="ChangeName"/></summary>
        public override ProteinMetadata ChangeGene(string gene)
        {
            return new ProteinGroupMetadata(this);
        }

        /// <summary><see cref="ChangeName"/></summary>
        public override ProteinMetadata ChangeSpecies(string species)
        {
            return new ProteinGroupMetadata(this);
        }

        public override ProteinMetadata ChangeSingleProteinMetadata(ProteinMetadata singleProteinMetadata)
        {
            Assume.IsTrue(singleProteinMetadata?.ProteinMetadataList?.Count == 1);
            return new ProteinGroupMetadata(ProteinMetadataList.ReplaceElement(singleProteinMetadata, m => m.Name == singleProteinMetadata!.Name));
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
                return new ProteinGroupMetadata(sourceGroup.ProteinMetadataList);
            }

            Assume.Fail(@"cannot merge ProteinMetadata into ProteinGroupMetadata");
            return null;
        }

        public override ProteinMetadata Merge(string name, string description)
        {
            return Merge(new ProteinGroupMetadata(ProteinMetadataList));
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
            if (!ArrayUtil.EqualsDeep(ProteinMetadataList, other.ProteinMetadataList))
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
                result = (result * 397) ^ ProteinMetadataList.GetHashCodeDeep();
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