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


#include "Calibrator.hpp"
#include "ErrorEstimator.hpp"
#include "MassSpread.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include <cmath>
#include <iterator>
#include <stdexcept>
#include <iostream>
#include <iomanip>
#include <fstream>
#include <sstream>


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


class CalibratorImpl : public Calibrator
{
    public:

    CalibratorImpl(const MassDatabase& massDatabase,
                   const vector<Measurement>& measurements,
                   const CalibrationParameters& initialParameters,
                   double initialErrorEstimate,
                   int errorEstimatorIterationCount,
                   const string& outputDirectory);

    virtual void iterate();
    virtual int iterationCount() const {return iterationCount_;}
    virtual const pwiz::data::CalibrationParameters& parameters() const {return parameters_;}
    virtual int measurementCount() const;
    virtual const Measurement* measurement(int index) const;
    virtual const MassSpread* massSpread(int index) const;
    virtual double error() const {return error_;}

    private:

    const MassDatabase& massDatabase_;
    const vector<Measurement>& measurements_;
    pwiz::data::CalibrationParameters parameters_;
    double error_;
    auto_ptr<ErrorEstimator> errorEstimator_;
    int iterationCount_;
    const int errorEstimatorIterationCount_;
    const string outputDirectory_;
    mutable ofstream log_;

    void recalculateParameters();

    void calculateSums(double& sum_z2_f2,
                       double& sum_z2_f3, 
                       double& sum_z2_f4, 
                       double& sum_z_f, 
                       double& sum_z_f2);
    
    void calculateParametersFromSums(double sum_z2_f2,
                                     double sum_z2_f3, 
                                     double sum_z2_f4, 
                                     double sum_z_f, 
                                     double sum_z_f2);

    void updateLog() const;
};


auto_ptr<Calibrator> Calibrator::create(const MassDatabase& massDatabase,
                                        const vector<Measurement>& measurements,
                                        const CalibrationParameters& initialParameters,
                                        double initialErrorEstimate,
                                        int errorEstimatorIterationCount,
                                        const string& outputDirectory)
{
    return auto_ptr<Calibrator>(new CalibratorImpl(massDatabase,
                                                   measurements,
                                                   initialParameters,
                                                   initialErrorEstimate,
                                                   errorEstimatorIterationCount,
                                                   outputDirectory));
}


CalibratorImpl::CalibratorImpl(const MassDatabase& massDatabase,
                               const vector<Measurement>& measurements,
                               const CalibrationParameters& initialParameters,
                               double initialErrorEstimate,
                               int errorEstimatorIterationCount,
                               const string& outputDirectory)
:   massDatabase_(massDatabase),
    measurements_(measurements),
    parameters_(initialParameters),
    error_(initialErrorEstimate),
    iterationCount_(0),
    errorEstimatorIterationCount_(errorEstimatorIterationCount),
    outputDirectory_(outputDirectory)
{
    if (!outputDirectory_.empty())
    {
        system(("mkdir " + outputDirectory_ + " 2> /dev/null").c_str());
        ostringstream oss;
        oss << outputDirectory << "/log";
        log_.open(oss.str().c_str());
        if (!log_) throw runtime_error(("[Calibrator] Unable to open log file " + oss.str()).c_str());
        updateLog();
    }
}


void CalibratorImpl::iterate()
{
    using namespace proteome; // for Ion

    iterationCount_++;

    // convert frequencies to neutral masses, using current calibration parameters
    vector<double> masses;
    for (vector<Measurement>::const_iterator it=measurements_.begin(); it!=measurements_.end(); ++it)
    {

      double mz = parameters_.mz(it->frequency);
      double neutralMass = Ion::neutralMass(mz, it->charge); 
      masses.push_back(neutralMass);
      //     cout<<it->frequency<<" "<<mz<<" "<<it->charge<<endl;
    }

    if (!outputDirectory_.empty())
    {
        ostringstream filename;
        filename << outputDirectory_ << "/ee." << setw(4) << setfill('0') << iterationCount_ << ".txt";
        errorEstimator_ = ErrorEstimator::create(massDatabase_, masses, error_, filename.str().c_str());
    }
    else
    {
        errorEstimator_ = ErrorEstimator::create(massDatabase_, masses, error_); // no logging
    }

    // ErrorEstimator calculates MassSpreads
    for (int i=0; i<errorEstimatorIterationCount_; i++)
        errorEstimator_->iterate();

    // get new Parameter estimate
    bool matrixError = false;
    try{
      recalculateParameters();
    }
    catch(range_error){
      cerr<<"probably had a singular matrix - sucks to be you!"<<endl;
      matrixError = true;
    }
    if(! matrixError){
      error_ = errorEstimator_->error();
    }

    if (log_)
        updateLog();
}


int CalibratorImpl::measurementCount() const
{
    return (int)measurements_.size();
}


const Calibrator::Measurement* CalibratorImpl::measurement(int index) const
{
    if (index >= measurementCount())
        throw out_of_range("[Calibrator::measurement()] Index out of range.");

    return &measurements_[index];
}


const MassSpread* CalibratorImpl::massSpread(int index) const
{
    if (index >= measurementCount())
        throw out_of_range("[Calibrator::massSpread()] Index out of range.");

    return errorEstimator_.get() ? errorEstimator_->massSpread(index) : 0;
}


void CalibratorImpl::recalculateParameters()
{
    double sum_z2_f2 = 0;
    double sum_z2_f3 = 0;
    double sum_z2_f4 = 0;
    double sum_z_f = 0;
    double sum_z_f2 = 0;

    calculateSums(sum_z2_f2, sum_z2_f3, sum_z2_f4, sum_z_f, sum_z_f2);
    cout<<sum_z2_f2<<" "<<sum_z2_f3<<" "<<sum_z2_f4<<" "<<sum_z_f<<" "<<sum_z_f2<<endl;
    calculateParametersFromSums(sum_z2_f2, sum_z2_f3, sum_z2_f4, sum_z_f, sum_z_f2);
}


void CalibratorImpl::calculateSums(double& sum_z2_f2,
                                   double& sum_z2_f3, 
                                   double& sum_z2_f4, 
                                   double& sum_z_f, 
                                   double& sum_z_f2)
{
    using namespace proteome; // for Ion

    if ((int)measurements_.size() != errorEstimator_->measurementCount())
        throw runtime_error("[CalibratorImpl::calculateSums()] Inconsistent measurement counts!");


    for (int i=0; i<errorEstimator_->measurementCount(); i++)
    {
        double f = measurements_[i].frequency;
        int z = measurements_[i].charge;
        
	cout<<"======================== "<<f<<" "<<z<<" ====================="<<endl;

	//THE PROBLEM IS HERE!!! --BEGIN

        // calculate ion MassSpread from the neutral MassSpread
        auto_ptr<MassSpread> ionMassSpread = MassSpread::create();
        const vector<MassSpread::Pair>& neutralDistribution = errorEstimator_->massSpread(i)->distribution();
        for (vector<MassSpread::Pair>::const_iterator it=neutralDistribution.begin(); 
             it!=neutralDistribution.end(); ++it)
        {
            MassSpread::Pair pair(Ion::ionMass(it->mass, z), it->probability);
            ionMassSpread->distribution().push_back(pair);
        }
        ionMassSpread->recalculate();

        double pa = ionMassSpread->sumProbabilityOverMass();
        double pa2 = ionMassSpread->sumProbabilityOverMass2();

	cout<<setprecision(10)<<"pa "<<pa<<" "<<pa2<<endl;

	//THE PROBLEM IS HERE!!! --END

        // TODO: uncommenting the following w/O2 gives different output! yuck!
        //errorEstimator_->massSpread(i);

        sum_z2_f2 += z*z*pa2/pow(f,2);
        sum_z2_f3 += z*z*pa2/pow(f,3);
        sum_z2_f4 += z*z*pa2/pow(f,4);
        sum_z_f += z*pa/f;
        sum_z_f2 += z*pa/pow(f,2);

	cout << sum_z2_f2 << "  " << sum_z2_f3 << " | " << sum_z_f << endl;
	cout << sum_z2_f3 << "  " << sum_z2_f4 << " | " << sum_z_f2 << endl;



    }


}


void CalibratorImpl::calculateParametersFromSums(double sum_z2_f2,
                                                 double sum_z2_f3, 
                                                 double sum_z2_f4, 
                                                 double sum_z_f, 
                                                 double sum_z_f2)
{
    ublas::matrix<double> M(2,2);
    ublas::vector<double> p(2);

    M(0,0) = sum_z2_f2;
    M(0,1) = M(1,0) = sum_z2_f3;
    M(1,1) = sum_z2_f4;
    p(0) = sum_z_f;
    p(1) = sum_z_f2;

    ublas::permutation_matrix<size_t> pm(2);
    int singular = lu_factorize(M, pm);
    if (singular)
      throw range_error("[CalibratorImpl::calculateParametersFromSums()] Matrix is singular.");
    //        throw runtime_error("[CalibratorImpl::calculateParametersFromSums()] Matrix is singular.");

    lu_substitute(M, pm, p);

    cout<<"CALIBRATION PARAMS "<<parameters_.A<<" "<<parameters_.B<<endl;
    parameters_.A = p[0];
    parameters_.B = p[1];
    cout<<"CALIBRATION PARAMS "<<parameters_.A<<" "<<parameters_.B<<endl;
}


namespace {
void reportMeasurement(ostream& os,
                       const Calibrator::Measurement& measurement,
                       const CalibrationParameters& parameters)
{
    using namespace proteome; // for Ion

    double mz = parameters.mz(measurement.frequency);
    double mass = Ion::neutralMass(mz, measurement.charge);
    os << measurement.frequency << " " << measurement.charge << " " << mz << " " << mass << endl; 
}
}//namespace


void CalibratorImpl::updateLog() const 
{
    if (!log_) 
    {
        cerr << "[CalibratorImpl::updateLog()] Warning: invalid log stream.\n";
        return;
    }

    log_.precision(12);

    log_ << "#\n";
    log_ << "# iteration: " << iterationCount_ <<
            " error: " << error_ << 
            " A: " << parameters_.A <<
            " B: " << parameters_.B << endl;
    log_ << "#\n";
    log_ << "# f z m/z m\n";

    for (vector<Measurement>::const_iterator it=measurements_.begin(); it!=measurements_.end(); ++it)
        reportMeasurement(log_, *it, parameters_);

    log_ << "\n\n";
}


/* matops matrix implementation
void CalibratorImpl::calculateParametersFromSums(double sum_z2_f2,
                                                 double sum_z2_f3, 
                                                 double sum_z2_f4, 
                                                 double sum_z_f, 
                                                 double sum_z_f2)
{
    vector<vector<double> > M(2); // build matrix from sums (entries)
    M[0] = vector<double>(2);
    M[1] = vector<double>(2);
    M[0][0] = sum_z2_f2;
    M[0][1] = M[1][0] = sum_z2_f3;
    M[1][1] = sum_z2_f4;
    vector<double> v(2); // build vector
    v[0] = sum_z_f;
    v[1] = sum_z_f2;
    vector<double> p = inverse(M)*v; // solution

    parameters_.A = p[0];
    parameters_.B = p[1];
}
*/


} // namespace calibration
} // namespace pwiz

