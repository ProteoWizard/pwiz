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


#include "ErrorEstimator.hpp"
#include "MassSpread.hpp"
#include "MassDatabase.hpp"
#include "auto_vector.h"
#include <iostream>
#include <fstream>
#include <functional>
#include <cmath>
#include <stdexcept>


namespace pwiz {
namespace calibration {


using namespace std;


class ErrorEstimatorImpl : public ErrorEstimator
{
    public:

    ErrorEstimatorImpl(const MassDatabase& massDatabase,
                       const vector<double>& measurements,
                       double initialErrorEstimate,
                       const char* outputFilename);

    virtual void iterate();

    virtual int measurementCount() const {return (int)massSpreads_.size();}
    virtual const MassSpread* massSpread(int index) const;
    virtual double error() const {return error_;}
    virtual void output(std::ostream& os) const;

    private:

    const MassDatabase& massDatabase_;
    const vector<double>& measurements_;
    auto_vector<const MassSpread> massSpreads_;
    double error_;
    int iterationCount_;
    ofstream outputFile_;

    void calculateMassSpread(double measurement);
};


auto_ptr<ErrorEstimator> ErrorEstimator::create(const MassDatabase& massDatabase,
                                                const vector<double>& massMeasurements,
                                                double initialErrorEstimate,
                                                const char* outputFilename)
{
    return auto_ptr<ErrorEstimator>(
        new ErrorEstimatorImpl(massDatabase, massMeasurements, initialErrorEstimate, outputFilename));
}


ErrorEstimatorImpl::ErrorEstimatorImpl(const MassDatabase& massDatabase,
                                       const vector<double>& measurements,
                                       double initialErrorEstimate,
                                       const char* outputFilename)
:   massDatabase_(massDatabase),
    measurements_(measurements),
    error_(initialErrorEstimate),
    iterationCount_(0)
{
    if (outputFilename)
    {
        //cout << "Creating file " << outputFilename << endl;
        outputFile_.open(outputFilename);
        if (!outputFile_)
            throw runtime_error("[ErrorEstimatorImpl::ErrorEstimatorImpl()] Unable to open file " + string(outputFilename));
        output(outputFile_);
    }
}


void ErrorEstimatorImpl::iterate()
{
    iterationCount_++;

    massSpreads_.clear();

    int measurementCount = int(measurements_.size());

    for (int i=0; i<measurementCount; i++)
        calculateMassSpread(measurements_[i]);

    double errorSum = 0;
    for (int i=0; i<measurementCount; i++)
        errorSum += massSpreads_[i]->error();

    error_ = sqrt(errorSum/measurementCount);

    if (outputFile_)
        output(outputFile_);
}


const MassSpread* ErrorEstimatorImpl::massSpread(int index) const
{
    if (index < 0 || index >= (int)massSpreads_.size())
        throw out_of_range("[ErrorEstimatorImpl::massSpread] Index out of range.");

    return massSpreads_[index];
}


void ErrorEstimatorImpl::calculateMassSpread(double measurement)
{
    auto_ptr<const MassSpread> massSpread(MassSpread::create(measurement, error_, &massDatabase_));
    massSpreads_.push_back(massSpread);
}


void ErrorEstimatorImpl::output(std::ostream& os) const
{
    os << "[ErrorEstimator] iterations:" << iterationCount_ <<
        " error:" << error_ << endl;
    for (int i=0; i<(int)massSpreads_.size(); i++)
        os << "  " << *massSpreads_[i] << endl;
}


} // namespace calibration
} // namespace pwiz

