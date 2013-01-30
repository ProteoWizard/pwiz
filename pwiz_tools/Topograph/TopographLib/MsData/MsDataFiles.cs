/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.MsData
{
    public static class MsDataFileUtil
    {
        /// <summary>
        /// Returns the ChromatogramPoint which has the intensities of the points
        /// closest to the specified MzRange.  The intensities are assumed to be
        /// from a centroided spectrum, and the mzRange is much smaller than the
        /// machine resolution.
        /// </summary>
        public static ChromatogramPoint GetPoint(MzRange mzRange, double[] mzs, double[] intensities)
        {
            if (mzs.Length == 0)
            {
                return new ChromatogramPoint();
            }
            int imin = ClosestIndex(mzRange.Min - .1, mzs);
            int imax = ClosestIndex(mzRange.Max + .1, mzs);
            var values = new List<KeyValuePair<float, float>>();
            for (int i = imin; i <= imax; i++)
            {
                values.Add(new KeyValuePair<float, float>((float)mzs[i], (float)intensities[i]));
            }
            return new ChromatogramPoint(values.ToArray());
        }

        private static int ClosestIndex(double key, double[] array)
        {
            var index = Array.BinarySearch(array, key);
            if (index < 0)
            {
                index = ~index;
            }
            if (index >= array.Length)
            {
                index--;
            }
            if (index > 0 && key - array[index - 1] < array[index] - key)
            {
                index--;
            }
            return index;
        }

        public static bool InitMsDataFile(Workspace workspace, MsDataFile msDataFile)
        {
            String error;
            return TryInitMsDataFile(workspace, msDataFile, CancellationToken.None, out error);
        }

        public static bool TryInitMsDataFile(Workspace workspace, MsDataFile msDataFile, CancellationToken cancellationToken, out String message)
        {
            if (string.IsNullOrEmpty(workspace.GetDataDirectory()))
            {
                message = "Workspace data directory has not been specified";
                return false;
            }
            var path = workspace.GetDataFilePath(msDataFile.Name);
            if (path == null)
            {
                message = "File could not be found";
                return false;
            }
            if (msDataFile.HasTimes())
            {
                message = "File exists and has been read before.";
                return true;
            }
            try
            {
                using(var cMsDataFile = new MsDataFileImpl(path))
                {
                    msDataFile.Init(cancellationToken, cMsDataFile);
                    using (var session = workspace.OpenWriteSession())
                    {
                        session.BeginTransaction();
                        msDataFile.SaveBinary(session);
                        session.Transaction.Commit();
                        message = "Success";
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                ErrorHandler.LogException("MsDataFiles", "Exception trying to open " + path, exception);
                message =
                    "An exception occurred while trying to open the file.  Either the file is corrupted, or the necessary libraries to read this file type have not been installed.";
                return false;
            }
        }
    }

    public class ChromatogramPoint
    {
        public ChromatogramPoint()
        {
            Values = new KeyValuePair<float, float>[0];
        }
        public ChromatogramPoint(KeyValuePair<float,float>[] values)
        {
            Values = values;
        }
        public IList<KeyValuePair<float,float>> Values { get; private set;}
        public double GetIntensity(MzRange mzRange, double massAccuracy)
        {
            double result = 0;
            foreach (var entry in Values)
            {
                if (mzRange.ContainsWithMassAccuracy(entry.Key, massAccuracy))
                {
                    result += entry.Value;
                }
            }
            return result;
        }
        private static void Write<T>(Stream stream, T value)
        {
            var array = new[] {value};
            var bytes = new byte[Buffer.ByteLength(array)];
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
        private static T Read<T>(Stream stream)
        {
            var array = new T[1];
            var bytes = new byte[Buffer.ByteLength(array)];
            stream.Read(bytes, 0, bytes.Length);
            Buffer.BlockCopy(bytes, 0, array, 0, bytes.Length);
            return array[0];
        }
            
        public void Write(Stream stream)
        {
            Write(stream, Values.Count);
            foreach (var value in Values)
            {
                Write(stream, value.Key);
                Write(stream, value.Value);
            }
        }
        public static ChromatogramPoint Read(Stream stream)
        {
            int count = Read<int>(stream);
            var values = new KeyValuePair<float,float>[count];
            for (int i = 0; i < count; i++)
            {
                var key = Read<float>(stream);
                var value = Read<float>(stream);
                values[i] = new KeyValuePair<float, float>(key, value);
            }
            return new ChromatogramPoint(values);
        }

        public static byte[] ToByteArray(IList<ChromatogramPoint> points)
        {
            var stream = new MemoryStream();
            foreach (var point in points)
            {
                point.Write(stream);
            }
            return stream.ToArray();
        }
        public static IList<ChromatogramPoint> FromByteArray(byte[] bytes)
        {
            var stream = new MemoryStream(bytes);
            var result = new List<ChromatogramPoint>();
            while (stream.Position < stream.Length)
            {
                result.Add(Read(stream));
            }
            return result;
        }
    }
}
