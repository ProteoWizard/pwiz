;//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#ifndef _MSDATA_HPP_CLI_
#define _MSDATA_HPP_CLI_


#pragma warning( push )
#pragma warning( disable : 4634 4635 )

#ifdef PWIZ_BINDINGS_CLI_COMBINED
    #include "../common/ParamTypes.hpp"
#else
    #include "../common/SharedCLI.hpp"
    #using "pwiz_bindings_cli_common.dll" as_friend
#endif

#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/data/msdata/Version.hpp"
#pragma warning( pop )


// list of friend assemblies that are permitted to access MSData's internal members
[assembly:System::Runtime::CompilerServices::InternalsVisibleTo("pwiz_bindings_cli_analysis")];


namespace pwiz {
namespace CLI {
namespace msdata {


using namespace data;


/// <summary>
/// This summarizes the different types of spectra that can be expected in the file. This is expected to aid processing software in skipping files that do not contain appropriate spectrum types for it.
/// </summary>
public ref class FileContent : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, FileContent, ParamContainer);
    public: FileContent();
};


/// <summary>
/// Description of the source file, including location and type.
/// </summary>
public ref class SourceFile : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, SourceFile, ParamContainer);

    public:

    /// <summary>
    /// an identifier for this file.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// name of the source file, without reference to location (either URI or local path).
    /// </summary>
    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// URI-formatted location where the file was retrieved.
    /// </summary>
    property System::String^ location
    {
        System::String^ get();
        void set(System::String^ value);
    }


    SourceFile();
    SourceFile(System::String^ _id);
    SourceFile(System::String^ _id, System::String^ _name);
    SourceFile(System::String^ _id, System::String^ _name, System::String^ _location);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// Structure allowing the use of a controlled (cvParam) or uncontrolled vocabulary (userParam), or a reference to a predefined set of these in this mzML file (paramGroupRef).
/// </summary>
public ref class Contact : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Contact, ParamContainer);
    public: Contact();
};


/// <summary>
/// A list of SourceFile references; implements the IList&lt;SourceFile&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SourceFileList, pwiz::msdata::SourceFilePtr, SourceFile, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of Contact references; implements the IList&lt;Contact&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ContactList, pwiz::msdata::Contact, Contact, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// Information pertaining to the entire mzML file (i.e. not specific to any part of the data set) is stored here.
/// </summary>
public ref class FileDescription
{
    DEFINE_INTERNAL_BASE_CODE(FileDescription, pwiz::msdata::FileDescription);

    public:

    /// <summary>
    /// this summarizes the different types of spectra that can be expected in the file. This is expected to aid processing software in skipping files that do not contain appropriate spectrum types for it.
    /// </summary>
    property FileContent^ fileContent
    {
        FileContent^ get();
    }

    /// <summary>
    /// list and descriptions of the source files this mzML document was generated or derived from.
    /// </summary>
    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get();
    }

    /// <summary>
    /// structure allowing the use of a controlled (cvParam) or uncontrolled vocabulary (userParam), or a reference to a predefined set of these in this mzML file (paramGroupRef)
    /// </summary>
    property ContactList^ contacts
    {
        ContactList^ get();
    }


    FileDescription();


    /// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty();
};


/// <summary>
/// Expansible description of the sample used to generate the dataset, named in sampleName.
/// </summary>
public ref class Sample : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Sample, ParamContainer);

    public:

    /// <summary>
    /// a unique identifier across the samples with which to reference this sample description.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// an optional name for the sample description, mostly intended as a quick mnemonic.
    /// </summary>
    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }


    Sample();
    Sample(System::String^ _id);
    Sample(System::String^ _id, System::String^ _name);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


public enum class ComponentType
{
    ComponentType_Unknown = -1,
    ComponentType_Source = 0,
    ComponentType_Analyzer,
    ComponentType_Detector
};


/// <summary>
/// A component of an instrument corresponding to a source (i.e. ion source), an analyzer (i.e. mass analyzer), or a detector (i.e. ion detector)
/// </summary>
public ref class Component : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Component, ParamContainer);

    public:

    /// <summary>
    /// the type of component (Source, Analyzer, or Detector)
    /// </summary>
    property ComponentType type
    {
        ComponentType get();
        void set(ComponentType value);
    }

    /// <summary>
    /// this attribute MUST be used to indicate the order in which the components are encountered from source to detector (e.g., in a Q-TOF, the quadrupole would have the lower order number, and the TOF the higher number of the two).
    /// </summary>
    property int order
    {
        int get();
        void set(int value);
    }


    Component();
    Component(ComponentType type, int order);
    Component(CVID cvid, int order);

    void define(CVID cvid, int order);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// A list of Component references; implements the IList&lt;Component&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ComponentBaseList, pwiz::msdata::Component, Component, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// List with the different components used in the mass spectrometer. At least one source, one mass analyzer and one detector need to be specified.
/// </summary>
public ref class ComponentList : public ComponentBaseList
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ComponentList, ComponentBaseList);

    public:
    ComponentList();

    /// <summary>
    /// returns the source component with ordinal &lt;index+1&gt;
    /// </summary>
    Component^ source(int index);

    /// <summary>
    /// returns the analyzer component with ordinal &lt;index+1&gt;
    /// </summary>
    Component^ analyzer(int index);

    /// <summary>
    /// returns the detector component with ordinal &lt;index+1&gt;
    /// </summary>
    Component^ detector(int index);
};


/// <summary>
/// A piece of software.
/// </summary>
public ref class Software : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Software, ParamContainer);

    public:

    /// <summary>
    /// an identifier for this software that is unique across all SoftwareTypes.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the software version.
    /// </summary>
    property System::String^ version
    {
        System::String^ get();
        void set(System::String^ value);
    }


    Software();
    Software(System::String^ _id);
    Software(System::String^ _id, CVParam^ _param, System::String^ _version);

    /// <summary>
    /// returns true iff all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// TODO
/// </summary>
public ref class Target : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Target, ParamContainer);
    public: Target();
};


/// <summary>
/// A list of Target references; implements the IList&lt;Target&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TargetList, pwiz::msdata::Target, Target, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// Description of the acquisition settings of the instrument prior to the start of the run.
/// </summary>
public ref class ScanSettings
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, ScanSettings);

    public:

    /// <summary>
    /// a unique identifier for this acquisition setting.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// container for a list of source file references.
    /// </summary>
    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get();
    }

    /// <summary>
    /// target list (or 'inclusion list') configured prior to the run.
    /// </summary>
    property TargetList^ targets
    {
        TargetList^ get();
    }


    ScanSettings();
    ScanSettings(System::String^ _id);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty();
};


/// <summary>
/// Description of a particular hardware configuration of a mass spectrometer. Each configuration MUST have one (and only one) of the three different components used for an analysis. For hybrid instruments, such as an LTQ-FT, there MUST be one configuration for each permutation of the components that is used in the document. For software configuration, reference the appropriate ScanSettings element.
/// </summary>
public ref class InstrumentConfiguration : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, InstrumentConfiguration, ParamContainer);

    public:

    /// <summary>
    /// an identifier for this instrument configuration.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// list with the different components used in the mass spectrometer. At least one source, one mass analyzer and one detector need to be specified.
    /// </summary>
    property ComponentList^ componentList
    {
        ComponentList^ get();
    }

    /// <summary>
    /// reference to a previously defined software element.
    /// </summary>
    property Software^ software
    {
        Software^ get();
    }


    InstrumentConfiguration();
    InstrumentConfiguration(System::String^ _id);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// Description of the default peak processing method. This element describes the base method used in the generation of a particular mzML file. Variable methods should be described in the appropriate acquisition section - if no acquisition-specific details are found, then this information serves as the default.
/// </summary>
public ref class ProcessingMethod : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ProcessingMethod, ParamContainer);

    public:

    /// <summary>
    /// this attributes allows a series of consecutive steps to be placed in the correct order.
    /// </summary>
    property int order
    {
        int get();
        void set(int value);
    }

    /// <summary>
    /// this attribute MUST reference the 'id' of the appropriate SoftwareType.
    /// </summary>
    property Software^ software
    {
        Software^ get();
    }

    ProcessingMethod();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// A list of ProcessingMethod references; implements the IList&lt;ProcessingMethod&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ProcessingMethodList, pwiz::msdata::ProcessingMethod, ProcessingMethod, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// Description of the way in which a particular software was used.
/// </summary>
public ref class DataProcessing
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, DataProcessing);

    public:

    /// <summary>
    /// a unique identifier for this data processing that is unique across all DataProcessingTypes.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }


    /// <summary>
    /// description of the default peak processing method(s). This element describes the base method used in the generation of a particular mzML file. Variable methods should be described in the appropriate acquisition section - if no acquisition-specific details are found, then this information serves as the default.
    /// </summary>
    property ProcessingMethodList^ processingMethods
    {
        ProcessingMethodList^ get();
    }


    DataProcessing();
    DataProcessing(System::String^ _id);

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty();
};


/// <summary>
/// TODO
/// </summary>
public ref class ScanWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ScanWindow, ParamContainer);

    public:
    ScanWindow();
    ScanWindow(double low, double high, CVID unit);
};


/// <summary>
/// A list of ScanWindow references; implements the IList&lt;ScanWindow&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ScanWindowList, pwiz::msdata::ScanWindow, ScanWindow, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// Scan or acquisition from original raw file used to create this peak list, as specified in sourceFile.
/// </summary>
public ref class Scan : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Scan, ParamContainer);

    public:

    /// <summary>
    /// for scans that are external to this document, this attribute MUST reference the 'id' attribute of a sourceFile representing that external document.
    /// <para>this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    /// <summary>
    /// for scans that are local to this document, this attribute MUST reference the 'id' attribute of the appropriate spectrum.
    /// <para>this attribute is mutually exclusive with externalSpectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property System::String^ spectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// for scans that are external to this document, this string MUST correspond to the 'id' attribute of a spectrum in the external document indicated by 'sourceFileRef'.
    /// <para>this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property System::String^ externalSpectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// this attribute MUST reference the 'id' attribute of the appropriate instrument configuration.
    /// </summary>
    property InstrumentConfiguration^ instrumentConfiguration
    {
        InstrumentConfiguration^ get();
        void set(InstrumentConfiguration^ value);
    }

    /// <summary>
    /// container for a list of select windows.
    /// </summary>
    property ScanWindowList^ scanWindows
    {
        ScanWindowList^ get();
    }



    Scan();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// A list of Scan references; implements the IList&lt;Scan&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Scans, pwiz::msdata::Scan, Scan, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// List and descriptions of acquisitions .
/// </summary>
public ref class ScanList : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ScanList, ParamContainer);

    public:
    property Scans^ scans
    {
        Scans^ get();
    }


    ScanList();

    bool empty() new;
};


/// <summary>
/// This element captures the isolation (or 'selection') window configured to isolate one or more precursors.
/// </summary>
public ref class IsolationWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, IsolationWindow, ParamContainer);
    public: IsolationWindow();
};


/// <summary>
/// TODO
/// </summary>
public ref class SelectedIon : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, SelectedIon, ParamContainer);
    public: SelectedIon();
};


/// <summary>
/// The type and energy level used for activation.
/// </summary>
public ref class Activation : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Activation, ParamContainer);
    public: Activation();
};


/// <summary>
/// A list of SelectedIon references; implements the IList&lt;ltSelectedIon&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SelectedIonList, pwiz::msdata::SelectedIon, SelectedIon, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// The method of precursor ion selection and activation
/// </summary>
public ref class Precursor : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Precursor, ParamContainer);

    public:

    /// <summary>
    /// for precursor spectra that are external to this document, this attribute MUST reference the 'id' attribute of a sourceFile representing that external document.
    /// <para>this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    /// <summary>
    /// reference to the id attribute of the spectrum from which the precursor was selected.
    /// <para>this attribute is mutually exclusive with externalSpectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property System::String^ spectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// for precursor spectra that are external to this document, this string MUST correspond to the 'id' attribute of a spectrum in the external document indicated by 'sourceFileRef'.
    /// <para>this attribute is mutually exclusive with spectrumID; i.e. use one or the other but not both</para>
    /// </summary>
    property System::String^ externalSpectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// this element captures the isolation (or 'selection') window configured to isolate one or more precursors.
    /// </summary>
    property IsolationWindow^ isolationWindow
    {
        IsolationWindow^ get();
        void set(IsolationWindow^ value);
    }

    /// <summary>
    /// this list of precursor ions that were selected.
    /// </summary>
    property SelectedIonList^ selectedIons
    {
        SelectedIonList^ get();
    }

    /// <summary>
    /// the type and energy level used for activation.
    /// </summary>
    property Activation^ activation
    {
        Activation^ get();
        void set(Activation^ value);
    }


    Precursor();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// A list of Precursor references; implements the IList&lt;Precursor&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(PrecursorList, pwiz::msdata::Precursor, Precursor, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Product
{
    DEFINE_INTERNAL_BASE_CODE(Product, pwiz::msdata::Product);

    public:

    /// <summary>
    /// this element captures the isolation (or 'selection') window configured to isolate one or more products.
    /// </summary>
    property IsolationWindow^ isolationWindow
    {
        IsolationWindow^ get();
        void set(IsolationWindow^ value);
    }

    Product();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty();
};


/// <summary>
/// A list of Product references; implements the IList&lt;Product&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ProductList, pwiz::msdata::Product, Product, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// A list of doubles; implements the IList&lt;double&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(BinaryData, double, double, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE);


/// <summary>
/// The structure into which encoded binary data goes. Byte ordering is always little endian (Intel style). Computers using a different endian style MUST convert to/from little endian when writing/reading mzML
/// </summary>
public ref class BinaryDataArray : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, BinaryDataArray, ParamContainer);

    public:

    /// <summary>
    /// this optional attribute may reference the 'id' attribute of the appropriate dataProcessing.
    /// </summary>
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        void set(DataProcessing^ value);
    }

    /// <summary>
    /// the binary data.
    /// </summary>
    property BinaryData^ data
    {
        BinaryData^ get();
        void set(BinaryData^ value);
    }


    BinaryDataArray();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;
};


/// <summary>
/// The data point type of a mass spectrum.
/// </summary>
public ref class MZIntensityPair
{
    DEFINE_INTERNAL_BASE_CODE(MZIntensityPair, pwiz::msdata::MZIntensityPair);

    public:
    property double mz
    {
        double get();
        void set(double value);
    }

    property double intensity
    {
        double get();
        void set(double value);
    }


    MZIntensityPair();
    MZIntensityPair(double mz, double intensity);
};


/// <summary>
/// The data point type of a chromatogram.
/// </summary>
public ref class TimeIntensityPair
{
    DEFINE_INTERNAL_BASE_CODE(TimeIntensityPair, pwiz::msdata::TimeIntensityPair);

    public:
    property double time
    {
        double get();
        void set(double value);
    }

    property double intensity
    {
        double get();
        void set(double value);
    }


    TimeIntensityPair();
    TimeIntensityPair(double time, double intensity);
};


/// <summary>
/// Identifying information for a spectrum
/// </summary>
public ref class SpectrumIdentity
{
    DEFINE_INTERNAL_BASE_CODE(SpectrumIdentity, pwiz::msdata::SpectrumIdentity);

    public:

    /// <summary>
    /// the zero-based, consecutive index of the spectrum in the SpectrumList.
    /// </summary>
    property int index
    {
        int get();
        void set(int value);
    }

    /// <summary>
    /// a unique identifier for this spectrum. It should be expected that external files may use this identifier together with the mzML filename or accession to reference a particular spectrum.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }


    /// <summary>
    /// the identifier for the spot from which this spectrum was derived, if a MALDI or similar run.
    /// </summary>
    property System::String^ spotID
    {
        System::String^ get();
        void set(System::String^ value);
    }

	/// <summary>
	/// for file-based MSData implementations, this attribute may refer to the spectrum's position in the file
	/// </summary>
	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    SpectrumIdentity();
};


public ref class ChromatogramIdentity
{
    DEFINE_INTERNAL_BASE_CODE(ChromatogramIdentity, pwiz::msdata::ChromatogramIdentity);

    public:

    /// <summary>
    /// the zero-based, consecutive index of the chromatogram in the ChromatogramList.
    /// </summary>
    property int index
    {
        int get();
        void set(int value);
    }

    /// <summary>
    /// a unique identifier for this chromatogram. It should be expected that external files may use this identifier together with the mzML filename or accession to reference a particular chromatogram.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

	/// <summary>
	/// for file-based MSData implementations, this attribute may refer to the chromatogram's position in the file
	/// </summary>
	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    ChromatogramIdentity();
};


/// <summary>
/// A list of BinaryDataArray references; implements the IList&lt;BinaryDataArray&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(BinaryDataArrayList, pwiz::msdata::BinaryDataArrayPtr, BinaryDataArray, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of MZIntensityPair references; implements the IList&lt;MZIntensityPair&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MZIntensityPairList, pwiz::msdata::MZIntensityPair, MZIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// A list of TimeIntensityPair references; implements the IList&lt;TimeIntensityPair&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TimeIntensityPairList, pwiz::msdata::TimeIntensityPair, TimeIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// The structure that captures the generation of a peak list (including the underlying scans)
/// </summary>
public ref class Spectrum : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Spectrum, ParamContainer);

    public:

    // SpectrumIdentity

    /// <summary>
    /// the zero-based, consecutive index of the spectrum in the SpectrumList.
    /// </summary>
    property int index
    {
        int get();
        void set(int value);
    }

    /// <summary>
    /// the native identifier for the spectrum, used by the acquisition software.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the identifier for the spot from which this spectrum was derived, if a MALDI or similar run.
    /// </summary>
    property System::String^ spotID
    {
        System::String^ get();
        void set(System::String^ value);
    }

	/// <summary>
	/// for file-based MSData implementations, this attribute may refer to the spectrum's position in the file
	/// </summary>
	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    // Spectrum
    /// <summary>
    /// default length of binary data arrays contained in this element.
    /// </summary>
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }

    /// <summary>
    /// this attribute can optionally reference the 'id' of the appropriate dataProcessing.
    /// </summary>
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        void set(DataProcessing^ value);
    }

    /// <summary>
    /// this attribute can optionally reference the 'id' of the appropriate sourceFile.
    /// </summary>
    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    /// <summary>
    /// list and descriptions of scans.
    /// </summary>
    property ScanList^ scanList
    {
        ScanList^ get();
    }

    /// <summary>
    /// list and descriptions of precursors to the spectrum currently being described.
    /// </summary>
    property PrecursorList^ precursors
    {
        PrecursorList^ get();
    }

    /// <summary>
    /// list and descriptions of products of the spectrum currently being described.
    /// </summary>
    property ProductList^ products
    {
        ProductList^ get();
    }

    /// <summary>
    /// list of binary data arrays.
    /// </summary>
    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get();
        void set(BinaryDataArrayList^ value);
    }


    Spectrum();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;

    /// <summary>
    /// copy binary data arrays into m/z-intensity pair array
    /// </summary>
    void getMZIntensityPairs(MZIntensityPairList^% output);

    /// <summary>
    /// get m/z array (may be null)
    /// </summary>
    BinaryDataArray^ getMZArray();

    /// <summary>
    /// get intensity array (may be null)
    /// </summary>
    BinaryDataArray^ getIntensityArray();

    /// <summary>
    /// get array with specified CVParam (may be null)
    /// </summary>
    BinaryDataArray^ getArrayByCVID(CVID arrayType);

    /// <summary>
    /// set binary data arrays
    /// </summary>
    void setMZIntensityPairs(MZIntensityPairList^ input);

    /// <summary>
    /// set binary data arrays
    /// </summary>
    void setMZIntensityPairs(MZIntensityPairList^ input, CVID intensityUnits);

    /// <summary>
    /// set m/z and intensity arrays separately (they must be the same size)
    /// </summary>
    void setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                              System::Collections::Generic::List<double>^ intensityArray);

    /// <summary>
    /// set m/z and intensity arrays separately (they must be the same size)
    /// </summary>
    void setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                              System::Collections::Generic::List<double>^ intensityArray,
                              CVID intensityUnits);
};


/// <summary>
/// A single chromatogram.
/// </summary>
public ref class Chromatogram : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Chromatogram, ParamContainer);

    public:

    // ChromatogramIdentity
    /// <summary>
    /// the zero-based, consecutive index of the chromatogram in the ChromatogramList.
    /// </summary>
    property int index
    {
        int get();
        void set(int value);
    }

    /// <summary>
    /// the native identifier for the chromatogram, used by the acquisition software.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

	/// <summary>
	/// for file-based MSData implementations, this attribute may refer to the chromatogram's position in the file
	/// </summary>
	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    // Chromatogram
    /// <summary>
    /// default length of binary data arrays contained in this element.
    /// </summary>
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }

    /// <summary>
    /// this attribute can optionally reference the 'id' of the appropriate dataProcessing.
    /// </summary>
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        //void set(DataProcessing^ value);
    }

    /// <summary>
    /// TODO: Precursor
    /// </summary>
    property Precursor^ precursor
    {
        Precursor^ get();
        void set(Precursor^ value);
    }

    /// <summary>
    /// TODO: Product
    /// </summary>
    property Product^ product
    {
        Product^ get();
        void set(Product^ value);
    }

    /// <summary>
    /// list of binary data arrays.
    /// </summary>
    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get();
        void set(BinaryDataArrayList^ value);
    }


    Chromatogram();

    /// <summary>
    /// returns true iff the element contains no params and all members are empty or null
    /// </summary>
    bool empty() new;

    /// <summary>
    /// copy binary data arrays into time-intensity pair array
    /// </summary>
    void getTimeIntensityPairs(TimeIntensityPairList^% output);

    /// <summary>
    /// set binary data arrays
    /// </summary>
    void setTimeIntensityPairs(TimeIntensityPairList^ input, CVID timeUnits, CVID intensityUnits);
};


/// <summary>
/// A list of spectrum or chromatogram indexes; implements the IList&lt;int&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(IndexList, size_t, int, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE);

/// <summary>
/// Detail level to use in populating spectrum returned from a specturm list.
/// Precursor ion and neutral loss/gain scans not yet supported for ms level.
/// </summary>
public enum class DetailLevel
{
	InstantMetadata,
    FastMetadata,
    FullMetadata,
    FullData
};

/// <summary>
/// Interface for accessing spectra, which may be stored in memory
/// or backed by a data file (RAW, mzXML, mzML).
/// <para>- Implementations are expected to keep a spectrum index in the form of
///   List&lt;SpectrumIdentity&gt; or equivalent. The default find*() functions search
///   the index linearly. Implementations may provide constant time indexing.</para>
/// <para/>
/// <para>- The semantics of spectrum() may vary slightly with implementation.  In particular,
///   a SpectrumList implementation that is backed by a file may choose either to cache
///   or discard the SpectrumPtrs for future access, with the caveat that the client
///   may write to the underlying data.</para>
/// <para/>
/// <para>- It is the implementation's responsibility to return a valid Spectrum^ from spectrum().
///   If this cannot be done, an exception must be thrown.</para>
/// <para/>
/// <para>- The 'getBinaryData' flag is a hint if false: implementations may provide valid
///   BinaryDataArrayPtrs on spectrum(index, false); implementations *must* provide
///   valid BinaryDataArrayPtrs on spectrum(index, true).</para>
/// </summary>
public ref class SpectrumList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, SpectrumList);

    public:

    /// <summary>
    /// returns the number of spectra
    /// </summary>
    virtual int size();

    /// <summary>
    /// returns true iff (size() == 0)
    /// </summary>
    virtual bool empty();

    /// <summary>
    /// access to a spectrum index
    /// </summary>
    virtual SpectrumIdentity^ spectrumIdentity(int index);

    /// <summary>
    /// find id in the spectrum index (returns size() on failure)
    /// </summary>
    virtual int find(System::String^ id);

    /// <summary>
    /// find an abbreviated id (e.g. "1.1.123.2" for "sample=1 period=1 cycle=123 experiment=2") in the spectrum index (returns size() on failure), using the default '.' delimiter
    /// </summary>
    virtual int findAbbreviated(System::String^ abbreviatedId);

    /// <summary>
    /// find an abbreviated id (e.g. "1.1.123.2" for "sample=1 period=1 cycle=123 experiment=2") in the spectrum index (returns size() on failure)
    /// </summary>
    virtual int findAbbreviated(System::String^ abbreviatedId, char delimiter);

    /// <summary>
    /// find all spectrum indexes with specified name/value pair
    /// </summary>
    virtual IndexList^ findNameValue(System::String^ name, System::String^ value);

    /// <summary>
    /// retrieve a spectrum by index without binary data
    /// <para>- client may assume the underlying Spectrum^ is valid</para>
    /// </summary>
    virtual Spectrum^ spectrum(int index);

    /// <summary>
    /// retrieve a spectrum by index
    /// <para>- binary data arrays will be provided if (getBinaryData == true)</para>
    /// <para>- client may assume the underlying Spectrum^ is valid</para>
    /// </summary>
    virtual Spectrum^ spectrum(int index, bool getBinaryData);

    /// <summary>
    /// retrieve a spectrum by index
    /// <para>- binary data arrays will be provided if (getBinaryData == true)</para>
    /// <para>- client may assume the underlying Spectrum^ is valid</para>
    /// </summary>
    virtual Spectrum^ spectrum(int index, DetailLevel detailLevel);

    /// <summary>
    /// returns the data processing affecting spectra retrieved through this interface
    /// <para>- may return a null shared pointer</para>
    /// </summary>
    virtual DataProcessing^ dataProcessing();

    /// <summary>
    /// set DataProcessing
    /// </summary>
    virtual void setDataProcessing(DataProcessing^ dp);
};



/// <summary>
/// A list of Spectrum references; implements the IList&lt;Spectrum&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Spectra, pwiz::msdata::SpectrumPtr, Spectrum, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// Simple writeable in-memory implementation of SpectrumList.
/// <para>- spectrum() returns internal Spectrum references.</para>
/// </summary>
public ref class SpectrumListSimple : public SpectrumList
{
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::msdata, SpectrumListSimple, SpectrumList);

    public:

    property Spectra^ spectra
    {
        Spectra^ get();
        void set(Spectra^ value);
    }


    SpectrumListSimple();

    // SpectrumList implementation

    virtual int size() override;
    virtual bool empty() override;
    virtual SpectrumIdentity^ spectrumIdentity(int index) override;
    virtual Spectrum^ spectrum(int index) override;
    virtual Spectrum^ spectrum(int index, bool getBinaryData) override;
};


/// <summary>
/// Interface for accessing chromatograms, which may be stored in memory
/// or backed by a data file (RAW, mzXML, mzML).
/// <para>- Implementations are expected to keep a chromatogram index in the form of
///   List&lt;ChromatogramIdentity&gt; or equivalent. The default find*() functions search
///   the index linearly. Implementations may provide constant time indexing.</para>
/// <para/>
/// <para>- The semantics of chromatogram() may vary slightly with implementation.  In particular,
///   a ChromatogramList implementation that is backed by a file may choose either to cache
///   or discard the Chromatogram for future access, with the caveat that the client
///   may write to the underlying data.</para>
/// <para/>
/// <para>- It is the implementation's responsibility to return a valid Chromatogram from chromatogram().
///   If this cannot be done, an exception must be thrown.</para>
/// <para/>
/// <para>- The 'getBinaryData' flag is a hint if false: implementations may provide valid
///   BinaryDataArrayPtrs on chromatogram(index, false); implementations *must* provide
///   valid BinaryDataArrayPtrs on chromatogram(index, true).</para>
/// </summary>
public ref class ChromatogramList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, ChromatogramList);

    public:

    /// <summary>
    /// returns the number of chromatograms
    /// </summary>
    virtual int size();

    /// <summary>
    /// returns true iff (size() == 0)
    /// </summary>
    virtual bool empty();

    /// <summary>
    /// access to a chromatogram index
    /// </summary>
    virtual ChromatogramIdentity^ chromatogramIdentity(int index);

    /// <summary>
    /// find id in the chromatogram index (returns size() on failure)
    /// </summary>
    virtual int find(System::String^ id);

    /// <summary>
    /// retrieve a chromatogram by index without binary data
    /// <para>- client may assume the underlying Chromatogram^ is valid</para>
    /// </summary>
    virtual Chromatogram^ chromatogram(int index);

    /// <summary>
    /// retrieve a chromatogram by index
    /// <para>- binary data arrays will be provided if (getBinaryData == true)</para>
    /// <para>- client may assume the underlying Chromatogram^ is valid</para>
    /// </summary>
    virtual Chromatogram^ chromatogram(int index, bool getBinaryData);

    /// <summary>
    /// returns the data processing affecting chromatograms retrieved through this interface
    /// <para>- may return a null shared pointer</para>
    /// </summary>
    virtual DataProcessing^ dataProcessing();

    /// <summary>
    /// set DataProcessing
    /// </summary>
    virtual void setDataProcessing(DataProcessing^ dp);
};



/// <summary>
/// A list of Chromatogram references; implements the IList&lt;Chromatogram&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Chromatograms, pwiz::msdata::ChromatogramPtr, Chromatogram, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// Simple writeable in-memory implementation of ChromatogramList.
/// <para>- note: chromatogram() returns internal Chromatogram references.</para>
/// </summary>
public ref class ChromatogramListSimple : public ChromatogramList
{
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(pwiz::msdata, ChromatogramListSimple, ChromatogramList);

    public:
    property Chromatograms^ chromatograms
    {
        Chromatograms^ get();
        void set(Chromatograms^ value);
    }


    ChromatogramListSimple();

    // ChromatogramList implementation

    virtual int size() override;
    virtual bool empty() override;
    virtual ChromatogramIdentity^ chromatogramIdentity(int index) override;
    virtual Chromatogram^ chromatogram(int index) override;
    virtual Chromatogram^ chromatogram(int index, bool getBinaryData) override;
};


/// <summary>
/// A run in mzML should correspond to a single, consecutive and coherent set of scans on an instrument.
/// </summary>
public ref class Run : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Run, ParamContainer);

    public:

    /// <summary>
    /// a unique identifier for this run.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// this attribute MUST reference the 'id' of the default instrument configuration. If a scan does not reference an instrument configuration, it implicitly refers to this configuration.
    /// </summary>
    property InstrumentConfiguration^ defaultInstrumentConfiguration
    {
        InstrumentConfiguration^ get();
        void set(InstrumentConfiguration^ value);
    }

    /// <summary>
    /// this attribute MUST reference the 'id' of the appropriate sample.
    /// </summary>
    property Sample^ sample
    {
        Sample^ get();
        void set(Sample^ value);
    }

    /// <summary>
    /// the optional start timestamp of the run, in UT.
    /// </summary>
    property System::String^ startTimeStamp
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// default source file reference.
    /// </summary>
    property SourceFile^ defaultSourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    /// <summary>
    /// all mass spectra and the acquisitions underlying them are described and attached here. Subsidiary data arrays are also both described and attached here.
    /// </summary>
    property SpectrumList^ spectrumList
    {
        SpectrumList^ get();
        void set(SpectrumList^ value);
    }

    /// <summary>
    /// all chromatograms for this run.
    /// </summary>
    property ChromatogramList^ chromatogramList
    {
        ChromatogramList^ get();
        void set(ChromatogramList^ value);
    }


    Run();
    bool empty() new;

    internal:
    // no copying - any implementation must handle:
    // - SpectrumList cloning
    // - internal cross-references to heap-allocated objects
    //Run(Run&);
    //Run& operator=(Run&);
};


/// <summary>
/// A list of Sample references; implements the IList&lt;Sample&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SampleList, pwiz::msdata::SamplePtr, Sample, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of InstrumentConfiguration references; implements the IList&lt;InstrumentConfiguration&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(InstrumentConfigurationList, pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of Software references; implements the IList&lt;Software&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SoftwareList, pwiz::msdata::SoftwarePtr, Software, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of DataProcessing references; implements the IList&lt;DataProcessing&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(DataProcessingList, pwiz::msdata::DataProcessingPtr, DataProcessing, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of ScanSettings references; implements the IList&lt;ScanSettings&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ScanSettingsList, pwiz::msdata::ScanSettingsPtr, ScanSettings, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// This is the root element of ProteoWizard; it represents the mzML element, defined as:
/// intended to capture the use of a mass spectrometer, the data generated, and the initial processing of that data (to the level of the peak list).
/// </summary>
public ref class MSData
{
    INTERNAL:
    MSData(boost::shared_ptr<pwiz::msdata::MSData>* base);
    virtual ~MSData();
    !MSData();
    boost::shared_ptr<pwiz::msdata::MSData>* base_;
    pwiz::msdata::MSData& base();

    public:

    /// <summary>
    /// an optional accession number for the mzML document.
    /// </summary>
    property System::String^ accession
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// an optional id for the mzML document. It is recommended to use LSIDs when possible.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// container for one or more controlled vocabulary definitions.
    /// <para>- note: one of the &lt;cv&gt; elements in this list MUST be the PSI MS controlled vocabulary. All &lt;cvParam&gt; elements in the document MUST refer to one of the &lt;cv&gt; elements in this list.</para>
    /// </summary>
    property CVList^ cvs
    {
        CVList^ get();
        void set(CVList^ value);
    }

    /// <summary>
    /// information pertaining to the entire mzML file (i.e. not specific to any part of the data set) is stored here.
    /// </summary>
    property FileDescription^ fileDescription
    {
        FileDescription^ get();
        void set(FileDescription^ value);
    }

    /// <summary>
    /// container for a list of referenceableParamGroups
    /// </summary>
    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get();
        void set(ParamGroupList^ value);
    }

    /// <summary>
    /// list and descriptions of samples.
    /// </summary>
    property SampleList^ samples
    {
        SampleList^ get();
        void set(SampleList^ value);
    }

    /// <summary>
    /// list and descriptions of instrument configurations.
    /// </summary>
    property InstrumentConfigurationList^ instrumentConfigurationList
    {
        InstrumentConfigurationList^ get();
        void set(InstrumentConfigurationList^ value);
    }

    /// <summary>
    /// list and descriptions of software used to acquire and/or process the data in this mzML file.
    /// </summary>
    property SoftwareList^ softwareList
    {
        SoftwareList^ get();
        void set(SoftwareList^ value);
    }

    /// <summary>
    /// list and descriptions of data processing applied to this data.
    /// </summary>
    property DataProcessingList^ dataProcessingList
    {
        DataProcessingList^ get();
        void set(DataProcessingList^ value);
    }

    /// <summary>
    /// list with the descriptions of the acquisition settings applied prior to the start of data acquisition.
    /// </summary>
    property ScanSettingsList^ scanSettingsList
    {
        ScanSettingsList^ get();
        void set(ScanSettingsList^ value);
    }

    /// <summary>
    /// a run in mzML should correspond to a single, consecutive and coherent set of scans on an instrument.
    /// </summary>
    property Run^ run
    {
        Run^ get();
        //void set(Run^ value);
    }


    MSData();

    bool empty();

    /// <summary>
    /// returns the version of this mzML document;
    /// for a document created programmatically, the version is the current release version of mzML;
    /// for a document created from a file/stream, the version is the schema version read from the file/stream
    /// </summary>
    System::String^ version();


    internal:
    // no copying
    //MSData(MSData&);
    //MSData& operator=(MSData&);
};


public ref class id {

// parses an id string into a map<string,string>
// TODO: std::map<std::string,std::string> parse(const std::string& id);

/// <summary>
/// convenience function to extract a named value from an id string
/// </summary>
public: static System::String^ value(System::String^ id, System::String^ name);

/// <summary>
/// returns the nativeID format from the defaultSourceFilePtr if set,
/// or from sourceFilePtrs[0] if the list isn't empty,
/// or CVID_Unknown
/// </summary>
public: static CVID getDefaultNativeIDFormat(MSData^ msd);

/// <summary>
/// translates a "scan number" to a string that is correct for the given nativeID format;
/// semantic validity requires that scanNumber be parseable as an integer;
/// some nativeID formats cannot be translated to and will always return an empty string
/// currently supported formats: Thermo, Bruker/Agilent YEP, Bruker BAF, mzXML, MGF, and mzData
/// </summary>
public: static System::String^ translateScanNumberToNativeID(CVID nativeIDFormat, System::String^ scanNumber);

/// <summary>
/// translates a nativeID in the given nativeID format to a simple integer "scan number";
/// some nativeID formats cannot be translated from and will always return an empty string
/// currently supported formats: Thermo, Bruker/Agilent YEP, Bruker BAF, mzXML, MGF, and mzData
/// </summary>
public: static System::String^ translateNativeIDToScanNumber(CVID nativeIDFormat, System::String^ id);

/// <summary>
/// abbreviates a nativeID ("name1=value1 name2=value2" translates to "value1.value2")
/// </summary>
public: static System::String^ abbreviate(System::String^ id);

/// <summary>
/// abbreviates a nativeID ("name1=value1 name2=value2" translates to "value1.value2")
/// </summary>
public: static System::String^ abbreviate(System::String^ id, char delimiter);

};


} // namespace msdata
} // namespace CLI
} // namespace pwiz

#endif // _MSDATA_HPP_CLI_
