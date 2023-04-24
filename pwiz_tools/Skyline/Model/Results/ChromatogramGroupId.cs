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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.Legacy;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Identifier of a group of chromatograms in a .skyd file.
    /// Originally, chromatograms were identified by a string ("TextId") which represented either the modified
    /// sequence or the small molecule attributes.
    /// <see cref="SpectrumClassFilter"/> was added in <see cref="CacheFormatVersion.Eighteen"/>.
    /// </summary>
    public class ChromatogramGroupId : Immutable, IComparable<ChromatogramGroupId>
    {
        private ChromatogramGroupId(Target target, string qcTraceName, SpectrumClassFilter spectrumClassFilter)
        {
            Target = target;
            QcTraceName = qcTraceName;
            SpectrumClassFilter = SpectrumClassFilter.EmptyToNull(spectrumClassFilter);
        }

        public static ChromatogramGroupId ForQcTraceName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return new ChromatogramGroupId(null, name, null);
        }

        public ChromatogramGroupId(Target target, SpectrumClassFilter spectrumClassFilter) : this(target, null,
            spectrumClassFilter)
        {
        }

        public Target Target { get; }
        public string QcTraceName { get; }
        public SpectrumClassFilter SpectrumClassFilter { get; private set; }

        public ChromatogramGroupId ChangeSpectrumClassFilter(SpectrumClassFilter spectrumClassFilter)
        {
            spectrumClassFilter = SpectrumClassFilter.EmptyToNull(spectrumClassFilter);
            if (ReferenceEquals(spectrumClassFilter, SpectrumClassFilter))
            {
                return this;
            }

            if (Target == null && spectrumClassFilter != null)
            {
                throw new InvalidOperationException();
            }

            return ChangeProp(ImClone(this), im => im.SpectrumClassFilter = spectrumClassFilter);
        }

        protected bool Equals(ChromatogramGroupId other)
        {
            return Equals(Target, other.Target) && QcTraceName == other.QcTraceName && Equals(SpectrumClassFilter, other.SpectrumClassFilter);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ChromatogramGroupId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Target != null ? Target.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (QcTraceName != null ? QcTraceName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SpectrumClassFilter != null ? SpectrumClassFilter.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static IEnumerable<ChromatogramGroupId> FromProto(ChromatogramGroupIdsProto proto)
        {
            var targets = new List<Target> {null};
            var filters = new List<SpectrumClassFilter> {null};
            foreach (var targetProto in proto.Targets)
            {
                if (!string.IsNullOrEmpty(targetProto.ModifiedPeptideSequence))
                {
                    targets.Add(new Target(targetProto.ModifiedPeptideSequence));
                }
                else
                {
                    targets.Add(new Target(new CustomMolecule(targetProto.Formula, new TypedMass(targetProto.MonoMass, MassType.Monoisotopic),
                        new TypedMass(targetProto.AverageMass, MassType.Average),
                        targetProto.Name, GetAccessionNumbers(targetProto))));
                }
            }

            foreach (var filterProto in proto.Filters)
            {
                var filterSpecs = new List<FilterSpec>();
                foreach (var filterPredicate in filterProto.Predicates)
                {
                    filterSpecs.Add(new FilterSpec(PropertyPath.Parse(filterPredicate.PropertyPath),
                        FilterPredicate.FromInvariantOperandText(
                            ChromatogramGroupIds.GetFilterOperation(filterPredicate.Operation),
                            filterPredicate.Operand)));
                }
                filters.Add(new SpectrumClassFilter(filterSpecs));
            }

            foreach (var id in proto.ChromatogramGroupIds)
            {
                yield return new ChromatogramGroupId(targets[id.TargetIndex], id.QcTraceName, filters[id.FilterIndex]);
            }
        }

        private static MoleculeAccessionNumbers GetAccessionNumbers(ChromatogramGroupIdsProto.Types.Target target)
        {
            var values = new[]
            {
                Tuple.Create(MoleculeAccessionNumbers.TagInChiKey, target.InChiKey),
                Tuple.Create(MoleculeAccessionNumbers.TagCAS, target.Cas),
                Tuple.Create(MoleculeAccessionNumbers.TagHMDB, target.Hmdb),
                Tuple.Create(MoleculeAccessionNumbers.TagInChI, target.InChi),
                Tuple.Create(MoleculeAccessionNumbers.TagSMILES, target.Smiles),
                Tuple.Create(MoleculeAccessionNumbers.TagKEGG, target.Kegg)
            };
            if (values.All(value => value.Item2.Length == 0))
            {
                return MoleculeAccessionNumbers.EMPTY;
            }

            var dictionary = values.Where(value => value.Item2.Length > 0)
                .ToDictionary(value => value.Item1, value => value.Item2);
            return new MoleculeAccessionNumbers(dictionary);
        }

        /// <summary>
        /// Used by <see cref="SpectrumFilterPair.CompareTo"/>
        /// </summary>
        public int CompareTo(ChromatogramGroupId other)
        {
            return ValueTuple.Create(Target, SpectrumClassFilter)
                .CompareTo(ValueTuple.Create(other.Target, other.SpectrumClassFilter));
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Target != null)
            {
                parts.Add(Target.ToString());
            }

            if (SpectrumClassFilter != null)
            {
                parts.Add(SpectrumClassFilter.ToString());
            }

            return TextUtil.SpaceSeparate(parts);
        }

        public static ChromatogramGroupId ForPeptide(PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode)
        {
            if (peptideDocNode == null)
            {
                return null;
            }

            return new ChromatogramGroupId(peptideDocNode.ModifiedTarget, transitionGroupDocNode?.SpectrumClassFilter);
        }
    }

    public class ChromatogramGroupIds : IEnumerable<ChromatogramGroupId>
    {
        private List<ChromatogramGroupId> _ids = new List<ChromatogramGroupId>();
        private Dictionary<ChromatogramGroupId, int> _idIndexes = new Dictionary<ChromatogramGroupId, int>();

        public ChromatogramGroupId GetId(int index)
        {
            if (index < 0)
            {
                return null;
            }

            return _ids[index];
        }

        public ChromatogramGroupId GetId(ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            return GetId(chromGroupHeaderInfo.TextIdIndex);
        }

        public int AddId(ChromatogramGroupId groupId)
        {
            if (groupId == null)
            {
                return -1;
            }
            if (!_idIndexes.TryGetValue(groupId, out int index))
            {
                index = _ids.Count;
                _ids.Add(groupId);
                _idIndexes.Add(groupId, index);
                return index;
            }

            return index;
        }

        public IEnumerable<ChromGroupHeaderInfo> ConvertFromTextIdBytes(byte[] textIdBytes,
            IEnumerable<ChromGroupHeaderInfo16> chromGroupHeaderInfos)
        {
            foreach (var chromGroupHeaderInfo in chromGroupHeaderInfos)
            {
                if (chromGroupHeaderInfo._textIdIndex == -1)
                {
                    yield return new ChromGroupHeaderInfo(chromGroupHeaderInfo, -1);
                    continue;
                }

                var textId = Encoding.UTF8.GetString(textIdBytes,
                    chromGroupHeaderInfo._textIdIndex, chromGroupHeaderInfo._textIdLen);
                ChromatogramGroupId chromatogramGroupId;
                if (0 != (chromGroupHeaderInfo._flagBits & ChromGroupHeaderInfo16.FlagValues.extracted_qc_trace))
                {
                    chromatogramGroupId = ChromatogramGroupId.ForQcTraceName(textId);
                }
                else
                {
                    chromatogramGroupId = new ChromatogramGroupId(Target.FromSerializableString(textId), null);
                }
                int index = AddId(chromatogramGroupId);
                yield return new ChromGroupHeaderInfo(chromGroupHeaderInfo, index);
            }
        }

        public ChromGroupHeaderInfo SetId(ChromGroupHeaderInfo chromGroupHeaderInfo, ChromatogramGroupId id)
        {
            return chromGroupHeaderInfo.ChangeTextIdIndex(AddId(id));
        }

        public ChromGroupHeaderInfo16 ConvertToTextId(List<byte> textIdBytes, Dictionary<Target, TextIdLocation> map,
            ChromGroupHeaderInfo chromGroupHeaderInfo)
        {
            var target = GetId(chromGroupHeaderInfo)?.Target;
            if (target == null)
            {
                return new ChromGroupHeaderInfo16(chromGroupHeaderInfo, -1, 0);
            }
            if (!map.TryGetValue(target, out var textIdLocation))
            {
                int textIdIndex = textIdBytes.Count;
                textIdBytes.AddRange(Encoding.UTF8.GetBytes(target.ToSerializableString()));
                textIdLocation = new TextIdLocation(textIdIndex, textIdBytes.Count - textIdIndex);
                map.Add(target, textIdLocation);
            }

            return new ChromGroupHeaderInfo16(chromGroupHeaderInfo, textIdLocation.Index,
                (ushort) textIdLocation.Length);
        }

        public int Count
        {
            get
            {
                return _ids.Count;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ChromatogramGroupId> GetEnumerator()
        {
            return _ids.GetEnumerator();
        }

        public ChromatogramGroupIdsProto ToProtoMessage()
        {
            var targets = new DistinctList<Target> {null};
            var filters = new DistinctList<SpectrumClassFilter> {null};
            var idsProto = new ChromatogramGroupIdsProto();
            foreach (var id in this)
            {
                idsProto.ChromatogramGroupIds.Add(new ChromatogramGroupIdsProto.Types.ChromatogramGroupId()
                {
                    TargetIndex = targets.Add(id.Target),
                    FilterIndex = filters.Add(id.SpectrumClassFilter)
                });
            }

            foreach (var target in targets.Skip(1))
            {
                var targetProto = new ChromatogramGroupIdsProto.Types.Target();
                if (target.IsProteomic)
                {
                    targetProto.ModifiedPeptideSequence = target.Sequence;
                }
                else
                {
                    var molecule = target.Molecule;
                    targetProto.Name = molecule.Name;
                    if (molecule.ParsedMolecule.IsMassOnly)
                    {
                        targetProto.MonoMass = molecule.MonoisotopicMass;
                        targetProto.AverageMass = molecule.AverageMass;
                    }
                    else
                    {
                        targetProto.Formula = molecule.Formula;
                    }

                    targetProto.InChiKey = molecule.AccessionNumbers.GetInChiKey() ?? string.Empty;
                    targetProto.Cas = molecule.AccessionNumbers.GetCAS() ?? string.Empty;
                    targetProto.Hmdb = molecule.AccessionNumbers.GetHMDB() ?? string.Empty;
                    targetProto.InChi = molecule.AccessionNumbers.GetInChI() ?? string.Empty;
                    targetProto.Smiles = molecule.AccessionNumbers.GetSMILES() ?? string.Empty;
                    targetProto.Kegg = molecule.AccessionNumbers.GetKEGG() ?? string.Empty;
                }

                idsProto.Targets.Add(targetProto);
            }
            foreach (var filter in filters.Skip(1))
            {
                var filterProto = new ChromatogramGroupIdsProto.Types.SpectrumFilter();
                foreach (var filterSpec in filter.FilterSpecs)
                {
                    filterProto.Predicates.Add(new ChromatogramGroupIdsProto.Types.SpectrumFilter.Types.Predicate()
                    {
                        PropertyPath = filterSpec.Column,
                        Operation = _filterOperationReverseMap[filterSpec.Operation],
                        Operand = filterSpec.Predicate.InvariantOperandText
                    });
                }
                idsProto.Filters.Add(filterProto);
            }


            return idsProto;
        }

        private static Dictionary<ChromatogramGroupIdsProto.Types.FilterOperation, IFilterOperation>
            _filterOperationMap = new Dictionary<ChromatogramGroupIdsProto.Types.FilterOperation, IFilterOperation>
            {
                {
                    ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpHasAnyValue,
                    FilterOperations.OP_HAS_ANY_VALUE
                },
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpEquals, FilterOperations.OP_EQUALS},
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpNotEquals, FilterOperations.OP_NOT_EQUALS},
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsBlank, FilterOperations.OP_IS_BLANK},
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsNotBlank, FilterOperations.OP_IS_NOT_BLANK},
                {
                    ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsGreaterThan,
                    FilterOperations.OP_IS_GREATER_THAN
                },
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsLessThan, FilterOperations.OP_IS_LESS_THAN},
                {
                    ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsGreaterThanOrEqualTo,
                    FilterOperations.OP_IS_GREATER_THAN_OR_EQUAL
                },
                {
                    ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpIsLessThanOrEqualTo,
                    FilterOperations.OP_IS_LESS_THAN_OR_EQUAL
                },
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpContains, FilterOperations.OP_CONTAINS},
                {ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpNotContains, FilterOperations.OP_NOT_CONTAINS},
                {ChromatogramGroupIdsProto.Types.FilterOperation.FitlerOpStartsWith, FilterOperations.OP_STARTS_WITH},
                {
                    ChromatogramGroupIdsProto.Types.FilterOperation.FilterOpNotStartsWith,
                    FilterOperations.OP_NOT_STARTS_WITH
                }
            };

        private static readonly Dictionary<IFilterOperation, ChromatogramGroupIdsProto.Types.FilterOperation>
            _filterOperationReverseMap = _filterOperationMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        public static IFilterOperation GetFilterOperation(
            ChromatogramGroupIdsProto.Types.FilterOperation protoFilterOperation)
        {
            _filterOperationMap.TryGetValue(protoFilterOperation, out var filterOperation);
            return filterOperation;
        }

    }

    public class TextIdLocation
    {
        public TextIdLocation(int index, int length)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (length < 0 || length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            Index = index;
            Length = length;
        }

        public int Index { get; }
        public int Length { get; }
    }
}