//
// $Id: $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using IDPicker;
using pwiz.CLI.cv;
using pwiz.CLI.msdata;

namespace IDPicker
{
    public static class QuantifyingTransmogrifier
    {
        private static bool inTolerance(double mz1, double mz2, double tolerance)
        {
            return Math.Abs(mz1-mz2) < tolerance;
        }

        public static void quantify (Workspace ws, string rootInputDirectory, QuantitationInfo.Method method)
        {
            if (method == QuantitationInfo.Method.None)
                return;

            ws.groups.assembleParentGroups();
            foreach (SourceInfo source in ws.groups["/"].getSources(true))
            {
                source.filepath = Util.FindSourceInSearchPath(source.name, rootInputDirectory);
                using (MSDataFile msd = new MSDataFile(source.filepath))
                using (pwiz.CLI.msdata.SpectrumList sl = msd.run.spectrumList)
                {
                    foreach (SpectrumInfo si in source.spectra.Values)
                    {
                        int index = sl.find(si.nativeID);
                        if (index == sl.size())
                            throw new KeyNotFoundException("spectrum \"" + si.nativeID + "\" not found in " + source.filepath);

                        si.quantitation = new QuantitationInfo()
                        {
                            method = method,
                            ITRAQ_113_intensity = 0,
                            ITRAQ_114_intensity = 0,
                            ITRAQ_115_intensity = 0,
                            ITRAQ_116_intensity = 0,
                            ITRAQ_117_intensity = 0,
                            ITRAQ_118_intensity = 0,
                            ITRAQ_119_intensity = 0,
                            ITRAQ_121_intensity = 0
                        };

                        const double tolerance = 0.5; // TODO: make user configurable?

                        pwiz.CLI.msdata.Spectrum s = sl.spectrum(index, true);
                        BinaryData mzData = s.getMZArray().data;
                        BinaryData intensityData = s.getIntensityArray().data;

                        if (method == QuantitationInfo.Method.ITRAQ4Plex)
                            for (int i = 0; i < mzData.Count; ++i)
                            {
                                if (mzData[i] < 113) continue;
                                else if (inTolerance(mzData[i], 114, tolerance)) si.quantitation.ITRAQ_114_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 115, tolerance)) si.quantitation.ITRAQ_115_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 116, tolerance)) si.quantitation.ITRAQ_116_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 117, tolerance)) si.quantitation.ITRAQ_117_intensity += intensityData[i];
                                else if (mzData[i] > 117) break;
                            }
                        else if(method == QuantitationInfo.Method.ITRAQ8Plex)
                            for (int i = 0; i < mzData.Count; ++i)
                            {
                                if (mzData[i] < 112) continue;
                                else if (inTolerance(mzData[i], 113, tolerance)) si.quantitation.ITRAQ_113_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 114, tolerance)) si.quantitation.ITRAQ_114_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 115, tolerance)) si.quantitation.ITRAQ_115_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 116, tolerance)) si.quantitation.ITRAQ_116_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 117, tolerance)) si.quantitation.ITRAQ_117_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 118, tolerance)) si.quantitation.ITRAQ_118_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 119, tolerance)) si.quantitation.ITRAQ_119_intensity += intensityData[i];
                                else if (inTolerance(mzData[i], 121, tolerance)) si.quantitation.ITRAQ_121_intensity += intensityData[i];
                                else if (mzData[i] > 121) break;
                            }
                    }
                }
            }
        }
    }
}