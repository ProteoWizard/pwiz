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
#include <boost/container/flat_map.hpp>
#include "timsdata_cpp_pwiz.h" // Derived from timsdata_cpp.h, has light changes to help with single-scan access efficiency


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
    double avgScanNumber; // average value from Precursors table
    double isolationMz;
    double isolationWidth;
    double collisionEnergy;
    double monoisotopicMz;
    int charge;
    double intensity;
};
typedef boost::shared_ptr<PasefPrecursorInfo> PasefPrecursorInfoPtr;

struct DiaPasefIsolationInfo
{
    double isolationMz;
    double isolationWidth;
    double collisionEnergy;
    int numScans;
};

enum class MsMsType
{
    MS1 = 0,
    MRM = 2,
    DDA_PASEF = 8,
    DIA_PASEF = 9,
    PRM_PASEF = 10
};

struct PWIZ_API_DECL TimsFrame
{


    TimsFrame(TimsDataImpl& timsDataImpl, int64_t frameId,
              MsMsType msmsType, double rt,
              double startMz, double endMz,
              double tic, double bpi,
              IonPolarity polarity, int scanMode, int numScans,
              const optional<uint64_t>& parentId,
              const optional<double>& precursorMz,
              const optional<double>& isolationWidth,
              const optional<int>& precursorCharge,
              int calibrationIndex,
              const vector<double>& oneOverK0);

    int64_t frameId() const { return frameId_; }
    int numScans() const { return numScans_; }

    private:
    friend struct TimsSpectrum;
    friend struct TimsDataImpl;
    int64_t frameId_;
    MsMsType msmsType_;
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
    optional<int> windowGroup_;

    optional<double> precursorMz_;
    int scanMode_;
    int calibrationIndex_;

    map<int, size_t> scanIndexByScanNumber_; // for frame/scan -> index calculation with support for missing scans (e.g. allowMsMsWithoutPrecursor == false)

    vector<PasefPrecursorInfoPtr> pasef_precursor_info_;
    map<int, DiaPasefIsolationInfo> diaPasefIsolationInfoByScanNumber_; // only the first scan number of each window is stored, so use lower_bound() to find the info for a given scan number

    TimsDataImpl & timsDataImpl_;
    const vector<double>& oneOverK0_;
};

typedef boost::shared_ptr<TimsFrame> TimsFramePtr;


struct PWIZ_API_DECL TimsSpectrum : public MSSpectrum
{
protected:
    TimsSpectrum(const TimsFramePtr& framePtr, int scanBegin) : 
        frame_(*framePtr), scanBegin_(scanBegin) {}
public:
    virtual ~TimsSpectrum() {}

    virtual bool hasLineData() const;
    virtual bool hasProfileData() const;
    virtual size_t getLineDataSize() const;
    virtual size_t getProfileDataSize() const;
    virtual void getLineData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;
    virtual void getProfileData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;

    virtual double getTIC() const { return frame_.tic_; }
    virtual double getBPI() const { return frame_.bpi_; }

    virtual int getMSMSStage() const;
    virtual double getRetentionTime() const;
    virtual void getIsolationData(std::vector<IsolationInfo>& isolationInfo) const;
    virtual void getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const;
    virtual IonPolarity getPolarity() const;

    virtual std::pair<double, double> getScanRange() const;
    virtual int getChargeState() const;
    virtual double getIsolationWidth() const;
    virtual int getWindowGroup() const;

    virtual bool isIonMobilitySpectrum() const { return oneOverK0() > 0; }
    virtual double oneOverK0() const;

    void getCombinedSpectrumData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, pwiz::util::BinaryData<double>& mobilities, bool sortAndJitter) const;
    size_t getCombinedSpectrumDataSize() const;
    virtual pwiz::util::IntegerSet getMergedScanNumbers() const;

    virtual bool HasPasefPrecursorInfo() const { return false; }
    virtual const PasefPrecursorInfo& GetPasefPrecursorInfo() const { return empty_; }

    const DiaPasefIsolationInfo& getDiaPasefIsolationInfo() const
    {
        if (frame_.diaPasefIsolationInfoByScanNumber_.empty())
            throw runtime_error("[TimsSpectrum::getDiaPasefIsolationInfo] no DIA PASEF info");

        auto diaPasefIsolationInfoPair = frame_.diaPasefIsolationInfoByScanNumber_.upper_bound(scanBegin_);
        --diaPasefIsolationInfoPair;
        return diaPasefIsolationInfoPair->second;
    }

    int scanBegin() const { return scanBegin_; }
    virtual int scanEnd() const = 0;
    virtual bool isCombinedScans() const = 0;

    virtual MSSpectrumParameterListPtr parameters() const;

    protected:
    friend struct TimsDataImpl;

    const TimsFrame& frame_;
    const int scanBegin_;
    const static PasefPrecursorInfo empty_;
};

struct TimsSpectrumNonPASEF : public TimsSpectrum
{
    TimsSpectrumNonPASEF(const TimsFramePtr& framePtr, int scanBegin) : 
        TimsSpectrum(framePtr, scanBegin) {}
    virtual ~TimsSpectrumNonPASEF() {}
    virtual int scanEnd() const { return scanBegin_; }
    virtual bool isCombinedScans() const { return false; }
};

struct TimsSpectrumCombinedNonPASEF : public TimsSpectrumNonPASEF
{
    TimsSpectrumCombinedNonPASEF(const TimsFramePtr& framePtr, int scanBegin, int scanEnd) : 
        TimsSpectrumNonPASEF(framePtr, scanBegin), scanEnd_(scanEnd) {}
    virtual ~TimsSpectrumCombinedNonPASEF() {}
    virtual int scanEnd() const { return scanEnd_; }
    virtual bool isCombinedScans() const { return scanEnd_ != scanBegin_; }
private:
    int scanEnd_; // 0-based index, scanEnd is inclusive (so for unmerged spectrum, begin==end)
};

struct TimsSpectrumPASEF : public TimsSpectrum
{
    TimsSpectrumPASEF(const TimsFramePtr& framePtr, int scanBegin, const PasefPrecursorInfo& pasefPrecursorInfo) :
        TimsSpectrum(framePtr, scanBegin), pasefPrecursorInfo_(pasefPrecursorInfo) {}
    virtual ~TimsSpectrumPASEF() {}
    virtual int scanEnd() const { return scanBegin_; }
    virtual bool isCombinedScans() const { return false; }
    virtual bool HasPasefPrecursorInfo() const { return &pasefPrecursorInfo_ != &empty_; };
    virtual const PasefPrecursorInfo& GetPasefPrecursorInfo() const { return pasefPrecursorInfo_; }
private:
    const PasefPrecursorInfo& pasefPrecursorInfo_;
};

struct TimsSpectrumCombinedPASEF : public TimsSpectrumPASEF
{
    TimsSpectrumCombinedPASEF(const TimsFramePtr& framePtr, int scanBegin, int scanEnd, const PasefPrecursorInfo& pasefPrecursorInfo) :
        TimsSpectrumPASEF(framePtr, scanBegin, pasefPrecursorInfo), scanEnd_(scanEnd) {}

    virtual ~TimsSpectrumCombinedPASEF() {}
    virtual int scanEnd() const { return scanEnd_; }
    virtual bool isCombinedScans() const { return scanEnd_ != scanBegin_; }
private:
    int scanEnd_; // 0-based index, scanEnd is inclusive (so for unmerged spectrum, begin==end)
};

typedef boost::shared_ptr<TimsSpectrum> TimsSpectrumPtr;

struct PWIZ_API_DECL TimsDataImpl : public CompassData
{
    TimsDataImpl(const std::string& rawpath, bool combineIonMobilitySpectra, int preferOnlyMsLevel = 0, bool allowMsMsWithoutPrecursor = true,
                 const std::vector<chemistry::MzMobilityWindow>& isolationMzFilter = std::vector<chemistry::MzMobilityWindow>());
    virtual ~TimsDataImpl() {}

    /// returns true if the source has MS spectra
    virtual bool hasMSData() const;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() const;

    /// returns true if the source is TIMS PASEF data
    virtual bool hasPASEFData() const;

    /// returns the number of spectra available from the MS source
    virtual size_t getMSSpectrumCount() const;

    /// returns a spectrum from the MS source
    virtual MSSpectrumPtr getMSSpectrum(int scan, DetailLevel detailLevel = DetailLevel_FullMetadata) const;

    virtual FrameScanRange getFrameScanPair(int scan) const;
    virtual size_t getSpectrumIndex(int frame, int scan) const;

    /// returns the number of sources available from the LC system
    virtual size_t getLCSourceCount() const;

    /// returns the number of spectra available from the specified LC source
    virtual size_t getLCSpectrumCount(int source) const;

    /// returns a source from the LC system
    virtual LCSpectrumSourcePtr getLCSource(int source) const;

    /// returns a spectrum from the specified LC source
    virtual LCSpectrumPtr getLCSpectrum(int source, int scan) const;

    /// returns a chromatogram with times and total ion currents of all spectra, or a null pointer if the format doesn't support fast access to TIC
    virtual ChromatogramPtr getTIC(bool ms1Only) const;

    /// returns a chromatogram with times and base peak intensities of all spectra, or a null pointer if the format doesn't support fast access to BPC
    virtual ChromatogramPtr getBPC(bool ms1Only) const;

    virtual std::string getOperatorName() const;
    virtual std::string getAnalysisName() const;
    virtual boost::local_time::local_date_time getAnalysisDateTime() const;
    virtual std::string getSampleName() const;
    virtual std::string getMethodName() const;
    virtual InstrumentFamily getInstrumentFamily() const;
    virtual int getInstrumentRevision() const;
    virtual std::string getInstrumentDescription() const;
    virtual std::string getInstrumentSerialNumber() const;
    virtual InstrumentSource getInstrumentSource() const;
    virtual std::string getAcquisitionSoftware() const;
    virtual std::string getAcquisitionSoftwareVersion() const;

    private:
    std::string tdfFilepath_;
    boost::container::flat_map<size_t, TimsFramePtr> frames_;
    std::vector<TimsSpectrumPtr> spectra_;
    std::string acquisitionSoftware_;
    std::string acquisitionSoftwareVersion_;
    InstrumentFamily instrumentFamily_;
    int instrumentRevision_;
    InstrumentSource instrumentSource_;
    std::string serialNumber_;
    std::string acquisitionDateTime_;
    std::string operatorName_;
    bool combineSpectra_;
    bool hasPASEFData_;
    int preferOnlyMsLevel_; // when nonzero, caller only wants spectra at this ms level
    bool allowMsMsWithoutPrecursor_; // when false, PASEF MS2 specta without precursor info will be excluded
    vector<chemistry::MzMobilityWindow> isolationMzFilter_; // when non-empty, only scans from precursors matching one of the included m/zs (i.e. within a precursor isolation window) will be enumerated
    vector<vector<double>> oneOverK0ByScanNumberByCalibration_;
    ChromatogramPtr tic_, ticMs1_, bpc_, bpcMs1_;

    int64_t currentFrameId_; // used for cacheing frame contents

    protected:
    friend struct TimsFrame;
    friend struct TimsSpectrum;
    TimsBinaryDataPtr tdfStoragePtr_;
    TimsBinaryData& tdfStorage_;

    ///
    /// cache entire frames while dealing with single spectrum access
    ///

    const timsdata::FrameProxy& readFrame(
        int64_t frame_id);     //< frame index

};


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz


#endif // _TIMSDATA_HPP_
