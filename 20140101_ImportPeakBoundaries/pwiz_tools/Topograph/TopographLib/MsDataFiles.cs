using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProteowizardWrapper;
using turnover.Data;
using turnover.Model;

namespace turnover
{
    public static class MsDataFileUtil
    {
        public const double MACHINE_RESOLUTION = 50000;
        public const double MASS_ACCURACY = 200000;
        public static ChromatogramPoint GetIntensity(double mz, double[] mzs, double[] intensities)
        {
            int imid = Array.BinarySearch(mzs, mz);
            if (imid < 0)
            {
                imid = ~imid;
            }
            imid = Math.Max(imid, 2);
            imid = Math.Min(imid, mzs.Length - 2);
            if (intensities[imid - 1] > intensities[imid])
            {
                imid--;
            }
            if (intensities[imid - 1] > intensities[imid] || intensities[imid + 1] > intensities[imid])
            {
                if (intensities[imid - 1] > intensities[imid + 1])
                {
                    while (imid > 0 && intensities[imid - 1] > intensities[imid])
                    {
                        imid--;
                    }
                }
                else
                {
                    while (imid < intensities.Length-1 && intensities[imid + 1] > intensities[imid])
                    {
                        imid++;
                    }
                }
                return new ChromatogramPoint
                {
                    PeakMz = mzs[imid],
                    Intensity = intensities[imid]
                };
            }
            int iNext;
            if (intensities[imid - 1] > intensities[imid + 1])
            {
                iNext = imid - 1;
            }
            else
            {
                iNext = imid + 1;
            }
            double gaussConst = 8*Math.Log(2);
            double fwhm = GetFullWidthHalfMax(mz);
            double mzPeak = fwhm*fwhm*Math.Log(intensities[imid]/intensities[iNext]);
            mzPeak /= gaussConst*(mzs[imid] - mzs[iNext]);
            mzPeak += (mzs[imid] + mzs[iNext])/2;
            double intensity = intensities[imid]/Math.Exp(-Math.Pow((mzs[imid] - mzPeak)/fwhm, 2)*gaussConst);
            if (double.IsNaN(intensity) || Math.Abs(intensity - intensities[imid]) > intensities[imid] / 10)
            {
                intensity = intensities[imid];
            }
            return new ChromatogramPoint
                       {
                           Intensity = intensity,
                           PeakMz = mzPeak
                       };
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
                        msDataFile.Save(session);
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
