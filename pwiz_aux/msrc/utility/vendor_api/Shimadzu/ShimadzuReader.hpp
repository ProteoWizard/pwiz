//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2014 Vanderbilt University - Nashville, TN 37232
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


#ifndef _SHIMADZUREADER_HPP_
#define _SHIMADZUREADER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include <string>
#include <vector>
#include <set>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace Shimadzu {


struct PWIZ_API_DECL TimeRange { double start, end; };

PWIZ_API_DECL enum Polarity
{
    Positive = 0,
    Negative = 1,
    Undefined = 2
};

struct PWIZ_API_DECL SRMTransition
{
    short id;
    short channel;
    short event;
    short segment;
    double collisionEnergy;
    short polarity;
    double Q1;
    double Q3;
    //TimeRange acquiredTimeRange;

    bool operator< (const SRMTransition& rhs) const;
};


struct PWIZ_API_DECL Chromatogram
{
    virtual int getTotalDataPoints() const = 0;
    virtual void getXArray(pwiz::util::BinaryData<double>& x) const = 0;
    virtual void getYArray(pwiz::util::BinaryData<double>& y) const = 0;

    virtual ~Chromatogram() {}
};

typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;


struct PWIZ_API_DECL SRMChromatogram : public Chromatogram
{
    virtual const SRMTransition& getTransition() const = 0;
};

typedef boost::shared_ptr<SRMChromatogram> SRMChromatogramPtr;


struct PWIZ_API_DECL Spectrum
{
    virtual double getScanTime() const = 0;
    virtual int getMSLevel() const = 0;
    virtual Polarity getPolarity() const = 0;

    virtual double getSumY() const = 0;
    virtual double getBasePeakX() const = 0;
    virtual double getBasePeakY() const = 0;
    virtual double getMinX() const = 0;
    virtual double getMaxX() const = 0;

    virtual bool getHasIsolationInfo() const = 0;
    virtual void getIsolationInfo(double& centerMz, double& lowerLimit, double& upperLimit) const = 0;

    virtual bool getHasPrecursorInfo() const = 0;
    virtual void getPrecursorInfo(double& selectedMz, double& intensity, int& charge) const = 0;

    virtual int getTotalDataPoints(bool doCentroid = false) const = 0;
    virtual void getProfileArrays(pwiz::util::BinaryData<double>& x, pwiz::util::BinaryData<double>& y) const = 0;
    virtual void getCentroidArrays(pwiz::util::BinaryData<double>& x, pwiz::util::BinaryData<double>& y) const = 0;

    virtual ~Spectrum() {}
};

typedef boost::shared_ptr<Spectrum> SpectrumPtr;


class PWIZ_API_DECL ShimadzuReader
{
public:
    typedef boost::shared_ptr<ShimadzuReader> Ptr;
    static Ptr create(const std::string& filepath);

    //virtual std::string getVersion() const = 0;
    //virtual DeviceType getDeviceType() const = 0;
    //virtual std::string getDeviceName(DeviceType deviceType) const = 0;
    virtual boost::local_time::local_date_time getAnalysisDate(bool adjustToHostTime) const = 0;

    virtual const std::set<SRMTransition>& getTransitions() const = 0;
    virtual SRMChromatogramPtr getSRM(const SRMTransition& transition) const = 0;

    virtual ChromatogramPtr getTIC(bool ms1Only = false) const = 0;

    virtual int getScanCount() const = 0;
    virtual SpectrumPtr getSpectrum(int scanNumber) const = 0;

    virtual const std::set<int>& getMSLevels() const = 0;

    virtual ~ShimadzuReader() {}
};

typedef ShimadzuReader::Ptr ShimadzuReaderPtr;


} // Shimadzu
} // vendor_api
} // pwiz


#endif // _SHIMADZUREADER_HPP_
