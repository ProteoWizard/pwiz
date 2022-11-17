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


#ifndef _TSFDATA_HPP_
#define _TSFDATA_HPP_

#ifdef _WIN64

#include "pwiz/utility/misc/Export.hpp"
#include "CompassData.hpp"
#include <boost/optional.hpp>
#include <boost/container/flat_map.hpp>
#include "tsfdata_cpp_pwiz.h" // Derived from timsdata_cpp.h, has light changes to help with single-scan access efficiency


using boost::optional;
typedef tsfdata::TsfData TsfBinaryData;


namespace pwiz {
namespace vendor_api {
namespace Bruker {


typedef boost::shared_ptr<TsfBinaryData> TsfBinaryDataPtr;
struct TsfSpectrum;
struct TsfDataImpl;

enum class ScanMode
{
    MS1 = 0,
    AutoMSMS = 1,
    MRM = 2,
    IS_CID = 3,
    BB_CID = 4,
    DDA_PASEF = 8,
    DIA_PASEF = 9,
    PRM_PASEF = 10,
    MALDI = 20
};

struct PWIZ_API_DECL TsfFrame
{
    TsfFrame(TsfDataImpl& tsfDataImpl, int64_t frameId,
             MsMsType msmsType, double rt,
             double startMz, double endMz,
             double tic, double bpi,
             IonPolarity polarity, ScanMode scanMode,
             const optional<uint64_t>& parentId,
             const optional<double>& precursorMz,
             const optional<double>& isolationWidth,
             const optional<int>& precursorCharge,
             const optional<double>& collisionEnergy,
             const optional<int>& maldiChip,
             const optional<string>& maldiSpotName);

    int64_t frameId() const { return frameId_; }

    private:
    friend struct TsfSpectrum;
    friend struct TsfDataImpl;
    int64_t frameId_;
    MsMsType msmsType_;
    int msLevel_;
    double rt_;
    optional<uint64_t> parentId_;
    double tic_;
    double bpi_;

    IonPolarity polarity_;

    std::pair<double, double> scanRange_;
    optional<int> chargeState_;
    optional<double> isolationWidth_;
    optional<double> collisionEnergy_;
    optional<int> maldiChip_;
    optional<string> maldiSpotName_;

    optional<double> precursorMz_;
    ScanMode scanMode_;

    TsfDataImpl & tsfDataImpl_;
};

typedef boost::shared_ptr<TsfFrame> TsfFramePtr;


struct PWIZ_API_DECL TsfSpectrum : public MSSpectrum
{
public:
    TsfSpectrum(const TsfFramePtr& framePtr) : 
        frame_(*framePtr) {}
    ~TsfSpectrum() {}

    bool hasLineData() const;
    bool hasProfileData() const;
    size_t getLineDataSize() const;
    size_t getProfileDataSize() const;
    void getLineData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;
    void getProfileData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;

    double getTIC() const { return frame_.tic_; }
    double getBPI() const { return frame_.bpi_; }

    int getMSMSStage() const;
    double getRetentionTime() const;
    void getIsolationData(std::vector<IsolationInfo>& isolationInfo) const;
    void getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const;
    IonPolarity getPolarity() const;

    std::pair<double, double> getScanRange() const;
    int getChargeState() const;
    double getIsolationWidth() const;

    boost::optional<int> getMaldiChip() const;
    boost::optional<std::string> getMaldiSpotName() const;

    MSSpectrumParameterListPtr parameters() const;

    protected:
    friend struct TsfDataImpl;

    const TsfFrame& frame_;
};

typedef boost::shared_ptr<TsfSpectrum> TsfSpectrumPtr;

struct PWIZ_API_DECL TsfDataImpl : public CompassData
{
    TsfDataImpl(const std::string& rawpath, int preferOnlyMsLevel = 0);
    virtual ~TsfDataImpl() {}

    /// returns true if the source has MS spectra
    virtual bool hasMSData() const;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() const;

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
    std::string tsfFilepath_;
    boost::container::flat_map<size_t, TsfFramePtr> frames_;
    std::vector<TsfSpectrumPtr> spectra_;
    std::string acquisitionSoftware_;
    std::string acquisitionSoftwareVersion_;
    InstrumentFamily instrumentFamily_;
    int instrumentRevision_;
    InstrumentSource instrumentSource_;
    std::string serialNumber_;
    std::string acquisitionDateTime_;
    std::string operatorName_;
    int preferOnlyMsLevel_; // when nonzero, caller only wants spectra at this ms level
    ChromatogramPtr tic_, ticMs1_, bpc_, bpcMs1_;

    bool hasLineSpectra_;
    bool hasProfileSpectra_;

    protected:
    friend struct TsfFrame;
    friend struct TsfSpectrum;
    TsfBinaryDataPtr tsfStoragePtr_;
    TsfBinaryData& tsfStorage_;
};


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz

#endif // _WIN64

#endif // _TSFDATA_HPP_
