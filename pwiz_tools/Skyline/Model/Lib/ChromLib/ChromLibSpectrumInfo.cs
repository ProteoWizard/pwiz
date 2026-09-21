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
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    public class ChromLibSpectrumInfo : ICachedSpectrumInfo
    {
        public ChromLibSpectrumInfo(LibKey key, int id, int sampleFileId, double peakArea, IndexedMultiArray<float> retentionTimesByFileIndex, IonMobilityAndCCS ionMobility, IEnumerable<SpectrumPeaksInfo.MI> transitionAreas, string protein)
        {
            Key = key;
            Id = id;
            SampleFileId = sampleFileId;
            PeakArea = peakArea;
            RetentionTimesByFileIndex = retentionTimesByFileIndex;
            IonMobility = ionMobility ?? IonMobilityAndCCS.EMPTY;
            TransitionAreas = ImmutableList.ValueOf(transitionAreas) ?? ImmutableList.Empty<SpectrumPeaksInfo.MI>();
            Protein = protein ?? string.Empty;
        }
        public LibKey Key { get; private set; }
        public int Id { get; private set; }
        public int SampleFileId { get; private set; }
        public double PeakArea { get; private set; }
        public string Protein { get; private set; } // Some .clib files provide a protein accession (or Molecule List Name for small molecules)
        public IndexedMultiArray<float> RetentionTimesByFileIndex { get; private set; }
        public IonMobilityAndCCS IonMobility { get; private set; }
        public IList<SpectrumPeaksInfo.MI> TransitionAreas { get; private set; }
        public void Write(Stream stream, IList<int> fileIds)
        {
            Key.Write(stream);
            PrimitiveArrays.WriteOneValue(stream, Id);
            PrimitiveArrays.WriteOneValue(stream, SampleFileId);
            PrimitiveArrays.WriteOneValue(stream, PeakArea);
            WriteRetentionTimes(stream, RetentionTimesByFileIndex, fileIds);
            IonMobility.Write(stream);
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

        public static ChromLibSpectrumInfo Read(ValueCache valueCache, Stream stream, IDictionary<int, int> fileIndexesById)
        {
            LibKey key = LibKey.Read(valueCache, stream);
            int id = PrimitiveArrays.ReadOneValue<int>(stream);
            int sampleFileId = PrimitiveArrays.ReadOneValue<int>(stream);
            double peakArea = PrimitiveArrays.ReadOneValue<double>(stream);
            var retentionTimesByFileIndex = ReadRetentionTimes(stream, fileIndexesById);
            var ionMobility = IonMobilityAndCCS.Read(stream);
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
            return new ChromLibSpectrumInfo(key, id, sampleFileId, peakArea, retentionTimesByFileIndex, ionMobility, mzAreas, protein);
        }

        /// <summary>
        /// Writes the times of each file, identified by its database id rather than by its index,
        /// so that the cache format does not depend on the order of the sample files.
        /// </summary>
        private static void WriteRetentionTimes(Stream stream, IndexedMultiArray<float> times, IList<int> fileIds)
        {
            int fileCount = 0;
            for (int fileIndex = 0; fileIndex < times.Count; fileIndex++)
            {
                if (times.GetCount(fileIndex) > 0)
                {
                    fileCount++;
                }
            }

            PrimitiveArrays.WriteOneValue(stream, fileCount);
            for (int fileIndex = 0; fileIndex < times.Count; fileIndex++)
            {
                int count = times.GetCount(fileIndex);
                if (count == 0)
                {
                    continue;
                }

                PrimitiveArrays.WriteOneValue(stream, fileIds[fileIndex]);
                PrimitiveArrays.WriteOneValue(stream, count);
                PrimitiveArrays.Write(stream, times[fileIndex].ToArray());
            }
        }

        private static IndexedMultiArray<float> ReadRetentionTimes(Stream stream, IDictionary<int, int> fileIndexesById)
        {
            int fileCount = PrimitiveArrays.ReadOneValue<int>(stream);
            var timesByFileIndex = new List<KeyValuePair<int, float>>();
            for (int i = 0; i < fileCount; i++)
            {
                int fileId = PrimitiveArrays.ReadOneValue<int>(stream);
                int timeCount = PrimitiveArrays.ReadOneValue<int>(stream);
                var times = PrimitiveArrays.Read<float>(stream, timeCount);
                if (!fileIndexesById.TryGetValue(fileId, out int fileIndex))
                {
                    continue;
                }

                foreach (var time in times)
                {
                    timesByFileIndex.Add(new KeyValuePair<int, float>(fileIndex, time));
                }
            }

            return timesByFileIndex.ToIndexedMultiArray();
        }
    }
}
