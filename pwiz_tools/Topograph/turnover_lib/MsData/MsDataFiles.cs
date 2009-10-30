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
using System.Linq;
using System.Text;
using pwiz.ProteowizardWrapper;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public static class MsDataFileUtil
    {
        public static ChromatogramPoint GetIntensity(MzRange mzRange, double[] mzs, double[] intensities)
        {
            int imin = ClosestIndex(mzRange.Min, mzs);
            int imax = ClosestIndex(mzRange.Max, mzs);
            double intensity = 0;
            double peakMz = 0;
            for (int index = imin; index <= imax; index++)
            {
                var newIntensity = intensity + intensities[index];
                peakMz = (mzs[index]*intensities[index] + peakMz*intensity)/newIntensity;
                intensity = newIntensity;
            }
            return new ChromatogramPoint {Intensity = intensity, PeakMz = peakMz};
        }
        private static int ClosestIndex(double mz, double[] mzs)
        {
            int index = Array.BinarySearch(mzs, mz);
            if (index < 0)
            {
                index = ~index;
            }
            if (index >= mzs.Length)
            {
                index--;
            }
            if (index > 0 && mz - mzs[index - 1] < mzs[index] - mz)
            {
                index--;
            }
            return index;
        }

        public static bool InitMsDataFile(Workspace workspace, MsDataFile msDataFile)
        {
            String error;
            return TryInitMsDataFile(workspace, msDataFile, msDataFile.Path, out error);
        }

        public static bool TryInitMsDataFile(Workspace workspace, MsDataFile msDataFile, String path, out String message)
        {
            if (path == null)
            {
                message = "Location of data file has never been specified.";
                return false;
            }
            if (!File.Exists(path))
            {
                message = "File does not exist.";
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
                    msDataFile.Init(path, cMsDataFile);
                    using (var session = workspace.OpenWriteSession())
                    {
                        session.BeginTransaction();
                        msDataFile.MsDataFileData.Save(session);
                        session.Transaction.Commit();
                        message = "Success";
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Console.Out.Write(exception);
                message =
                    "An exception occurred while trying to open the file.  Either the file is corrupted, or the necessary libraries to read this file type have not been installed.";
                return false;
            }
        }
        public static double GetFullWidthHalfMax(double mz)
        {
            return mz * mz / 400 /50000;
        }
        public class ChromatogramPoint
        {
            public double Intensity { get; set; }
            public double PeakMz { get; set; }
        }
    }
}
