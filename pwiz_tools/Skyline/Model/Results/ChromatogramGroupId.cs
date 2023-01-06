using System.Collections.Generic;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Model.Results.Spectra;

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
    }

    public class ChromatogramGroupIds
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

        public int AddId(ChromatogramGroupId groupId)
        {
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
            IEnumerable<ChromGroupHeaderInfo> chromGroupHeaderInfos)
        {
            Dictionary<KeyValuePair<int, ushort>, int> groupIdIndexes =
                new Dictionary<KeyValuePair<int, ushort>, int>();
            foreach (var chromGroupHeaderInfo in chromGroupHeaderInfos)
            {
                if (chromGroupHeaderInfo.TextIdIndex == -1)
                {
                    yield return chromGroupHeaderInfo;
                    continue;
                }

                var target = Target.FromSerializableString(Encoding.UTF8.GetString(textIdBytes,
                    chromGroupHeaderInfo.TextIdIndex, chromGroupHeaderInfo.TextIdLen));
                int index = AddId(new ChromatogramGroupId(target, null));
                yield return chromGroupHeaderInfo.ChangeTextIdIndex(index, 0);
            }
        }

        public IEnumerable<ChromGroupHeaderInfo> ToTextIdBytes(IEnumerable<ChromGroupHeaderInfo> chromGroupHeaderInfos,
            List<byte> textIdBytes)
        {
            var textIdLocations = new Dictionary<int, KeyValuePair<int, ushort>>();
            var targetTextLocations = new Dictionary<string, KeyValuePair<int, ushort>>();
            foreach (var chromGroupHeaderInfo in chromGroupHeaderInfos)
            {
                if (chromGroupHeaderInfo.TextIdIndex < 0)
                {
                    yield return chromGroupHeaderInfo;
                    continue;
                }

                KeyValuePair<int, ushort> textIdLocation;
                if (!textIdLocations.TryGetValue(chromGroupHeaderInfo.TextIdIndex, out textIdLocation))
                {
                    var targetText = GetId(chromGroupHeaderInfo.TextIdIndex).Target.ToSerializableString();
                    if (targetTextLocations.TryGetValue(targetText, out textIdLocation))
                    {
                        textIdLocations.Add(chromGroupHeaderInfo.TextIdIndex, textIdLocation);
                    }
                    else
                    {
                        var bytes = Encoding.UTF8.GetBytes(targetText);
                        textIdLocation = new KeyValuePair<int, ushort>(textIdBytes.Count, (ushort) bytes.Length);
                        targetTextLocations.Add(targetText, textIdLocation);
                    }

                    textIdLocations.Add(chromGroupHeaderInfo.TextIdIndex, textIdLocation);
                }

                yield return chromGroupHeaderInfo.ChangeTextIdIndex(textIdLocation.Key, textIdLocation.Value);
            }
        }
    }
}