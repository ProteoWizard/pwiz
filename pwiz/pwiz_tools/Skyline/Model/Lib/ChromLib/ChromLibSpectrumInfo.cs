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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    public class ChromLibSpectrumInfo : ICachedSpectrumInfo, IComparable
    {
        public ChromLibSpectrumInfo(LibKey key, int id, double peakArea, IndexedRetentionTimes retentionTimesByFileId, IEnumerable<SpectrumPeaksInfo.MI> transitionAreas)
        {
            Key = key;
            Id = id;
            PeakArea = peakArea;
            RetentionTimesByFileId = retentionTimesByFileId;
            TransitionAreas = ImmutableList.ValueOf(transitionAreas) ?? ImmutableList.Empty<SpectrumPeaksInfo.MI>();
        }
        public LibKey Key { get; private set; }
        public int Id { get; private set; }
        public double PeakArea { get; private set; }
        public IndexedRetentionTimes RetentionTimesByFileId { get; private set; }
        public IList<SpectrumPeaksInfo.MI> TransitionAreas { get; private set; }
        public int CompareTo(object obj)
        {
            if (null == obj)
            {
                return 1;
            }
            return Key.Compare(((ChromLibSpectrumInfo) obj).Key);
        }
        public void Write(Stream stream)
        {
            Key.Write(stream);
            PrimitiveArrays.WriteOneValue(stream, Id);
            PrimitiveArrays.WriteOneValue(stream, PeakArea);
            RetentionTimesByFileId.Write(stream);
            PrimitiveArrays.WriteOneValue(stream, TransitionAreas.Count);
            PrimitiveArrays.Write(stream, TransitionAreas.Select(mi => mi.Mz).ToArray());
            PrimitiveArrays.Write(stream, TransitionAreas.Select(mi=>mi.Intensity).ToArray());
        }

        public static ChromLibSpectrumInfo Read(Stream stream)
        {
            LibKey key = LibKey.Read(stream);
            int id = PrimitiveArrays.ReadOneValue<int>(stream);
            double peakArea = PrimitiveArrays.ReadOneValue<double>(stream);
            var retentionTimesByFileId = IndexedRetentionTimes.Read(stream);
            int mzCount = PrimitiveArrays.ReadOneValue<int>(stream);
            var mzs = PrimitiveArrays.Read<double>(stream, mzCount);
            var areas = PrimitiveArrays.Read<float>(stream, mzCount);
            var mzAreas = ImmutableList.ValueOf(Enumerable.Range(0, mzCount)
                .Select(index => new SpectrumPeaksInfo.MI
                    {
                        Mz = mzs[index],
                        Intensity = areas[index]
                    }));
            return new ChromLibSpectrumInfo(key, id, peakArea, retentionTimesByFileId, mzAreas);
        }
    }
}
