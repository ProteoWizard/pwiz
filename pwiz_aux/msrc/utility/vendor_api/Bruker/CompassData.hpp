//
// $Id$
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _COMPASSDATA_HPP_
#define _COMPASSDATA_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/chemistry/MzMobilityWindow.hpp"
#include <string>
#include <vector>
#include <boost/smart_ptr.hpp>
#include <boost/date_time.hpp>
#include <boost/iterator/iterator_facade.hpp>
#include "pwiz/data/vendor_readers/Bruker/Reader_Bruker_Detail.hpp"


namespace pwiz {
namespace vendor_api {
namespace Bruker {


PWIZ_API_DECL enum SpectrumType
{
    SpectrumType_Line = 0,
    SpectrumType_Profile = 1
};

PWIZ_API_DECL enum IonPolarity
{
    IonPolarity_Positive = 0,
    IonPolarity_Negative = 1,
    IonPolarity_Unknown = 255
};

PWIZ_API_DECL enum FragmentationMode
{
    FragmentationMode_Off = 0,
    FragmentationMode_CID = 1,
    FragmentationMode_ETD = 2,
    FragmentationMode_CIDETD_CID = 3,
    FragmentationMode_CIDETD_ETD = 4,
    FragmentationMode_ISCID = 5,
    FragmentationMode_ECD = 6,
    FragmentationMode_IRMPD = 7,
    FragmentationMode_PTR = 8,
    FragmentationMode_Unknown = 255
};

PWIZ_API_DECL enum InstrumentFamily
{
    InstrumentFamily_Trap = 0,
    InstrumentFamily_OTOF = 1,
    InstrumentFamily_OTOFQ = 2,
    InstrumentFamily_BioTOF = 3,
    InstrumentFamily_BioTOFQ = 4,
    InstrumentFamily_MaldiTOF = 5,
    InstrumentFamily_FTMS = 6,
    InstrumentFamily_maXis = 7,
    InstrumentFamily_timsTOF = 9, // not from CXT
    InstrumentFamily_impact = 90, // not from CXT
    InstrumentFamily_compact = 91, // not from CXT
    InstrumentFamily_solariX = 92, // not from CXT
    InstrumentFamily_Unknown = 255
};

PWIZ_API_DECL enum IsolationMode
{
    IsolationMode_Off = 0,
    IsolationMode_On = 1,
    IsolationMode_Unknown = 255
};

PWIZ_API_DECL enum InstrumentSource // not from CXT
{
    InstrumentSource_AlsoUnknown = 0,
    InstrumentSource_ESI = 1,
    InstrumentSource_APCI = 2,
    InstrumentSource_NANO_ESI_OFFLINE = 3,
    InstrumentSource_NANO_ESI_ONLINE = 4,
    InstrumentSource_APPI = 5,
    InstrumentSource_AP_MALDI = 6,
    InstrumentSource_MALDI = 7,
    InstrumentSource_MULTI_MODE = 8,
    InstrumentSource_NANO_FLOW_ESI = 9,
    InstrumentSource_Ultraspray = 10,
    InstrumentSource_CaptiveSpray = 11,
    InstrumentSource_EI = 16,
    InstrumentSource_GC_APCI = 17,
    InstrumentSource_VIP_HESI = 18,
    InstrumentSource_VIP_APCI = 19,
    InstrumentSource_Unknown = 255
};

PWIZ_API_DECL enum LCUnit
{
    LCUnit_NanoMeter = 1,
    LCUnit_MicroLiterPerMinute,
    LCUnit_Bar,
    LCUnit_Percent,
    LCUnit_Kelvin,
    LCUnit_Intensity,
    LCUnit_Unknown = 7
};

PWIZ_API_DECL enum DetailLevel
{
    DetailLevel_InstantMetadata,
    DetailLevel_FullMetadata,
    DetailLevel_FullData
};

struct PWIZ_API_DECL IsolationInfo
{
    double isolationMz;
    IsolationMode isolationMode;
    double collisionEnergy;
};

struct PWIZ_API_DECL MSSpectrumParameter
{
    std::string group;
    std::string name;
    std::string value;
};

struct MSSpectrumParameterList;

class PWIZ_API_DECL MSSpectrumParameterIterator
    : public boost::iterator_facade<MSSpectrumParameterIterator,
                                    const MSSpectrumParameter,
                                    boost::random_access_traversal_tag>
{
    public:
    MSSpectrumParameterIterator();
    explicit MSSpectrumParameterIterator(const MSSpectrumParameterList& pl, size_t index = 0);

    MSSpectrumParameterIterator(const MSSpectrumParameterIterator& other);
    ~MSSpectrumParameterIterator();

    private:
    friend class boost::iterator_core_access;
    void increment();
    void decrement();
    void advance(difference_type n);
    bool equal(const MSSpectrumParameterIterator& that) const;
    const MSSpectrumParameter& dereference() const;

    struct Impl;
    boost::scoped_ptr<Impl> impl_;
};

struct PWIZ_API_DECL MSSpectrumParameterList
{
    typedef MSSpectrumParameter value_type;
    typedef value_type& reference;
    typedef const value_type& const_reference;
    typedef MSSpectrumParameterIterator iterator;
    typedef MSSpectrumParameterIterator const_iterator;
    
    virtual size_t size() const = 0;
    virtual value_type operator[] (size_t index) const = 0;
    virtual const_iterator begin() const = 0;
    virtual const_iterator end() const = 0;
};

typedef boost::shared_ptr<MSSpectrumParameterList> MSSpectrumParameterListPtr;

struct PWIZ_API_DECL MSSpectrum
{
    virtual ~MSSpectrum() {}

    virtual bool hasLineData() const = 0;
    virtual bool hasProfileData() const = 0;
    virtual size_t getLineDataSize() const = 0;
    virtual size_t getProfileDataSize() const = 0;
    virtual void getLineData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const = 0;
    virtual void getProfileData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities) const = 0;

    virtual double getTIC() const = 0;
    virtual double getBPI() const = 0;

    virtual int getMSMSStage() const = 0;
    virtual double getRetentionTime() const = 0;
    virtual void getIsolationData(std::vector<IsolationInfo>& isolationInfo) const = 0;
    virtual void getFragmentationData(std::vector<double>& fragmentedMZs,
                                      std::vector<FragmentationMode>& fragmentationModes) const = 0;
    virtual IonPolarity getPolarity() const = 0;

    virtual std::pair<double, double> getScanRange() const = 0;
    virtual int getChargeState() const = 0;
    virtual double getIsolationWidth() const = 0;

    virtual bool isIonMobilitySpectrum() const { return false; }
    virtual double oneOverK0() const { return 0.0; }
    virtual std::pair<double, double> getIonMobilityRange() const { return std::pair<double, double>(0, 0); }
	virtual int getWindowGroup() const { return 0; } // for diaPASEF data

    virtual void getCombinedSpectrumData(pwiz::util::BinaryData<double>& mz, pwiz::util::BinaryData<double>& intensities, pwiz::util::BinaryData<double>& mobilities, bool sortAndJitter) const { }
    virtual size_t getCombinedSpectrumDataSize() const { return 0; }
    virtual pwiz::util::IntegerSet getMergedScanNumbers() const { return pwiz::util::IntegerSet(); }

    virtual MSSpectrumParameterListPtr parameters() const = 0;
};

typedef boost::shared_ptr<MSSpectrum> MSSpectrumPtr;


struct PWIZ_API_DECL LCSpectrumSource
{
    virtual ~LCSpectrumSource() {}

    virtual int getCollectionId() const = 0;
    virtual std::string getInstrument() const = 0;
    virtual std::string getInstrumentId() const = 0;
    virtual double getTimeOffset() const = 0;
    virtual void getXAxis(std::vector<double>& xAxis) const = 0;
    virtual LCUnit getXAxisUnit() const = 0;
};

typedef boost::shared_ptr<LCSpectrumSource> LCSpectrumSourcePtr;


struct PWIZ_API_DECL LCSpectrum
{
    virtual ~LCSpectrum() {}

    virtual void getData(std::vector<double>& intensities) const = 0;
    virtual double getTime() const = 0;
};

typedef boost::shared_ptr<LCSpectrum> LCSpectrumPtr;


struct PWIZ_API_DECL Chromatogram
{
    std::vector<double> times;
    std::vector<double> intensities;
};

typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;


struct FrameScanRange
{
    int frame;
    int scanStart;
    int scanEnd;
};


struct PWIZ_API_DECL CompassData
{
    typedef boost::shared_ptr<CompassData> Ptr;
    static Ptr create(const std::string& rawpath, bool combineIonMobilitySpectra = false,
                      msdata::detail::Bruker::Reader_Bruker_Format format = msdata::detail::Bruker::Reader_Bruker_Format_Unknown, 
                      int preferOnlyMsLevel = 0, // when nonzero, caller only wants spectra at this ms level
                      bool allowMsMsWithoutPrecursor = true, // when false, PASEF MS2 specta without precursor info will be excluded
                      const std::vector<chemistry::MzMobilityWindow>& isolationMzFilter = std::vector<chemistry::MzMobilityWindow>()); // when non-empty, only scans from precursors matching one of the included m/zs (i.e. within a precursor isolation window) will be enumerated

    virtual ~CompassData() {}

    /// returns true if the source has MS spectra
    virtual bool hasMSData() const = 0;

    /// returns true if the source has LC spectra or traces
    virtual bool hasLCData() const = 0;

    /// returns true if the source is TIMS PASEF data
    virtual bool hasPASEFData() const { return false; }

    virtual bool canConvertOneOverK0AndCCS() const { return false; }
    virtual double oneOverK0ToCCS(double oneOverK0, double mz, int charge) const { return 0; }
    virtual double ccsToOneOverK0(double ccs, double mz, int charge) const { return 0; }

    /// returns the number of spectra available from the MS source
    virtual size_t getMSSpectrumCount() const = 0;

    /// converts a one-dimensional, one-based scan number to a one-based frame number and one-based scan number range within the frame (only for TDF data);
    /// for non-combined IMS data, scanStart and scanEnd will be the same
    virtual FrameScanRange getFrameScanPair(int scan) const;

    /// converts a one-based frame number and one-based scan number to a one-dimensional, one-based scan index (only for TDF data)
    virtual size_t getSpectrumIndex(int frame, int scan) const;

    /// returns a spectrum from the MS source
    virtual MSSpectrumPtr getMSSpectrum(int scan, DetailLevel detailLevel = DetailLevel_FullMetadata) const = 0;

    /// returns the number of sources available from the LC system
    virtual size_t getLCSourceCount() const = 0;

    /// returns the number of spectra available from the specified LC source
    virtual size_t getLCSpectrumCount(int source) const = 0;

    /// returns a source from the LC system
    virtual LCSpectrumSourcePtr getLCSource(int source) const = 0;

    /// returns a spectrum from the specified LC source
    virtual LCSpectrumPtr getLCSpectrum(int source, int scan) const = 0;

    /// returns a chromatogram with times and total ion currents of all spectra, or a null pointer if the format doesn't support fast access to TIC
    virtual ChromatogramPtr getTIC(bool ms1Only = false) const = 0;

    /// returns a chromatogram with times and base peak intensities of all spectra, or a null pointer if the format doesn't support fast access to BPC
    virtual ChromatogramPtr getBPC(bool ms1Only = false) const = 0;

    virtual std::string getOperatorName() const = 0;
    virtual std::string getAnalysisName() const = 0;
    virtual boost::local_time::local_date_time getAnalysisDateTime() const = 0;
    virtual std::string getSampleName() const = 0;
    virtual std::string getMethodName() const = 0;
    virtual InstrumentFamily getInstrumentFamily() const = 0;
    virtual int getInstrumentRevision() const = 0;
    virtual std::string getInstrumentDescription() const = 0;
    virtual std::string getInstrumentSerialNumber() const = 0;
    virtual InstrumentSource getInstrumentSource() const = 0;
    virtual std::string getAcquisitionSoftware() const = 0;
    virtual std::string getAcquisitionSoftwareVersion() const = 0;
};

typedef CompassData::Ptr CompassDataPtr;


} // namespace Bruker
} // namespace vendor_api
} // namespace pwiz


#endif // _COMPASSDATA_HPP_
