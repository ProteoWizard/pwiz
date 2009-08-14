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


#include "ParameterEstimator.hpp"
#include "TruncatedLorentzian.hpp"
#include "TruncatedLorentzianParameters.hpp"
#include "Parabola.hpp"
#include <iostream>
#include <fstream>
#include <vector>
#include <iterator>
#include <iomanip>
#include <sstream>


using namespace std;
using namespace pwiz::peaks;


namespace {

double interp(double x1, double y1, double x2, double y2, double x)
{
    return y1 + (x-x1)*(y2-y1)/(x2-x1);
}


double phase(const FrequencyDatum& datum)
{
    return atan2(datum.y.imag(),datum.y.real());
}


double phase(const ublas::vector<double>& p)
{
    return atan2(p(TruncatedLorentzian::AlphaI), p(TruncatedLorentzian::AlphaR));
}

} // namespace


class Main
{
    public:

    Main(int argc, char* argv[]);
    int run();

    private:

    FrequencyData fd_;

    string dataFilename_;
    int iterationCount_;

    double T_;
    double tau_;
    double f0_;
    complex<double> alpha_;

    Parabola parabola_;

    void calculateInitialEstimate();
    void getF0();
    void getTau();
    void getAlpha();

    ParameterEstimator::Parameters initialEstimate() const;
    void printStatus(const ParameterEstimator& estimator) const;
};


Main::Main(int argc, char* argv[])
:   iterationCount_(0),
    T_(.384),
    tau_(1),
    f0_(0),
    alpha_(1)
{
    cout.precision(10);

    // handle command line

    bool doInitialEstimate = false;

    if (argc == 8)
    {
        tau_ = atof(argv[4]);
        f0_ = atof(argv[5]);
        alpha_ = polar(atof(argv[6]), atof(argv[7]));
    }
    else if (argc == 4)
    {
        doInitialEstimate = true;
    }
    else
    {
        throw runtime_error("Usage: EstimatePeakParameters datafile iterationCount T [tau f0 amplitude phase]");
    }

    dataFilename_ = argv[1];
    iterationCount_ = atoi(argv[2]);
    T_ = atof(argv[3]);


    // read in data and do initial estimate (if necessary)

    cout << "Reading data from " << dataFilename_ << endl << endl;
    fd_.read(dataFilename_);

    if (doInitialEstimate)
        calculateInitialEstimate();

    fd_.normalize();
}


void Main::calculateInitialEstimate()
{
    getF0();
    getTau();
    getAlpha();
    cout << endl;
}


void Main::getF0()
{
    FrequencyData::const_iterator max = fd_.max();

    if (fd_.data().begin()+1>max || max+2>fd_.data().end())
        throw runtime_error("No buffer around max.\n");

    vector< pair<double,double> > samples;
    transform(max-1, max+2, back_inserter(samples), FrequencyData::magnitudeSample);

    // fit parabola to the 3 highest points

    parabola_ = Parabola(samples);
    f0_ = parabola_.center();


    cout << "Parabola fit check:\n";
    for (int i=0; i<3; i++)
        cout << i << ": " << parabola_(samples[i].first) << ", " << samples[i].second << endl;
    cout << "center: (" << parabola_.center() << ", " << parabola_(parabola_.center()) << ")\n";
}


void Main::getTau()
{
    tau_ = 1;
    cout << "tau: " << tau_ << endl;
}


void Main::getAlpha()
{
    double alphaMagnitude = parabola_(parabola_.center())/(tau_*(1-exp(-T_/tau_)));

    // calculate phase by interpolatation at f0 from data_

    double alphaPhase = 0;
    for (FrequencyData::const_iterator it=fd_.data().begin(), prev=it; it!=fd_.data().end(); prev=it++)
    {
       if (it->x > f0_)
       {
            cout << "phase(prev): " << phase(*prev) << endl;
            cout << "phase(next): " << phase(*it) << endl;

            alphaPhase = interp(prev->x, phase(*prev), it->x, phase(*it), f0_);
            break;
       }
    }

    alpha_ = polar(alphaMagnitude, alphaPhase);

    cout << "alphaMagnitude: " << alphaMagnitude << endl;
    cout << "alphaPhase: " << alphaPhase << endl;
    cout << "alpha: " << alpha_ << endl;
}


ParameterEstimator::Parameters Main::initialEstimate() const
{
    ublas::vector<double> p(4);
    p(TruncatedLorentzian::AlphaR) = alpha_.real()/fd_.scale();
    p(TruncatedLorentzian::AlphaI) = alpha_.imag()/fd_.scale();
    p(TruncatedLorentzian::Tau) = tau_;
    p(TruncatedLorentzian::F0) = f0_ - fd_.shift();
    return p;
}


void Main::printStatus(const ParameterEstimator& estimator) const
{
    ublas::vector<double> p = estimator.estimate();
    p(TruncatedLorentzian::AlphaR) *= fd_.scale();
    p(TruncatedLorentzian::AlphaI) *= fd_.scale();
    p(TruncatedLorentzian::F0) += fd_.shift();

    cout << "estimate: " << estimator.estimate() << endl;
    cout << "phase: " << phase(estimator.estimate()) << endl;
    cout << "error: " << estimator.error() << endl;
    cout << "unnormalized: " << p << endl;
}



int Main::run()
{
    TruncatedLorentzian L(T_);

    auto_ptr<ParameterEstimator> estimator =
        ParameterEstimator::create(L, fd_.data(), initialEstimate());

    cout << "initial estimate:\n";
    printStatus(*estimator);
    L.outputSamples(dataFilename_+".model.0", initialEstimate(), fd_.shift(), fd_.scale());

    // iterate
    for (int i=0; i<iterationCount_; i++)
    {
        cout << "Iteration " << i+1 << endl;
        double errorChange = estimator->iterate();
        printStatus(*estimator);

        ostringstream filename;
        filename << dataFilename_ << ".model." << i+1;
        L.outputSamples(filename.str(), estimator->estimate(), fd_.shift(), fd_.scale());

        if (errorChange == 0)
        {
            cout << "No error change.\n";
            break;
        }

        cout << endl;
    }

    cout << "final estimate:\n";
    printStatus(*estimator);

    // write out final estimate
    L.outputSamples(dataFilename_+".model", estimator->estimate(), fd_.shift(), fd_.scale());

    return 0;
}



int main(int argc, char* argv[])
{
    try
    {
        cout << "EstimatePeakParameters\n";
        Main m(argc, argv);
        return m.run();
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

