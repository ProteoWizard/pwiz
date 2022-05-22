using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public interface IResultFileMetadata
    {
        byte[] ToByteArray();
        MsDataFileScanIds ToMsDataFileScanIds();
    }
    public class ResultFileMetaData : Immutable, IResultFileMetadata
    {
        public ResultFileMetaData(IEnumerable<SpectrumMetadata> spectrumMetadatas)
        {
            SpectrumMetadatas = ImmutableList.ValueOf(spectrumMetadatas);
        }
        public ImmutableList<SpectrumMetadata> SpectrumMetadatas { get; private set; }

        public static ResultFileMetaData FromProtoBuf(ResultFileMetaDataProto proto)
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
                if (precursorsByLevel.Any())
                {
                    spectrumMetadata = spectrumMetadata.ChangePrecursors(Enumerable
                        .Range(1, precursorsByLevel.Max(group => group.Key)).Select(level => precursorsByLevel[level]));
                }
                spectrumMetadatas.Add(spectrumMetadata);
            }

            return new ResultFileMetaData(spectrumMetadatas);
        }

        private static SpectrumPrecursor SpectrumPrecursorFromProto(
            ResultFileMetaDataProto.Types.Precursor protoPrecursor)
        {
            var spectrumPrecursor =
                new SpectrumPrecursor(new SignedMz(protoPrecursor.TargetMz, protoPrecursor.TargetMz < 0));
            if (protoPrecursor.CollisionEnergy != 0)
            {
                spectrumPrecursor = spectrumPrecursor.ChangeCollisionEnergy(protoPrecursor.CollisionEnergy);
            }

            return spectrumPrecursor;
        }

        public ResultFileMetaDataProto ToProtoBuf()
        {
            var proto = new ResultFileMetaDataProto();
            var precursors = new Dictionary<Tuple<int, SpectrumPrecursor>, int>();
            var scanDescriptions = new Dictionary<string, int>();
            foreach (var spectrumMetadata in SpectrumMetadatas)
            {
                var spectrum = new ResultFileMetaDataProto.Types.SpectrumMetadata
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
                            var protoPrecursor = new ResultFileMetaDataProto.Types.Precursor()
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

        public MsDataFileScanIds ToMsDataFileScanIds()
        {
            var byteStream = new MemoryStream();
            var startBytesList = new List<int>();
            var lengths = new List<int>();
            for (int i = 0; i < SpectrumMetadatas.Count; i++)
            {
                var spectrum = SpectrumMetadatas[i];
                var startIndex = byteStream.Length;
                var scanIdBytes = Encoding.UTF8.GetBytes(spectrum.Id);
                byteStream.Write(scanIdBytes, 0, scanIdBytes.Length);
                Assume.AreEqual(startIndex + scanIdBytes.Length, byteStream.Length);
                startBytesList.Add(Convert.ToInt32(startIndex));
                lengths.Add(scanIdBytes.Length);
            }
            return new MsDataFileScanIds(startBytesList.ToArray(), lengths.ToArray(), byteStream.ToArray());
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

        public static ResultFileMetaData FromByteArray(byte[] bytes)
        {
            var proto = new ResultFileMetaDataProto();
            proto.MergeFrom(bytes);
            return FromProtoBuf(proto);
        }
    }
}
