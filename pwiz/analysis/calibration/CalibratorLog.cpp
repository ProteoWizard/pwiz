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


#include "CalibratorLog.hpp"
#include "Calibrator.hpp"
#include "data/CalibrationParameters.hpp"
#include "MassSpread.hpp"
#include "Ion.hpp"
#include "msaux/Path.h"
#include <iostream>
#include <fstream>
#include <iomanip>


namespace pwiz {
namespace calibration {


using namespace std;


class CalibratorLogImpl : public CalibratorLog
{
    public:

    CalibratorLogImpl(const Calibrator* calibrator,
                      const string& outputDirectory,
                      const vector<double>* trueMasses,
                      const CalibrationParameters* trueParameters);

    virtual void outputState();

    private:

    const Calibrator* calibrator_;
    const string outputDirectory_;
    const vector<double>* trueMasses_;
    const CalibrationParameters* trueParameters_;

    ofstream osOutput_;
    ofstream osSummary_;

    static const int columnWidth_ = 15;
};


auto_ptr<CalibratorLog> CalibratorLog::create(const Calibrator* calibrator,
                                              const string& outputDirectory,
                                              const vector<double>* trueMasses,
                                              const CalibrationParameters* trueParameters)
{
    return auto_ptr<CalibratorLog>(new CalibratorLogImpl(calibrator,
                                                         outputDirectory,
                                                         trueMasses,
                                                         trueParameters));
}


CalibratorLogImpl::CalibratorLogImpl(const Calibrator* calibrator,
                                     const string& outputDirectory,
                                     const vector<double>* trueMasses,
                                     const CalibrationParameters* trueParameters)
:   calibrator_(calibrator),
    outputDirectory_(outputDirectory),
    trueMasses_(trueMasses),
    trueParameters_(trueParameters)
{
    Path::mkdir(outputDirectory);
    cout << "[CalibratorLog] Output directory: " << outputDirectory << endl;

    string filenameOutput = outputDirectory + "/CalibratorLog.output.txt";
    cout << "Creating file " << filenameOutput << endl;
    osOutput_.open(filenameOutput.c_str());
    osOutput_ << fixed << setprecision(7);
    
    string filenameSummary = outputDirectory + "/CalibratorLog.summary.txt";
    cout << "Creating file " << filenameSummary << endl;
    osSummary_.open(filenameSummary.c_str());
    osSummary_ << fixed << setprecision(7);
    osSummary_ << 
        "#          A                  B        trueError estError  calError   correct  confident\n"; 
}


void CalibratorLogImpl::outputState()
{
    osOutput_ << "\nCalibrator iterations completed: " << calibrator_->iterationCount() << endl << endl;

    const CalibrationParameters& p = calibrator_->parameters();
    osOutput_ << "A: " << p.A << endl;
    osOutput_ << "B: " << p.B << endl << endl;

    osOutput_ <<
        setw(columnWidth_) <<  "mass_true" <<
        setw(3) <<  "z" <<
        setw(columnWidth_) << "f_obs" <<
        setw(columnWidth_) << "mass_calc" <<
        setw(columnWidth_) << "error (ppm)" <<
        setw(3) << "id" <<
        "  distribution" << endl;

    double totalSquaredMassError = 0;
    double totalSquaredCalibrationError = 0;
    int totalCorrect = 0;
    int totalConfident = 0;

    int N = calibrator_->measurementCount();
    for (int i=0; i<N; i++)
    {
        // calculations

        double f = calibrator_->measurement(i)->frequency;
        int z = calibrator_->measurement(i)->charge;
        double mz = p.mz(f);
        double massCalculated = Ion::neutralMass(mz, z); 

        double massTrue = 0;
        double error = 0;
        bool correct = false;
        bool confident = false;

        if (trueMasses_)
        {
            // error calculations
            massTrue = trueMasses_->at(i);
            error = (massCalculated - massTrue)/massTrue;
            totalSquaredMassError += error*error;

            if (trueParameters_)
            {
                double term1 = fabs(p.A - trueParameters_->A)/f;
                double term2 = fabs(p.B - trueParameters_->B)/(f*f);
                double deviation = (term1 + term2)/massTrue;  
                totalSquaredCalibrationError += deviation*deviation; 
            }
           
            // id verification
            if (calibrator_->massSpread(i) && !calibrator_->massSpread(i)->distribution().empty())
            {
                const MassSpread::Pair& id = calibrator_->massSpread(i)->distribution()[0];
                if (fabs(id.mass-massTrue) < 1e-8)
                {
                    correct = true;
                    totalCorrect++;
                    if (id.probability > .95)
                    {
                        confident = true;
                        totalConfident++;
                    }
                }
            }
        }

        // output

        osOutput_ <<
            setw(columnWidth_) << massTrue <<
            setw(3) << z <<
            setw(columnWidth_) << f <<
            setw(columnWidth_) << massCalculated <<
            setw(columnWidth_) << error * 1e6 << 
            setw(2) << (correct ? "*" : " ") <<
            (confident ? "!" : " ") << "  ";

        if (calibrator_->massSpread(i))
            calibrator_->massSpread(i)->output(osOutput_);
        osOutput_ << endl;
    }

    double rmsMassError = sqrt(totalSquaredMassError / N); 
    double rmsCalibrationError = sqrt(totalSquaredCalibrationError / N); 

    osOutput_ << "\nTotal error: " << calibrator_->error() * 1e6 << " ppm\n\n";

    // update summary
    osSummary_ << calibrator_->iterationCount() << " " <<
        p.A << " " <<
        p.B << " " <<
        rmsMassError * 1e6 << " " <<
        calibrator_->error() * 1e6 << " " <<
        rmsCalibrationError * 1e6 << " " <<
        (double)totalCorrect/N << " " <<
        (double)totalConfident/N << endl;
}

} // namespace calibration
} // namespace pwiz

