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


#include "Model.hpp"
#include "TransientData.hpp"
#include "pwiz/data/misc/FrequencyData.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/analysis/frequency/FrequencyEstimatorPhysicalModel.hpp"
#include "pwiz/utility/proteome/Peptide.hpp"
#include "pwiz/utility/proteome/IsotopeCalculator.hpp"
#include "pwiz/data/misc/CalibrationParameters.hpp"
#include "pwiz/utility/misc/Timer.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>


using namespace pwiz::proteome;
using namespace pwiz::data;
using namespace std;
using namespace pwiz::id::model;


const double A_ = 1.075339687500000e+008;
const double B_ = -3.454602661132810e+008;
const double T_ = .768;
const int sampleCount_ = 1048576;


class DecayingSinusoid
{
    public:

    DecayingSinusoid(double A, double tau, double f, double phi) // TODO: reorder
    :   A_(A), tau_(tau), f_(f), phi_(phi)
    {}

    double operator()(double t) const
    {
        return A_ * exp(-t/tau_) * cos(2.*M_PI*f_*t + phi_);
    }

    private:

    double A_;      // amplitude
    double tau_;    // decay constant
    double f_;      // frequency
    double phi_;    // phase
};


class CombinedSignal : public TransientData::Signal
{
    public:

    void push_back(DecayingSinusoid signal) {signals_.push_back(signal);}

    virtual double operator()(double t) const
    {
        double value = 0;
        for (vector<DecayingSinusoid>::const_iterator it=signals_.begin(); 
             it!=signals_.end(); ++it) 
            value += (*it)(t);
        return value;
    }

    private:

    vector<DecayingSinusoid> signals_;
};


void testInteraction(double separation)
{
    double f1 = 100000/.768;
    double f2 = f1 + separation;

    CombinedSignal signal;
    const double A = 1;
    const double tau = .768;
    signal.push_back(DecayingSinusoid(A, tau, f1, 0)); 
    signal.push_back(DecayingSinusoid(A, tau, f2, 0)); 

    TransientData td;
    td.observationDuration(T_);
    td.A(A_);
    td.B(B_);
    td.data().resize(sampleCount_);
    td.add(signal);

    FrequencyData fd;
    td.computeFFT(1, fd);

    vector<PeakInfo> peaks;
    peaks.push_back(PeakInfo(f1));
    peaks.push_back(PeakInfo(f2));

    int windowRadius = 10;
    int iterationCount = 20;
    auto_ptr<FrequencyEstimatorPhysicalModel> estimator = 
        FrequencyEstimatorPhysicalModel::create(windowRadius, iterationCount);
                                                       
    vector<PeakInfo> estimatedPeaks;
    estimator->estimateFrequencies(fd, peaks, estimatedPeaks, ""); 
    if (estimatedPeaks.size() != 2) throw runtime_error("not happening");

    cout << setprecision(2) << fixed 
         << setw(10) << separation
         << setw(15) << setprecision(8) << estimatedPeaks[0].frequency - f1 << " " 
         << setw(15) << setprecision(8) << estimatedPeaks[1].frequency - f2 << " " 
         << endl;
}


void interactionExperiment()
{
    for (double i=10; i<=100; i+=1)
    //for (double i=10; i<=1000; i+=10)
    //for (double i=10000; i<=10010; i+=.10)
        testInteraction(i);
}


class SinusoidSignal : public TransientData::Signal
{
    public:

    virtual double operator()(double t) const
    {
        return cos(2.*M_PI*t/T_);
    }
};


void sinusoidTest()
{
    TransientData td;
    td.observationDuration(T_);
    td.A(A_);
    td.B(B_);
    td.data().resize(sampleCount_);
    SinusoidSignal signal;
    td.add(signal);
    td.write("sinusoid.dat");

    FrequencyData fd;
    td.computeFFT(1, fd);
    fd.write("sinusoid.cfd");
}


int main(int argc, char* argv[])
{
    try
    {
        //interactionExperiment();   
        sinusoidTest(); 
        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}
