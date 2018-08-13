//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers <a.t> vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "ThresholdFilter.hpp"
#include <numeric>


namespace {

using namespace pwiz::msdata;

/// A type which contains iterators to the main x/y coodinartes as well as all extra arrays (which have a 1:1 element relationship with the main arrays)
struct PWIZ_API_DECL XYPlusPair
{
    double x;
    double y;
    vector<double> extra;

    XYPlusPair() : x(0), y(0) {}

    XYPlusPair(double x, double y) : x(x), y(y)
    {}

    XYPlusPair(double x, double y, const vector<double>& extra) : x(x), y(y), extra(extra)
    {}
};

vector<BinaryDataArrayPtr> getExtraArrays(const Spectrum& s, const vector<double>& x, const vector<double>& y)
{
    vector<BinaryDataArrayPtr> output;
    for (const auto& arrayPtr : s.binaryDataArrayPtrs)
        if (&arrayPtr->data != &x && &arrayPtr->data != &y && arrayPtr->data.size() == x.size())
            output.push_back(arrayPtr);
    return output;
}

void getXYPlusPairs(const Spectrum& s, vector<XYPlusPair>& output)
{
    // retrieve and validate m/z and intensity arrays

    const auto& x = s.getMZArray();
    const auto& y = s.getIntensityArray();

    if (!x.get() || !y.get())
        return;

    if (x->data.size() != y->data.size())
        throw runtime_error("[getXYPlusPairs()] Sizes do not match.");

    output.clear();
    output.resize(x->data.size());

    auto extraArrays = getExtraArrays(s, x->data, y->data);

    if (!output.empty())
    {
        double* mz = &x->data[0];
        double* intensity = &y->data[0];

        vector<double*> extraItrs; extraItrs.reserve(extraArrays.size());
        for (const auto& extraArray : extraArrays)
            extraItrs.push_back(&extraArray->data[0]);
        size_t end = extraItrs.size();

        XYPlusPair* start = &output[0];
        for (XYPlusPair* p = start; p != start + output.size(); ++p)
        {
            p->x = *mz++;
            p->y = *intensity++;
            p->extra.resize(end);
            for (size_t i = 0; i < end; ++i)
                p->extra[i] = *(extraItrs[i])++;
        }
    }
}

void setXYPlusPairs(Spectrum& s, XYPlusPair* input, size_t size, CVID yUnits, const vector<CVID>& extraArrayTypes)
{
    s.setMZIntensityArrays(vector<double>(), vector<double>(), yUnits);

    if (size == 0)
        return;

    if (input[0].extra.size() != extraArrayTypes.size())
        throw runtime_error("[setXYPlusPairs] number of extra values in pair does not match number of extraArrayTypes");

    auto& x = s.getMZArray()->data;
    auto& y = s.getIntensityArray()->data;

    x.resize(size);
    y.resize(size);
    vector<vector<double>*> extraArrays;
    for (size_t i = 0; i < input[0].extra.size(); ++i)
    {
        auto arrayPtr = s.getArrayByCVID(extraArrayTypes[i]);
        if (!arrayPtr)
            throw runtime_error("[setXYPlusPairs] array of given unit type not found in spectrum");
        arrayPtr->data.clear();
        extraArrays.push_back(&arrayPtr->data);
    }
    size_t end = extraArrays.size();

    double* bdX = &x[0];
    double* bdY = &y[0];
    for (const XYPlusPair& pair : boost::iterator_range<XYPlusPair*>(input, input+size))
    {
        *bdX++ = pair.x;
        *bdY++ = pair.y;
        if (pair.extra.size() != end)
            throw runtime_error("[setXYPlusPairs] pair has mismatched number of extra values");
        for (size_t i = 0; i < end; ++i)
            extraArrays[i]->push_back(pair.extra[i]);
    }

    s.defaultArrayLength = size;
}

bool orientationLess_Predicate (const XYPlusPair& lhs, const XYPlusPair& rhs)
{
    return lhs.y < rhs.y;
}

bool orientationMore_Predicate (const XYPlusPair& lhs, const XYPlusPair& rhs)
{
    return lhs.y > rhs.y;
}

struct MZIntensityPairSortByMZ
{
    bool operator() (const XYPlusPair& lhs, const XYPlusPair& rhs) const
    {
        return lhs.x < rhs.x;
    }
};

struct MZIntensityPairIntensitySum
{
    double operator() (double lhs, const XYPlusPair& rhs)
    {
        return lhs + rhs.y;
    }
};

struct MZIntensityPairIntensityFractionLessThan
{
    MZIntensityPairIntensityFractionLessThan(double denominator)
        : denominator_(denominator)
    {
    }

    bool operator() (const XYPlusPair& lhs, const XYPlusPair& rhs)
    {
        return (lhs.y / denominator_) < (rhs.y / denominator_);
    }

private:
    double denominator_;
};

struct MZIntensityPairIntensityFractionGreaterThan
{
    MZIntensityPairIntensityFractionGreaterThan(double denominator)
        : denominator_(denominator)
    {
    }

    bool operator() (const XYPlusPair& lhs, const XYPlusPair& rhs)
    {
        return (lhs.y / denominator_) > (rhs.y / denominator_);
    }

private:
    double denominator_;
};
} // namespace


namespace pwiz {
namespace analysis {

using namespace msdata;

// Filter params class initialization

const char* ThresholdFilter::byTypeMostIntenseName[] = {"most intense count (excluding ties at the threshold)",
    "most intense count (including ties at the threshold)",
    "absolute intensity greater than",
    "with greater intensity relative to BPI",
    "with greater intensity relative to TIC",
    "most intense TIC cutoff"};

const char* ThresholdFilter::byTypeLeastIntenseName[] = {"least intense count (excluding ties at the threshold)",
    "least intense count (including ties at the threshold)",
    "absolute intensity less than",
    "with less intensity relative to BPI",
    "with less intensity relative to TIC",
    "least intense TIC cutoff"};

PWIZ_API_DECL
ThresholdFilter::ThresholdFilter(ThresholdingBy_Type byType_ /* = ThresholdingBy_Count */, 
                                 double threshold_ /* = 1.0 */, 
                                 ThresholdingOrientation orientation_, /* = Orientation_MostIntense */
                                 const pwiz::util::IntegerSet& msLevelsToThreshold /* = [1-] */ )
    :
    byType(byType_),
    threshold(byType == ThresholdingBy_Count || byType == ThresholdingBy_CountAfterTies ? round(threshold_) : threshold_),
    orientation(orientation_),
    msLevelsToThreshold(msLevelsToThreshold)
{
}

PWIZ_API_DECL void ThresholdFilter::describe(ProcessingMethod& method) const
{
    string name = orientation == Orientation_MostIntense ? byTypeMostIntenseName[byType] 
                                                         : byTypeLeastIntenseName[byType];
    method.userParams.push_back(UserParam(name, lexical_cast<string>(threshold)));
}

PWIZ_API_DECL void ThresholdFilter::operator () (const SpectrumPtr& s) const
{
    if (!msLevelsToThreshold.contains(s->cvParam(MS_ms_level).valueAs<int>()))
        return;

    // do nothing to empty spectra
    if (s->defaultArrayLength == 0)
        return;

    auto& mz = s->getMZArray()->data;
    auto& intensity = s->getIntensityArray()->data;

    if (byType == ThresholdingBy_Count ||
        byType == ThresholdingBy_CountAfterTies)
    {
        // if count threshold is greater than number of data points, return as is
        if (s->defaultArrayLength <= threshold)
            return;
        else if (threshold == 0)
        {
            for (auto& extraArray : getExtraArrays(*s, mz, intensity))
                extraArray->data.clear();
            mz.clear();
            intensity.clear();
            s->defaultArrayLength = 0;
            return;
        }
    }

    vector<XYPlusPair> mzIntensityPairs;
    getXYPlusPairs(*s, mzIntensityPairs);

    if (orientation == Orientation_MostIntense)
        sort(mzIntensityPairs.begin(), mzIntensityPairs.end(), orientationMore_Predicate);
    else if (orientation == Orientation_LeastIntense)
        sort(mzIntensityPairs.begin(), mzIntensityPairs.end(), orientationLess_Predicate);
    else
        throw runtime_error("[threshold()] invalid orientation type");

    double tic = accumulate(mzIntensityPairs.begin(), mzIntensityPairs.end(), 0.0, MZIntensityPairIntensitySum());

    if (tic == 0)
    {
        for (auto& extraArray : getExtraArrays(*s, mz, intensity))
            extraArray->data.clear();
        mz.clear();
        intensity.clear();
        s->defaultArrayLength = 0;
        return;
    }

    double bpi = orientation == Orientation_MostIntense ? mzIntensityPairs.front().y
                                                        : mzIntensityPairs.back().y;

    // after the threshold is applied, thresholdItr should be set to the first data point to erase
    vector<XYPlusPair>::iterator thresholdItr;

    switch (byType)
    {
        case ThresholdingBy_Count:
            // no need to check bounds on thresholdItr because it gets checked above
            thresholdItr = mzIntensityPairs.begin() + (size_t) threshold;

            // iterate backward until a non-tie is found
            while (true)
            {
                const double& i = thresholdItr->y;
                if (thresholdItr == mzIntensityPairs.begin())
                    break;
                else if (i != (--thresholdItr)->y)
                {
                    ++thresholdItr;
                    break;
                }
            }
            break;

        case ThresholdingBy_CountAfterTies:
            // no need to check bounds on thresholdItr because it gets checked above
            thresholdItr = mzIntensityPairs.begin() + ((size_t) threshold)-1;

            // iterate forward until a non-tie is found
            while (true)
            {
                const double& i = thresholdItr->y;
                if (++thresholdItr == mzIntensityPairs.end() ||
                    i != thresholdItr->y)
                    break;
            }
            break;

        case ThresholdingBy_AbsoluteIntensity:
            if (orientation == Orientation_MostIntense)
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                                           mzIntensityPairs.end(),
                                           XYPlusPair(0, threshold),
                                           orientationMore_Predicate);
            else
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                                           mzIntensityPairs.end(),
                                           XYPlusPair(0, threshold),
                                           orientationLess_Predicate);
            break;

        case ThresholdingBy_FractionOfBasePeakIntensity:
            if (orientation == Orientation_MostIntense)
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                                           mzIntensityPairs.end(),
                                           XYPlusPair(0, threshold*bpi),
                                           MZIntensityPairIntensityFractionGreaterThan(bpi));
            else
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                                           mzIntensityPairs.end(),
                                           XYPlusPair(0, threshold*bpi),
                                           MZIntensityPairIntensityFractionLessThan(bpi));
            break;

        case ThresholdingBy_FractionOfTotalIntensity:
            if (orientation == Orientation_MostIntense)
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                mzIntensityPairs.end(),
                XYPlusPair(0, threshold*tic),
                MZIntensityPairIntensityFractionGreaterThan(tic));
            else
                thresholdItr = lower_bound(mzIntensityPairs.begin(),
                mzIntensityPairs.end(),
                XYPlusPair(0, threshold*tic),
                MZIntensityPairIntensityFractionLessThan(tic));
            break;

        case ThresholdingBy_FractionOfTotalIntensityCutoff:
        {
            // example (ties are included)
            // intensities:     12  2   2   1   1   1   1   0   0  (TIC 20)
            // cumulative:      12  14  16  17  18  19  20  20  20
            // fraction:        .60 .70 .80 .85 .90 .95 1.0 1.0 1.0
            // at threshold 1.0 ---------------------------^ cut here
            // at threshold .99 ---------------------------^ cut here
            // at threshold .90 ---------------------------^ cut here
            // at threshold .80 -----------^ cut here
            // at threshold .65 -----------^ cut here
            // at threshold .60 ---^ cut here
            // at threshold .15 ---^ cut here

            // starting at the (most/least intense point)/TIC fraction, calculate the running sum
            vector<double> cumulativeIntensityFraction;
            cumulativeIntensityFraction.reserve(mzIntensityPairs.size());
            cumulativeIntensityFraction.push_back(mzIntensityPairs[0].y / tic);
            size_t i=1;
            while (cumulativeIntensityFraction.back() < threshold - 1e-6 &&
                   i < mzIntensityPairs.size())
            {
                cumulativeIntensityFraction.push_back(cumulativeIntensityFraction[i-1] +
                                                      mzIntensityPairs[i].y / tic);
                ++i;
            }

            thresholdItr = mzIntensityPairs.begin() + (i-1);

            // iterate forward until a non-tie is found
            while (thresholdItr != mzIntensityPairs.end())
            {
                const double& i = thresholdItr->y;
                if (++thresholdItr == mzIntensityPairs.end() ||
                    i != thresholdItr->y)
                    break;
            }
        }
        break;

        default:
            throw runtime_error("[threshold()] invalid thresholding type");
    }

    sort(mzIntensityPairs.begin(), thresholdItr, MZIntensityPairSortByMZ());
    vector<CVID> extraArrayTypes;
    for (const auto& arrayPtr : getExtraArrays(*s, mz, intensity))
        extraArrayTypes.push_back(arrayPtr->cvParamChild(MS_binary_data_array).cvid);
    setXYPlusPairs(*s, &mzIntensityPairs[0], thresholdItr - mzIntensityPairs.begin(), s->getIntensityArray()->cvParamChild(MS_intensity_array).units, extraArrayTypes);
}

} // namespace analysis 
} // namespace pwiz
