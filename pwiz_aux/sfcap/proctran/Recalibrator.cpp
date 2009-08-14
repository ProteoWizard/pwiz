//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#include "Recalibrator.hpp"
#include <algorithm>
#include <stdexcept>


namespace pwiz {
namespace pdanalysis {


using namespace std;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;


namespace {

class RecalibratePeak 
{
    public:

    RecalibratePeak(const CalibrationParameters& cp) : cp_(cp) {}
    
    void operator()(Peak& peak)
    {
        peak.mz = cp_.mz(peak.frequency);
    }

    private:

    CalibrationParameters cp_;
};

class RecalibratePeakFamily
{
    public:

    RecalibratePeakFamily(const CalibrationParameters& cp) : cp_(cp) {}

    void operator()(PeakFamily& peakFamily)
    {
        for_each(peakFamily.peaks.begin(), peakFamily.peaks.end(), RecalibratePeak(cp_));
        peakFamily.mzMonoisotopic = !peakFamily.peaks.empty() ? peakFamily.peaks[0].mz : 0;
    }

    private:

    CalibrationParameters cp_;
};

} // namespace


void Recalibrator::recalibrate(Scan& scan) const
{
    try
    {
        CalibrationParameters cp = calculateCalibrationParameters(scan);
        scan.calibrationParameters = cp;
        for_each(scan.peakFamilies.begin(), scan.peakFamilies.end(), RecalibratePeakFamily(cp));    
    }
    catch (exception& e)
    {
        // calculateCalibrationParameters() may throw; report problem and don't do recalibration
        cerr << e.what() << endl;
    }
}



} // namespace pdanalysis 
} // namespace pwiz

