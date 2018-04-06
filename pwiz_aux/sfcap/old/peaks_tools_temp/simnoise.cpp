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


#include "peaks/TruncatedLorentzianParameters.hpp"
#include "peaks/TruncatedLorentzianEstimator.hpp"
#include "extmath/Stats.hpp"
#include "extmath/Random.hpp"
#include <iostream>
#include <fstream>
#include <vector>
#include <iomanip>


using namespace std;
using namespace pwiz::peaks;
using namespace pwiz::extmath;
using namespace pwiz::data;
namespace ublas = boost::numeric::ublas;


struct NoisyData
{
    const TruncatedLorentzianParameters& tlp_; 
    double signalToNoise_;
    double relativeGridOffset_;

    FrequencyData fd_;
    TruncatedLorentzian L_;

    double noiseVariance_; 
    double predictedDeltaF0_;
    double crBound_;

    NoisyData(const TruncatedLorentzianParameters& tlp, 
              double signalToNoise,
              double relativeGridOffset);

    private:
    void generateData();
    void analyzeData();
    void errorFunctionCheck();
};


NoisyData::NoisyData(const TruncatedLorentzianParameters& tlp, 
                     double signalToNoise,
                     double relativeGridOffset)
:   tlp_(tlp),
    signalToNoise_(signalToNoise),
    relativeGridOffset_(relativeGridOffset),
    L_(tlp_.T),
    noiseVariance_(0),
    predictedDeltaF0_(0),
    crBound_(0)
{
    generateData();
    analyzeData();
//    errorFunctionCheck();
}


void NoisyData::generateData()
{
    const ublas::vector<double>& p = tlp_.parameters();

    const int sampleCount = 21;
    double delta = 1/tlp_.T;
    double fBegin = tlp_.f0 - delta*(sampleCount/2) + delta*relativeGridOffset_; 

    double peakHeight = abs(L_(tlp_.f0, p));
    double noiseLevel = peakHeight / signalToNoise_;
    noiseVariance_ = noiseLevel*noiseLevel;

    for (int i=0; i<sampleCount; i++)
    {
        double f = fBegin + i*delta;
        complex<double> value = L_(f, p);
        complex<double> noise(Random::gaussian(noiseLevel/sqrt(2.)), Random::gaussian(noiseLevel/sqrt(2.)));
        fd_.data().push_back(FrequencyDatum(f, value+noise));
    }

    fd_.calibration(Calibration(1.075e8, -3.455e8));
    fd_.observationDuration(tlp_.T);
    fd_.analyze(); // recache
}


void NoisyData::analyzeData()
{
    const ublas::vector<double>& p = tlp_.parameters();

    double sum_norm_dLdf0 = 0;
    double sum_Re_dLdf0_conj_noise = 0;
    double sum_Re_d2Ldf02_conj_noise = 0;

    for (FrequencyData::iterator it=fd_.data().begin(); it!=fd_.data().end(); ++it)
    {
        double f = it->x;
        complex<double> noise = it->y - L_(f, p); 

        complex<double> dLdf0 = L_.dp(f, p)(TruncatedLorentzian::F0); 
        complex<double> d2Ldf02 = L_.dp2(f, p)(TruncatedLorentzian::F0, TruncatedLorentzian::F0); 

        sum_Re_dLdf0_conj_noise += real(dLdf0 * conj(noise));   
        sum_norm_dLdf0 += norm(dLdf0);
        sum_Re_d2Ldf02_conj_noise += real(d2Ldf02 * conj(noise));
    }
    
    predictedDeltaF0_ = sum_Re_dLdf0_conj_noise / (sum_norm_dLdf0 - sum_Re_d2Ldf02_conj_noise);
    crBound_ = noiseVariance_ / sum_norm_dLdf0;
}


void NoisyData::errorFunctionCheck()
{
    cout << "errorFunctionCheck()\n";

    const ublas::vector<double>& p = tlp_.parameters();

    for (double delta=1.; delta>1e-9; delta/=10.)
    {
        TruncatedLorentzianParameters tlp_shifted(tlp_);
        tlp_shifted.f0 += delta;
        const ublas::vector<double>& p_shifted = tlp_shifted.parameters();

        double e = 0;
        double e_shifted = 0;
        double de1 = 0;
        double de2 = 0; 

        for (FrequencyData::iterator it=fd_.data().begin(); it!=fd_.data().end(); ++it)
        {
            double f = it->x;
            complex<double> L = L_(f, p);
            complex<double> L_shifted = L_(f, p_shifted); 
            complex<double> noise = it->y - L;
            complex<double> dL = L_.dp(f, p)(TruncatedLorentzian::F0); 
            complex<double> d2L = L_.dp2(f, p)(TruncatedLorentzian::F0, TruncatedLorentzian::F0); 

            e += norm(noise);
            e_shifted += norm(it->y - L_shifted);

            complex<double> term1 = dL + d2L*delta;
            complex<double> term2 = dL*delta  + .5*d2L*delta*delta - noise;
            de1 += 2*real(term1 * conj(term2));    
    
            complex<double> term = norm(dL)*delta - dL*conj(noise) - d2L*conj(noise)*delta; 
            de2 += 2*real(term);
        }

        double differential = (e_shifted - e) / delta;

        cout << delta << " " <<
            differential << " " <<
            de1 << " " << 
            de2 << endl;
             
    }
}


enum ParameterIndex
{
    F0_predicted,
    F0,
    Tau,
    Magnitude,
    Phase,
    Error,
    ZScore,
    ParameterCount
};


Stats::vector_type runTrial(const TruncatedLorentzianParameters& tlp, 
                            double signalToNoise, 
                            double relativeGridOffset,
                            double& crBound)
{
    NoisyData nd(tlp, signalToNoise, relativeGridOffset);
    crBound = nd.crBound_;

    auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create();
    estimator->log(0);
    TruncatedLorentzianParameters init = estimator->initialEstimate(nd.fd_);
    const int iterationCount = 20;
    TruncatedLorentzianParameters final = estimator->iteratedEstimate(nd.fd_, init, iterationCount); 

    int N = nd.fd_.data().size();
    double error = estimator->error(nd.fd_, final);
    double zscore = (error/nd.noiseVariance_ - N) / (sqrt(double(N))); 
    
    Stats::vector_type result(ParameterCount);
    result(F0_predicted) = nd.predictedDeltaF0_;
    result(F0) = final.f0 - tlp.f0;
    result(Tau) = final.tau - tlp.tau;
    result(Magnitude) = abs(final.alpha) - abs(tlp.alpha);
    result(Phase) = arg(final.alpha) - arg(tlp.alpha);
    result(Error) = error; 
    result(ZScore) = zscore;

    return result;
}


void outputVector(const Stats::vector_type& v, ostream& os=cout)
{
    os << showpos << scientific << setprecision(5);
    for (int i=0; i<v.size(); i++)
        os << v(i) << " ";
    os << endl;
}


void outputStats(const Stats& stats, ostream& os=cout)
{
    os << showpos << scientific << setprecision(5);

    os << "mean:\n";
    Stats::vector_type mean = stats.mean();
    for (int i=0; i<mean.size(); i++)
        os << mean(i) << " ";
    os << endl;

    os << "covariance:\n";
    Stats::matrix_type covariance = stats.covariance();
    for (int i=0; i<covariance.size1(); i++)
    {
        for (int j=0; j<covariance.size2(); j++)
            os << covariance(i,j) << " ";
        os << endl;
    } 
}


const char* columnHeader_ = 
     "#  snr    grid      df0_mean     df0_variance    df0_std_dev     <df0^2>        CR-bound"; 
    //  2.000  -0.500  -1.2548602210  10.2806335974   3.2063427137  11.8553077718   0.0535073828


void processTLP(const TruncatedLorentzianParameters& tlp, int trialCount, bool verbose)
{
    if (verbose) 
        cout << tlp << "\n\n\n";
    else
        cout << columnHeader_ << endl; 

    for (double snr=2; snr<=10; snr+=.5)
    for (double gridOffset=-.5; gridOffset<=.5; gridOffset+=.1)
/*
    double snr = 100;
    double gridOffset = 0;
*/
    {
        if (verbose)
        {
            cout << fixed;
            cout << "signal-to-noise ratio: " << snr << endl; 
            cout << "relative grid offset: " << gridOffset << endl;
        }

        Stats::data_type data;
        double crBound = 0;

        for (int i=0; i<trialCount; i++)
        {
            try
            {
                Stats::vector_type result = runTrial(tlp, snr, gridOffset, crBound); 
                data.push_back(result);
            }
            catch (exception& e)
            {
                cerr << e.what() << endl;
                cerr << "Caught exception, continuing.\n";
            }
        }

        if (verbose)
        {
            cout << endl <<
                setw(13) << "err_f0_pred" <<
                setw(14) << "err_f0" <<
                setw(14) << "err_tau" <<
                setw(14) << "err_magnitude" << 
                setw(14) << "err_phase" << 
                setw(14) << "model_error" << 
                setw(14) << "z-score" << 
                endl;

            for (Stats::data_type::iterator it=data.begin(); it!=data.end(); ++it)
                outputVector(*it);
        }

        Stats stats(data);
        double mean_f0 = stats.mean()(F0);
        double variance_f0 = stats.covariance()(F0,F0);
        double expectedF0Squared = stats.meanOuterProduct()(F0,F0);

        if (verbose)
        {
            outputStats(stats);
            cout << endl;

            cout << fixed << noshowpos;
            cout << "<err_f0^2>: " << expectedF0Squared << endl;
            cout << "Cramer-Rao bound: " << crBound << endl << endl;

            cout << columnHeader_ << endl; 
        }

        cout << noshowpos << fixed << setprecision(3) 
            << setw(7) << snr << " " << 
            setw(7) << gridOffset << " " << 
            setprecision(10) << 
            setw(14) << mean_f0 << " " << 
            setw(14) << variance_f0 << " " << 
            setw(14) << sqrt(variance_f0) << " " <<
            setw(14) << expectedF0Squared << " " << 
            setw(14) << crBound << endl; 

        if (verbose)
            cout << "\n\n";
    }
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc < 2)
        {
            cout << "Usage: simnoise model.tlp [trialCount] [verbose]\n";
            return 1; 
        }

        const string& filename = argv[1];
        int trialCount = argc>2 ? atoi(argv[2]) : 20; 
        bool verbose = argc>3 ? !strcmp(argv[3],"verbose") : false;

        if (trialCount <= 0)
            throw runtime_error("Nothing to do.");

//        Random::initialize();
        if (verbose) cout << "Processing file " << filename << endl;
        TruncatedLorentzianParameters tlp(filename);
        processTLP(tlp, trialCount, verbose);

        return 0;
    }
    catch (exception& e)
    {
        cout << e.what() << endl;
        return 1;
    }
}

