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


#ifndef _UIMFREADER_HPP_
#define _UIMFREADER_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <string>
#include <vector>
#include <set>
#include <boost/shared_ptr.hpp>
#include <boost/date_time.hpp>


namespace pwiz {
namespace vendor_api {
namespace UIMF {


PWIZ_API_DECL enum FrameType
{
    FrameType_MS1 = 1,
    FrameType_MS2 = 2,
    FrameType_Calibration = 3,
    FrameType_Prescan = 4
};


struct PWIZ_API_DECL DriftScanInfo
{
    virtual int getFrameNumber() const = 0;
    virtual FrameType getFrameType() const = 0;
    virtual int getDriftScanNumber() const = 0;
    virtual double getDriftTime() const = 0;
    virtual double getRetentionTime() const = 0;
    virtual int getNonZeroCount() const = 0;
    virtual double getTIC() const = 0;
};

typedef boost::shared_ptr<DriftScanInfo> DriftScanInfoPtr;

class PWIZ_API_DECL UIMFReader
{
    public:
    typedef boost::shared_ptr<UIMFReader> Ptr;
    static Ptr create(const std::string& path);

    struct IndexEntry
    {
        int frame;
        int scan;
        FrameType frameType;
    };

    static int getMsLevel(FrameType frameType);

    virtual const vector<IndexEntry>& getIndex() const = 0;
    virtual const set<FrameType>& getFrameTypes() const = 0;
    virtual size_t getFrameCount() const = 0;
    virtual pair<double, double> getScanRange() const = 0; // this appears to be constant across the file

    virtual const vector<DriftScanInfoPtr> getDriftScansForFrame(int frame) const = 0;
    virtual size_t getMaxDriftScansPerFrame() const = 0;

    virtual boost::local_time::local_date_time getAcquisitionTime() const = 0;

    virtual bool hasIonMobility() const = 0;
    virtual bool canConvertIonMobilityAndCCS() const = 0;
    virtual double ionMobilityToCCS(double driftTimeInMilliseconds, double mz, int charge) const = 0;
    virtual double ccsToIonMobility(double ccs, double mz, int charge) const = 0;

    virtual void getScan(int frame, int scan, FrameType frameType, pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray, bool ignoreZeroIntensityPoints = false) const = 0;
    virtual double getDriftTime(int frame, int scan) const = 0;
    virtual double getRetentionTime(int frame) const = 0;

    virtual const void getTic(std::vector<double>& timeArray, std::vector<double>& intensityArray) const = 0;

    virtual ~UIMFReader() {}
};

typedef boost::shared_ptr<UIMFReader> UIMFReaderPtr;


} // UIMF
} // vendor_api
} // pwiz


#endif // _UIMFREADER_HPP_
