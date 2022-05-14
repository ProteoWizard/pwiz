using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class ResultFileData : Immutable
    {
        public ResultFileData(IEnumerable<SpectrumMetadata> spectrumMetadatas)
        {
            SpectrumMetadatas = ImmutableList.ValueOf(spectrumMetadatas);
        }
        public ImmutableList<SpectrumMetadata> SpectrumMetadatas { get; private set; }

        public static ResultFileData FromProtoBuf(ProtoBuf.ResultFileData proto)
        {
            var spectrumMetadatas = new List<SpectrumMetadata>();
            var precursors = proto.Precursors.Select(SpectrumPrecursorFromProto).ToList();
            foreach (var protoSpectrum in proto.Spectra)
            {
                string id;
                if (string.IsNullOrEmpty(protoSpectrum.ScanIdText))
                {
                    id = string.Join(@".",
                        protoSpectrum.ScanIdParts.Select(part => part.ToString(CultureInfo.InvariantCulture)));
                }
                else
                {
                    id = protoSpectrum.ScanIdText;
                }

                var spectrumMetadata = new SpectrumMetadata(id, protoSpectrum.RetentionTime);
                if (protoSpectrum.ScanDescriptionIndex > 0)
                {
                    spectrumMetadata =
                        spectrumMetadata.ChangeScanDescription(
                            proto.ScanDescriptions[protoSpectrum.ScanDescriptionIndex - 1]);
                }

                var precursorsByLevel =
                    protoSpectrum.PrecursorIndex.ToLookup(index => proto.Precursors[index - 1].MsLevel, index=>precursors[index - 1]);
                spectrumMetadata = spectrumMetadata.ChangePrecursors(Enumerable
                    .Range(1, precursorsByLevel.Max(group => group.Key)).Select(level => precursorsByLevel[level]));
                spectrumMetadatas.Add(spectrumMetadata);
            }

            return new ResultFileData(spectrumMetadatas);
        }

        private static SpectrumPrecursor SpectrumPrecursorFromProto(
            ProtoBuf.ResultFileData.Types.Precursor protoPrecursor)
        {
            var spectrumPrecursor =
                new SpectrumPrecursor(new SignedMz(protoPrecursor.TargetMz, protoPrecursor.TargetMz < 0));
            if (protoPrecursor.CollisionEnergy != 0)
            {
                spectrumPrecursor = spectrumPrecursor.ChangeCollisionEnergy(protoPrecursor.CollisionEnergy);
            }

            return spectrumPrecursor;
        }

        public ProtoBuf.ResultFileData ToProtoBuf()
        {
            var proto = new ProtoBuf.ResultFileData();
            var precursors = new Dictionary<Tuple<int, SpectrumPrecursor>, int>();
            var scanDescriptions = new Dictionary<string, int>();
            foreach (var spectrumMetadata in SpectrumMetadatas)
            {
                var spectrum = new ProtoBuf.ResultFileData.Types.SpectrumMetadata
                {
                    RetentionTime = spectrumMetadata.RetentionTime,
                };
                var intParts = GetScanIdParts(spectrumMetadata.Id);
                if (intParts == null)
                {
                    spectrum.ScanIdText = spectrumMetadata.Id;
                }
                else
                {
                    spectrum.ScanIdParts.AddRange(intParts);
                }

                if (!string.IsNullOrEmpty(spectrumMetadata.ScanDescription))
                {
                    if (!scanDescriptions.TryGetValue(spectrumMetadata.ScanDescription, out int scanDescriptionIndex))
                    {
                        proto.ScanDescriptions.Add(spectrumMetadata.ScanDescription);
                        scanDescriptionIndex = proto.ScanDescriptions.Count;
                        scanDescriptions.Add(spectrumMetadata.ScanDescription, scanDescriptionIndex);
                    }

                    spectrum.ScanDescriptionIndex = scanDescriptionIndex;
                }

                for (int msLevel = 1; msLevel < spectrumMetadata.MsLevel; msLevel++)
                {
                    foreach (var precursor in spectrumMetadata.GetPrecursors(msLevel))
                    {
                        var key = Tuple.Create(msLevel, precursor);
                        if (!precursors.TryGetValue(key, out int precursorIndex))
                        {
                            var protoPrecursor = new ProtoBuf.ResultFileData.Types.Precursor()
                            {
                                MsLevel = msLevel,
                                TargetMz = precursor.PrecursorMz.RawValue
                            };
                            if (precursor.CollisionEnergy.HasValue)
                            {
                                protoPrecursor.CollisionEnergy = precursor.CollisionEnergy.Value;
                            }
                            proto.Precursors.Add(protoPrecursor);
                            precursorIndex = proto.Precursors.Count;
                            precursors.Add(key, precursorIndex);
                        }
                        spectrum.PrecursorIndex.Add(precursorIndex);
                    }
                }
                proto.Spectra.Add(spectrum);
            }

            return proto;
        }

        private IEnumerable<int> GetScanIdParts(string scanId)
        {
            var parts = scanId.Split('.');
            var intParts = new List<int>();
            foreach (var part in parts)
            {
                if (!int.TryParse(part, out int intPart))
                {
                    return null;
                }

                if (!Equals(part, intPart.ToString(CultureInfo.InvariantCulture)))
                {
                    return null;
                }
                intParts.Add(intPart);
            }

            return intParts;
        }

        public byte[] ToByteArray()
        {
            return ToProtoBuf().ToByteArray();
        }

        public static ResultFileData FromByteArray(byte[] bytes)
        {
            var proto = new ProtoBuf.ResultFileData();
            proto.MergeFrom(bytes);
            return FromProtoBuf(proto);
        }
    }
}
