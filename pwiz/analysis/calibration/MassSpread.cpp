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


#include "MassSpread.hpp"
#include "MassDatabase.hpp"
#include <cmath>
#include <iostream>
#include <iomanip>
#include <algorithm>
#include <stdexcept>


namespace pwiz {
namespace calibration {


using namespace std;


class MassSpreadImpl : public MassSpread
{
    public:

    MassSpreadImpl();
    MassSpreadImpl(double measurement, double initialError, const MassDatabase* massDatabase);
    virtual double measurement() const {return measurement_;}
    virtual const vector<Pair>& distribution() const {return distribution_;}
    virtual double error() const {return error_;}
    virtual double sumProbabilityOverMass() const {return sumProbabilityOverMass_;}
    virtual double sumProbabilityOverMass2() const {return sumProbabilityOverMass2_;}
    virtual void output(std::ostream& os) const;
    
    virtual vector<Pair>& distribution() {return distribution_;}
    virtual void recalculate();

    private:

    double measurement_;
    double initialError_;
    const MassDatabase* massDatabase_;
    double error_;
    vector<Pair> distribution_;
    double sumProbabilityOverMass_;
    double sumProbabilityOverMass2_;

    void calculateDistributionFromMassDatabase();
    double normalDensity(double x, double mean, double sd);
};

auto_ptr<const MassSpread> MassSpread::create(double measurement,
                                              double initialError,
                                              const MassDatabase* massDatabase)
{
    return auto_ptr<const MassSpread>(new MassSpreadImpl(measurement,
                                                         initialError,
                                                         massDatabase));
}


auto_ptr<MassSpread> MassSpread::create()
{
    return auto_ptr<MassSpread>(new MassSpreadImpl);
}


MassSpreadImpl::MassSpreadImpl()
:   measurement_(0),
    initialError_(0),
    error_(0),
    sumProbabilityOverMass_(0),
    sumProbabilityOverMass2_(0)
{}


MassSpreadImpl::MassSpreadImpl(double measurement,
                               double initialError,
                               const MassDatabase* massDatabase)
:   measurement_(measurement),
    initialError_(initialError),
    massDatabase_(massDatabase),
    error_(0),
    sumProbabilityOverMass_(0),
    sumProbabilityOverMass2_(0)
{
    calculateDistributionFromMassDatabase();
}


namespace {
bool hasGreaterProbability(const MassSpread::Pair& a, const MassSpread::Pair& b)
{
    return a.probability > b.probability;
}
} // namespace


void MassSpreadImpl::calculateDistributionFromMassDatabase()
{
    if (!massDatabase_)
        throw runtime_error("[MassSpreadImpl::MassSpreadImpl] Null MassDatabase*");

    double sd = measurement_ * initialError_; //I'm futzing with this
    double radius = sd * 5; //PM: huh?  Why 5
    vector<MassDatabase::Entry> entries = massDatabase_->range(measurement_-radius, measurement_+radius);

    int massCount = (int)entries.size();

    if(massCount > 0){
      cout<<"measurement "<<setprecision(10)<<measurement_ - radius
	  <<" "<<measurement_ 
	  <<" "<<measurement_ + radius<<" "<<initialError_<<endl;
      cout<<"mass count "<<massCount<<endl;
    }

    // calculate conditional probabilities p(b|a)
    vector<double> pba;
    for (int i=0; i<massCount; i++)
    {
        double a = entries[i].mass;
        pba.push_back(normalDensity(measurement_, a, a*initialError_));
    }

    // calculate normalization constant
    double normalization = 0;
    for (int i=0; i<massCount; i++)
        normalization += pba[i] * entries[i].weight;

    // calculate probabilities p(a|b) and save each mass/probability Pair
    for (int i=0; i<massCount; i++)
    {
        Pair pair;
        pair.mass = entries[i].mass;
        pair.probability = pba[i] * entries[i].weight / normalization;
        distribution_.push_back(pair);
    }
    
    // sort and calculate sums
    recalculate();
}


double MassSpreadImpl::normalDensity(double x, double mean, double sd)
{
    double diff2 = (x-mean)*(x-mean);
    double variance = sd*sd;
    double n = exp(-diff2/(2*variance))/sqrt(2*M_PI*variance);
    cout<<"norm "<<x<<" "<<mean<<" "<<fabs(1.0e6 * (x-mean)/x)<<" "<<sd<<" "<<n<<endl;
    return n;
}


void MassSpreadImpl::output(ostream& os) const
{
  os<<setprecision(10);
    os << measurement_ << " [" << error_ * 1e12 << "] ";
    for (int j=0; j<(int)distribution_.size(); j++)
    {
        const Pair& p = distribution_[j];
        os << "(" << p.mass << ", " << p.probability << ") ";
    }
}


void MassSpreadImpl::recalculate()
{
    // sort by greater probability
    sort(distribution_.begin(), distribution_.end(), hasGreaterProbability);
    // PM - why bother?

    // calculate error and other sums 
    for (unsigned int i=0; i<distribution_.size(); i++)
    {
        double m = distribution_[i].mass;
        double p = distribution_[i].probability;
        error_ += pow((measurement_-m)/m, 2) * p;
        sumProbabilityOverMass_ += p/m;
        sumProbabilityOverMass2_ += p/(m*m);
    }
}


ostream& operator<<(ostream& os, const MassSpread& massSpread)
{
    massSpread.output(os);
    return os;
}


} // namespace calibration
} // namespace pwiz

