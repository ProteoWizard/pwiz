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


#include "LeastSquaresCalibrator.hpp"
#include <iostream>
#include <iomanip>

#include <boost/numeric/ublas/vector.hpp>
#include <boost/numeric/ublas/matrix.hpp>
#include <boost/numeric/ublas/io.hpp>
#include <boost/numeric/ublas/lu.hpp>
#include <boost/numeric/ublas/triangular.hpp>
#include <boost/numeric/ublas/vector_proxy.hpp>
namespace ublas = boost::numeric::ublas;


using namespace std;
using pwiz::data::CalibrationParameters;


namespace pwiz {
namespace calibration {


class LeastSquaresCalibratorImpl : public LeastSquaresCalibrator
{
    public:
    LeastSquaresCalibratorImpl(const vector<double>& trueMasses,
                               const vector<double>& observedFrequencies);

    virtual void calibrate();
    virtual const CalibrationParameters& parameters() const {return parameters_;}
    virtual double error() const {return error_;}

    private:
    const vector<double>& trueMasses_;
    const vector<double>& observedFrequencies_;
    CalibrationParameters parameters_;
    double error_;

    void calculateSums(double& sum_f2m2,
                       double& sum_f3m2, 
                       double& sum_f4m2, 
                       double& sum_fm, 
                       double& sum_f2m);
    
    void calculateParametersFromSums(double sum_f2m2,
                                     double sum_f3m2, 
                                     double sum_f4m2, 
                                     double sum_fm, 
                                     double sum_f2m);
 
    void calculateError();
};


auto_ptr<LeastSquaresCalibrator> LeastSquaresCalibrator::create(const vector<double>& trueMasses,
                                                        const vector<double>& observedFrequencies)
{
    return auto_ptr<LeastSquaresCalibrator>(new LeastSquaresCalibratorImpl(trueMasses, observedFrequencies));
}


LeastSquaresCalibratorImpl::LeastSquaresCalibratorImpl(const vector<double>& trueMasses,
                                                       const vector<double>& observedFrequencies)
:   trueMasses_(trueMasses),
    observedFrequencies_(observedFrequencies),
    error_(-1)
{
    if (trueMasses.size() != observedFrequencies.size())
        throw logic_error("[LeastSquaresCalibratorImpl::LeastSquaresCalibratorImpl()] Data size mismatch.");
}


void LeastSquaresCalibratorImpl::calibrate()
{
    // observed frequencies {fi}
    // true masses {mi}
    // calibration function m_AB(f) = A/f + B/f^2
    // error function E(A,B) = sum [(m_AB(fi) - mi)/mi]^2  (sum of squared relative deviations)
    // dE/dA == 0 == dE/dB gives:
    //   ( sum[1/(fi^2*mi^2)]  sum[1/(fi^3*mi^2)] ) (A) == ( sum[1/(fi*mi)]   ) 
    //   ( sum[1/(fi^3*mi^2)]  sum[1/(fi^4*mi^2)] ) (B) == ( sum[1/(fi^2*mi)] ) 

    double sum_f2m2 = 0;
    double sum_f3m2 = 0;
    double sum_f4m2 = 0;
    double sum_fm = 0;
    double sum_f2m = 0;

    calculateSums(sum_f2m2, sum_f3m2, sum_f4m2, sum_fm, sum_f2m);
    calculateParametersFromSums(sum_f2m2, sum_f3m2, sum_f4m2, sum_fm, sum_f2m);
    calculateError();
}


void LeastSquaresCalibratorImpl::calculateSums(double& sum_f2m2,
                                               double& sum_f3m2, 
                                               double& sum_f4m2, 
                                               double& sum_fm, 
                                               double& sum_f2m)
{
    sum_f2m2 = 0;
    sum_f3m2 = 0;
    sum_f4m2 = 0;
    sum_fm = 0;
    sum_f2m = 0;
    
    vector<double>::const_iterator mit = trueMasses_.begin();
    vector<double>::const_iterator fit = observedFrequencies_.begin();

    for (; mit!=trueMasses_.end(); ++mit, ++fit)
    {
        double m = *mit; 
        double f = *fit; 
        double m2 = m*m;
        double f2 = f*f;
        double f3 = f2*f;
        double f4 = f3*f;
    
        sum_f2m2 += 1/(f2*m2);
        sum_f3m2 += 1/(f3*m2);
        sum_f4m2 += 1/(f4*m2);
        sum_fm += 1/(f*m);
        sum_f2m += 1/(f2*m);
    }
}


void LeastSquaresCalibratorImpl::calculateParametersFromSums(double sum_f2m2,
                                                             double sum_f3m2, 
                                                             double sum_f4m2, 
                                                             double sum_fm, 
                                                             double sum_f2m)
{
    ublas::matrix<double> M(2,2);
    ublas::vector<double> p(2);

    M(0,0) = sum_f2m2;
    M(0,1) = M(1,0) = sum_f3m2;
    M(1,1) = sum_f4m2;
    p(0) = sum_fm;
    p(1) = sum_f2m;

    ublas::permutation_matrix<size_t> pm(2);
    int singular = lu_factorize(M, pm);
    if (singular)
        throw runtime_error("[LeastSquaresCalibratorImpl::calculateParametersFromSums()] Matrix is singular.");

    lu_substitute(M, pm, p);

    parameters_.A = p[0];
    parameters_.B = p[1];
}


void LeastSquaresCalibratorImpl::calculateError()
{
    // calculate root mean squared relative deviation

    double sum = 0;
    vector<double>::const_iterator mit = trueMasses_.begin();
    vector<double>::const_iterator fit = observedFrequencies_.begin();

    for (; mit!=trueMasses_.end(); ++mit, ++fit)
    {
        double m_true = *mit; 
        double m_calc = parameters_.mz(*fit); 
        double term = (m_calc - m_true)/m_true; 
        sum += term*term; 
    } 

    error_ = sqrt(sum/trueMasses_.size());
}


} // namespace calibration
} // namespace pwiz

