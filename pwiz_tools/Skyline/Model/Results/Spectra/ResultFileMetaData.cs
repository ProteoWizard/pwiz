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
    /// <summary>
    /// Information about the spectra from which chromatograms were collected.
    /// </summary>
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

                var spectrumMetadata = new SpectrumMetadata(id, protoSpectrum.RetentionTime)
                    .ChangePresetScanConfiguration(protoSpectrum.PresetScanConfiguration);
                if (protoSpectrum.ScanDescriptionIndex > 0)
                {
                    spectrumMetadata =
                        spectrumMetadata.ChangeScanDescription(
                            proto.ScanDescriptions[protoSpectrum.ScanDescriptionIndex - 1]);
                }

                if (protoSpectrum.AnalyzerIndex > 0)
                {
                    spectrumMetadata =
                        spectrumMetadata.ChangeAnalyzer(proto.Analyzers[protoSpectrum.AnalyzerIndex - 1]);
                }

                if (protoSpectrum.ScanWindowIndex > 0)
                {
                    var scanWindow = proto.ScanWindows[protoSpectrum.ScanWindowIndex - 1];
                    spectrumMetadata = spectrumMetadata.ChangeScanWindow(scanWindow.LowerLimit, scanWindow.UpperLimit);
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
            if (protoPrecursor.IsolationWindowLower != 0 || protoPrecursor.IsolationWindowUpper != 0)
            {
                spectrumPrecursor = spectrumPrecursor.ChangeIsolationWindowWidth(
                    protoPrecursor.IsolationWindowLower, protoPrecursor.IsolationWindowUpper);
            }
            if (protoPrecursor.CollisionEnergy != 0)
            {
                spectrumPrecursor = spectrumPrecursor.ChangeCollisionEnergy(protoPrecursor.CollisionEnergy);
            }

            return spectrumPrecursor;
        }

        public ResultFileMetaDataProto ToProtoBuf()
        {
            var proto = new ResultFileMetaDataProto();
            var precursors = new DistinctList<(int MsLevel, SpectrumPrecursor SpectrumPrecuror)>();
            var scanDescriptions = new DistinctList<string>{null};
            var analyzers = new DistinctList<string>{null};
            var scanWindows = new DistinctList<Tuple<double, double>> { null };
            foreach (var spectrumMetadata in SpectrumMetadatas)
            {
                var spectrum = new ResultFileMetaDataProto.Types.SpectrumMetadata
                {
                    RetentionTime = spectrumMetadata.RetentionTime,
                };
                spectrum.PresetScanConfiguration = spectrumMetadata.PresetScanConfiguration;
                var intParts = GetScanIdParts(spectrumMetadata.Id);
                if (intParts == null)
                {
                    spectrum.ScanIdText = spectrumMetadata.Id;
                }
                else
                {
                    spectrum.ScanIdParts.AddRange(intParts);
                }

                spectrum.ScanDescriptionIndex = scanDescriptions.Add(spectrumMetadata.ScanDescription);
                spectrum.AnalyzerIndex = analyzers.Add(spectrumMetadata.Analyzer);
                for (int msLevel = 1; msLevel < spectrumMetadata.MsLevel; msLevel++)
                {
                    foreach (var precursor in spectrumMetadata.GetPrecursors(msLevel))
                    {
                        spectrum.PrecursorIndex.Add(precursors.Add((msLevel, precursor)) + 1);
                    }
                }

                if (spectrumMetadata.ScanWindowLowerLimit.HasValue && spectrumMetadata.ScanWindowUpperLimit.HasValue)
                {
                    spectrum.ScanWindowIndex = scanWindows.Add(Tuple.Create(spectrumMetadata.ScanWindowLowerLimit.Value,
                        spectrumMetadata.ScanWindowUpperLimit.Value));
                }
                proto.Spectra.Add(spectrum);
            }

            proto.ScanDescriptions.AddRange(scanDescriptions.Skip(1));
            proto.Analyzers.AddRange(analyzers.Skip(1));
            proto.ScanWindows.AddRange(scanWindows.Skip(1).Select(tuple=>new ResultFileMetaDataProto.Types.ScanWindow
            {
                LowerLimit = tuple.Item1,
                UpperLimit = tuple.Item2
            }));
            foreach (var precursorTuple in precursors)
            {
                var spectrumPrecursor = precursorTuple.SpectrumPrecuror;
                var protoPrecursor = new ResultFileMetaDataProto.Types.Precursor()
                {
                    MsLevel = precursorTuple.MsLevel,
                    TargetMz = spectrumPrecursor.PrecursorMz.RawValue
                };
                if (spectrumPrecursor.CollisionEnergy.HasValue)
                {
                    protoPrecursor.CollisionEnergy = spectrumPrecursor.CollisionEnergy.Value;
                }

                if (spectrumPrecursor.IsolationWindowLowerWidth.HasValue)
                {
                    protoPrecursor.IsolationWindowLower = spectrumPrecursor.IsolationWindowLowerWidth.Value;
                }

                if (spectrumPrecursor.IsolationWindowUpperWidth.HasValue)
                {
                    protoPrecursor.IsolationWindowUpper = spectrumPrecursor.IsolationWindowUpperWidth.Value;
                }
                proto.Precursors.Add(protoPrecursor);
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
