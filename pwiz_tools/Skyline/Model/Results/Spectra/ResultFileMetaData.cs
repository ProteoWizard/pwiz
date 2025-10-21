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
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results.ProtoBuf;

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
            SpectrumMetadatas = spectrumMetadatas as SpectrumMetadatas ?? new SpectrumMetadatas(spectrumMetadatas.ToList());
        }
        public SpectrumMetadatas SpectrumMetadatas { get; private set; }

        public static ResultFileMetaData FromProtoBuf(ResultFileMetaDataProto proto)
        {
            return new ResultFileMetaData(new SpectrumMetadatas(proto));
        }

        public ResultFileMetaDataProto ToProtoBuf()
        {
            return SpectrumMetadatas.ToProto();
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
