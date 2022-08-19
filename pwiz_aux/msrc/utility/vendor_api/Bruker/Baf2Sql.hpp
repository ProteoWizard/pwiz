//
// $Id: CompassData.hpp 6478 2014-07-08 20:01:38Z chambm $
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


#ifndef _BAF2SQL_HPP_
#define _BAF2SQL_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "CompassData.hpp"
#include <boost/optional.hpp>
#include "baf2sql_cpp.h"


using boost::optional;
using baf2sql::BinaryStorage;


namespace pwiz {
namespace vendor_api {
namespace Bruker {


typedef shared_ptr<BinaryStorage> BinaryStoragePtr;


struct PWIZ_API_DECL Baf2SqlSpectrum : public MSSpectrum
{
    Baf2SqlSpectrum(BinaryStoragePtr storage, int index,
                    int msLevel, double rt, int segment, int acqKey,
                    double startMz, double endMz, double tic, double bpi,
                    IonPolarity polarity, int scanMode,
                    const optional<uint64_t>& profileMzArrayId, const optional<uint64_t>& profileIntensityArrayId,
                    const optional<uint64_t>& lineMzarrayId, const optional<uint64_t>& lineIntensityArrayId);

    Baf2SqlSpectrum(BinaryStoragePtr storage, int index,
                    int msLevel, double rt, int segment, int acqKey,
                    double startMz, double endMz, double tic, double bpi,
                    IonPolarity polarity, int scanMode,
                    const optional<uint64_t>& profileMzArrayId, const optional<uint64_t>& profileIntensityArrayId,
                    const optional<uint64_t>& lineMzarrayId, const optional<uint64_t>& lineIntensityArrayId,
                    const optional<uint64_t>& parentId, const optional<double>& precursorMz,
                    const optional<int>& isolationMode, const optional<int>& reactionMode,
                    const optional<double>& isolationWidth,
                    const optional<int>& precursorCharge,
                    const optional<double>& collisionEnergy);

    virtual ~Baf2SqlSpectrum() {}

    virtual bool hasLineData() const;
    virtual bool hasProfileData() const;
    virtual size_t getLineDataSize() const;
    virtual size_t getProfileDataSize() const;
    virtual void getLineData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;
    virtual void getProfileData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const;

    virtual double getTIC() const { return tic_; }
    virtual double getBPI() const { return bpi_; }

    virtual int getMSMSStage() const;
    virtual double getRetentionTime() const;
    virtual void getIsolationData(std::vector<IsolationInfo>& isolationInfo) const;
    virtual void getFragmentationData(std::vector<double>& fragmentedMZs, std::vector<FragmentationMode>& fragmentationModes) const;
    virtual IonPolarity getPolarity() const;

    virtual pair<double, double> getScanRange() const;
    virtual int getChargeState() const;
    virtual double getIsolationWidth() const;

    virtual MSSpectrumParameterListPtr parameters() const;

    private:
    virtual void handleAllIons(); // Deal with all-ions MS1 data by presenting it as MS2 with a wide isolation window
    virtual void readArray(uint64_t id, pwiz::util::BinaryData<double> & result) const;
    virtual void readArray(uint64_t id, pwiz::util::BinaryData<double> & result, size_t n) const; // For use when the id's array size is known, as when reading mz after reading intensity

    int index_;
    int msLevel_;
    double rt_;
    int segment_;
    int acqKey_;
    optional<uint64_t> parentId_;
    double tic_;
    double bpi_;
    optional<uint64_t> profileMzArrayId_, profileIntensityArrayId_;
    optional<uint64_t> lineMzArrayId_, lineIntensityArrayId_;

    IonPolarity polarity_;

    pair<double, double> scanRange_;
    optional<int> chargeState_;
    optional<double> isolationWidth_;
    optional<double> collisionEnergy_;

    optional<int> isolationMode_, reactionMode_;
    optional<double> precursorMz_;
    int scanMode_;

    BinaryStoragePtr storage_;
};

struct PWIZ_API_DECL Baf2SqlImpl : public CompassData
{
    Baf2SqlImpl(const std::string& rawpath);
    virtual ~Baf2SqlImpl() {}

    /// returns true if the source has MS spectra
    virtual bool hasMSData() const;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() const;

    /// returns the number of spectra available from the MS source
    virtual size_t getMSSpectrumCount() const;

    /// returns a spectrum from the MS source
    virtual MSSpectrumPtr getMSSpectrum(int scan, DetailLevel detailLevel = DetailLevel_FullMetadata) const;

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
    virtual std::string getAnalysisName() const ;
    virtual boost::local_time::local_date_time getAnalysisDateTime() const;
    virtual std::string getSampleName() const;
    virtual std::string getMethodName() const;
    virtual InstrumentFamily getInstrumentFamily() const;
    virtual int getInstrumentRevision() const;
    virtual std::string getInstrumentDescription() const;
    virtual std::string Baf2SqlImpl::getInstrumentSerialNumber() const;
    virtual InstrumentSource getInstrumentSource() const;
    virtual std::string getAcquisitionSoftware() const;
    virtual std::string getAcquisitionSoftwareVersion() const;

    private:
    std::string rawpath_;
    std::string bafFilepath_;
    BinaryStoragePtr bafStorage_;
    std::vector<MSSpectrumPtr> spectra_;
    std::string acquisitionSoftware_;
    std::string acquisitionSoftwareVersion_;
    InstrumentFamily instrumentFamily_;
    int instrumentRevision_;
    InstrumentSource instrumentSource_;
    std::string serialNumber_;
    std::string acquisitionDateTime_;
    std::string operatorName_;
    ChromatogramPtr tic_, ticMs1_, bpi_, bpiMs1_;
};


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz


#endif // _BAF2SQL_HPP_
