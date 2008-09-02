//
// MSData.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _MSDATA_HPP_
#define _MSDATA_HPP_

#include "utility/misc/Export.hpp"

#include "CVParam.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/iostreams/positioning.hpp"
#include <vector>
#include <string>
#include <map>


namespace pwiz {
namespace msdata {


struct PWIZ_API_DECL CV
{
    std::string id;
    std::string URI;
    std::string fullName;
    std::string version;

    bool empty() const;
    bool operator==(const CV& that) const;
};


PWIZ_API_DECL std::vector<CV> defaultCVList();


struct PWIZ_API_DECL UserParam
{
    std::string name;
    std::string value;
    std::string type;
    CVID units;

    UserParam(const std::string& _name = "", 
              const std::string& _value = "", 
              const std::string& _type = "",
              CVID _units = CVID_Unknown);

    /// templated value access with type conversion
    template<typename value_type>
    value_type valueAs() const
    {
        return !value.empty() ? boost::lexical_cast<value_type>(value) 
                              : boost::lexical_cast<value_type>(0);
    } 

    bool empty() const;
    bool operator==(const UserParam& that) const;
    bool operator!=(const UserParam& that) const;
};


/// special case for bool (outside the class for gcc 3.4, and inline for msvc)
template<>
inline bool UserParam::valueAs<bool>() const
{
    return value == "true";
}


struct ParamGroup;
typedef boost::shared_ptr<ParamGroup> ParamGroupPtr;


struct PWIZ_API_DECL ParamContainer
{
    std::vector<ParamGroupPtr> paramGroupPtrs;
    std::vector<CVParam> cvParams;
    std::vector<UserParam> userParams;
    
    /// Finds cvid in the container:
    /// - returns first CVParam result such that (result.cvid == cvid); 
    /// - if not found, returns CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParam(CVID cvid) const; 

    /// Finds child of cvid in the container:
    /// - returns first CVParam result such that (result.cvid is_a cvid); 
    /// - if not found, CVParam(CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam cvParamChild(CVID cvid) const; 

    /// returns true iff cvParams contains exact cvid (recursive)
    bool hasCVParam(CVID cvid) const;

    /// returns true iff cvParams contains a child (is_a) of cvid (recursive)
    bool hasCVParamChild(CVID cvid) const;

    /// Finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam userParam(const std::string&) const; 

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, const std::string& value = "", CVID units = CVID_Unknown);

    /// set/add a CVParam (not recursive)
    template <typename value_type>
    void set(CVID cvid, value_type value, CVID units = CVID_Unknown)
    {
        set(cvid, boost::lexical_cast<std::string>(value), units);
    }

    bool empty() const;
    bool operator==(const ParamContainer& that) const;
    bool operator!=(const ParamContainer& that) const;
};


/// special case for bool (outside the class for gcc 3.4, and inline for msvc)
template<>
inline void ParamContainer::set<bool>(CVID cvid, bool value, CVID units)
{
    set(cvid, (value ? "true" : "false"), units);
}


struct PWIZ_API_DECL ParamGroup : public ParamContainer
{
    std::string id;

    ParamGroup(const std::string& _id = "");
    bool empty() const;
};


struct PWIZ_API_DECL FileContent : public ParamContainer {};


struct PWIZ_API_DECL SourceFile : public ParamContainer
{
    std::string id;
    std::string name;
    std::string location;

    SourceFile(const std::string _id = "",
               const std::string _name = "",
               const std::string _location = "");

    bool empty() const;
};


typedef boost::shared_ptr<SourceFile> SourceFilePtr;


struct PWIZ_API_DECL Contact : public ParamContainer {};


struct PWIZ_API_DECL FileDescription
{
    FileContent fileContent;
    std::vector<SourceFilePtr> sourceFilePtrs;
    std::vector<Contact> contacts;

    bool empty() const;
};


struct PWIZ_API_DECL Sample : public ParamContainer
{
    std::string id;
    std::string name;

    Sample(const std::string _id = "",
           const std::string _name = "");

    bool empty() const;
};


typedef boost::shared_ptr<Sample> SamplePtr;


enum ComponentType
{
    ComponentType_Unknown = -1,
    ComponentType_Source = 0,
    ComponentType_Analyzer,
    ComponentType_Detector
};


struct PWIZ_API_DECL Component : public ParamContainer
{
    ComponentType type;
    int order;

    Component() : type(ComponentType_Unknown), order(0) {}
    Component(ComponentType type, int order) : type(type), order(order) {}
    Component(CVID cvid, int order) { define(cvid, order); }
    virtual ~Component(){}

    void define(CVID cvid, int order);
    bool empty() const;
};


//struct PWIZ_API_DECL Source : public Component {};
//struct PWIZ_API_DECL Analyzer : public Component {};
//struct PWIZ_API_DECL Detector : public Component {};


struct PWIZ_API_DECL ComponentList : public std::vector<Component>
{
    Component& source(size_t index);
    Component& analyzer(size_t index);
    Component& detector(size_t index);

    const Component& source(size_t index) const;
    const Component& analyzer(size_t index) const;
    const Component& detector(size_t index) const;
};


struct PWIZ_API_DECL Software
{
    std::string id;

    CVParam softwareParam;
    std::string softwareParamVersion;

    Software(const std::string& _id = "");

    Software(const std::string& _id,
             const CVParam& _softwareParam,
             const std::string& _softwareParamVersion);

    bool empty() const;
};


typedef boost::shared_ptr<Software> SoftwarePtr;


struct PWIZ_API_DECL InstrumentConfiguration : public ParamContainer
{
    std::string id;
    ComponentList componentList;
    SoftwarePtr softwarePtr;

    InstrumentConfiguration(const std::string& _id = "");
    bool empty() const;
};


typedef boost::shared_ptr<InstrumentConfiguration> InstrumentConfigurationPtr;


struct PWIZ_API_DECL ProcessingMethod : public ParamContainer
{
    int order;

    ProcessingMethod() : order(0) {}

    bool empty() const;
};


typedef boost::shared_ptr<ProcessingMethod> ProcessingMethodPtr;


struct PWIZ_API_DECL DataProcessing
{
    std::string id;
    SoftwarePtr softwarePtr;
    std::vector<ProcessingMethod> processingMethods;

    DataProcessing(const std::string& _id = "");

    bool empty() const;
};


typedef boost::shared_ptr<DataProcessing> DataProcessingPtr; 


struct PWIZ_API_DECL Target : public ParamContainer {};


struct PWIZ_API_DECL AcquisitionSettings
{
    std::string id;
    InstrumentConfigurationPtr instrumentConfigurationPtr;
    std::vector<SourceFilePtr> sourceFilePtrs;
    std::vector<Target> targets;

    AcquisitionSettings(const std::string& _id = "");

    bool empty() const;
};


typedef boost::shared_ptr<AcquisitionSettings> AcquisitionSettingsPtr; 


struct PWIZ_API_DECL Acquisition : public ParamContainer
{
    int number;
    SourceFilePtr sourceFilePtr;
    std::string externalNativeID;
    std::string externalSpectrumID;
    std::string spectrumID;

    Acquisition() : number(0) {}

    bool empty() const;
};


struct PWIZ_API_DECL AcquisitionList : public ParamContainer
{
    std::vector<Acquisition> acquisitions;

    bool empty() const;
};


struct PWIZ_API_DECL IsolationWindow : public ParamContainer {};


struct PWIZ_API_DECL SelectedIon : public ParamContainer
{
    SelectedIon() {}
    explicit SelectedIon(double mz);
    explicit SelectedIon(double mz, double intensity);
    explicit SelectedIon(double mz, int chargeState);
    explicit SelectedIon(double mz, double intensity, int chargeState);
};


struct PWIZ_API_DECL Activation : public ParamContainer {};


struct PWIZ_API_DECL Precursor : public ParamContainer
{
    SourceFilePtr sourceFilePtr;
    std::string externalNativeID;
    std::string externalSpectrumID;
    std::string spectrumID;
    IsolationWindow isolationWindow;
    std::vector<SelectedIon> selectedIons;
    Activation activation;


    Precursor() {}
    explicit Precursor(double mz);
    explicit Precursor(double mz, double intensity);
    explicit Precursor(double mz, int chargeState);
    explicit Precursor(double mz, double intensity, int chargeState);


    bool empty() const;
};


struct PWIZ_API_DECL ScanWindow : public ParamContainer
{
    ScanWindow(){}
    ScanWindow(double mzLow, double mzHigh);
};


struct PWIZ_API_DECL Scan : public ParamContainer
{
    InstrumentConfigurationPtr instrumentConfigurationPtr;
    std::vector<ScanWindow> scanWindows;

    bool empty() const;
};


struct PWIZ_API_DECL SpectrumDescription : public ParamContainer
{
    AcquisitionList acquisitionList;
    std::vector<Precursor> precursors;
    Scan scan;

    bool empty() const;
};


struct PWIZ_API_DECL BinaryDataArray : public ParamContainer
{
    DataProcessingPtr dataProcessingPtr;
    std::vector<double> data;

    bool empty() const;
};


typedef boost::shared_ptr<BinaryDataArray> BinaryDataArrayPtr;


#pragma pack(1)
struct PWIZ_API_DECL MZIntensityPair
{
    double mz;
    double intensity;

    MZIntensityPair(double _mz = 0, double _intensity = 0)
    :   mz(_mz), intensity(_intensity)
    {}

    bool operator==(const MZIntensityPair& that) const;
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MZIntensityPair& mzi);


#pragma pack(1)
struct PWIZ_API_DECL TimeIntensityPair
{
    double time;
    double intensity;

    TimeIntensityPair(double _time = 0, double _intensity = 0)
    :   time(_time), intensity(_intensity)
    {}

    bool operator==(const TimeIntensityPair& that) const;
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const TimeIntensityPair& ti);


struct PWIZ_API_DECL SpectrumIdentity
{
    size_t index;
    std::string id;
    std::string nativeID;
    std::string spotID;
	boost::iostreams::stream_offset sourceFilePosition;

    SpectrumIdentity() : index(0), sourceFilePosition(-1) {}
};


struct PWIZ_API_DECL ChromatogramIdentity
{
    size_t index;
    std::string id;
    std::string nativeID;
	boost::iostreams::stream_offset sourceFilePosition;

    ChromatogramIdentity() : index(0), sourceFilePosition(-1) {}
};


struct PWIZ_API_DECL Spectrum : public SpectrumIdentity, public ParamContainer
{
    size_t defaultArrayLength; 
    DataProcessingPtr dataProcessingPtr;
    SourceFilePtr sourceFilePtr;
    SpectrumDescription spectrumDescription;
    std::vector<BinaryDataArrayPtr> binaryDataArrayPtrs; 

    Spectrum() : defaultArrayLength(0) {}

    bool empty() const;

    /// copy binary data arrays into m/z-intensity pair array
    void getMZIntensityPairs(std::vector<MZIntensityPair>& output) const;

    /// copy binary data arrays into m/z-intensity pair array
    /// note: this overload is to allow client to allocate own buffer; the client
    /// must determine the correct size beforehand, or an exception will be thrown
    void getMZIntensityPairs(MZIntensityPair* output, size_t expectedSize) const;

    /// get m/z array (may be null)
    BinaryDataArrayPtr getMZArray() const;

    /// get intensity array (may be null)
    BinaryDataArrayPtr getIntensityArray() const;

    /// set binary data arrays 
    void setMZIntensityPairs(const std::vector<MZIntensityPair>& input, CVID intensityUnits = CVID_Unknown);

    /// set binary data arrays 
    void setMZIntensityPairs(const MZIntensityPair* input, size_t size, CVID intensityUnits = CVID_Unknown);

    /// set m/z and intensity arrays separately (they must be the same size)
    void setMZIntensityArrays(const std::vector<double>& mzArray, const std::vector<double>& intensityArray, CVID intensityUnits = CVID_Unknown);
};


typedef boost::shared_ptr<Spectrum> SpectrumPtr;


struct PWIZ_API_DECL Chromatogram : public ChromatogramIdentity, public ParamContainer
{
    size_t defaultArrayLength; 
    DataProcessingPtr dataProcessingPtr;
    std::vector<BinaryDataArrayPtr> binaryDataArrayPtrs; 

    Chromatogram() : defaultArrayLength(0) {}

    bool empty() const;

    /// copy binary data arrays into time-intensity pair array
    void getTimeIntensityPairs(std::vector<TimeIntensityPair>& output) const;

    /// copy binary data arrays into time-intensity pair array
    /// note: this overload is to allow client to allocate own buffer; the client
    /// must determine the correct size beforehand, or an exception will be thrown
    void getTimeIntensityPairs(TimeIntensityPair* output, size_t expectedSize) const;

    /// set binary data arrays 
    void setTimeIntensityPairs(const std::vector<TimeIntensityPair>& input);

    /// set binary data arrays 
    void setTimeIntensityPairs(const TimeIntensityPair* input, size_t size);
};


typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;


// note: derived container to support dynamic linking on Windows
class IndexList : public std::vector<size_t> {};


/// 
/// Interface for accessing spectra, which may be stored in memory
/// or backed by a data file (RAW, mzXML, mzML).  
///
/// Implementation notes:
///
/// - Implementations are expected to keep a spectrum index in the form of
///   vector<SpectrumIdentity> or equivalent.  The default find*() functions search
///   the index linearly.  Implementations may provide constant time indexing.
///
/// - The semantics of spectrum() may vary slightly with implementation.  In particular,
///   a SpectrumList implementation that is backed by a file may choose either to cache 
///   or discard the SpectrumPtrs for future access, with the caveat that the client 
///   may write to the underlying data.
///
/// - It is the implementation's responsibility to return a valid SpectrumPtr from spectrum().
///   If this cannot be done, an exception must be thrown. 
/// 
/// - The 'getBinaryData' flag is a hint if false : implementations may provide valid 
///   BinaryDataArrayPtrs on spectrum(index, false);  implementations *must* provide 
///   valid BinaryDataArrayPtrs on spectrum(index, true).
///
class PWIZ_API_DECL SpectrumList
{
    public:
    
    /// returns the number of spectra
    virtual size_t size() const = 0;

    /// returns true iff (size() == 0)
    virtual bool empty() const;

    /// access to a spectrum index
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const = 0;

    /// find id in the spectrum index (returns size() on failure)
    virtual size_t find(const std::string& id) const;

    /// find nativeID in the spectrum index (returns size() on failure)
    virtual size_t findNative(const std::string& nativeID) const;

    /// find all spectrum indexes with spotID (returns empty vector on failure)
    virtual IndexList findSpotID(const std::string& spotID) const;

    /// retrieve a spectrum by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Spectrum* is valid 
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const = 0;

    virtual ~SpectrumList(){} 
};


typedef boost::shared_ptr<SpectrumList> SpectrumListPtr;


/// Simple writeable in-memory implementation of SpectrumList.
/// Note:  This spectrum() implementation returns internal SpectrumPtrs.
struct PWIZ_API_DECL SpectrumListSimple : public SpectrumList
{
    std::vector<SpectrumPtr> spectra;

    // SpectrumList implementation

    virtual size_t size() const {return spectra.size();}
    virtual bool empty() const {return spectra.empty();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
};


typedef boost::shared_ptr<SpectrumListSimple> SpectrumListSimplePtr;


class PWIZ_API_DECL ChromatogramList
{
    public:
    
    /// returns the number of chromatograms 
    virtual size_t size() const = 0;

    /// returns true iff (size() == 0)
    bool empty() const;

    /// access to a chromatogram index
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const = 0;

    /// find id in the chromatogram index (returns size() on failure)
    virtual size_t find(const std::string& id) const;

    /// find nativeID in the chromatogram index (returns size() on failure)
    virtual size_t findNative(const std::string& nativeID) const;

    /// retrieve a chromatogram by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Chromatogram* is valid 
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData = false) const = 0;

    virtual ~ChromatogramList(){} 
};


typedef boost::shared_ptr<ChromatogramList> ChromatogramListPtr;


/// Simple writeable in-memory implementation of ChromatogramList.
/// Note:  This chromatogram() implementation returns internal ChromatogramPtrs.
struct PWIZ_API_DECL ChromatogramListSimple : public ChromatogramList
{
    std::vector<ChromatogramPtr> chromatograms;

    // ChromatogramList implementation

    virtual size_t size() const {return chromatograms.size();}
    virtual bool empty() const {return chromatograms.empty();}
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
};


typedef boost::shared_ptr<ChromatogramListSimple> ChromatogramListSimplePtr;


struct PWIZ_API_DECL Run : public ParamContainer
{
    std::string id;
    InstrumentConfigurationPtr defaultInstrumentConfigurationPtr;
    SamplePtr samplePtr;
    std::string startTimeStamp;
    std::vector<SourceFilePtr> sourceFilePtrs;
    SpectrumListPtr spectrumListPtr;
    ChromatogramListPtr chromatogramListPtr;

    Run(){}
    bool empty() const;

    private:
    // no copying - any implementation must handle:
    // - SpectrumList cloning
    // - internal cross-references to heap-allocated objects 
    Run(const Run&);
    Run& operator=(const Run&);
};


struct PWIZ_API_DECL MSData
{
    std::string accession;
    std::string id;
    std::string version;
    std::vector<CV> cvs; 
    FileDescription fileDescription;
    std::vector<ParamGroupPtr> paramGroupPtrs;
    std::vector<SamplePtr> samplePtrs;
    std::vector<InstrumentConfigurationPtr> instrumentConfigurationPtrs;
    std::vector<SoftwarePtr> softwarePtrs;
    std::vector<DataProcessingPtr> dataProcessingPtrs;
    std::vector<AcquisitionSettingsPtr> acquisitionSettingsPtrs;
    Run run;

    MSData();
    virtual ~MSData();
    bool empty() const;

    private:
    // no copying
    MSData(const MSData&);
    MSData& operator=(const MSData&);
};


} // namespace msdata
} // namespace pwiz


#endif // _MSDATA_HPP_

