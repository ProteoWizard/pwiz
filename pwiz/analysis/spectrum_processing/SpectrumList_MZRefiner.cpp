//
// $Id$
//
//
// Original author: Bryson Gibbons <bryson.gibbons@pnnl.gov>
//
// Copyright 2014 Pacific Northwest National Laboratory
//                Richland, WA 99352
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

#include "SpectrumList_MZRefiner.hpp"
#include "pwiz/data/vendor_readers/Agilent/SpectrumList_Agilent.hpp"
#include "pwiz/data/common/CVTranslator.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Singleton.hpp"
#include <iomanip>
#include <numeric>

namespace pwiz {
namespace analysis {

using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::identdata;

// Constants specific to mzRefiner
const unsigned int MINIMUM_RESULTS_FOR_GLOBAL_SHIFT = 100;
// 500 is kind of arbitrary, but it seemed like a good number to use as a requirement for dependent data shifts
// If there are only 500 data points, there will be very limited shift smoothing.
const unsigned int MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT = 500;

struct ScanData
{
    size_t scanId;
    string nativeID;
    int rank;
    double scanTime; // In Seconds
    double massError;
    double ppmError;
    double calcMz;
    double experMz;
    double scoreValue;
    int charge;

    // Sorting functions for vector of ScanData
    static bool byScanId(ScanData i, ScanData j) { return i.scanId < j.scanId; }
    static bool byScanTime(ScanData i, ScanData j) { return i.scanTime < j.scanTime; }
    static bool byPpmError(ScanData i, ScanData j) { return i.ppmError < j.ppmError; }
    static bool byCalcMz(ScanData i, ScanData j) { return i.calcMz < j.calcMz; }
    static bool byExperMz(ScanData i, ScanData j) { return i.experMz < j.experMz; }
    // Not particularly useful, I think - depends on score used, and on other functions that would need to be added to CVConditionalFilter
    static bool byScore(ScanData i, ScanData j) { return i.scoreValue < j.scoreValue; }
};

class PWIZ_API_DECL CVConditionalFilter
{
    public:
    class CVConditionalFilterConfigData
    {
    public:
        CVConditionalFilterConfigData() : software(CVID_Unknown), step(0.0), maxSteps(0) {}
        CVID software;
        string cvTerm;
        string rangeSet;
        double step;
        int maxSteps;
    };

    CVConditionalFilter(CVConditionalFilterConfigData configData);
    CVConditionalFilter(CVID software, string cvTerm, string rangeSet, double step = 0.0, int maxStep = 0);
    CVConditionalFilter(CVID software, string cvTerm, double maxValue, double minValue, bool useMax, bool useMin, double step = 0, int maxStep = 0);
    void updateFilter(CVID software, string cvTerm, double maxValue, double minValue, bool useMax, bool useMin, double step = 0, int maxStep = 0);
    void updateFilter(CVID software, string cvTerm, string rangeSet, double step = 0.0, int maxStep = 0);
    bool passesFilter(identdata::SpectrumIdentificationItemPtr& sii, double& scoreVal) const;
    bool isBetter(double lScoreVal, double rScoreVal) const;
    bool isBetter(const ScanData& lData, const ScanData& rData) const;
    bool adjustFilterByStep();
    cv::CVID getCVID() const { return cvid; }
    double getMax() const { return max; }
    double getMin() const { return min; }
    bool getAnd() const { return isAnd; }
    string getScoreName() { return scoreName; }
    string getThreshold() { return threshold; }

    private:
    CVConditionalFilter();
    string scoreName;
    string threshold;
    cv::CVID cvid;
    bool useName;
    double max;
    double min;
    double step;
    double maxSteps;
    int stepCount;
    bool isAnd;
    double center;
    bool isMin;
    bool isMax;
};
typedef boost::shared_ptr<CVConditionalFilter> CVConditionalFilterPtr;
ostream& operator<<(ostream& out, CVConditionalFilter filter);

// A functor for using STL sorts with an object instance
struct sortFilter
{
    CVConditionalFilterPtr filter;
    sortFilter(CVConditionalFilterPtr& f) : filter(f) {};
    bool operator() (const ScanData& lData, const ScanData& rData) const
    {
        return filter->isBetter(lData, rData);
    }
};

// Abstract class for a shift: common functionality required for all shifts
class AdjustmentObject
{
    public:
    AdjustmentObject(double gShift = 0.0, double gStDev = 0.0, double gMAD = 0.0) : globalShift(gShift), globalStDev(gStDev), globalMAD(gMAD) {}
    virtual ~AdjustmentObject() {}

    string getAdjustmentType() const    { return adjustmentType; }
    string getPrettyAdjustment() const  { return prettyAdjustment; }
    double getStDev() const             { return stdev; }
    double getMAD() const               { return mad; }
    double getPctImp() const            { return percentImprovement; }
    double getPctImpMAD() const         { return percentImprovementMAD; }
    double getGlobalShift() const       { return globalShift; }
    double getGlobalStDev() const       { return globalStDev; }
    double getGlobalMAD() const         { return globalMAD; }
    virtual double shift(double scanTime, double mass) const = 0;
    virtual void calculate(vector<ScanData>& data) = 0;
    virtual string getShiftRange() const = 0;
    virtual string getShiftOutString() const = 0;
    
    protected:
    string adjustmentType;
    string prettyAdjustment;
    string adjTypeShort;
    double stdev;
    double mad;
    double percentImprovement;
    double percentImprovementMAD;
    double globalShift;
    double globalStDev;
    double globalMAD;
};
typedef boost::shared_ptr<AdjustmentObject> AdjustmentObjectPtr;

// A basic shift - everything is being shifted by the same approximate value
class AdjustSimpleGlobal : public AdjustmentObject
{
    public:
    double getShift() const       { return shiftError; }
    double getModeError() const   { return modeError; }
    double getMedianError() const { return medianError; }
    double getAvgError() const    { return avgError; }
    double getAvgStDev() const    { return avgStDev; }
    double getModeStDev() const   { return modeStDev; }
    double getMedianStDev() const { return medianStDev; }
    AdjustSimpleGlobal()    { adjustmentType = "SimpleGlobal"; adjTypeShort = "SG"; prettyAdjustment = "Global Shift"; }
    void setZeroShift()     { shiftError = 0.0;  }
    virtual double shift(double scanTime, double mass) const;
    virtual void calculate(vector<ScanData>& data);
    virtual string getShiftRange() const     { return lexical_cast<string>(shiftError); }
    virtual string getShiftOutString() const { return "Global PPM Shift"; }
    bool checkForPeak() const;
    
    private:
    vector<int> freqHistBins;
    double freqHistBinSize;
    int freqHistBinCount;
    double shiftError;
    double avgError;
    double modeError;
    double medianError;
    double avgStDev;
    double modeStDev;
    double medianStDev;
};
typedef boost::shared_ptr<AdjustSimpleGlobal> AdjustSimpleGlobalPtr;

// Support common functionality for a "binned" dependency shift
class BinnedAdjustmentObject : public AdjustmentObject
{
    public:
    BinnedAdjustmentObject(double gShift, double gStDev, double gMAD) : AdjustmentObject(gShift, gStDev, gMAD) {}
    virtual double shift(double scanTime, double mass) const = 0;
    virtual void calculate(vector<ScanData>& data) = 0;
    virtual string getShiftRange() const;
    virtual string getShiftOutString() const = 0;
    double getRoughStDev() const           { return roughStDev; }
    double getRoughPctImp() const          { return percentImprovementRough; }
    double getSmoothedStDev() const        { return smoothedStDev; }
    double getSmoothedPctImp() const       { return percentImprovementSmoothed; }
    double getRoughMAD() const             { return roughMAD; }
    double getRoughPctImpMAD() const       { return percentImprovementRoughMAD; }
    double getSmoothedMAD() const          { return smoothedMAD; }
    double getSmoothedPctImpMAD() const    { return percentImprovementSmoothedMAD; }
    
    protected:
    virtual double binShift(double dependency, double mass) const;
    virtual void smoothBins();
    virtual void cleanNoise();
    virtual void cleanNoiseHelper(size_t start, int step);
    virtual void processBins();
    virtual void getStats(double& stDev, double& mad);
    vector<double> shifts;
    vector<bool> isValidBin;
    vector<unsigned int> counts;
    double binSize;
    vector< vector<double> > sortBins;
    size_t bins;
    size_t highestCountBin;
    size_t lowestValidBin;
    size_t highestValidBin;
    double roughStDev;
    double percentImprovementRough;
    double smoothedStDev;
    double percentImprovementSmoothed;
    double roughMAD;
    double percentImprovementRoughMAD;
    double smoothedMAD;
    double percentImprovementSmoothedMAD;
};
typedef boost::shared_ptr<BinnedAdjustmentObject> BinnedAdjustmentObjectPtr;

/*******************************************************************************************************
 * Sorts a list by the default sorting function, and returns the median
 * The parameter would be const, but we can't sort a const list.
 * Using pass-by-reference to avoid vector copy costs.
 * Returns: median, list is sorted in ascending order (by default comparison)
 ******************************************************************************************************/
double median(vector<double>& list)
{
    double median = 0.0;
    std::sort(list.begin(), list.end());

    // Pseudo-median: use integer division to always get center (odd count) or next lower value (even count)
    //median = list[list.size() / 2];
    // True median: average the two center values when the count is even.
    // Possibility of replacing this with Boost's statistical accumulators
    if (list.size() == 0)
    {
        return 0.0;
    }
    if (list.size() % 2) // Get "false" (0) if even, "true" (1) if odd
    {
        median = list[list.size() / 2];
    }
    else
    {
        // list size / 2 gives the size divided by 2 - will always be the high index for the center (e.g., 2 / 2 = 1, the highest valid index...)
        // Subtract 1 to get the low index for the center
        double lShift = list[(list.size() / 2) - 1]; // next lower value
        double rShift = list[list.size() / 2]; // next higher value
        median = (lShift + rShift) / 2.0;
    }
    return median;
}

class AdjustByScanTime : public BinnedAdjustmentObject
{
public:
    // Bin size of 1.25 (min) captures 20 +/- 5 ms1 scans on Thermo Orbitrap
    // Changed to 75 seconds (make all references change to that as well)
    // Captures at least 14 scans, and up to 50, on Agilent QTOF
    // Should probably be made variable, but determining what it should be in the end is the challenge.
    AdjustByScanTime(double gShift, double gStDev, double gMAD) : BinnedAdjustmentObject(gShift, gStDev, gMAD) { adjustmentType = "ByScanTime"; adjTypeShort = "Time"; binSize = 75; prettyAdjustment = "Using scan time dependency"; }
    virtual double shift(double scanTime, double mass) const;
    virtual void calculate(vector<ScanData>& data);
    virtual string getShiftOutString() const { return "scan time dependent shift"; }

private:
};
typedef boost::shared_ptr<AdjustByScanTime> AdjustByScanTimePtr;

class AdjustByMassToCharge : public BinnedAdjustmentObject
{
    public:
    AdjustByMassToCharge(double gShift, double gStDev, double gMAD) : BinnedAdjustmentObject(gShift, gStDev, gMAD) { adjustmentType = "ByMassToCharge"; adjTypeShort = "MZ"; binSize = 25;  prettyAdjustment = "Using mass to charge dependency"; }
    virtual double shift(double scanTime, double mass) const;
    virtual void calculate(vector<ScanData>& data);
    virtual string getShiftOutString() const { return "m/z dependent shift"; }
    
    private:
};
typedef boost::shared_ptr<AdjustByMassToCharge> AdjustByMassToChargePtr;

/*********************************************************************************************
 * Determine the mode within a specified accuracy for a global shift
 ********************************************************************************************/
void AdjustSimpleGlobal::calculate(vector<ScanData>& data)
{
    std::sort(data.begin(), data.end(), ScanData::byPpmError);
    medianError = data[data.size() / 2].ppmError;
    double sumPpmError = 0.0;
    freqHistBinSize = 0.5;
    /****************************************************************************************
     * Decent ideas for bin size
     * 0.1 = 1001 bins
     * 0.2 = 501 bins
     * 0.4 = 251 bins
     * 0.5 = 201 bins
     * 0.8 = 126 bins
     * 1   = 101 bins
     * 2   = 51 bins
     ***************************************************************************************/
    // -50 to 50 is 100 values, if we don't count zero
    freqHistBinCount = 100.0 / freqHistBinSize + 1.0;
    freqHistBins.resize(freqHistBinCount, 0); // -50 to 50, with extra for zero
    BOOST_FOREACH(const ScanData &i, data)
    {
        if (-50 <= i.ppmError && i.ppmError <= 50)
        {
            // Add 50 to enter valid range, add .5 for rounding purposes, then truncate for valid rounding
            // -50.5+50.5 = 0, -0.5+50.5=50, -0.2+50.5 = 50.7=>50, 50.499+50.5=100.999=>100.
            // 0.5 for rounding is a constant - do not change.
            freqHistBins[(int)((i.ppmError + 50.0) * (1.0 / freqHistBinSize) + 0.5)]++;
            sumPpmError += i.ppmError;
        }
    }
    
    avgError = sumPpmError / (double)data.size();
    double sumVariance = 0;
    double modeVariance = 0;
    double medianVariance = 0;
    vector<double> madList;
    
    /// Output ppmError Histogram...
    int high = 0;
    for (int i = 0; i < freqHistBinCount; ++i)
    {
        if (freqHistBins[i] > high)
        {
            high = freqHistBins[i];
            modeError = (i * freqHistBinSize) - 50.0;
        }
    }
    BOOST_FOREACH(const ScanData &i, data)
    {
        sumVariance += pow(i.ppmError - avgError, 2);
        modeVariance += pow(i.ppmError - modeError, 2);
        medianVariance += pow(i.ppmError - medianError, 2);
        madList.push_back(abs(i.ppmError - medianError));
    }
    // variance, average of squared differences from mean
    //double avgVariance = sumVariance / (double)data.size();
    avgStDev = sqrt(sumVariance / (double)data.size());
    modeStDev = sqrt(modeVariance / (double)data.size());
    medianStDev = sqrt(medianVariance / (double)data.size());
    mad = median(madList);

    // This is the global stdev, it's what we are trying to improve
    percentImprovement = 0.0;
    
    // Set the type of shift to use here.
    shiftError = medianError;
    stdev = medianStDev;

    ostringstream oss2;
    oss2 << ": " << shiftError << " ppm";
    prettyAdjustment += oss2.str();
    
    // Set the global shift/stdev values
    globalShift = shiftError;
    globalStDev = stdev;
    globalMAD = mad;
}

/*********************************************************************************************
* Check for a peak where the median is found - if no significant peak, return false
********************************************************************************************/
bool AdjustSimpleGlobal::checkForPeak() const
{
    if (freqHistBins.empty())
    {
        return false;
    }
    // Checking 10 ppm to either side of the shift value
    size_t medianBin = (int)((shiftError + 50.0) * (1.0 / freqHistBinSize) + 0.5);
    size_t tenPpmBins = (1.0 / freqHistBinSize) * 10;
    size_t medianLessTenBin = medianBin >= tenPpmBins ? medianBin - tenPpmBins : 0;
    size_t medianPlusTenBin = medianBin < freqHistBinCount - tenPpmBins ? medianBin + tenPpmBins : freqHistBinCount - 1;
    // Find max
    // Use the mode; why re-find it?
    // We need the bin index
    size_t maxBin = (int)((modeError + 50.0) * (1.0 / freqHistBinSize) + 0.5);
    if (maxBin < medianLessTenBin || medianPlusTenBin < maxBin)
    {
        // The max is outside of +/- 10 ppm from the median; obvious problems
        ////////////// Or, severe bi-modal dataset; might be a nice quick check, but might exclude resonable data. ////////////////////////////////////////////////////////////////////////////////////////////////////////
        return false;
    }
    // Find average of all values not in the +/- 10 ppm range
    int sum = 0;
    int count = 0;
    for (size_t i = 0; i < freqHistBins.size(); i++)
    {
        if (medianLessTenBin <= i && i <= medianPlusTenBin)
        {
            continue;
        }
        // outside of +/- 10 ppm range - get average
        // Exclude zero counts - prevent bias
        if (freqHistBins[i] > 0)
        {
            sum += freqHistBins[i];
            count++;
        }
    }

    if (count == 0)
    {
        // All data points were within 10 ppm of the median.
        return true; ////////////////////////////////////////////////// May warrant further evaluation? ///////////////////////////////////////////////////////////////////////////////////////////////////////
    }
    double average = (double)sum / (double)count;
    // Is max > average * 5?
    return freqHistBins[maxBin] >= average * 5;
}

/*********************************************************************************************
 * Return a value shifted by the global ppm mode
 ********************************************************************************************/
double AdjustSimpleGlobal::shift(double scanTime, double mass) const
{
    return mass * (1 - shiftError / 1.0e6);
}

/*********************************************************************************************
 * Perform some basic smoothing to balance erratic or missing data
 ********************************************************************************************/
void BinnedAdjustmentObject::smoothBins()
{
    // Do not store shifts on original data - We don't want bias based on the shifts
    vector<double> newShifts(shifts.size(), globalShift);

    for (int i = lowestValidBin; (size_t)i <= highestValidBin; ++i)
    {
        int count = counts[i];
        double sum = shifts[i] * (double)counts[i];
        // Run at least once - we want to include next and previous values, as weights
        // We also want at least 100 scans to increase reliability
        for (int j = 1; (j < 2 || count < 100) && (size_t)j < shifts.size(); ++j)
        {
            if ((size_t)(i + j) <= highestValidBin)
            {
                count += counts[i + j];
                sum += shifts[i + j] * (double)counts[i + j];
            }
            // Prevent negatives, compared as signed values...
            if (i - j > 0 && (size_t)(i - j) >= lowestValidBin)
            {
                count += counts[i - j];
                sum += shifts[i - j] * (double)counts[i - j];
            }
        }
        // Stored the smoothed data
        isValidBin[i] = true;
        newShifts[i] = sum / (double)count;
    }
    
    // Overwrite the original shifts with the smoothed ones.
    shifts.swap(newShifts);
}

/*********************************************************************************************
 * Clean out the noise at the tail ends of the run
 * This can have some undesired effects, should probably be made optional
 * It may be useful for scores/datasets that are noisier, by limiting the dependency shifts
 *    where data is sparse
 ********************************************************************************************/
void BinnedAdjustmentObject::cleanNoiseHelper(size_t start, int step)
{
    // Enforce stepping one item at a time, in the set direction
    if (step < 0)
        step = -1;
    else
        step = 1;
    double stDevToUse = globalStDev;
    if (stDevToUse < 3.0)
    {
        // We have a pretty good dataset, we can probably just exit this function;
        //return;
        stDevToUse *= 2.0; // Set it to something not overly sensitive; 2 StDev
    }
    bool wipeoutRest = false;
    size_t lastNonZeroIndex = start; // Assume we are not starting at zero
    for (int i = start; i < (int)bins && i >= 0; i += step)
    {
        if (wipeoutRest)
        {
            // We've hit two zero counts in a row, clear out the rest.
            shifts[i] = globalShift;
            counts[i] = 0;
            continue;
        }
        else if (counts[i] > 20)
        {
            // Figure that it is probably good data
            lastNonZeroIndex = i;
            continue;
        }
        else if (counts[i] > 0)
        {
            double thisShift = shifts[i];
            // Use lastNonZeroIndex to get the last one we considered valid.
            double lastShift = shifts[lastNonZeroIndex];
            // Check to see if the difference from the last bin to this one is more than a reasonable amount (usually 1 StDev)...
            if ((thisShift + stDevToUse) < lastShift || (thisShift - stDevToUse) > lastShift)
            {
                // Reset to the global; This will later be wiped out during smoothing, if in range.
                shifts[i] = globalShift;
                counts[i] = 0;
                if (abs((int)i - (int)lastNonZeroIndex) > 1)
                {
                    // We got two pretty bad sets in a row. Ignore whatever is left.
                    wipeoutRest = true;
                }
            }
            else
            {
                lastNonZeroIndex = i;
            }
        }
        else
        {
            if (abs((int)i - (int)lastNonZeroIndex) > 1)
            {
                // Found two zero counts in a row. Assume no good data in rest of vector.
                wipeoutRest = true;
            }
            // Otherwise zero, don't do anything special.
        }
    }
    // Reset the lowest and highest valid bins to the new limits.
    if (step < 0)
    {
        lowestValidBin = lastNonZeroIndex;
    }
    else
    {
        highestValidBin = lastNonZeroIndex;
    }
}

/*********************************************************************************************
 * Clear out the noisy results on either end - this is risky, but the results if we don't do this
 *    can be worthless.
 * This is more useful for identification tools that don't provide a score that easily lends
 *    itself to this shift - MS-GF+'s SpecEValue seems to provide a pretty clean plot, but
 *    MyriMatch's mvh and xcorr tend to provide a noisy plot in my experience.
 ********************************************************************************************/
void BinnedAdjustmentObject::cleanNoise()
{
    cleanNoiseHelper(highestCountBin, -1);
    cleanNoiseHelper(highestCountBin, 1);
}

/*********************************************************************************************
 * Calculate bin medians and statistics
 ********************************************************************************************/
void BinnedAdjustmentObject::processBins()
{
    // Need to eventually add some way of cleaning up noisy ends - where there is no apparent "quality data"
    // Ideas: Look for data density, significant changes in stdev, etc.
    shifts.resize(bins, globalShift);
    isValidBin.resize(bins, false);
    size_t maxCount = 0;
    for (size_t i = 0; i < bins; ++i)
    {
        if (sortBins[i].size() > 0)
        {
            // Get the median
            shifts[i] = median(sortBins[i]);
            // Find the bin with the highest number of scans
            if (maxCount < sortBins[i].size())
            {
                highestCountBin = i;
                maxCount = sortBins[i].size();
            }
        }
    }

    //cleanNoise();

    getStats(roughStDev, roughMAD);
    
    // Run the smoothing algorithm
    smoothBins();
    getStats(smoothedStDev, smoothedMAD);

    // Need all absolute to be accurate - we don't want worse values to come back as "better"
    percentImprovementRough = 100.0 * ((abs(globalStDev) - abs(roughStDev)) / abs(globalStDev));
    percentImprovementSmoothed = 100.0 * ((abs(globalStDev) - abs(smoothedStDev)) / abs(globalStDev));
    percentImprovementRoughMAD = 100.0 * ((abs(globalMAD) - abs(roughMAD)) / abs(globalMAD));
    percentImprovementSmoothedMAD = 100.0 * ((abs(globalMAD) - abs(smoothedMAD)) / abs(globalMAD));

    stdev = smoothedStDev;
    mad = smoothedMAD;
    percentImprovement = percentImprovementSmoothedMAD;
}

/*********************************************************************************************
 * Calculate the bin standard deviations and average, and bin median absolute deviations and median
 ********************************************************************************************/
void BinnedAdjustmentObject::getStats(double& stDev, double& mad)
{
    int validBins = 0;
    vector<double> binStDev(bins, 0.0);
    vector<double> binMad;
    vector<double> madWorker;
    double binStDevSum = 0.0;
    // This is kinda rough for a standard deviation calculation - it doesn't reflect what the final result would really be.
    // Should change this to use basic smoothing between bins, similar to the binShift() function. Would require using the scan time or calculated mass to charger, depending on the shift type... //////////////////////////////////////////////////////////////////////////
    for (size_t i = lowestValidBin; i <= highestValidBin; ++i)
    {
        if (sortBins[i].size() > 0)
        {
            double varianceSumBin = 0.0;
            ++validBins;
            BOOST_FOREACH(double &j, sortBins[i])
            {
                varianceSumBin += pow(j - shifts[i], 2);
                // For bin Median Absolute Deviation
                madWorker.push_back(abs(j - shifts[i]));
            }
            binStDev[i] = sqrt(varianceSumBin / (double)sortBins[i].size());
            binStDevSum += binStDev[i];
            // Bin Median Absolute Deviation: median of absolute deviations from the median
            binMad.push_back(median(madWorker));
            // Clear out the helper
            madWorker.clear();
        }
    }
    // Get final Median Absolute deviation: median of bin Median Absolute Deviation
    mad = median(binMad);
    stDev = binStDevSum / (double)validBins;
}

/*********************************************************************************************
 * Perform a shift on a mass, given a dependency
 ********************************************************************************************/
double BinnedAdjustmentObject::binShift(double dependency, double mass) const
{
    size_t useBin = dependency / binSize;
    if (useBin >= lowestValidBin && useBin <= highestValidBin)
    {
        // Handle limits: no data points either before or after to slope to.
        if ((dependency <= ((double)lowestValidBin * binSize) + binSize / 2.0) || (dependency >= ((double)highestValidBin * binSize) + binSize / 2.0) )
        {
            //return mass * (1 - globalShift / 1.0e6);
            return mass * (1 - shifts[useBin] / 1.0e6);
        }
        // Handle +/- 0.5 of center - no need to slope?
        double binCenter = ((double)useBin * binSize) + binSize / 2.0;
        int lowBin = lowestValidBin;
        int highBin = highestValidBin;
        // Set the low bin and high bin for smooth shifting between two points
        if (dependency < binCenter)
        {
            // since levelling is being done and the previous bin is at minimum the
            //    lowestValidBin, we can just use:
            //  lowBin = useBin - 1;
            lowBin = useBin - 1;
            highBin = useBin;
        }
        else if (dependency > binCenter)
        {
            // since levelling is being done and the next bin is at maximum the
            //    highestValidBin, we can just use:
            //  highBin = useBin + 1;
            highBin = useBin + 1;
            lowBin = useBin;
        }
        else
        {
            // The float comparisons thought it wasn't greater than or less than the bin center. Assume equal.
            return mass * (1 - shifts[useBin] / 1.0e6);
        }
        // Center index of lower bin
        double lowMid = ((double)lowBin * binSize) + binSize / 2.0;
        // Center index of higher bin
        double highMid = ((double)highBin * binSize) + binSize / 2.0;
        // Index difference between low and high bins //////////////////////////// This is probably only ever one, with smoothing in use.
        double rangeBin = highMid - lowMid;
        // Position in range of window - min 0, max rangeBin
        double inRange = dependency - lowMid;
        // Value difference from the low bin to the high bin
        double ctcDiff = shifts[highBin] - shifts[lowBin];
        // Percentage position of dependency in range from lowMid to highMid
        double pctInRange = inRange / rangeBin;
        // The shifting value that we can now calculate
        double newShift = shifts[lowBin] + pctInRange * ctcDiff;
        return mass * (1 - newShift / 1.0e6);
    }
    // When dependency value is less than the lowest bin with valid data, use the lowestValidBin for the shift
    if (useBin < lowestValidBin)
    {
        return mass * (1 - shifts[lowestValidBin] / 1.0e6);
    }
    // When dependency value is greater than the highest bin with valid data, use the highestValidBin for the shift
    if (useBin > highestValidBin)
    {
        return mass * (1 - shifts[highestValidBin] / 1.0e6);
    }
    // If all else fails, default to whatever bin it falls into. Should never be used, the above should catch everything this one does
    if (useBin < counts.size() && counts[useBin] > 0)
    {
        return mass * (1 - shifts[useBin] / 1.0e6);
    }
    // If the data we got is out of the shift range, just use the global shift.
    return mass * (1 - globalShift / 1.0e6);
}

/*********************************************************************************************
* Output the range of shifts being performed
********************************************************************************************/
string BinnedAdjustmentObject::getShiftRange() const
{
    double min = shifts[lowestValidBin];
    double max = shifts[lowestValidBin];
    for (size_t i = lowestValidBin; i <= highestValidBin; i++)
    {
        if (shifts[i] < min)
        {
            min = shifts[i];
        }
        if (shifts[i] > max)
        {
            max = shifts[i];
        }
    }
    // May want to use an ostringstream to set a specific precision
    return lexical_cast<string>(min) + " to " + lexical_cast<string>(max);
}

/*********************************************************************************************
* Configure the shifting object, populate data bins, and run calculations
********************************************************************************************/
void AdjustByScanTime::calculate(vector<ScanData>& data)
{
    // Sort the data by scan time, and populate the bins
    std::sort(data.begin(), data.end(), ScanData::byScanTime);
    // Set the store the highest and lowest valid bins
    lowestValidBin = data.front().scanTime / binSize;
    highestValidBin = data.back().scanTime / binSize;
    // Calculate the number of bins needed; add 3-4 extra bins to avoid going out of bounds
    bins = (data.back().scanTime + (binSize * 4.0)) / binSize;
    // Resize the data vectors to the appropriate size
    sortBins.resize(bins);
    counts.resize(bins, 0);
    // Populate the data vectors
    BOOST_FOREACH(const ScanData &i, data)
    {
        int useBin = i.scanTime / binSize;
        sortBins[useBin].push_back(i.ppmError);
        counts[useBin]++;
    }

    // The generic bin-based shift calculation can take care of the rest.
    processBins();
}

/*********************************************************************************************
* Perform a shift on a mass, given a dependency
********************************************************************************************/
double AdjustByScanTime::shift(double scanTime, double mass) const
{
    return binShift(scanTime, mass);
}

/*********************************************************************************************
 * Configure the shifting object, populate data bins, and run calculations
 ********************************************************************************************/ 
void AdjustByMassToCharge::calculate(vector<ScanData>& data)
{
    // Sort the data by mass, and populate the bins
    std::sort(data.begin(), data.end(), ScanData::byCalcMz);
    // Set the store the highest and lowest valid bins
    lowestValidBin = data.front().experMz / binSize;
    highestValidBin = data.back().experMz / binSize;
    // Calculate the number of bins needed; add 3-4 extra bins to avoid going out of bounds
    bins = (data.back().experMz + (binSize * 4)) / binSize;
    // Resize the data vectors to the appropriate size
    sortBins.resize(bins);
    counts.resize(bins, 0);
    // Populate the data vectors
    BOOST_FOREACH(const ScanData &i, data)
    {
        int useBin = i.experMz / binSize;
        sortBins[useBin].push_back(i.ppmError);
        counts[useBin]++;
    }

    // The generic bin-based shift calculation can take care of the rest.
    processBins();
}

/*********************************************************************************************
 * Perform a shift on a mass, given a dependency
 ********************************************************************************************/
double AdjustByMassToCharge::shift(double scanTime, double mass) const
{
    return binShift(mass, mass);
}


/*******************************************************************************************************************************************
* Chunk of code copied from pwiz/data/identdata/Serializer_pepXML.cpp
* Seemed useful for users inputting score names.
********************************************************************************************************************************************/
struct ScoreTranslation
{
    CVID softwareCVID;
    CVID scoreCVID;
    const char* scoreNames; // first name is the preferred one
};

struct ci_less
{
    bool operator() (const string& lhs, const string& rhs) const
    {
        if (lhs.length() != rhs.length())
            return lhs.length() < rhs.length();
        for (size_t i = 0; i < lhs.length(); ++i)
            if (tolower(lhs[i]) != tolower(rhs[i]))
                return tolower(lhs[i]) < tolower(rhs[i]);
        return false;
    }
};

const ScoreTranslation scoreTranslationTable[] =
{
    { MS_SEQUEST, MS_SEQUEST_xcorr, "xcorr" },
    { MS_SEQUEST, MS_SEQUEST_deltacn, "deltacn;deltcn" },
    { MS_Mascot, MS_Mascot_score, "ionscore;score" },
    { MS_Mascot, MS_Mascot_identity_threshold, "identityscore" },
    { MS_Mascot, MS_Mascot_homology_threshold, "homologyscore" },
    { MS_Mascot, MS_Mascot_expectation_value, "expect" }, // ??
    { MS_OMSSA, MS_OMSSA_pvalue, "pvalue" },
    { MS_OMSSA, MS_OMSSA_evalue, "expect" },
    { MS_Phenyx, MS_Phenyx_Pepzscore, "zscore" },
    { MS_Phenyx, MS_Phenyx_PepPvalue, "zvalue" },
    //{MS_greylag, MS_greylag_??, "??"},
    //{MS_Phenyx, MS_Phenyx_Score, "??"},
    //{MS_ProteinPilot_Software, MS_Paragon_score, "??"},
    //{MS_ProteinLynx_Global_Server, MS_ProteinLynx_Ladder_Score, "??"},
    //{MS_ProteinLynx_Global_Server, MS_ProteinLynx_Log_Likelihood, "??"},
    { MS_MyriMatch, MS_MyriMatch_MVH, "mvh" },
    { MS_TagRecon, MS_MyriMatch_MVH, "mvh" },
    { MS_Pepitome, MS_MyriMatch_MVH, "mvh" },
    { MS_MyriMatch, MS_MyriMatch_mzFidelity, "mzFidelity" },
    { MS_TagRecon, MS_MyriMatch_mzFidelity, "mzFidelity" },
    { MS_Pepitome, MS_MyriMatch_mzFidelity, "mzFidelity" },
    { MS_X_Tandem, MS_X_Tandem_hyperscore, "hyperscore" },
    { MS_X_Tandem, MS_X_Tandem_expect, "expect" },
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_Score, "??"},
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_Discriminant_Score, "??"},
    //{MS_Spectrum_Mill_for_MassHunter_Workstation, MS_SpectrumMill_SPI, "??"},
    //{MS_Proteios, MS_Proteios_??, "??"},
    { MS_MS_GF, MS_MS_GF_RawScore, "raw" },
    { MS_MS_GF, MS_MS_GF_DeNovoScore, "denovo" },
    { MS_MS_GF, MS_MS_GF_Energy, "energy" },
    { MS_MS_GF, MS_MS_GF_EValue, "EValue" },
    { MS_MS_GF, MS_MS_GF_QValue, "QValue" },
    { MS_MS_GF, MS_MS_GF_SpecEValue, "SpecEValue" },
    { MS_MS_GF, MS_MS_GF_PepQValue, "PepQValue" },
    { MS_MS_GF, MS_MS_GF_PEP, "PEP" },
    { MS_MS_GF_, MS_MS_GF_RawScore, "raw" },
    { MS_MS_GF_, MS_MS_GF_DeNovoScore, "denovo" },
    { MS_MS_GF_, MS_MS_GF_Energy, "energy" },
    { MS_MS_GF_, MS_MS_GF_EValue, "EValue" },
    { MS_MS_GF_, MS_MS_GF_QValue, "QValue" },
    { MS_MS_GF_, MS_MS_GF_SpecEValue, "SpecEValue" },
    { MS_MS_GF_, MS_MS_GF_PepQValue, "PepQValue" },
    { MS_MS_GF_, MS_MS_GF_PEP, "PEP" },
    { MS_Comet, MS_Comet_xcorr, "xcorr" },
    { MS_Comet, MS_Comet_deltacn, "deltacn" },
    { MS_Comet, MS_Comet_deltacnstar, "deltacnstar" },
    { MS_Comet, MS_Comet_sprank, "sprank" },
    { MS_Comet, MS_Comet_spscore, "spscore" },
    { MS_Comet, MS_Comet_expectation_value, "expect" }
};

const size_t scoreTranslationTableSize = sizeof(scoreTranslationTable) / sizeof(ScoreTranslation);

struct ScoreTranslator : public boost::singleton<ScoreTranslator>
{
    ScoreTranslator(boost::restricted)
    {
        preferredScoreNameBySoftwareAndScoreCVID[CVID_Unknown][CVID_Unknown] = "";

        for (size_t i = 0; i < scoreTranslationTableSize; ++i)
        {
            const ScoreTranslation& st = scoreTranslationTable[i];
            vector<string> names;
            bal::split(names, st.scoreNames, bal::is_any_of(";"));
            if (names.empty())
                throw runtime_error("[mzRefiner::ScoreTranslator] Invalid software name list.");

            preferredScoreNameBySoftwareAndScoreCVID[st.softwareCVID][st.scoreCVID] = names[0];
            for (size_t j = 0; j < names.size(); ++j)
                scoreCVIDBySoftwareAndScoreName[st.softwareCVID][names[j]] = st.scoreCVID;
        }
    }

    CVID translate(CVID softwareCVID, const string& scoreName) const
    {
        map<CVID, map<string, CVID, ci_less> >::const_iterator itr = scoreCVIDBySoftwareAndScoreName.find(softwareCVID);
        if (itr == scoreCVIDBySoftwareAndScoreName.end())
            return CVID_Unknown;
        map<string, CVID, ci_less>::const_iterator itr2 = itr->second.find(scoreName);
        if (itr2 == itr->second.end())
            return CVID_Unknown;
        return itr2->second;
    }

    const string& translate(CVID softwareCVID, CVID scoreCVID) const
    {
        map<CVID, map<CVID, string> >::const_iterator itr = preferredScoreNameBySoftwareAndScoreCVID.find(softwareCVID);
        if (itr == preferredScoreNameBySoftwareAndScoreCVID.end())
            return empty;
        map<CVID, string>::const_iterator itr2 = itr->second.find(scoreCVID);
        if (itr2 == itr->second.end())
            return empty;
        return itr2->second;
    }

private:
    // TODO: use boost::multi_index?
    map<CVID, map<CVID, string> > preferredScoreNameBySoftwareAndScoreCVID;
    map<CVID, map<string, CVID, ci_less> > scoreCVIDBySoftwareAndScoreName;
    const string empty;
};

/*******************************************************************************************************************************************
* END Chunk of code copied from pwiz/data/identdata/Serializer_pepXML.cpp
********************************************************************************************************************************************/


//
// CVConditionalFilter
//

PWIZ_API_DECL CVConditionalFilter::CVConditionalFilter(CVConditionalFilterConfigData configData)
{
    updateFilter(configData.software, configData.cvTerm, configData.rangeSet, configData.step, configData.maxSteps);
}

PWIZ_API_DECL CVConditionalFilter::CVConditionalFilter(CVID software, string cvTerm, string rangeSet, double step, int maxStep)
{
    updateFilter(software, cvTerm, rangeSet, step, maxStep);
}

PWIZ_API_DECL CVConditionalFilter::CVConditionalFilter(CVID software, string cvTerm, double maxValue, double minValue, bool useMax, bool useMin, double step, int maxStep)
{
    updateFilter(software, cvTerm, maxValue, minValue, useMax, useMin, step, maxStep);
}

PWIZ_API_DECL void CVConditionalFilter::updateFilter(CVID software, string cvTerm, double maxValue, double minValue, bool useMax, bool useMin, double step, int maxStep)
{
    scoreName = cvTerm;
    useName = false;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    // Matt Chambers
    // Let the user specify a score name to look for. I think cvTermInfo() can also look up a term by name? Could be misremembering that though.
    // You're probably better off just taking the name of the score the user wants to use, iterating through each param in the SpectrumIdentificationItem, 
    // doing cvTermInfo(<that CVID>) and then doing a search for the score name as a substring of cvTermInfo::name 
    // (actually, use bal::iends_with(cvTermInfo(cvid).name(), userSpecifiedScore)
    //
    //
    // -Actually, what I was thinking of was how the pepXML conversion process can go back and forth from short names
    // This is contained only in pwiz/data/identdata/Serializer_pepXML.cpp
    // I copied the desired code from that file to this one.
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // This score translator is cheap - will run pretty fast - small subset of full CV
    cvid = ScoreTranslator::instance->translate(software, cvTerm);
    if (cvid == CVID_Unknown)
    {
        // Then run the more expensive one.
        CVTranslator findCV;
        cvid = findCV.translate(cvTerm);
        if (cvid == CVID_Unknown)
        {
            // TODO: log output at a high detail level.
            //cout << "Warning: cvTerm for \"" << cvTerm << "\" not found. Will search scores for the provided term." << endl;
            useName = true;
        }
    }
    if (!useName)
    {
        scoreName = cvTermInfo(cvid).name;
    }

    string threshold = "";
    isMin = false;
    isMax = false;
    if (!useMax)
    {
        maxValue = numeric_limits<double>::max();
        threshold = ">= " + lexical_cast<string>(minValue);
        isMin = true;
    }
    else if (!useMin)
    {
        minValue = -maxValue;
        threshold = "<= " + lexical_cast<string>(maxValue);
        isMax = true;
    }
    else
    {
        threshold = lexical_cast<string>(minValue) + " <= MME <= " + lexical_cast<string>(maxValue);
    }
    threshold = threshold;
    max = maxValue;
    min = minValue;
    this->step = step;
    maxSteps = maxStep;
    stepCount = 0;
    center = (min + max) / 2.0;
    // Allow for tests where the value must be outside of a certain range
    isAnd = minValue < maxValue;
}

/*************************************************************************************
* Implementation of a DoubleSet, like an IntSet
* There is probably a better way, but I couldn't find one.
* The return value is a vector of two doubles, with minValue at 0 and maxValue at 1
* If the range was something like "-1" or "1-", the minValue/maxValue (whichever was not specified)
*     is set to lowest/highest (respectively) possible value for double
**************************************************************************************/
vector<double> parseDoubleSet(string rangeSet)
{
    double maxValue = numeric_limits<double>::max();
    double minValue = -maxValue;

    // [200,] [,-200] [-5,5] [5,-5] [1,5]
    if (rangeSet[0] == '[')
    {
        string inner = rangeSet.substr(1, rangeSet.length() - 2);
        string lower = inner.substr(0, inner.rfind(','));
        if (lower.length() > 0)
        {
            minValue = boost::lexical_cast<double>(lower);
        }
        if (inner.rfind(',') != string::npos)
        {
            string upper = inner.substr(inner.rfind(',') + 1);
            if (upper.length() > 0)
            {
                maxValue = boost::lexical_cast<double>(upper);
            }
        }
    }
    else
    {
        // for less than negative value, use "--(number)"
        // "-(number)" will interpret as less than (positive number)
        // allowed formats: "-(num)" "(num)-(num)" "(num)-"
        // -1.0e-10, --1.0e-10, 5.0-, 1-5 
        vector<string> numbers;
        string numBuilder = "";
        string numTester = "1234567890.eE";
        bool startMinus = false;
        bool endMinus = false;
        bool separated = false;
        for (size_t i = 0; i < rangeSet.length(); i++)
        {
            if (numTester.find(rangeSet[i]) != string::npos)
            {
                // all digits, plus '.' and 'e'/'E'
                numBuilder += rangeSet[i];
            }
            else if (rangeSet[i] == '-' && i != 0 && rangeSet[i - 1] == 'e')
            {
                // A '-' that modifies an exponent
                numBuilder += rangeSet[i];
            }
            else if (rangeSet[i] == '-')
            {
                //handle other '-'
                if (i == 0)
                {
                    startMinus = true;
                }
                else if (i == 1)
                {
                    numBuilder += rangeSet[i];
                }
                else if (i == rangeSet.length() - 1)
                {
                    if (!separated)
                    {
                        endMinus = true;
                        if (startMinus)
                        {
                            startMinus = false;
                            numBuilder = "-" + numBuilder;
                        }
                    }
                    else
                    {
                        //report error
                        throw pwiz::util::user_error("[mzRefiner::parseRange] syntax error (start and end specified, with an infinite end also)");
                    }
                }
                else
                {
                    if (!separated)
                    {
                        separated = true;
                        if (startMinus && numBuilder[0] != '-')
                        {
                            numBuilder = "-" + numBuilder;
                            startMinus = false;
                        }
                        numbers.push_back(numBuilder);
                        numBuilder = "";
                    }
                    else
                    {
                        numBuilder += rangeSet[i];
                    }
                }
            }
            else
            {
                // Error: not a valid digit/character
                throw pwiz::util::user_error("[mzRefiner::parseRange] invalid characters");
            }
        }
        numbers.push_back(numBuilder);
        if ((startMinus && endMinus) || ((startMinus || endMinus) && separated))
        {
            //report error - searching both directions from a value, or other invalid syntax
            throw pwiz::util::user_error("[mzRefiner::parseRange] invalid syntax");
        }
        vector<double> numbers2(numbers.size(), 0.0);
        for (size_t i = 0; i < numbers.size(); i++)
        {
            numbers2[i] = lexical_cast<double>(numbers[i]);
        }
        if (startMinus)
        {
            maxValue = numbers2[0];
        }
        else if (endMinus)
        {
            minValue = numbers2[0];
        }
        else
        {
            minValue = numbers2[0];
            maxValue = numbers2[1];
        }
    }
    vector<double> set;
    set.push_back(minValue);
    set.push_back(maxValue);
    return set;
}

/*************************************************************************************
* Step in the creation of a filter when using a DoubleSet for the threshold.
**************************************************************************************/
PWIZ_API_DECL void CVConditionalFilter::updateFilter(CVID software, string cvTerm, string rangeSet, double step, int maxStep)
{
    double maxValue;
    double minValue;
    bool useMax = false;
    bool useMin = false;

    vector<double> threshold = parseDoubleSet(rangeSet);
    useMin = true;
    useMax = true;
    minValue = threshold[0];
    maxValue = threshold[1];

    updateFilter(software, cvTerm, maxValue, minValue, useMax, useMin, step, maxStep);
}

/*****************************************************************************************************************
* Evaluate if the spectrum identification item passes the filter
*****************************************************************************************************************/
PWIZ_API_DECL bool CVConditionalFilter::passesFilter(SpectrumIdentificationItemPtr& sii, double& scoreVal) const
{
    bool found = false;
    double value = 0;
    if (!useName)
    {
        vector<CVParam>::iterator it = find_if(sii->cvParams.begin(), sii->cvParams.end(), CVParamIs(cvid));
        if (it == sii->cvParams.end())
        {
            return false;
        }
        found = true;
        value = it->valueAs<double>();
    }
    else
    {
        // Handle search userParams and cvParams for the score name
        // Search the userParams first, under the assumption that we would have matched a CVID if it was a cvParam.
        BOOST_FOREACH(UserParam &up, sii->userParams)
        {
            if (bal::iends_with(up.name, scoreName))
            {
                found = true;
                value = up.valueAs<double>();
                break;
            }
        }
        if (!found)
        {
            BOOST_FOREACH(CVParam &cvp, sii->cvParams)
            {
                if (bal::iends_with(cvp.name(), scoreName))
                {
                    found = true;
                    value = cvp.valueAs<double>();
                    break;
                }
            }
        }
    }
    if (found)
    {
        scoreVal = value;
        if (!isAnd)
        {
            return (value <= max) || (value >= min);
        }
        return (value <= max) && (value >= min);
    }
    return false;
}

/**********************************************************************************
* A function to allow us to sort a set of values from best to worst,
*   depending on the threshold values definition of "better"
***********************************************************************************/
PWIZ_API_DECL bool CVConditionalFilter::isBetter(double lScoreVal, double rScoreVal) const
{
    if (!isAnd && isMin)
    {
        // Return true if the left value is larger
        return lScoreVal > rScoreVal;
    }
    if (!isAnd && isMax)
    {
        // Return true if the left value is smaller
        return lScoreVal < rScoreVal;
    }

    double left = abs(lScoreVal - center);
    double right = abs(rScoreVal - center);
    // Assumptions: If a score is based in a range (like -5 to 5), closer to the center is better.
    //              If a score excludes a range (value < -5 or 5 < value), further from the center is better.
    if (isAnd)
    {
        // Return true if the left value is closer to "center"
        return left < right;
    }
    else
    {
        // Return true if the left value is further from "center"
        return left > right;
    }
}

/***********************************************************************************
* Comparison function for sorting by score.
* Should probably change to prefer rank first, and the sort identical rank by score.
***********************************************************************************/
PWIZ_API_DECL bool CVConditionalFilter::isBetter(const ScanData& lData, const ScanData& rData) const
{
    // If rank == 0, probably PMF data, rely on the score used; otherwise, sort by rank unless it is equal
    if (lData.rank != 0 && rData.rank != 0 && lData.rank != rData.rank)
    {
        // A lower rank is better
        if (lData.rank < rData.rank)
        {
            return true;
        }
        return false;
    }
    return isBetter(lData.scoreValue, rData.scoreValue);
}

/********************************************************************************************
* For the edge case where you just have to have an adjustment and therefore you want values from
*   poorer scoring identifications gradually included until you have the required number of data points.
* (For the record, Sam Payne is against this, and Matt Monroe wants it.)
********************************************************************************************/
PWIZ_API_DECL bool CVConditionalFilter::adjustFilterByStep()
{
    bool hasStep = false;
    ostringstream oss;
    oss << "Adjusted filters: " << endl;
    oss << "\tOld: " << *this << endl;
    if (step != 0.0 && stepCount < maxSteps)
    {
        hasStep = true;
        if (fabs(max) != numeric_limits<double>::max())
        {
            max = max * step;
        }
        if (fabs(min) != numeric_limits<double>::max())
        {
            min = min * step;
        }
        stepCount++;
    }
    if (hasStep)
    {
        oss << "\tNew: " << *this << endl;
        cout << oss.str() << endl;
    }
    return hasStep;
}

/***************************************************************************************************************
* Overloaded output to easily output filter state
***************************************************************************************************************/
ostream& operator<<(ostream& out, CVConditionalFilter filter)
{
    CVID temp = filter.getCVID();
    double max = filter.getMax();
    double min = filter.getMin();
    bool isAnd = filter.getAnd();
    CVTermInfo cv = cvTermInfo(temp);

    out << filter.getScoreName() << "; " << min << " <= value " << (isAnd ? "&&" : "||") << " value <= " << max;

    return out;
}


//
// SpectrumList_MZRefiner::Impl
//

class SpectrumList_MZRefiner::Impl
{
public:
    Impl() : bad(0), haveAllStartTimes(false) {}
    AdjustmentObjectPtr adjust;
    string identFilePath;
    vector<ScanData> data;
    CVConditionalFilterPtr filter;
    bool haveAllStartTimes;
    void configureShift(const MSData& msd, string identFile, CVConditionalFilter::CVConditionalFilterConfigData filterConfigData, pwiz::util::IterationListenerRegistry* ilr);

private:
    int bad;
    void processIdentData(const MSData& msd, CVConditionalFilter::CVConditionalFilterConfigData filterConfigData, pwiz::util::IterationListenerRegistry* ilr);
    void getScanTimesFromMSData(const MSData& msd, pwiz::util::IterationListenerRegistry* ilr);
    void shiftCalculator();
    bool cleanIsotopes(ScanData& sd); // Utility function. Doesn't really need to be a member function, but it isn't used anywhere else than in processIdentData
};

/********************************************************************************
* Primary function for SpectrumList_MZRefiner::Impl
* Performs the entire workflow for creating the shift.
*******************************************************************************/
void SpectrumList_MZRefiner::Impl::configureShift(const MSData& msd, string identFile, CVConditionalFilter::CVConditionalFilterConfigData filterConfigData, pwiz::util::IterationListenerRegistry* ilr)
{
    identFilePath = identFile;
    bad = 0;
    adjust.reset();
    // Will initialize the filter, using the software cv if necessary
    processIdentData(msd, filterConfigData, ilr);
    // Will only read the MSData when the identfile did not contain the scan start time(or corresponding cvParam) for at least one result
    getScanTimesFromMSData(msd, ilr);
    shiftCalculator();
}

/****************************************************************************************
* Some identification programs output the experimental m/z with isotope error removed, some do not.
* This gives a way to clean up a good number of the results so they are usable
* There is undoubtedly room for improvement.
*****************************************************************************************/
bool SpectrumList_MZRefiner::Impl::cleanIsotopes(ScanData& sd)
{
    double windowAdj = 0.05;
    double chargeWithSign = sd.charge;
    if (sd.massError < 0)
    {
        chargeWithSign = -chargeWithSign;
    }
    bool changed = false;
    if (sd.charge != 0)
    {
        for (int i = 1; i <= 5; ++i)
        {
            double adjustment = (double)i / chargeWithSign;
            // Check to see if the adjustment will put the mass error inside of a tight window
            if ((adjustment - windowAdj) <= sd.massError && sd.massError <= (adjustment + windowAdj))
            {
                sd.experMz = sd.experMz - adjustment;
                sd.massError = sd.experMz - sd.calcMz;
                sd.ppmError = (sd.massError / sd.calcMz) * 1.0e6;
                changed = true;
                break;
            }
        }
    }
    return changed;
}

/*******************************************************************************************
* Read the mzid file, and pull out the necessary data.
*******************************************************************************************
* There is some console output in this function which may not be very useful in the final version.
******************************************************************************************/
void SpectrumList_MZRefiner::Impl::processIdentData(const MSData& msd, CVConditionalFilter::CVConditionalFilterConfigData filterConfigData, pwiz::util::IterationListenerRegistry* ilr)
{
    haveAllStartTimes = true;
    double isotopeScreenAdj = 0.15; // 0.05 less than charge 5 error
    double isotopeFilter = 0.20; // May include charge 5 unadjusted error, but it is not common
    double ppmErrorLimit = 50.0; // Used as the negative and positive limit.

    // TODO: log output at a high detail level...
    cout << "Reading file \"" << identFilePath << "\"..." << endl;
    //ilr->broadcastUpdateMessage(); ////////////////////////////////////////////////////////////////////////////////////////////////   ADD ILR USAGE  //////////////////////////////////////////////////////////
    IdentDataFile b(identFilePath, 0, ilr);

    BOOST_FOREACH(const AnalysisSoftwarePtr &as, b.analysisSoftwareList)
    {
        // softwareName must have either a cvParam or a userParam
        // If it has a userParam, we don't really care - the identification software probably doesn't have any score cvParams either
        if (!as->softwareName.cvParams.empty())
        {
            filterConfigData.software = as->softwareName.cvParams.front().cvid; /// Get the analysis software name.....
            break; // Get the name of the first analysis software only; might need to change to the last one.
        }
    }

    filter = CVConditionalFilterPtr(new CVConditionalFilter(filterConfigData));
    sortFilter f(filter); // Make a functor out of the filter.

    int lessBad = 0;
    int excess = 0;
    bool adjustedFilter = true;
    vector<ScanData> tempData;
    while (adjustedFilter)
    {
        adjustedFilter = false;
        data.clear();
        bad = 0;
        lessBad = 0;
        excess = 0;
        BOOST_FOREACH(const SpectrumIdentificationListPtr& sil, b.dataCollection.analysisData.spectrumIdentificationList)
        {
            BOOST_FOREACH(const SpectrumIdentificationResultPtr& sir, sil->spectrumIdentificationResult)
            {
                size_t scanId;
                string nativeID = sir->spectrumID;
                /********************************************************************************************************
                * Formats and scan id/native id:
                * Ident formats:
                * * mzIdentML: Has Native ID, not necessarily a scan number
                * * pepXML: Has Native ID, start/end scan may constitute a scan number
                *
                * MSData formats:
                * * Format_Text: (similar to mzML, without XML tags) Has Native ID       Tested (as input): No
                * * Format_mzXML: Has a scan number (no Native ID)                       Tested (as input): No
                * * Format_mzML: Has Native ID                                           Tested (as input): Yes
                * * Format_MGF: No scan number or Native ID (only RTINSECONDS)           Tested (as input): No
                * * Format_MS1: No Native ID                                             Tested (as input): No
                * * Format_CMS1: No Native ID                                            Tested (as input): No
                * * Format_MS2: No Native ID                                             Tested (as input): No
                * * Format_CMS2: No Native ID                                            Tested (as input): No
                * * Format_MZ5: ????                                                     Tested (as input): No
                *
                * This chunk of code will probably only be useful in limited cases. The native ID and scan number are
                *    now only used when we need to read the scan start times from the data file, because they will (hopefully)
                *    allow us to match up the identifications with their respective spectra in the data file.
                *    The mzIdentML output from MS-GF+ currently does not have scan start times, so this has been tested with
                *    mzML and mzIdentML input for Thermo data and Bruker QqTof data. Will be tested on Agilent QTOF data.
                *    The scanId/scan number will be a problem with Waters instruments in its current implementation.
                *********************************************************************************************************/
                // Backwards compatibility, it is correct on older files (where the spectrumID value is inaccurate)
                // This is part of the output from MS-GF+; but not reliable for anything else that I know of.
                CVParam scanNum;
                scanNum = sir->cvParam(MS_scan_number_s__OBSOLETE);
                CVParam peakScan = sir->cvParam(MS_peak_list_scans);
                if (scanNum.empty())
                {
                    scanNum = sir->cvParam(MS_peak_list_scans);
                }
                if (scanNum.empty())
                {
                    CVID natIdType = pwiz::msdata::id::getDefaultNativeIDFormat(msd);
                    scanId = lexical_cast<size_t>(pwiz::msdata::id::translateNativeIDToScanNumber(natIdType, sir->spectrumID));
                }
                else
                {
                    scanId = scanNum.valueAs<size_t>();
                }

                tempData.clear();

                // Check for and store the scan start time, if provided.
                double scanStartTime = 0.0;
                CVParam scanTime = sir->cvParam(MS_scan_start_time);
                if (scanTime.empty())
                {
                    scanTime = sir->cvParam(MS_retention_time);
                }
                if (scanTime.empty())
                {
                    scanTime = sir->cvParam(MS_retention_time_s__OBSOLETE);
                }
                if (scanTime.empty())
                {
                    haveAllStartTimes = false;
                }
                else
                {
                    scanStartTime = scanTime.timeInSeconds(); // Make sure to store it as seconds.
                }

                BOOST_FOREACH(SpectrumIdentificationItemPtr& sii, sir->spectrumIdentificationItem)
                {
                    double calcMz = sii->calculatedMassToCharge;
                    double experMz = sii->experimentalMassToCharge;
                    double massError = experMz - calcMz;
                    double ppmError = (massError / calcMz) * 1.0e6;
                    double scoreVal = 0.0;

                    if (filter->passesFilter(sii, scoreVal))
                    {
                        ScanData sd;
                        sd.scanId = scanId;
                        sd.nativeID = nativeID;
                        sd.scanTime = scanStartTime;
                        sd.calcMz = calcMz;
                        sd.experMz = experMz;
                        sd.charge = sii->chargeState;
                        sd.rank = sii->rank;
                        sd.massError = massError;
                        sd.ppmError = ppmError;
                        sd.scoreValue = scoreVal;

                        bool cleaned = false;
                        bool worked = false;
                        if (sd.massError < -isotopeScreenAdj || isotopeScreenAdj < sd.massError)
                        {
                            cleaned = true;
                            worked = cleanIsotopes(sd);
                        }
                        if ((-isotopeFilter < sd.massError && sd.massError < isotopeFilter) && (-ppmErrorLimit <= sd.ppmError && sd.ppmError <= ppmErrorLimit))
                        {
                            // If there is only one match for the spectrum, use it
                            if (sir->spectrumIdentificationItem.size() == 1)
                            {
                                data.push_back(sd);
                            }
                            else
                            {
                                // Since there are multiple identifications for this spectrum, we will temporarily store them to only use the best one.
                                tempData.push_back(sd);
                            }
                        }
                        else
                        {
                            ++bad;
                            ++lessBad;
                        }
                    }
                    else
                    {
                        ++bad;
                    }
                } // End BOOST_FOREACH SpectrumIdentificationItem
                // Sort multiple SpectrumIdentificationItems by score, and only keep the best.
                if (!tempData.empty())
                {
                    if (tempData.size() == 1)
                    {
                        //cout << "Ended up with only one item in the tempData vector!" << endl;
                        // Only one of the multiple matches passed the threshold...
                        data.push_back(tempData.front());
                    }
                    else
                    {
                        // Sort by score value
                        // could also use : (but requires more allocations, or something of the sort.)
                        // std::sort(tempData.begin(), tempData.end(), sortFilter(filter));
                        std::sort(tempData.begin(), tempData.end(), f);
                        // Select and insert highest score value
                        data.push_back(tempData.front());
                        // increment excess by number not used
                        excess += tempData.size() - 1;
                    }
                }
            } // End BOOST_FOREACH SpectrumIdentificationResult
        } // End BOOST_FOREACH SpectrumIdentificationList
        if (data.size() < MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT)
        {
            adjustedFilter = filter->adjustFilterByStep();
        }
    }

    if (data.size() < MINIMUM_RESULTS_FOR_GLOBAL_SHIFT)
    {
        // If there are less than 100 high-quality data points, it's not worth shifting
        cout << "Excluding file \"" << identFilePath << "\" from data set." << endl;
        cout << "\tLess than " << MINIMUM_RESULTS_FOR_GLOBAL_SHIFT << " (" << data.size() << ") results after filtering." << endl;
        data.clear();
        throw runtime_error("[mzRefiner::ctor] Less than " + lexical_cast<string>(MINIMUM_RESULTS_FOR_GLOBAL_SHIFT)+" (" + lexical_cast<string>(data.size()) + ") values in identfile that pass the threshold.");
    }
    else if (data.size() < MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT)
    {
        // 500 is kind of arbitrary, but it seemed like a good number to use as a requirement for dependent data shifts
        // If there are only 500 data points, there will be very limited shift smoothing.
        cout << "Low number of good identifications found. Will not perform dependent shifts." << endl;
        cout << "\tLess than " << MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT << " (" << data.size() << ") results after filtering." << endl;
    }
    // Number if identifications that didn't pass the threshold.
    cout << "\t" << "Filtered out " << bad - lessBad << " identifications because of score." << endl;
    // Number of identifications that passed the threshold, but the isotope error couldn't be fixed (out of range)
    cout << "\t" << "Filtered out " << lessBad << " identifications because of mass error." << endl;
}

/***************************************************************
* Basic function to read the scan times from an MSData object
* A good improvement would be to use nativeID indexes to only get the spectra we need.
* But that improvement would be limited in use to files input from native, mzML, mzXML, and (maybe) text.
****************************************************************/
void SpectrumList_MZRefiner::Impl::getScanTimesFromMSData(const MSData& msd, pwiz::util::IterationListenerRegistry* ilr)
{
    if (haveAllStartTimes || data.size() < MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT)
    {
        // Don't bother reading the scan start times if we don't have enough results to run the dependent shifts
        return;
    }

    // TODO: Log at a high detail level
    cout << "Reading scan start times from the data file...." << endl;
    // Report what is going on using the iteration listener... /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //ilr->broadcastUpdateMessage()
    const SpectrumListPtr& sl = msd.run.spectrumListPtr;
    sl.get();
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    // Probably need to get something better than scanId to sort by, but needs to be reliable.
    // Searching would be really expensive
    // (Using nativeID indexes would remove this issue) (but would not be applicable to all possible MSData input types)
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    std::sort(data.begin(), data.end(), ScanData::byScanId);
    size_t dataIndex = 0;
    for (size_t i = 0; i < sl->size() && dataIndex < data.size(); ++i)
    {
        SpectrumPtr s = sl->spectrum(i, true);
        if (!s)
        {
            continue;
        }
        if (s->id != data[dataIndex].nativeID)
        {
            continue;
        }
        vector<double> startTimes;
        BOOST_FOREACH(const Scan &scan, s->scanList.scans)
        {
            startTimes.push_back(scan.cvParam(MS_scan_start_time).timeInSeconds()); // Make sure it's in seconds...
        }
        std::sort(startTimes.begin(), startTimes.end());
        while (s->id == data[dataIndex].nativeID)
        {
            data[dataIndex].scanTime = startTimes[0];
            ++dataIndex;
            if (dataIndex >= data.size())
            {
                break;
            }
        }
    }
}

/********************************************************************************
* Calculate statistics, and determine best shifting strategy
********************************************************************************
* This function has a lot of output that has been useful in development,
* most of which will probably be removed from the final version, depending
* on how useful it is for further analysis.
*******************************************************************************/
void SpectrumList_MZRefiner::Impl::shiftCalculator()
{
    adjust.reset();
    // TODO: log output at a high detail level...
    int widths = 50;
    cout << std::left;

    // Sanity check
    if (data.size() < MINIMUM_RESULTS_FOR_GLOBAL_SHIFT)
    {
        return;
    }
    // Get the global shift data
    AdjustSimpleGlobalPtr globalShift(new AdjustSimpleGlobal());
    globalShift->calculate(data);

    // Output the global shift stats
    cout << "\t" << setw(widths) << "Good data points: " << data.size() << endl;
    // Average drift, the mode in histBins
    cout << "\t" << setw(widths) << "Systematic Drift (mean): " << globalShift->getAvgError() << endl;
    cout << "\t" << setw(widths) << "Systematic Drift (mode): " << globalShift->getModeError() << endl;
    cout << "\t" << setw(widths) << "Systematic Drift (median): " << globalShift->getMedianError() << endl;
    cout << "\t" << setw(widths) << "Measurement Precision (MAD ppm): " << globalShift->getMAD() << endl;

    // Check for a decent peak; if we don't have one, exit/throw exception
    if (!(globalShift->checkForPeak()))
    {
        // Return null - no shift will be performed
        cout << "Chose no shift - poor histogram peak." << endl;
        throw runtime_error("[mzRefiner::shiftCalculator] No significant peak (ppm error histogram) found.");
        return;
        // Better?? - throw an exception
    }

    string chosen = "";

    // Only calculate the dependent shifts if there is enough data.
    if (data.size() >= MINIMUM_RESULTS_FOR_DEPENDENT_SHIFT)
    {
        double gShift = globalShift->getShift();
        double gStDev = globalShift->getStDev();
        double gMAD = globalShift->getMAD();
        AdjustByScanTimePtr scanTimeShift(new AdjustByScanTime(gShift, gStDev, gMAD));
        AdjustByMassToChargePtr mzShift(new AdjustByMassToCharge(gShift, gStDev, gMAD));

        // Perform their respective calculations
        scanTimeShift->calculate(data);
        mzShift->calculate(data);

        // Dependent shift data
        cout << endl;
        cout << "\t" << setw(widths) << "Average BinWise MAD (scan): " << scanTimeShift->getRoughMAD() << endl;
        cout << "\t" << setw(widths) << "Average BinWise MAD (smoothed scan): " << scanTimeShift->getSmoothedMAD() << endl;
        cout << "\t" << setw(widths) << "Average BinWise MAD (mz): " << mzShift->getRoughMAD() << endl;
        cout << "\t" << setw(widths) << "Average BinWise MAD (smoothed mz): " << mzShift->getSmoothedMAD() << endl;
        cout << endl;
        cout << "\t" << setw(widths) << "Expected % Improvement (MAD scan): " << scanTimeShift->getRoughPctImpMAD() << endl;
        cout << "\t" << setw(widths) << "Expected % Improvement (MAD smoothed scan): " << scanTimeShift->getSmoothedPctImpMAD() << endl;
        cout << "\t" << setw(widths) << "Expected % Improvement (MAD mz): " << mzShift->getRoughPctImpMAD() << endl;
        cout << "\t" << setw(widths) << "Expected % Improvement (MAD smoothed mz): " << mzShift->getSmoothedPctImpMAD() << endl;

        double improvThreshold = 3.0;
        // Determine the best solution
        // Prefer a scan time shift over a mass to charge shift if the improvement for the mass to charge shift is less than 10% more
        //if (scanTimeShift->getPctImp() > improvThreshold && scanTimeShift->getPctImp() > (mzShift->getPctImp() - 10.0))
        if (scanTimeShift->getPctImp() > improvThreshold && scanTimeShift->getPctImp() > mzShift->getPctImp())
        {
            chosen = "Chose scan time shift...";
            this->adjust = scanTimeShift;
        }
        else if (mzShift->getPctImp() > improvThreshold)
        {
            chosen = "Chose mass to charge shift...";
            this->adjust = mzShift;
        }
    }
    // If we didn't/couldn't choose a dependent shift, do a global shift
    if (!this->adjust)
    {
        chosen = "Chose global shift...";
        this->adjust = globalShift;
    }

    cout << chosen << endl;
    cout << "\t" << setw(widths) << ("Applied " + this->adjust->getShiftOutString()) << this->adjust->getShiftRange() << endl;
    cout << "\t" << setw(widths) << "Estimated final stDev: " << this->adjust->getStDev() << endl;
    cout << "\t" << setw(widths) << "Estimated tolerance for 99% (ppm): 0 +/- " << this->adjust->getStDev() * 3 << endl;
    cout << "\t" << setw(widths) << "Estimated final MAD: " << this->adjust->getMAD() << endl;
    // Estimated conversion scale from MAD to StDev, Normal Population: 1 StDev ~= 1.4826 MAD
    cout << "\t" << setw(widths) << "Estimated MAD tolerance for 99% (ppm): 0 +/- " << this->adjust->getMAD() * (3 * 1.4826) << endl;
}


/**********************************************************************************************
 * Check an instrument configuration for a final high-res analyzer
 ***********************************************************************************************/
bool configurationIsHighRes(const InstrumentConfigurationPtr& ic)
{
    Component* la = &(ic->componentList.analyzer(0));
    // Get the last analyzer
    BOOST_FOREACH(Component &c, ic->componentList)
    {
        if (c.type == ComponentType_Analyzer)
        {
            la = &c;
        }
    }
    // Look for Orbitrap, FT, or TOF to test for high-res
    if (la->hasCVParam(MS_orbitrap)
        || la->hasCVParam(MS_time_of_flight)
        || la->hasCVParam(MS_fourier_transform_ion_cyclotron_resonance_mass_spectrometer)
        || la->hasCVParam(MS_stored_waveform_inverse_fourier_transform))
    {
        return true;
    }
    return false;
}


//
// SpectrumList_MZRefiner
//

PWIZ_API_DECL SpectrumList_MZRefiner::SpectrumList_MZRefiner(
    const MSData& msd, const string& identFilePath, const string& cvTerm, const string& rangeSet, double step, int maxStep, pwiz::util::IterationListenerRegistry* ilr)
:   SpectrumListWrapper(msd.run.spectrumListPtr), impl_(new Impl())
{
    // Determine if file has High-res scans...
    bool hasHighRes = false;
    BOOST_FOREACH(const InstrumentConfigurationPtr& ic, msd.instrumentConfigurationPtrs)
    {
        // Set isHighRes to true, don't allow it to be set back to false after it has been set to true.
        if (configurationIsHighRes(ic))
        {
            hasHighRes = true;
        }
    }
    // Exit if we don't have any high-res data
    if (!hasHighRes)
    {
        cerr << "\tError: No high-resolution data in input file.\n\tSkipping mzRefiner." << endl;
        throw pwiz::util::user_error("[mzRefiner::ctor] No high-resolution data in input file.");
    }

    // Configure and run shift calculations
    CVConditionalFilter::CVConditionalFilterConfigData filterConfigData;
    filterConfigData.cvTerm = cvTerm;
    filterConfigData.rangeSet = rangeSet;
    filterConfigData.step = step;
    filterConfigData.maxSteps = maxStep;

    impl_->configureShift(msd, identFilePath, filterConfigData, ilr);

    // Exit if the shift calculations did not succeed for some reason
    if (impl_->data.size() == 0 || !impl_->adjust)
    {
        // Throw exception: Could not shift - Reason unknown (specific reasons are thrown where they occur)
        throw runtime_error("[mzRefiner::ctor] Shift calculation failed.");
    }
    // add processing methods to the copy of the inner SpectrumList's data processing
    ProcessingMethod method;
    method.order = dp_->processingMethods.size();
    method.cvParams.push_back(CVParam(MS_m_z_calibration));

    // userparams:
    // All parameters that affect the outcome that are not available as cvParams
    // add identfile name, path
    method.userParams.push_back(UserParam("Identification File", identFilePath));
    // filter score param name
    method.userParams.push_back(UserParam("Filter score name", impl_->filter->getScoreName()));
    // filter score threshold
    method.userParams.push_back(UserParam("Filter score threshold", impl_->filter->getThreshold()));
    // shift type
    method.userParams.push_back(UserParam("Shift dependency", impl_->adjust->getPrettyAdjustment()));
    // shift range
    method.userParams.push_back(UserParam("Shift range", impl_->adjust->getShiftRange()));
    // global ppm error (on all)
    method.userParams.push_back(UserParam("Global Median Mass Measurement Error (PPM)", lexical_cast<string>(impl_->adjust->getGlobalShift())));

    if (!dp_->processingMethods.empty())
        method.softwarePtr = dp_->processingMethods[0].softwarePtr;

    dp_->processingMethods.push_back(method);
}


PWIZ_API_DECL SpectrumPtr SpectrumList_MZRefiner::spectrum(size_t index, bool getBinaryData) const
{
    if (!impl_->adjust)
    {
        // This shouldn't be hit with the exceptions being thrown elsewhere; still here for safety
        // May be useful in the future with chained filters and a user option to continue with failed filters.
        return inner_->spectrum(index, getBinaryData);
    }
    SpectrumPtr originalSpectrum = inner_->spectrum(index, getBinaryData);  
    
    // Determine if is High-res scan...
    bool isHighRes = false;
    double scanTime = 0.0;
    CVParam scanStartTime;
    BOOST_FOREACH(Scan &sls, originalSpectrum->scanList.scans)
    {
        // Set isHighRes to true, don't allow it to be set back to false after it has been set to true.
        if (configurationIsHighRes(sls.instrumentConfigurationPtr))
        {
            isHighRes = true;
        }

        if (scanStartTime.empty())
        {
            scanStartTime = sls.cvParam(MS_scan_start_time);
        }
    }
    scanTime = scanStartTime.timeInSeconds(); // Use seconds

    // Commonly used items, declare them only once - each use is atomic:
    //      get a iterator to the desired CVParam, read and store shifted value to 'value',
    //      convert 'value' to string and overwrite the CVParam's value variable.
    vector<CVParam>::iterator it;
    double value = 0;
    
    // On a high-res result, we want to adjust more than just precursor data; we can shift the m/z values in the data array
    if (isHighRes)
    {
        double value = 0;
        // Adjust the metadata
        // Using an iterator to allow direct overwrite of the value - other methods return a copy of the CVParam
        it = find_if(originalSpectrum->cvParams.begin(), originalSpectrum->cvParams.end(), CVParamIs(MS_base_peak_m_z));
        if (it != originalSpectrum->cvParams.end())
        {
            value = impl_->adjust->shift(scanTime, it->valueAs<double>());
            it->value = boost::lexical_cast<std::string>(value);
        }
        it = find_if(originalSpectrum->cvParams.begin(), originalSpectrum->cvParams.end(), CVParamIs(MS_lowest_observed_m_z));
        if (it != originalSpectrum->cvParams.end())
        {
            value = impl_->adjust->shift(scanTime, it->valueAs<double>());
            it->value = boost::lexical_cast<std::string>(value);
        }
        it = find_if(originalSpectrum->cvParams.begin(), originalSpectrum->cvParams.end(), CVParamIs(MS_highest_observed_m_z));
        if (it != originalSpectrum->cvParams.end())
        {
            value = impl_->adjust->shift(scanTime, it->valueAs<double>());
            it->value = boost::lexical_cast<std::string>(value);
        }
        
        // Adjust the spectrum data (all m/z values)
        BOOST_FOREACH(BinaryDataArrayPtr &bda, originalSpectrum->binaryDataArrayPtrs)
        {
            if (bda->hasCVParam(MS_m_z_array))
            {
                BOOST_FOREACH(double &mass, bda->data)
                {
                    mass = impl_->adjust->shift(scanTime, mass);
                }
            }
        }
    } // High res adjustments
    
    // return MS1 without precursor adjustments...
    CVParam msLevel = originalSpectrum->cvParam(MS_ms_level);
    // MS/MS precursor adjustment : Find and check the precursor for high-res-ness ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    if (!msLevel.empty() && msLevel.valueAs<int>() >= 2)
    {
        // MS2 spectrum, with only precursor adjustment right now
        BOOST_FOREACH(Precursor &p, originalSpectrum->precursors)
        {
            it = find_if(p.isolationWindow.cvParams.begin(), p.isolationWindow.cvParams.end(), CVParamIs(MS_isolation_window_target_m_z));
            if (it != p.isolationWindow.cvParams.end())
            {
                value = impl_->adjust->shift(scanTime, it->valueAs<double>());
                it->value = boost::lexical_cast<std::string>(value);
            }
            BOOST_FOREACH(SelectedIon &si, p.selectedIons)
            {
                it = find_if(si.cvParams.begin(), si.cvParams.end(), CVParamIs(MS_selected_ion_m_z));
                if (it != si.cvParams.end())
                {
                    value = impl_->adjust->shift(scanTime, it->valueAs<double>());
                    it->value = boost::lexical_cast<std::string>(value);
                }
            }
        }

        BOOST_FOREACH(Scan &s, originalSpectrum->scanList.scans)
        {
            // Adjust the thermo-specific Monoisotopic m/z
            BOOST_FOREACH(UserParam &up, s.userParams)
            {
                if (up.name == "[Thermo Trailer Extra]Monoisotopic M/Z:")
                {
                    value = impl_->adjust->shift(scanTime, up.valueAs<double>());
                    up.value = boost::lexical_cast<std::string>(value);
                }
            }
        }
    } // MS/MS precursor adjustment

    // Is this necessary? I'm not sure, I just copied this code from SpectrumList_PrecursorRefine.cpp.
    // What risks, if any, are there to just returning "originalSpectrum"? (Or will that wipe out all of my changes?)
    //SpectrumPtr  newSpectrum = SpectrumPtr(new Spectrum(*originalSpectrum));
    
    return originalSpectrum;
}

} // namespace analysis
} // namespace pwiz
