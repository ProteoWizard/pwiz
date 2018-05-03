//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2017 Matt Chambers - Nashville, TN 37221
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


#ifndef _TIMSDATA_HPP_
#define _TIMSDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "CompassData.hpp"
#include <boost/optional.hpp>
#include "timsdata_cpp.h"


using boost::optional;
typedef timsdata::TimsData TimsBinaryData;


namespace pwiz {
namespace vendor_api {
namespace Bruker {


typedef boost::shared_ptr<TimsBinaryData> TimsBinaryDataPtr;
struct TimsSpectrum;
struct TimsDataImpl;

struct PasefPrecursorInfo
{
    int scanBegin, scanEnd; // scan end is inclusive
    double oneOverK0; // average value from scanBegin to scanEnd
    double isolationMz;
    double isolationWidth;
    double collisionEnergy;
    double monoisotopicMz;
    int charge;
    double intensity;
};
typedef boost::shared_ptr<PasefPrecursorInfo> PasefPrecursorInfoPtr;

struct PWIZ_API_DECL TimsFrame
{


    TimsFrame(TimsBinaryDataPtr storage, int64_t frameId,
              int msLevel, double rt,
              double startMz, double endMz,
              double tic, double bpi,
              IonPolarity polarity, int scanMode, int numScans,
              const optional<uint64_t>& parentId,
              const optional<double>& precursorMz,
              const optional<double>& isolationWidth,
              const optional<int>& precursorCharge);

    int64_t frameId() const { return frameId_; }

    private:
    friend struct TimsSpectrum;
    friend struct TimsDataImpl;
    int64_t frameId_;
    int msLevel_;
    double rt_;
    optional<uint64_t> parentId_;
    double tic_;
    double bpi_;
    int numScans_;

    IonPolarity polarity_;

    std::pair<double, double> scanRange_;
    optional<int> chargeState_;
    optional<double> isolationWidth_;

    optional<double> precursorMz_;
    int scanMode_;

    vector<PasefPrecursorInfoPtr> pasef_precursor_info_;

    TimsBinaryDataPtr storage_;
    vector<double> oneOverK0_; // access by (scan number - 1)
};

typedef boost::shared_ptr<TimsFrame> TimsFramePtr;


struct PWIZ_API_DECL TimsSpectrum : public MSSpectrum
{
    TimsSpectrum(const TimsFramePtr& framePtr, int scanBegin, int scanEnd, PasefPrecursorInfoPtr pasefPrecursorInfoPtr = nullptr);

    virtual ~TimsSpectrum() {}

    virtual bool hasLineData() const;
    virtual bool hasProfileData() const;
    virtual size_t getLineDataSize() const;
    virtual size_t getProfileDataSize() const;
    virtual void getLineData(automation_vector<double>& mz, automation_vector<double>& intensities) const;
    virtual void getProfileData(automation_vector<double>& mz, automation_vector<double>& intensities) const;

    virtual double getTIC() const { return framePtr_->tic_; }
    virtual double getBPI() const { return framePtr_->bpi_; }

    virtual int getMSMSStage() const;
    virtual double getRetentionTime() const;
    virtual void getIsolationData(std::vector<double>& isolatedMZs, std::vector<IsolationMode>& isolationModes) const;
    virtual void getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const;
    virtual IonPolarity getPolarity() const;

    virtual std::pair<double, double> getScanRange() const;
    virtual int getChargeState() const;
    virtual double getIsolationWidth() const;

    virtual bool isIonMobilitySpectrum() const { return oneOverK0() > 0; }
    virtual double oneOverK0() const;

    void getCombinedSpectrumData(std::vector<double>& mz, std::vector<double>& intensities, std::vector<double>& mobilities) const;
    virtual pwiz::util::IntegerSet getMergedScanNumbers() const;

    int scanBegin() const { return scanBegin_; }
    int scanEnd() const { return scanEnd_; }
    TimsFramePtr framePtr() const { return framePtr_; }

    virtual MSSpectrumParameterListPtr parameters() const;

    private:

    int scanBegin_, scanEnd_; // 0-based index, scanEnd is inclusive (so for unmerged spectrum, begin==end)
    TimsFramePtr framePtr_;
    PasefPrecursorInfoPtr pasefPrecursorInfoPtr_;
};

typedef boost::shared_ptr<TimsSpectrum> TimsSpectrumPtr;


struct PWIZ_API_DECL TimsDataImpl : public CompassData
{
    TimsDataImpl(const std::string& rawpath, bool combineIonMobilitySpectra);
    virtual ~TimsDataImpl() {}

    /// returns true if the source has MS spectra
    virtual bool hasMSData() const;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() const;

    /// returns the number of spectra available from the MS source
    virtual size_t getMSSpectrumCount() const;

    /// returns a spectrum from the MS source
    virtual MSSpectrumPtr getMSSpectrum(int scan, DetailLevel detailLevel = DetailLevel_FullMetadata) const;

    virtual std::pair<size_t, size_t> getFrameScanPair(int scan) const;

    /// returns the number of sources available from the LC system
    virtual size_t getLCSourceCount() const;

    /// returns the number of spectra available from the specified LC source
    virtual size_t getLCSpectrumCount(int source) const;

    /// returns a source from the LC system
    virtual LCSpectrumSourcePtr getLCSource(int source) const;

    /// returns a spectrum from the specified LC source
    virtual LCSpectrumPtr getLCSpectrum(int source, int scan) const;

    virtual std::string getOperatorName() const;
    virtual std::string getAnalysisName() const ;
    virtual boost::local_time::local_date_time getAnalysisDateTime() const;
    virtual std::string getSampleName() const;
    virtual std::string getMethodName() const;
    virtual InstrumentFamily getInstrumentFamily() const;
    virtual std::string getInstrumentDescription() const;
    virtual InstrumentSource getInstrumentSource() const;
    virtual std::string getAcquisitionSoftware() const;
    virtual std::string getAcquisitionSoftwareVersion() const;

    private:
    std::string tdfFilepath_;
    TimsBinaryDataPtr tdfStorage_;
    std::vector<TimsFramePtr> frames_;
    std::vector<TimsSpectrumPtr> spectra_;
    std::string acquisitionSoftware_;
    std::string acquisitionSoftwareVersion_;
    InstrumentFamily instrumentFamily_;
    InstrumentSource instrumentSource_;
    std::string acquisitionDateTime_;
    std::string operatorName_;
    bool combineSpectra_;
};


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz


#endif // _TIMSDATA_HPP_
