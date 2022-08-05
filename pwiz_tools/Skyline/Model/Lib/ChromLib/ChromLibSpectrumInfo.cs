/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    public class ChromLibSpectrumInfo : ICachedSpectrumInfo
    {
        public ChromLibSpectrumInfo(LibKey key, int id, int sampleFileId, double peakArea, IndexedRetentionTimes retentionTimesByFileId, IEnumerable<SpectrumPeaksInfo.MI> transitionAreas, string protein)
        {
            Key = key;
            Id = id;
            SampleFileId = sampleFileId;
            PeakArea = peakArea;
            RetentionTimesByFileId = retentionTimesByFileId;
            TransitionAreas = ImmutableList.ValueOf(transitionAreas) ?? ImmutableList.Empty<SpectrumPeaksInfo.MI>();
            Protein = protein ?? string.Empty;
        }
        public LibKey Key { get; private set; }
        public int Id { get; private set; }
        public int SampleFileId { get; private set; }
        public double PeakArea { get; private set; }
        public string Protein { get; private set; } // Some .clib files provide a protein accession (or Molecule List Name for small molecules)
        public IndexedRetentionTimes RetentionTimesByFileId { get; private set; }
        public IonMobilityAndCCS IonMobility => Key.IonMobility;
        public IList<SpectrumPeaksInfo.MI> TransitionAreas { get; private set; }
        public void Write(Stream stream)
        {
            Key.Write(stream);
            PrimitiveArrays.WriteOneValue(stream, Id);
            PrimitiveArrays.WriteOneValue(stream, SampleFileId);
            PrimitiveArrays.WriteOneValue(stream, PeakArea);
            RetentionTimesByFileId.Write(stream);
            PrimitiveArrays.WriteOneValue(stream, TransitionAreas.Count);
            PrimitiveArrays.Write(stream, TransitionAreas.Select(mi => mi.Mz).ToArray());
            PrimitiveArrays.Write(stream, TransitionAreas.Select(mi=>mi.Intensity).ToArray());
            var hasAnnotations = TransitionAreas.Any(mi => mi.Annotations != null && mi.Annotations.Count != 0);
            PrimitiveArrays.WriteOneValue(stream, hasAnnotations ? TransitionAreas.Count : 0);
            if (hasAnnotations)
            {
                foreach (var mi in TransitionAreas)
                {
                    PrimitiveArrays.WriteString(stream, (mi.Annotations == null || mi.Annotations.Count == 0) ?
                        null :
                        mi.Annotations.First().Ion.ToSerializableString());
                }
            }
            PrimitiveArrays.WriteString(stream, Protein);
        }

        public static ChromLibSpectrumInfo Read(ValueCache valueCache, Stream stream)
        {
            LibKey key = LibKey.Read(valueCache, stream);
            int id = PrimitiveArrays.ReadOneValue<int>(stream);
            int sampleFileId = PrimitiveArrays.ReadOneValue<int>(stream);
            double peakArea = PrimitiveArrays.ReadOneValue<double>(stream);
            var retentionTimesByFileId = IndexedRetentionTimes.Read(stream);
            int mzCount = PrimitiveArrays.ReadOneValue<int>(stream);
            var mzs = PrimitiveArrays.Read<double>(stream, mzCount);
            var areas = PrimitiveArrays.Read<float>(stream, mzCount);
            var annotationsCount = PrimitiveArrays.ReadOneValue<int>(stream);
            var annotations = annotationsCount > 0 ? new List<List<SpectrumPeakAnnotation>>() : null;
            if (annotations != null)
            {
                for (var a = 0; a < annotationsCount; a++)
                {
                    var ionString = PrimitiveArrays.ReadString(stream);
                    var annotation = string.IsNullOrEmpty(ionString)
                        ? null
                        : new List<SpectrumPeakAnnotation>
                            {SpectrumPeakAnnotation.Create(CustomIon.FromSerializableString(ionString), null)};
                    annotations.Add(annotation);
                }
            }
            var mzAreas = ImmutableList.ValueOf(Enumerable.Range(0, mzCount)
                .Select(index => new SpectrumPeaksInfo.MI // TODO (bspratt) annotation?
                {
                    Mz = mzs[index],
                    Intensity = areas[index],
                    Annotations = annotations?[index]
                }));
            var protein = PrimitiveArrays.ReadString(stream);
            return new ChromLibSpectrumInfo(key, id, sampleFileId, peakArea, retentionTimesByFileId, mzAreas, protein);
        }
    }
}
