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
// The Original Code is the Quameter software.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):


#ifndef _QUAMETERSHAREDTYPES_H
#define _QUAMETERSHAREDTYPES_H

#include <boost/icl/interval_set.hpp>
#include <boost/icl/continuous_interval.hpp>

#include "percentile.hpp"
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

#include <boost/optional.hpp>
#include <boost/cstdint.hpp>
#include <vector>
#include <map>
#include <iostream>

#include "crawdad/SimpleCrawdad.h"
using namespace pwiz::SimpleCrawdad;
using namespace crawpeaks;

using namespace boost::icl;
using namespace std;
namespace accs = boost::accumulators;
namespace bmi = boost::multi_index;


namespace freicore
{
namespace quameter
{

    enum EnzymaticStatus {NON_ENZYMATIC = 0, SEMI_ENZYMATIC, FULLY_ENZYMATIC};
    enum PrecursorCharges {ONE = 1, TWO = 2, THREE = 3, FOUR = 4};
    enum PeptideSpCCategories {ONCE = 1, TWICE = 2, THRICE = 3, MORE_THAN_THRICE = 4};

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
        size_t distinctPeptideID;
        string precursorNativeID;
        double precursorMZ;
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
                   distinctPeptideID == rhs.distinctPeptideID &&
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
                                           bmi::ordered_unique<bmi::tag<nativeID>, bmi::member<MS1ScanInfo, string, &MS1ScanInfo::nativeID> >,
                                           bmi::ordered_unique<bmi::tag<time>, bmi::member<MS1ScanInfo, double, &MS1ScanInfo::scanStartTime> >
                                       >
                                      > MS1ScanMap;

    typedef bmi::multi_index_container<MS2ScanInfo,
                                       bmi::indexed_by<
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

    struct ScanRankerMS2PrecInfo
    {
        string nativeID;
        double precursorMZ;
        int charge;
        double precursorMass;

        ScanRankerMS2PrecInfo()
        {
        }

        ScanRankerMS2PrecInfo(string nid, double precMz, int z, double precMass)
        {
            nativeID = nid;
            precursorMZ = precMz;
            charge = z;
            precursorMass = precMass;
        }

        ScanRankerMS2PrecInfo(const ScanRankerMS2PrecInfo& that)
        {
            nativeID = that.nativeID;
            precursorMZ = that.precursorMZ;
            charge = that.charge;
            precursorMass = that.precursorMass;
        }

        bool operator< ( const ScanRankerMS2PrecInfo& rhs ) const
		{
            if(nativeID.compare(rhs.nativeID)!=0)
                return nativeID < rhs.nativeID;
            if(charge != rhs.charge)
                return charge < rhs.charge;
            if(precursorMass != rhs.precursorMass)
                return precursorMass < rhs.precursorMass;

            return 0;
		}

		/// Operator to compare the equality of two search scores (MVH)
		bool operator== ( const ScanRankerMS2PrecInfo& rhs ) const
		{
			return nativeID.compare(rhs.nativeID)==0 && charge == rhs.charge && precursorMass == rhs.precursorMass;
		}

    };

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

    struct MassErrorStats
    {
        double medianError;
        double meanAbsError;
        double medianPPMError;
        double PPMErrorIQR;
    };

    struct UnidentifiedPrecursorInfo
    {
        const MS2ScanInfo* spectrum;
        interval_set<double> scanTimeWindow;
        interval_set<double> mzWindow;
        LocalChromatogram chromatogram;
    };

    struct PeptideSpectrumMatch
    {
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
        double maxScore;
        double maxScoreScanStartTime;
        string peptide;
        vector<PeptideSpectrumMatch> PSMs; // sorted by ascending scan time

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
} // namespace quameter
} // namespace freicore

#endif
