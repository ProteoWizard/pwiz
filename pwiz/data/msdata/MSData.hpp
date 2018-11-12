//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/utility/misc/BinaryData.hpp"
#include "pwiz/data/common/ParamTypes.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/iostreams/positioning.hpp"
#include <vector>
#include <string>
#include <map>
#include <set>


namespace pwiz {
namespace msdata {


    using namespace pwiz::data;


PWIZ_API_DECL std::vector<CV> defaultCVList();


/// This summarizes the different types of spectra that can be expected in the file. This is expected to aid processing software in skipping files that do not contain appropriate spectrum types for it.
struct PWIZ_API_DECL FileContent : public ParamContainer {};


/// Description of the source file, including location and type.
struct PWIZ_API_DECL SourceFile : public ParamContainer
{
    /// an identifier for this file.
    std::string id;

    /// name of the source file, without reference to location (either URI or local path).
    std::string name;

    /// URI-formatted location where the file was retrieved.
    std::string location;

    SourceFile(const std::string _id = "",
               const std::string _name = "",
               const std::string _location = "");


    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


/// Description of the source file, including location and type.
typedef boost::shared_ptr<SourceFile> SourceFilePtr;


/// Structure allowing the use of a controlled (cvParam) or uncontrolled vocabulary (userParam), or a reference to a predefined set of these in this mzML file (paramGroupRef).
struct PWIZ_API_DECL Contact : public ParamContainer {};


/// Information pertaining to the entire mzML file (i.e. not specific to any part of the data set) is stored here.
struct PWIZ_API_DECL FileDescription
{
    /// this summarizes the different types of spectra that can be expected in the file. This is expected to aid processing software in skipping files that do not contain appropriate spectrum types for it.
    FileContent fileContent;

    /// list and descriptions of the source files this mzML document was generated or derived from.
    std::vector<SourceFilePtr> sourceFilePtrs;

    /// structure allowing the use of a controlled (cvParam) or uncontrolled vocabulary (userParam), or a reference to a predefined set of these in this mzML file (paramGroupRef)
    std::vector<Contact> contacts;

    /// returns true iff all members are empty or null
    bool empty() const;
};


/// Expansible description of the sample used to generate the dataset, named in sampleName.
struct PWIZ_API_DECL Sample : public ParamContainer
{
    /// a unique identifier across the samples with which to reference this sample description.
    std::string id;

    /// an optional name for the sample description, mostly intended as a quick mnemonic.
    std::string name;

    Sample(const std::string _id = "",
           const std::string _name = "");


    /// returns true iff the element contains no params and all members are empty or null
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


/// A component of an instrument corresponding to a source (i.e. ion source), an analyzer (i.e. mass analyzer), or a detector (i.e. ion detector)
struct PWIZ_API_DECL Component : public ParamContainer
{
    /// the type of component (Source, Analyzer, or Detector)
    ComponentType type;

    /// this attribute MUST be used to indicate the order in which the components are encountered from source to detector (e.g., in a Q-TOF, the quadrupole would have the lower order number, and the TOF the higher number of the two).
    int order;

    Component() : type(ComponentType_Unknown), order(0) {}
    Component(ComponentType type, int order) : type(type), order(order) {}
    Component(CVID cvid, int order) { define(cvid, order); }

    void define(CVID cvid, int order);

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


//struct PWIZ_API_DECL Source : public Component {};
//struct PWIZ_API_DECL Analyzer : public Component {};
//struct PWIZ_API_DECL Detector : public Component {};


/// List with the different components used in the mass spectrometer. At least one source, one mass analyzer and one detector need to be specified.
struct PWIZ_API_DECL ComponentList : public std::vector<Component>
{
    /// returns the source component with ordinal <index+1>
    Component& source(size_t index);

    /// returns the analyzer component with ordinal <index+1>
    Component& analyzer(size_t index);

    /// returns the detector component with ordinal <index+1>
    Component& detector(size_t index);

    /// returns the source component with ordinal <index+1>
    const Component& source(size_t index) const;

    /// returns the analyzer component with ordinal <index+1>
    const Component& analyzer(size_t index) const;

    /// returns the detector component with ordinal <index+1>
    const Component& detector(size_t index) const;
};


/// A piece of software.
struct PWIZ_API_DECL Software : public ParamContainer
{
    /// an identifier for this software that is unique across all SoftwareTypes.
    std::string id;

    /// the software version.
    std::string version;

    Software(const std::string& _id = "");

    Software(const std::string& _id,
             const CVParam& _param,
             const std::string& _version);

    /// returns true iff all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<Software> SoftwarePtr;


/// TODO
struct PWIZ_API_DECL Target : public ParamContainer {};


/// Description of the acquisition settings of the instrument prior to the start of the run.
struct PWIZ_API_DECL ScanSettings
{
    /// a unique identifier for this acquisition setting.
    std::string id;

    /// container for a list of source file references.
    std::vector<SourceFilePtr> sourceFilePtrs;

    /// target list (or 'inclusion list') configured prior to the run.
    std::vector<Target> targets;

    ScanSettings(const std::string& _id = "");


    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<ScanSettings> ScanSettingsPtr;


/// Description of a particular hardware configuration of a mass spectrometer. Each configuration MUST have one (and only one) of the three different components used for an analysis. For hybrid instruments, such as an LTQ-FT, there MUST be one configuration for each permutation of the components that is used in the document. For software configuration, reference the appropriate ScanSettings element.
struct PWIZ_API_DECL InstrumentConfiguration : public ParamContainer
{
    /// an identifier for this instrument configuration.
    std::string id;

    /// list with the different components used in the mass spectrometer. At least one source, one mass analyzer and one detector need to be specified.
    ComponentList componentList;

    /// reference to a previously defined software element.
    SoftwarePtr softwarePtr;

    /// reference to a scan settings element defining global scan settings used by this configuration
    ScanSettingsPtr scanSettingsPtr;

    InstrumentConfiguration(const std::string& _id = "");

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<InstrumentConfiguration> InstrumentConfigurationPtr;


/// Description of the default peak processing method. This element describes the base method used in the generation of a particular mzML file. Variable methods should be described in the appropriate acquisition section - if no acquisition-specific details are found, then this information serves as the default.
struct PWIZ_API_DECL ProcessingMethod : public ParamContainer
{
    /// this attributes allows a series of consecutive steps to be placed in the correct order.
    int order;

    /// this attribute MUST reference the 'id' of the appropriate SoftwareType.
    SoftwarePtr softwarePtr;

    ProcessingMethod() : order(0) {}

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<ProcessingMethod> ProcessingMethodPtr;


/// Description of the way in which a particular software was used.
struct PWIZ_API_DECL DataProcessing
{
    /// a unique identifier for this data processing that is unique across all DataProcessingTypes.
    std::string id;

    /// description of the default peak processing method(s). This element describes the base method used in the generation of a particular mzML file. Variable methods should be described in the appropriate acquisition section - if no acquisition-specific details are found, then this information serves as the default.
    std::vector<ProcessingMethod> processingMethods;

    DataProcessing(const std::string& _id = "");

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<DataProcessing> DataProcessingPtr;


/// This element captures the isolation (or 'selection') window configured to isolate one or more precursors.
struct PWIZ_API_DECL IsolationWindow : public ParamContainer {};


/// TODO
struct PWIZ_API_DECL SelectedIon : public ParamContainer
{
    SelectedIon() {}
    explicit SelectedIon(double mz);
    explicit SelectedIon(double mz, double intensity, CVID intensityUnit);
    explicit SelectedIon(double mz, int chargeState);
    explicit SelectedIon(double mz, double intensity, int chargeState, CVID intensityUnit);
};


/// The type and energy level used for activation.
struct PWIZ_API_DECL Activation : public ParamContainer {};


/// The method of precursor ion selection and activation
struct PWIZ_API_DECL Precursor : public ParamContainer
{
    /// for precursor spectra that are external to this document, this attribute MUST reference the 'id' attribute of a sourceFile representing that external document.
    /// note: this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both
    SourceFilePtr sourceFilePtr;

    /// for precursor spectra that are external to this document, this string MUST correspond to the 'id' attribute of a spectrum in the external document indicated by 'sourceFileRef'.
    /// note: this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both
    std::string externalSpectrumID;

    /// reference to the id attribute of the spectrum from which the precursor was selected.
    /// note: this attribute is mutually exclusive with externalSpectrumID; i.e. use one or the other but not both
    std::string spectrumID;

    /// this element captures the isolation (or 'selection') window configured to isolate one or more precursors.
    IsolationWindow isolationWindow;

    /// this list of precursor ions that were selected.
    std::vector<SelectedIon> selectedIons;

    /// the type and energy level used for activation.
    Activation activation;

    Precursor() {}
    explicit Precursor(double mz);
    explicit Precursor(double mz, double intensity, CVID intensityUnit);
    explicit Precursor(double mz, int chargeState);
    explicit Precursor(double mz, double intensity, int chargeState, CVID intensityUnit);


    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


/// product ion information
struct PWIZ_API_DECL Product
{
    /// this element captures the isolation (or 'selection') window configured to isolate one or more precursors.
    IsolationWindow isolationWindow;

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;

    /// returns true iff this product's isolation window is equal to that product's
    bool operator==(const Product& that) const;
};


/// TODO
struct PWIZ_API_DECL ScanWindow : public ParamContainer
{
    ScanWindow(){}
    ScanWindow(double low, double high, CVID unit);
};


/// Scan or acquisition from original raw file used to create this peak list, as specified in sourceFile.
struct PWIZ_API_DECL Scan : public ParamContainer
{
    /// if this attribute is set, it must reference the 'id' attribute of a sourceFile representing the external document containing the spectrum referred to by 'externalSpectrumID'.
    /// note: this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both
    SourceFilePtr sourceFilePtr;

    /// for scans that are external to this document, this string must correspond to the 'id' attribute of a spectrum in the external document indicated by 'sourceFileRef'.
    /// note: this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both
    std::string externalSpectrumID;

    /// for scans that are local to this document, this attribute can be used to reference the 'id' attribute of the spectrum corresponding to the scan.
    /// note: this attribute is mutually exclusive with externalSpectrumID; i.e. use one or the other but not both
    std::string spectrumID;

    /// this attribute MUST reference the 'id' attribute of the appropriate instrument configuration.
    InstrumentConfigurationPtr instrumentConfigurationPtr;

    /// container for a list of select windows.
    std::vector<ScanWindow> scanWindows;

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


/// List and descriptions of scans.
struct PWIZ_API_DECL ScanList : public ParamContainer
{
    std::vector<Scan> scans;

    bool empty() const;
};


/// The structure into which encoded binary data goes. Byte ordering is always little endian (Intel style). Computers using a different endian style MUST convert to/from little endian when writing/reading mzML
struct PWIZ_API_DECL BinaryDataArray : public ParamContainer
{
    /// this optional attribute may reference the 'id' attribute of the appropriate dataProcessing.
    DataProcessingPtr dataProcessingPtr;

    /// the binary data.
    pwiz::util::BinaryData<double> data;

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;
};


typedef boost::shared_ptr<BinaryDataArray> BinaryDataArrayPtr;


#pragma pack(1)
/// The data point type of a mass spectrum.
struct PWIZ_API_DECL MZIntensityPair
{
    double mz;
    double intensity;

    MZIntensityPair()
    :   mz(0), intensity(0)
    {}

    MZIntensityPair(double mz, double intensity)
    :   mz(mz), intensity(intensity)
    {}

    /// returns true iff mz and intensity are pairwise equal
    bool operator==(const MZIntensityPair& that) const;
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const MZIntensityPair& mzi);


#pragma pack(1)
/// The data point type of a chromatogram.
struct PWIZ_API_DECL TimeIntensityPair
{
    double time;
    double intensity;

    TimeIntensityPair()
    :   time(0), intensity(0)
    {}

    TimeIntensityPair(double time, double intensity)
    :   time(time), intensity(intensity)
    {}

    /// returns true iff time and intensity are pairwise equal
    bool operator==(const TimeIntensityPair& that) const;
};
#pragma pack()


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const TimeIntensityPair& ti);

const size_t IDENTITY_INDEX_NONE = (size_t)-1;

/// Identifying information for a spectrum
struct PWIZ_API_DECL SpectrumIdentity
{
    /// the zero-based, consecutive index of the spectrum in the SpectrumList.
    size_t index;

    /// a unique identifier for this spectrum. It should be expected that external files may use this identifier together with the mzML filename or accession to reference a particular spectrum.
    std::string id;

    /// the identifier for the spot from which this spectrum was derived, if a MALDI or similar run.
    std::string spotID;

    /// for file-based MSData implementations, this attribute may refer to the spectrum's position in the file
    boost::iostreams::stream_offset sourceFilePosition;

 
    SpectrumIdentity() : index(IDENTITY_INDEX_NONE), sourceFilePosition((boost::iostreams::stream_offset)-1) {}
};

/// Identifying information for a chromatogram
struct PWIZ_API_DECL ChromatogramIdentity
{
    /// the zero-based, consecutive index of the chromatogram in the ChromatogramList.
    size_t index;

    /// a unique identifier for this chromatogram. It should be expected that external files may use this identifier together with the mzML filename or accession to reference a particular chromatogram.
    std::string id;

    /// for file-based MSData implementations, this attribute may refer to the chromatogram's position in the file
    boost::iostreams::stream_offset sourceFilePosition;

    ChromatogramIdentity() : index(IDENTITY_INDEX_NONE), sourceFilePosition(-1) {}
};


/// The structure that captures the generation of a peak list (including the underlying acquisitions)
struct PWIZ_API_DECL Spectrum : public SpectrumIdentity, public ParamContainer
{
    /// default length of binary data arrays contained in this element.
    size_t defaultArrayLength;

    /// this attribute can optionally reference the 'id' of the appropriate dataProcessing.
    DataProcessingPtr dataProcessingPtr;

    /// this attribute can optionally reference the 'id' of the appropriate sourceFile.
    SourceFilePtr sourceFilePtr;

    /// list of scans
    ScanList scanList;

    /// list and descriptions of precursors to the spectrum currently being described.
    std::vector<Precursor> precursors;

    /// list and descriptions of product ion information
    std::vector<Product> products;

    /// list of binary data arrays.
    std::vector<BinaryDataArrayPtr> binaryDataArrayPtrs; 


    Spectrum() : defaultArrayLength(0) {}

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;

    /// returns true iff has nonnull and nonempty BinaryDataArrayPtr
    bool hasBinaryData() const {
        return binaryDataArrayPtrs.size() && 
               binaryDataArrayPtrs[0] &&
              !binaryDataArrayPtrs[0]->data.empty();
    };

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

    /// get array with specified CVParam (may be null)
    BinaryDataArrayPtr getArrayByCVID(CVID arrayType) const;

    /// set binary data arrays 
    void setMZIntensityPairs(const std::vector<MZIntensityPair>& input, CVID intensityUnits);

    /// set binary data arrays 
    void setMZIntensityPairs(const MZIntensityPair* input, size_t size, CVID intensityUnits);

    /// set m/z and intensity arrays separately (they must be the same size)
    void setMZIntensityArrays(const std::vector<double>& mzArray, const std::vector<double>& intensityArray, CVID intensityUnits);

    /// set m/z and intensity arrays separately (they must be the same size) by swapping the vector contents
    /// this allows for a more nearly zero copy setup.  Contents of mzArray and intensityArray are undefined after calling.
    void swapMZIntensityArrays(pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray, CVID intensityUnits);
};


typedef boost::shared_ptr<Spectrum> SpectrumPtr;


/// A single chromatogram.
struct PWIZ_API_DECL Chromatogram : public ChromatogramIdentity, public ParamContainer
{
    /// default length of binary data arrays contained in this element.
    size_t defaultArrayLength; 

    /// this attribute can optionally reference the 'id' of the appropriate dataProcessing.
    DataProcessingPtr dataProcessingPtr;

    /// description of precursor ion information (i.e. Q1 settings)
    Precursor precursor;

    /// description of product ion information (i.e. Q3 settings)
    Product product;

    /// list of binary data arrays.
    std::vector<BinaryDataArrayPtr> binaryDataArrayPtrs; 

    Chromatogram() : defaultArrayLength(0) {}

    /// returns true iff the element contains no params and all members are empty or null
    bool empty() const;

    /// copy binary data arrays into time-intensity pair array
    void getTimeIntensityPairs(std::vector<TimeIntensityPair>& output) const;

    /// copy binary data arrays into time-intensity pair array
    /// note: this overload is to allow client to allocate own buffer; the client
    /// must determine the correct size beforehand, or an exception will be thrown
    void getTimeIntensityPairs(TimeIntensityPair* output, size_t expectedSize) const;

    /// get time array (may be null)
    BinaryDataArrayPtr getTimeArray() const;

    /// get intensity array (may be null)
    BinaryDataArrayPtr getIntensityArray() const;

    /// set binary data arrays
    void setTimeIntensityPairs(const std::vector<TimeIntensityPair>& input, CVID timeUnits, CVID intensityUnits);

    /// set binary data arrays
    void setTimeIntensityPairs(const TimeIntensityPair* input, size_t size, CVID timeUnits, CVID intensityUnits);

    /// set time and intensity arrays separately (they must be the same size)
    void setTimeIntensityArrays(const std::vector<double>& timeArray, const std::vector<double>& intensityArray, CVID timeUnits, CVID intensityUnits);
};


typedef boost::shared_ptr<Chromatogram> ChromatogramPtr;


// note: derived container to support dynamic linking on Windows
class IndexList : public std::vector<size_t> {};

enum DetailLevel
{
    DetailLevel_InstantMetadata,
    DetailLevel_FastMetadata,
    DetailLevel_FullMetadata,
    DetailLevel_FullData
};

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

    /// returns true iff (size() == 0) and (dataProcessingPtr.get() == NULL)
    virtual bool empty() const;

    /// access to a spectrum index
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const = 0;

    /// find id in the spectrum index (returns size() on failure)
    virtual size_t find(const std::string& id) const;

    /// find an abbreviated id (e.g. "1.1.123.2" for "sample=1 period=1 cycle=123 experiment=2") in the spectrum index (returns size() on failure)
    virtual size_t findAbbreviated(const std::string& abbreviatedId, char delimiter = '.') const;

    /// find all spectrum indexes with specified name/value pair 
    virtual IndexList findNameValue(const std::string& name, const std::string& value) const;

    /// find all spectrum indexes with spotID (returns empty vector on failure)
    virtual IndexList findSpotID(const std::string& spotID) const;

    /// retrieve a spectrum by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Spectrum* is valid 
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData = false) const = 0;

    /// get a copy of the seed spectrum, optionally with its binary data populated
    /// this is useful for formats like mzML that can delay loading of binary data
    /// - client may assume the underlying Spectrum* is valid 
    virtual SpectrumPtr spectrum(const SpectrumPtr& seed, bool getBinaryData) const;

    /// retrieve a spectrum by index
    /// - detailLevel determines what fields are guaranteed present on the spectrum after the call
    /// - client may assume the underlying Spectrum* is valid 
    virtual SpectrumPtr spectrum(size_t index, DetailLevel detailLevel) const;

    /// returns the data processing affecting spectra retrieved through this interface
    /// - may return a null shared pointer
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const;

    /// issues a warning once per SpectrumList instance (based on string hash)
    virtual void warn_once(const char* msg) const; 

    virtual ~SpectrumList(){} 
};


typedef boost::shared_ptr<SpectrumList> SpectrumListPtr;


/// Simple writeable in-memory implementation of SpectrumList.
/// Note:  This spectrum() implementation returns internal SpectrumPtrs.
struct PWIZ_API_DECL SpectrumListSimple : public SpectrumList
{
    std::vector<SpectrumPtr> spectra;
    DataProcessingPtr dp;

    // SpectrumList implementation

    virtual size_t size() const {return spectra.size();}
    virtual bool empty() const {return spectra.empty() && !dp.get();}
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const;
};


typedef boost::shared_ptr<SpectrumListSimple> SpectrumListSimplePtr;


/// 
/// Interface for accessing chromatograms, which may be stored in memory
/// or backed by a data file (RAW, mzXML, mzML).  
///
/// Implementation notes:
///
/// - Implementations are expected to keep a chromatogram index in the form of
///   vector<ChromatogramIdentity> or equivalent.  The default find*() functions search
///   the index linearly.  Implementations may provide constant time indexing.
///
/// - The semantics of chromatogram() may vary slightly with implementation.  In particular,
///   a ChromatogramList implementation that is backed by a file may choose either to cache 
///   or discard the ChromatogramPtrs for future access, with the caveat that the client 
///   may write to the underlying data.
///
/// - It is the implementation's responsibility to return a valid ChromatogramPtr from chromatogram().
///   If this cannot be done, an exception must be thrown. 
/// 
/// - The 'getBinaryData' flag is a hint if false : implementations may provide valid 
///   BinaryDataArrayPtrs on chromatogram(index, false);  implementations *must* provide 
///   valid BinaryDataArrayPtrs on chromatogram(index, true).
///
class PWIZ_API_DECL ChromatogramList
{
    public:
    
    /// returns the number of chromatograms 
    virtual size_t size() const = 0;

    /// returns true iff (size() == 0) and (dataProcessingPtr.get() == NULL)
    bool empty() const;

    /// access to a chromatogram index
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const = 0;

    /// find id in the chromatogram index (returns size() on failure)
    virtual size_t find(const std::string& id) const;

    /// retrieve a chromatogram by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Chromatogram* is valid 
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData = false) const = 0;

    /// returns the data processing affecting spectra retrieved through this interface
    /// - may return a null shared pointer
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const;

    virtual ~ChromatogramList(){} 
};


typedef boost::shared_ptr<ChromatogramList> ChromatogramListPtr;


/// Simple writeable in-memory implementation of ChromatogramList.
/// Note:  This chromatogram() implementation returns internal ChromatogramPtrs.
struct PWIZ_API_DECL ChromatogramListSimple : public ChromatogramList
{
    std::vector<ChromatogramPtr> chromatograms;
    DataProcessingPtr dp;

    // ChromatogramList implementation

    virtual size_t size() const {return chromatograms.size();}
    virtual bool empty() const {return chromatograms.empty() && !dp.get();}
    virtual const ChromatogramIdentity& chromatogramIdentity(size_t index) const;
    virtual ChromatogramPtr chromatogram(size_t index, bool getBinaryData) const;
    virtual const boost::shared_ptr<const DataProcessing> dataProcessingPtr() const;
};


typedef boost::shared_ptr<ChromatogramListSimple> ChromatogramListSimplePtr;


/// A run in mzML should correspond to a single, consecutive and coherent set of scans on an instrument.
struct PWIZ_API_DECL Run : public ParamContainer
{
    /// a unique identifier for this run.
    std::string id;

    /// this attribute MUST reference the 'id' of the default instrument configuration. If a scan does not reference an instrument configuration, it implicitly refers to this configuration.
    InstrumentConfigurationPtr defaultInstrumentConfigurationPtr;

    /// this attribute MUST reference the 'id' of the appropriate sample.
    SamplePtr samplePtr;

    /// the optional start timestamp of the run, in UT.
    std::string startTimeStamp;

    /// default source file reference 
    SourceFilePtr defaultSourceFilePtr;

    /// all mass spectra and the acquisitions underlying them are described and attached here. Subsidiary data arrays are also both described and attached here.
    SpectrumListPtr spectrumListPtr;

    /// all chromatograms for this run.
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


namespace IO {struct HandlerMSData;} // forward declaration for friend


/// This is the root element of ProteoWizard; it represents the mzML element, defined as:
/// intended to capture the use of a mass spectrometer, the data generated, and the initial processing of that data (to the level of the peak list).
struct PWIZ_API_DECL MSData
{
    /// an optional accession number for the mzML document.
    std::string accession;

    /// an optional id for the mzML document. It is recommended to use LSIDs when possible.
    std::string id;

    /// container for one or more controlled vocabulary definitions.
    /// note: one of the <cv> elements in this list MUST be the PSI MS controlled vocabulary. All <cvParam> elements in the document MUST refer to one of the <cv> elements in this list.
    std::vector<CV> cvs;

    /// information pertaining to the entire mzML file (i.e. not specific to any part of the data set) is stored here.
    FileDescription fileDescription;

    /// container for a list of referenceableParamGroups
    std::vector<ParamGroupPtr> paramGroupPtrs;

    /// list and descriptions of samples.
    std::vector<SamplePtr> samplePtrs;

    /// list and descriptions of software used to acquire and/or process the data in this mzML file.
    std::vector<SoftwarePtr> softwarePtrs;

    /// list with the descriptions of the acquisition settings applied prior to the start of data acquisition.
    std::vector<ScanSettingsPtr> scanSettingsPtrs;

    /// list and descriptions of instrument configurations.
    std::vector<InstrumentConfigurationPtr> instrumentConfigurationPtrs;

    /// list and descriptions of data processing applied to this data.
    std::vector<DataProcessingPtr> dataProcessingPtrs;

    /// return dataProcessingPtrs augmented by the dataProcessingPtr() set in SpectrumList and/or ChromatogramList
    std::vector<DataProcessingPtr> allDataProcessingPtrs() const;

    /// a run in mzML should correspond to a single, consecutive and coherent set of scans on an instrument.
    Run run;

    MSData();
    virtual ~MSData();
    bool empty() const;

    /// returns the version of this mzML document;
    /// for a document created programmatically, the version is the current release version of mzML;
    /// for a document created from a file/stream, the version is the schema version read from the file/stream
    const std::string& version() const;

    // for detecting out of order filters
    void filterApplied() {nFiltersApplied_++;};
    int countFiltersApplied() const {return nFiltersApplied_;};

    private:
    // no copying
    MSData(const MSData&);
    MSData& operator=(const MSData&);

    protected:
    std::string version_; // schema version read from the file/stream
    friend struct IO::HandlerMSData;
    int nFiltersApplied_; // useful for flagging filters that need to be first, like vendor centroiding
};


typedef boost::shared_ptr<MSData> MSDataPtr;


namespace id {

/// parses an id string into a map<string,string>
PWIZ_API_DECL std::map<std::string,std::string> parse(const std::string& id);

/// convenience function to extract a named value from an id string
PWIZ_API_DECL std::string value(const std::string& id, const std::string& name);

/// templated convenience function to extract a named value from an id string 
template<typename value_type>
value_type valueAs(const std::string& id, const std::string& name)
{
    std::string result = value(id, name);
    return !result.empty() ? boost::lexical_cast<value_type>(result) 
                          : boost::lexical_cast<value_type>(0);
}

/// returns the nativeID format from the defaultSourceFilePtr if set,
/// or from sourceFilePtrs[0] if the list isn't empty,
/// or CVID_Unknown
PWIZ_API_DECL CVID getDefaultNativeIDFormat(const MSData& msd);

/// translates a "scan number" to a string that is correct for the given nativeID format;
/// semantic validity requires that scanNumber be parseable as an integer;
/// some nativeID formats cannot be translated to and will always return an empty string
/// currently supported formats: Thermo, Bruker/Agilent YEP, Bruker BAF, mzXML, MGF, and mzData
PWIZ_API_DECL std::string translateScanNumberToNativeID(CVID nativeIDFormat, const std::string& scanNumber);

/// translates a nativeID in the given nativeID format to a simple integer "scan number";
/// some nativeID formats cannot be translated from and will always return an empty string
/// currently supported formats: Thermo, Bruker/Agilent YEP, Bruker BAF, mzXML, MGF, and mzData
PWIZ_API_DECL std::string translateNativeIDToScanNumber(CVID nativeIDFormat, const std::string& id);

/// abbreviates a nativeID ("name1=value1 name2=value2" translates to "value1.value2")
PWIZ_API_DECL std::string abbreviate(const std::string& id, char delimiter = '.');

} // namespace id


} // namespace msdata
} // namespace pwiz


#endif // _MSDATA_HPP_

