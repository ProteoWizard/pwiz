//
// $Id$
//
//
// Original author: Brendan MacLean <brendanx .@. u.washington.edu>
//
// Copyright 2009 University of Washington - Seattle, WA
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


#ifndef _MIDACDATA_HPP_
#define _MIDACDATA_HPP_


#ifndef BOOST_DATE_TIME_NO_LIB
#define BOOST_DATE_TIME_NO_LIB // prevent MSVC auto-link
#endif


#include "MassHunterData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"


#pragma managed
#include "pwiz/utility/misc/cpp_cli_utilities.hpp"
using namespace pwiz::util;


using System::String;
using System::Math;
using System::Object;
namespace MHDAC = Agilent::MassSpectrometry::DataAnalysis;
namespace MIDAC = Agilent::MassSpectrometry::MIDAC;


namespace pwiz {
namespace vendor_api {
namespace Agilent {


class MidacDataImpl : public MassHunterData
{
    public:
    MidacDataImpl(const std::string& path);
    ~MidacDataImpl() noexcept(false);

    virtual std::string getVersion() const;
    virtual DeviceType getDeviceType() const;
    virtual std::string getDeviceName(DeviceType deviceType) const;
    virtual blt::local_date_time getAcquisitionTime(bool adjustToHostTime) const;
    virtual IonizationMode getIonModes() const;
    virtual MSScanType getScanTypes() const;
    virtual MSStorageMode getSpectraFormat() const;
    virtual int getTotalScansPresent() const;
    virtual bool hasProfileData() const;

    virtual bool hasIonMobilityData() const;
    virtual int getTotalIonMobilityFramesPresent() const;
    virtual FramePtr getIonMobilityFrame(int frameIndex) const;
    virtual bool canConvertDriftTimeAndCCS() const;
    virtual double driftTimeToCCS(double driftTimeInMilliseconds, double mz, int charge) const;
    virtual double ccsToDriftTime(double ccs, double mz, int charge) const;

    virtual const std::set<Transition>& getTransitions() const;
    virtual ChromatogramPtr getChromatogram(const Transition& transition) const;

    virtual const BinaryData<double>& getTicTimes(bool ms1Only) const;
    virtual const BinaryData<double>& getBpcTimes(bool ms1Only) const;
    virtual const BinaryData<float>& getTicIntensities(bool ms1Only) const;
    virtual const BinaryData<float>& getBpcIntensities(bool ms1Only) const;

    virtual ScanRecordPtr getScanRecord(int row) const;
    virtual SpectrumPtr getProfileSpectrumByRow(int row) const;
    virtual SpectrumPtr getPeakSpectrumByRow(int row, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    virtual SpectrumPtr getProfileSpectrumById(int scanId) const;
    virtual SpectrumPtr getPeakSpectrumById(int scanId, PeakFilterPtr peakFilter = PeakFilterPtr()) const;

    private:
    gcroot<MIDAC::IMidacImsReader^> imsReader_;
    gcroot<MIDAC::IImsCcsInfoReader^> imsCcsReader_;
    gcroot<MHDAC::IBDAMSScanFileInformation^> scanFileInfo_;
    BinaryData<double> ticTimes_, ticTimesMs1_, bpcTimes_, bpcTimesMs1_;
    BinaryData<float> ticIntensities_, ticIntensitiesMs1_, bpcIntensities_, bpcIntensitiesMs1_;
    set<Transition> transitions_;
    map<Transition, int> transitionToChromatogramIndexMap_;

    bool hasProfileData_;
};

typedef boost::shared_ptr<MidacDataImpl> MidacDataImplPtr;


} // Agilent
} // vendor_api
} // pwiz


#endif // _MASSHUNTERDATA_HPP_
