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


#include "MatchedFilterData.hpp"
#include "data/FrequencyData.hpp"
#include <iostream>
#include <stdexcept>
#include <iomanip>
#include <iterator>
#include <algorithm>
#include <limits>


namespace pwiz {
namespace peaks {


using namespace std;
using namespace pwiz::data;


namespace {

struct Correlation
{
    double frequency;
    complex<double> dot;
    double e2;
    double tan2angle;

    Correlation(double _frequency = 0, complex<double> _dot = 0, 
                double _e2 = 0, double _tan2angle = 0)
    :   frequency(_frequency), dot(_dot), e2(_e2), tan2angle(_tan2angle)
    {}

    double angle() const {return atan(sqrt(tan2angle))*180/M_PI;}

    bool operator<(const Correlation& that) const {return norm(dot) < norm(that.dot);}
    bool operator>(const Correlation& that) const {return norm(dot) > norm(that.dot);}
};


bool hasLowerFrequency(const Correlation& a, const Correlation& b)
{
    return a.frequency < b.frequency;
}


ostream& operator<<(ostream& os, const Correlation& c)
{
    os << fixed << setprecision(2) << c.frequency << " " << c.dot << " " << abs(c.dot) << "e^i(" 
        << arg(c.dot) << ") e2=" << c.e2 << " angle=" << c.angle();
    return os;
} 

} // namespace


class MatchedFilterData::Impl
{
    public:

    Impl(const FrequencyData& fd, const Kernel& kernel, int sampleFactor, int sampleRadius);

    void compute();
    void findPeaks(double minMagnitude, double maxAngle, vector<double>& result) const;
    complex<double> correlationValue(double targetFrequency) const;
    double correlationAngle(double targetFrequency) const;

    void printFilters(ostream& os) const;
    void printCorrelationMatrix(ostream& os) const;

    private:

    const FrequencyData& fd_;
    const int filterCount_;
    const int sampleRadius_;

    double df_; // frequency step

    typedef vector< complex<double> > Filter;
    vector<Filter> filters_;

    vector<Correlation> correlations_;

    const Correlation& correlation(double targetFrequency) const;
    void initializeFilters(const Kernel& kernel);
    void calculateCorrelation(int dataIndex, int filterIndex);
};


MatchedFilterData::Impl::Impl(const FrequencyData& fd, const Kernel& kernel, 
                              int sampleFactor, int sampleRadius)
:   fd_(fd), filterCount_(sampleFactor), sampleRadius_(sampleRadius), df_(0), filters_(filterCount_)
{
    initializeFilters(kernel);
}


void MatchedFilterData::Impl::compute()
{
    correlations_.resize(fd_.data().size() * filterCount_);
    
    for (int dataIndex=0; dataIndex<(int)fd_.data().size(); dataIndex++)
    for (int filterIndex=0; filterIndex<filterCount_; filterIndex++)
        calculateCorrelation(dataIndex, filterIndex); 
}


void MatchedFilterData::Impl::findPeaks(double minMagnitude, double maxAngle,
                                        vector<double>& result) const
{
    if (correlations_.empty()) 
        throw runtime_error("[MatchedFilterData::Impl::findPeaks()] No correlations.");

    double minNorm = minMagnitude*minMagnitude; 
    double maxTan2Angle = pow(tan(maxAngle*M_PI/180), 2);

    for (vector<Correlation>::const_iterator it=correlations_.begin()+1; it+1!=correlations_.end(); ++it)
    if (norm(it->dot) >= minNorm &&             // magnitude >= minMagnitude
        it->tan2angle <= maxTan2Angle &&        // angle <= maxAngle,
        *it > *(it-1) && *it > *(it+1))         // magnitude local maximum
    {
        result.push_back(it->frequency);
    }
}


complex<double> MatchedFilterData::Impl::correlationValue(double targetFrequency) const
{
    const Correlation& c = correlation(targetFrequency);
    return c.dot;
}


double MatchedFilterData::Impl::correlationAngle(double targetFrequency) const
{
    const Correlation& c = correlation(targetFrequency);
    return c.angle();
}


void MatchedFilterData::Impl::printFilters(ostream& os) const
{
    os << "[MatchedFilterData] filters: " << filters_.size() << endl;
    for (unsigned int i=0; i<filters_.size(); i++)
    {
        for (unsigned int j=0; j<filters_[i].size(); j++)
            os << filters_[i][j] << " "; 
        os << endl;
    }
}


void MatchedFilterData::Impl::printCorrelationMatrix(ostream& os) const
{
    os << "[MatchedFilterData] correlation matrix:\n";
    for (int dataIndex=0; dataIndex<(int)fd_.data().size(); dataIndex++)
    for (int filterIndex=0; filterIndex<filterCount_; filterIndex++)
        os << "(" << dataIndex << "," << filterIndex << ") " << 
            correlations_[dataIndex*filterCount_ + filterIndex] << endl; 
}


const Correlation& MatchedFilterData::Impl::correlation(double targetFrequency) const
{
    if (correlations_.empty()) 
        throw runtime_error("[MatchedFilterData::Impl::correlation()] No correlations.");

    // binary search for insertion point
    vector<Correlation>::const_iterator it = lower_bound(correlations_.begin(),
                                                         correlations_.end(),
                                                         targetFrequency,
                                                         hasLowerFrequency);
    // decrement result if necessary
    if (it==correlations_.end() ||
        it!=correlations_.begin() && 
            targetFrequency-(it-1)->frequency < it->frequency-targetFrequency)
        --it;

    return *it;
}


void MatchedFilterData::Impl::initializeFilters(const Kernel& kernel)
{
    const double T = fd_.observationDuration();
    if (T==0) 
    {
        cout << "[MatchedFilterData::Impl::initializeFilters()] " 
             << "Warning: T == 0.  Cannot sample filters.\n";
        for (vector<Filter>::iterator it=filters_.begin(); it!=filters_.end(); ++it)
            it->resize(sampleRadius_*2+1);
        return;
    }

    df_ = 1/T;

    for (int filterIndex=0; filterIndex<filterCount_; filterIndex++)
    {
        Filter& filter = filters_[filterIndex];
        double shift = df_ * filterIndex / filterCount_;

        // compute the samples 
        for (int sampleIndex=-sampleRadius_; sampleIndex<=sampleRadius_; sampleIndex++) 
        {
            double f = sampleIndex * df_ - shift;
            complex<double> value = kernel(f); 
            filter.push_back(value);
        }

        // normalize
        double sumNorms = 0; 
        for (Filter::iterator it=filter.begin(); it!=filter.end(); ++it)
            sumNorms += norm(*it);

        double normalization = sqrt(sumNorms);
        for (Filter::iterator it=filter.begin(); it!=filter.end(); ++it)
            *it /= normalization;
    }
}


void MatchedFilterData::Impl::calculateCorrelation(int dataIndex, int filterIndex)
{
    // initial calculations
    int correlationIndex = dataIndex * filterCount_ + filterIndex;
    Correlation& correlation = correlations_[correlationIndex];
    double shift = df_ * filterIndex / filterCount_;
    correlation.frequency = fd_.data()[dataIndex].x + shift; 

    // quit now if filter is hanging off the edge
    if (dataIndex < sampleRadius_ ||
        dataIndex + sampleRadius_ >= (int)fd_.data().size())
        return;

    // calculate iterator ranges
    FrequencyData::const_iterator itData = fd_.data().begin() + dataIndex - sampleRadius_;
    FrequencyData::const_iterator endData = fd_.data().begin() + dataIndex + sampleRadius_ + 1;
    Filter::const_iterator itFilter = filters_[filterIndex].begin();
    Filter::const_iterator endFilter = filters_[filterIndex].end();

    if (endData-itData != endFilter-itFilter)
        throw runtime_error("[MatchedFilterData::Impl::calculateCorrelation()] Bad pointer calculation.");
    
    // compute dot product and norm
    complex<double> dot = 0;
    double normData = 0;
    for (; itData!=endData; ++itData, ++itFilter)
    {
        dot += (itData->y) * conj(*itFilter); 
        normData += norm(itData->y);
    }

    // fill in rest of Correlation structure
    correlation.dot = dot;
    double normDot = norm(dot);
    correlation.e2 = max(normData - normDot, 0.);
    correlation.tan2angle = normDot>0 ? correlation.e2/normDot : numeric_limits<double>::infinity();
}


//
// MatchedFilterData implementation (forward everything to Impl)
//


MatchedFilterData::MatchedFilterData(const FrequencyData& fd, const Kernel& kernel, 
                                     int sampleFactor, int sampleRadius)
:   impl_(new Impl(fd, kernel, sampleFactor, sampleRadius))
{}

MatchedFilterData::~MatchedFilterData()
{} // auto destruction of impl_

void MatchedFilterData::compute() 
{
    impl_->compute();
}

void MatchedFilterData::findPeaks(double minMagnitude, double maxAngle, 
                                  vector<double>& result) const
{
    impl_->findPeaks(minMagnitude, maxAngle, result);
}

complex<double> MatchedFilterData::correlationValue(double targetFrequency) const 
{
    return impl_->correlationValue(targetFrequency);
}

double MatchedFilterData::correlationAngle(double targetFrequency) const 
{
    return impl_->correlationAngle(targetFrequency);
}

void MatchedFilterData::printFilters(ostream& os) const 
{
    impl_->printFilters(os);
}

void MatchedFilterData::printCorrelationMatrix(ostream& os) const 
{
    impl_->printCorrelationMatrix(os);
}
 

} // namespace peaks
} // namespace pwiz

