using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public class ChromatogramGroupId
    {
        public ChromatogramGroupId(Target target, SpectrumClassFilter spectrumClassFilter)
        {
            Target = target;
            SpectrumClassFilter = spectrumClassFilter;
        }

        public Target Target { get; }
        public SpectrumClassFilter SpectrumClassFilter { get; }

        protected bool Equals(ChromatogramGroupId other)
        {
            return Equals(Target, other.Target) && Equals(SpectrumClassFilter, other.SpectrumClassFilter);
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
                return ((Target != null ? Target.GetHashCode() : 0) * 397) ^
                       (SpectrumClassFilter != null ? SpectrumClassFilter.GetHashCode() : 0);
            }
        }

        public static ChromatogramGroupIdsProto ToProto(IEnumerable<ChromatogramGroupId> ids)
        {
            var targets = new ValueIndex<Target>();
            var filters = new ValueIndex<SpectrumClassFilter>();
            var idsProto = new ChromatogramGroupIdsProto();
            foreach (var id in ids)
            {
                idsProto.ChromatogramGroupIds.Add(new ChromatogramGroupIdsProto.Types.ChromatogramGroupId()
                {
                    TargetIndex = targets.IndexForValue(id.Target),
                    FilterIndex = filters.IndexForValue(id.SpectrumClassFilter)
                });
            }

            foreach (var target in targets.Values)
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
                    targetProto.Formula = molecule.Formula;
                    targetProto.MonoMass = molecule.MonoisotopicMass;
                    targetProto.AverageMass = molecule.AverageMass;
                    // TODO: Accession numbers
                }

                idsProto.Targets.Add(targetProto);
            }

            return idsProto;
        }

        public static IEnumerable<ChromatogramGroupId> FromProto(ChromatogramGroupIdsProto proto)
        {
            var targets = new List<Target>() {null};
            foreach (var targetProto in proto.Targets)
            {
                if (!string.IsNullOrEmpty(targetProto.ModifiedPeptideSequence))
                {
                    targets.Add(new Target(targetProto.ModifiedPeptideSequence));
                }
                else
                {
                    MoleculeAccessionNumbers moleculeAccessionNumbers = MoleculeAccessionNumbers.EMPTY; // TODO
                    targets.Add(new Target(new CustomMolecule(targetProto.Formula, new TypedMass(targetProto.MonoMass, MassType.Monoisotopic),
                        new TypedMass(targetProto.AverageMass, MassType.Average),
                        targetProto.Name, moleculeAccessionNumbers)));
                }
            }

            foreach (var id in proto.ChromatogramGroupIds)
            {
                yield return new ChromatogramGroupId(targets[id.TargetIndex], null);
            }
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

                var target = Target.FromSerializableString(Encoding.UTF8.GetString(textIdBytes,
                    chromGroupHeaderInfo._textIdIndex, chromGroupHeaderInfo._textIdLen));
                int index = AddId(new ChromatogramGroupId(target, null));
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
            throw new NotImplementedException();
            // var target = GetId(chromGroupHeaderInfo)?.Target;
            // if (target == null)
            // {
            //     return chromGroupHeaderInfo.ChangeTextIdLocation(null);
            // }
            // if (!map.TryGetValue(target, out var textIdLocation))
            // {
            //     int textIdIndex = textIdBytes.Count;
            //     textIdBytes.AddRange(Encoding.UTF8.GetBytes(target.ToSerializableString()));
            //     textIdLocation = new TextIdLocation(textIdIndex, textIdBytes.Count - textIdIndex);
            //     map.Add(target, textIdLocation);
            // }
            //
            // return chromGroupHeaderInfo.ChangeTextIdLocation(textIdLocation);
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
            var targets = new ValueIndex<Target>();
            var filters = new ValueIndex<SpectrumClassFilter>();
            var idsProto = new ChromatogramGroupIdsProto();
            foreach (var id in this)
            {
                idsProto.ChromatogramGroupIds.Add(new ChromatogramGroupIdsProto.Types.ChromatogramGroupId()
                {
                    TargetIndex = targets.IndexForValue(id.Target),
                    FilterIndex = filters.IndexForValue(id.SpectrumClassFilter)
                });
            }

            foreach (var target in targets.Values)
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
                    targetProto.Formula = molecule.Formula;
                    targetProto.MonoMass = molecule.MonoisotopicMass;
                    targetProto.AverageMass = molecule.AverageMass;
                    // TODO: Accession numbers
                }

                idsProto.Targets.Add(targetProto);
            }

            return idsProto;
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