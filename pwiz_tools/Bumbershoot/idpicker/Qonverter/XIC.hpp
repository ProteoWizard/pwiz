//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

#ifndef _XIC_HPP_
#define _XIC_HPP_

//#include <string>
//#include <vector>
//#include <map>
//#include "pwiz/utility/misc/IterationListener.hpp"
//#include "pwiz/utility/chemistry/MZTolerance.hpp"
//#include <boost/date_time.hpp>
//#include <boost/filesystem/path.hpp>
//#include <boost/shared_ptr.hpp>
//#include <boost/enum.hpp>
#include "sqlite3pp.h"

#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>
#include <boost/accumulators/accumulators.hpp>
#include <boost/accumulators/statistics/stats.hpp>
#include <boost/accumulators/statistics/mean.hpp>
#include <boost/accumulators/statistics/min.hpp>
#include <boost/accumulators/statistics/max.hpp>
#include <boost/accumulators/statistics/error_of.hpp>
#include <boost/accumulators/statistics/error_of_mean.hpp>
#include <boost/accumulators/statistics/kurtosis.hpp>
#include <boost/accumulators/statistics/skewness.hpp>
#include <boost/accumulators/statistics/variance.hpp>
#include <boost/accumulators/framework/accumulator_set.hpp>
#include <boost/multi_index_container.hpp>
#include <boost/multi_index/member.hpp>
#include <boost/multi_index/ordered_index.hpp>
#include <boost/multi_index/random_access_index.hpp>
#include <boost/shared_array.hpp>
#include <boost/range/algorithm/lower_bound.hpp>
#include <boost/range/algorithm/upper_bound.hpp>
#include <boost/math/distributions/normal.hpp>
#include "crawdad/SimpleCrawdad.h"
#include <algorithm>
#include "Embedder.hpp"

#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE

using namespace pwiz::SimpleCrawdad;
//using namespace crawpeaks;

using namespace boost::icl;
namespace accs = boost::accumulators;
namespace bmi = boost::multi_index;


namespace XIC {

using std::string;
using std::vector;
using std::map;
using std::pair;
using pwiz::chemistry::MZTolerance;

struct XICConfiguration
{
    XICConfiguration(bool AlignRetentionTime = false, double MaxQValue = 0.05,
                     int MonoisotopicAdjustmentMin = -1, int MonoisotopicAdjustmentMax = 1,
                     int RetentionTimeLowerTolerance = 120, int RetentionTimeUpperTolerance = 120,
                     MZTolerance ChromatogramMzLowerOffset = MZTolerance(0.5, MZTolerance::MZ),
                     MZTolerance ChromatogramMzUpperOffset = MZTolerance(1.0, MZTolerance::MZ));


    bool AlignRetentionTime;
    double MaxQValue;
    int MonoisotopicAdjustmentMin;
    int MonoisotopicAdjustmentMax;
    int RetentionTimeLowerTolerance;
    int RetentionTimeUpperTolerance;
    MZTolerance ChromatogramMzLowerOffset;
    MZTolerance ChromatogramMzUpperOffset;
};

struct MS1ScanInfo
{
    string nativeID;
    double scanStartTime;
    double totalIonCurrent;
};

struct MS2ScanInfo
{
    string nativeID;
    double scanStartTime;
    bool identified;
    int msLevel; // levels higher than 2 are currently ignored
    string distinctModifiedPeptide;
    string precursorNativeID;
    double precursorMZ;
    int precursorCharge;
    double precursorIntensity;
    double precursorScanStartTime;

    bool operator< ( const MS2ScanInfo& rhs ) const
    {
        return nativeID < rhs.nativeID;
    }

    bool operator== ( const MS2ScanInfo& rhs ) const
    {
        return scanStartTime == rhs.scanStartTime &&
               nativeID == rhs.nativeID &&
               distinctModifiedPeptide == rhs.distinctModifiedPeptide &&
               precursorScanStartTime == rhs.precursorScanStartTime &&
               precursorMZ == rhs.precursorMZ &&
               precursorNativeID == rhs.precursorNativeID;
    }

};

struct Peak
{
    double startTime;
    double endTime;
    double peakTime;
    double fwhm;
    double intensity;

    Peak(double startTime = 0, double endTime = 0, double peakTime = 0, double fwhm = 0, double intensity = 0)
         : startTime(startTime), endTime(endTime), peakTime(peakTime), fwhm(fwhm), intensity(intensity)
    {}
};

struct nativeID {};
struct time {};
struct identified {};
struct intensity {};

typedef bmi::multi_index_container<MS1ScanInfo,
                                   bmi::indexed_by<
                                       bmi::random_access<>,
                                       bmi::ordered_unique<bmi::tag<nativeID>, bmi::member<MS1ScanInfo, string, &MS1ScanInfo::nativeID> >,
                                       bmi::ordered_unique<bmi::tag<time>, bmi::member<MS1ScanInfo, double, &MS1ScanInfo::scanStartTime> >
                                   >
                                  > MS1ScanMap;

typedef bmi::multi_index_container<MS2ScanInfo,
                                   bmi::indexed_by<
                                       bmi::random_access<>,
                                       bmi::ordered_unique<bmi::tag<nativeID>, bmi::member<MS2ScanInfo, string, &MS2ScanInfo::nativeID> >,
                                       bmi::ordered_unique<bmi::tag<time>, bmi::member<MS2ScanInfo, double, &MS2ScanInfo::scanStartTime> >,
                                       bmi::ordered_non_unique<bmi::tag<identified>, bmi::member<MS2ScanInfo, bool, &MS2ScanInfo::identified> >
                                   >
                                  > MS2ScanMap;

typedef bmi::multi_index_container<Peak,
                                   bmi::indexed_by<
                                       bmi::ordered_unique<bmi::tag<time>, bmi::member<Peak, double, &Peak::peakTime> >,
                                       bmi::ordered_unique<bmi::tag<intensity>, bmi::member<Peak, double, &Peak::intensity> >
                                   >
                                  > PeakList;

struct LocalChromatogram
{
    string id;
    vector<double> MS1Intensity;
    vector<double> MS1RT;
    PeakList peaks;
    boost::optional<Peak> bestPeak;

    LocalChromatogram(){}

    LocalChromatogram(const vector<double>& intens, const vector<double>& rt)
    {
        MS1Intensity = intens;
        MS1RT = rt;
    }
};

struct RegDefinedPrecursorInfo
{
    string peptide;
    double exactMZ;
    int charge;
    string mods;
    double RegTime;
    LocalChromatogram chromatogram;
    interval_set<double> scanTimeWindow;
    interval_set<double> mzWindow;
    double baselineIntensity;
};


struct XICPeptideSpectrumMatch
{
    boost::int64_t id;
    boost::int64_t peptide;
    const MS2ScanInfo* spectrum;
    double exactMZ;
    int charge;
    double score;
};

struct XICWindow
{
    double firstMS2RT;
    double lastMS2RT;
    double meanMS2RT;
    double bestScore;
    double bestScoreScanStartTime;
    string peptide;
    vector<XICPeptideSpectrumMatch> PSMs; // sorted by ascending scan time
    string distinctMatch;
    string source;

    mutable interval_set<double> preMZ;
    mutable interval_set<double> preRT;
    mutable vector<double> MS1Intensity;
    mutable vector<double> MS1RT;
    mutable PeakList peaks;
    mutable boost::optional<Peak> bestPeak;
};

typedef bmi::multi_index_container<XICWindow,
                                   bmi::indexed_by<
                                       bmi::ordered_unique<bmi::tag<time>, bmi::member<XICWindow, double, &XICWindow::firstMS2RT> >
                                   >
                                  > XICWindowList;

struct SortByScanTime
{
    bool operator() (const XICPeptideSpectrumMatch& lhs, const XICPeptideSpectrumMatch& rhs) const
    {
        return lhs.spectrum->scanStartTime < rhs.spectrum->scanStartTime;
    }
};

struct ModifyPrecursorMZ
{
    double newMZ;
    ModifyPrecursorMZ(double newMZ) : newMZ(newMZ) {}
    void operator() (MS2ScanInfo& info) const {info.precursorMZ = newMZ;}
};

double pchst ( double arg1, double arg2 );

void spline_pchip_set ( int n, double x[], double f[], double d[] );

 int chfev ( double x1, double x2, double f1, double f2, double d1, double d2,
   int ne, double xe[], double fe[], int next[] );

void spline_pchip_val ( int n, double x[], double f[], double d[],
  int ne, double xe[], double fe[] );

struct Interpolator
{
    Interpolator(vector<double>& x, vector<double>& y)
    {
        _size = x.size();

        if (_size < 4)
            return;

        BOOST_ASSERT(x.size() == y.size());

        //_ypp.reset(spline_cubic_set(_size, const_cast<double*>(&x[0]), const_cast<double*>(&y[0]), 1, 0, 1, 0));
        
        for ( int n = 0; n < x.size(); n++ )
        {
            if ( x[n] <= x[n-1] ) //only reorder if already out of order
            {
                map<double, double > peakMap;
                for ( int z = 0; z < x.size(); z++ ) //throw everything into a map
                    peakMap[x[z]] = y[z];
                x.clear();
                y.clear();
                
                map<double, double >::iterator itr;
                for (itr = peakMap.begin(); itr != peakMap.end(); ++itr) //toss map back into x and y
                {
                    x.push_back(itr->first);
                    y.push_back(itr->second);
                }            
                break;
            }
        }

        _ypp.reset(new double[_size]);
        spline_pchip_set(_size, const_cast<double*>(&x[0]), const_cast<double*>(&y[0]), _ypp.get());
    }

    // uses interpolation on piecewise cubic splines to make an f(x) function evenly spaced on the x axis
    //WARNING: this function seems to change the size of x and y without updating _size
    void resample(vector<double>& x, vector<double>& y) const
    {
        BOOST_ASSERT(_size == x.size());
        BOOST_ASSERT(_size == y.size());

        if (x.size() < 4)
            return;

        double minSampleSize = x[1] - x[0];
        for (int i=2; i < _size; ++i)
        {
            if ((x[i] - x[i-1]) > minSampleSize)
                minSampleSize = x[i] - x[i-1];
            // Throws error for some odd reason //minSampleSize = std::min(minSampleSize, x[i] - x[i-1]);
        }
        //double ypval, yppval;
        vector<double> newX, newY;
        newX.reserve(_size);
        newY.reserve(_size);
        newX.push_back(x[0]);
        newY.push_back(y[0]);
        for (size_t i=1; newX.back() < x.back(); ++i)
        {
            newX.push_back(newX.back() + minSampleSize);
            newY.push_back(0);
            spline_pchip_val(_size, &x[0], &y[0], _ypp.get(), 1, &newX.back(), &newY.back());
        }
        swap(x, newX);
        swap(y, newY);
    }

    double interpolate(const vector<double>& xs, const vector<double>& ys, double x) const
    {
        if (x < xs.front() || x > xs.back() || xs.size() < 4)
            return -1;

        //double ypval, yppval;
        //return spline_cubic_val(_size, const_cast<double*>(&xs[0]), const_cast<double*>(&ys[0]), _ypp.get(), x, &ypval, &yppval);

        double y;
        //_size and xs.size() don't always match //spline_pchip_val(_size, const_cast<double*>(&xs[0]), const_cast<double*>(&ys[0]), _ypp.get(), 1, &x, &y);
        spline_pchip_val((int)xs.size(), const_cast<double*>(&xs[0]), const_cast<double*>(&ys[0]), _ypp.get(), 1, &x, &y);
        return y;
    }

    private:
    boost::shared_array<double> _ypp;
    int _size;
};

int EmbedMS1ForFile(sqlite3pp::database& idpDb,
                     const string& idpDBFilePath,
                     const string& sourceFilePath,
                     const string& sourceId,
                     XICConfiguration& cofig,
                     pwiz::util::IterationListenerRegistry* ilr,
                     const int& currentFile, const int& totalFiles);

} // namespace Embedder
END_IDPICKER_NAMESPACE


#endif // _XIC_HPP_
