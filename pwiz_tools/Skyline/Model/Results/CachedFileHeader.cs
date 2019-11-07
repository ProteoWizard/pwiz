/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;

namespace pwiz.Skyline.Model.Results
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CachedFileHeaderStruct
    {
        public Int64 modified;
        public int lenPath;
        // Version 3 file header addition
        public Int64 runstart;
        // Version 4 file header addition
        public int lenInstrumentInfo;
        // Version 5 file header addition
        public ChromCachedFile.FlagValues flags;
        // Version 6 file header addition
        public float maxRetentionTime;
        public float maxIntensity;
        // Version 9 file header addition
        public int sizeScanIds;
        public long locationScanIds;
        // Version 13 file header addition
        public float ticArea;
        // Version 14 file header addition
        public int lenSampleId;
        public int lenSerialNumber;

        // ReSharper disable LocalizableElement
        public static int GetStructSize(CacheFormatVersion formatVersion)
        {
            if (formatVersion >= CacheFormatVersion.Fourteen)
            {
                return 60;
            }
            if (formatVersion >= CacheFormatVersion.Thirteen)
            {
                return 52;
            }
            if (formatVersion > CacheFormatVersion.Eight)
            {
                return 48;
            }
            else if (formatVersion > CacheFormatVersion.Five)
            {
                return 36;
            }
            else if (formatVersion > CacheFormatVersion.Four)
            {
                return 28;
            }
            else if (formatVersion > CacheFormatVersion.Three)
            {
                return 24;
            }
            else if (formatVersion > CacheFormatVersion.Two)
            {
                return 20;
            }
            else
            {
                return 12;
            }
        }
        // ReSharper restore LocalizableElement
    }
}
