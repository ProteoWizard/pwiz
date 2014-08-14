/*
 * Original author: Jarrett Egertson <jegertso .at .u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results
{
    interface IDemultiplexer
    {
        MsDataSpectrum[] GetDeconvolvedSpectra(int index, MsDataSpectrum originalSpectrum);
    }

    public interface IIsoWinMapper
    {
        int NumWindows { get; }
        int NumDeconvRegions { get; }
        int Add(IEnumerable<MsPrecursor> precursors, SpectrumFilter filter);
        bool TryGetWindowIndex(double isolationWindow, out int index);
        IsoWin GetIsolationWindow(int isoIndex);
        MsPrecursor GetPrecursor(int isoIndex);
        bool TryGetWindowFromMz(double mz, out int windowIndex);
        bool TryGetDeconvFromMz(double mz, out int windowIndex);
    }

    /// <summary>
    /// Comparer for sorting and binary search of key-value pair list by key only.
    /// </summary>
    public class KvpKeyComparer : IComparer<KeyValuePair<double, int>>
    {
        public int Compare(KeyValuePair<double, int> x, KeyValuePair<double, int> y)
        {
            return x.Key.CompareTo(y.Key);
        }
    }

    /// <summary>
    /// A method of hashing an isolation window to a unique long value
    /// isolationCenter is the m/z of the center of the isolation window,
    /// this value is multiplied by 100000000 and rounded to convert the
    /// isolation m/z to a long which is used as the hash.
    /// For example: a window with m/z 475.235 would get hashed to 47523500000
    /// </summary>
    public sealed class IsoWindowHasher
    {
        /// <param name="isolationCenter"></param>
        /// <returns>The hashed isolation window center</returns>
        public static long Hash(double isolationCenter)
        {
            return (long)Math.Round(isolationCenter * 100000000.0);
        }

        public static double UnHash(long hashed)
        {
            return hashed/100000000.0;
        }
    }
}
