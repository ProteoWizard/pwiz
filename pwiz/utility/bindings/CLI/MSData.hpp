//
// MSData.hpp
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


#include "CVParam.hpp"
#include "../../../data/msdata/MSData.hpp"


namespace pwiz {
namespace CLI {
namespace msdata {


public ref class CV
{
    DEFINE_INTERNAL_BASE_CODE(CV, pwiz::msdata::CV);
             
    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ URI
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ fullName
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ version
    {
        System::String^ get();
        void set(System::String^ value);
    }


    CV();

    bool empty();
};


public ref class UserParamValue
{
    internal: UserParamValue(boost::shared_ptr<pwiz::msdata::UserParam>* base) : base_(new boost::shared_ptr<pwiz::msdata::UserParam>(*base)) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParamValue))}
              virtual ~UserParamValue() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(UserParamValue)) SAFEDELETE(base_);}
              !UserParamValue() {delete this;}
              boost::shared_ptr<pwiz::msdata::UserParam>* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(UserParamValue^ value) {return gcnew System::String((*value->base_)->value.c_str());};
    static explicit operator float(UserParamValue^ value) {return (*value->base_)->valueAs<float>();}
    static operator double(UserParamValue^ value) {return (*value->base_)->valueAs<double>();}
    static explicit operator int(UserParamValue^ value) {return (*value->base_)->valueAs<int>();}
    static explicit operator System::UInt64(UserParamValue^ value) {return (System::UInt64) (*value->base_)->valueAs<size_t>();}
    static explicit operator bool(UserParamValue^ value) {return (*value->base_)->value == "true";}
    UserParamValue^ operator=(System::String^ value) {(*base_)->value = ToStdString(value); return this;} 
};


public ref class UserParam
{
    internal: UserParam(pwiz::msdata::UserParam* base, System::Object^ owner) : base_(new boost::shared_ptr<pwiz::msdata::UserParam>(base)), owner_(owner), value_(gcnew UserParamValue(base_)) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParam))}
              UserParam(pwiz::msdata::UserParam* base) : base_(new boost::shared_ptr<pwiz::msdata::UserParam>(base)), owner_(nullptr), value_(gcnew UserParamValue(base_)) {LOG_CONSTRUCT(BOOST_PP_STRINGIZE(UserParam))}
              virtual ~UserParam() {LOG_DESTRUCT(BOOST_PP_STRINGIZE(UserParam)) if (owner_ == nullptr) SAFEDELETE(base_);}
              !UserParam() {delete this;}
              boost::shared_ptr<pwiz::msdata::UserParam>* base_;
              System::Object^ owner_;
              UserParamValue^ value_;

    public:
    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property UserParamValue^ value
    {
        UserParamValue^ get();
    }

    property System::String^ type
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property CVID units
    {
        CVID get();
        void set(CVID value);
    }

    UserParam();
    UserParam(System::String^ _name);
    UserParam(System::String^ _name, System::String^ _value);
    UserParam(System::String^ _name, System::String^ _value, System::String^ _type);
    UserParam(System::String^ _name, System::String^ _value, System::String^ _type, CVID _units);

    bool empty();
    bool operator==(UserParam^ that) {return (*base_) == *that->base_;}
    bool operator!=(UserParam^ that) {return (*base_) != *that->base_;}
};


ref class ParamGroupList;
ref class CVParamList;
ref class UserParamList;


public ref class ParamContainer
{
    internal: ParamContainer(pwiz::msdata::ParamContainer* base) : base_(base) {}
              virtual ~ParamContainer() {/*LOG_DESTRUCT(BOOST_PP_STRINGIZE(ParamContainer)) SAFEDELETE(base_);*/}
              pwiz::msdata::ParamContainer* base_;

    public:
    ParamContainer() : base_(new pwiz::msdata::ParamContainer()) {}

    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get();
    }

    property CVParamList^ cvParams
    {
        CVParamList^ get();
    }

    property UserParamList^ userParams
    {
        UserParamList^ get();
    }

    
    /// Finds pwiz::msdata::CVID in the container:
    /// - returns first CVParam result such that (result.pwiz::msdata::CVID == pwiz::msdata::CVID); 
    /// - if not found, returns CVParam(pwiz::msdata::CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam^ cvParam(CVID cvid);

    /// Finds child of pwiz::msdata::CVID in the container:
    /// - returns first CVParam result such that (result.pwiz::msdata::CVID is_a pwiz::msdata::CVID); 
    /// - if not found, CVParam(pwiz::msdata::CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam^ cvParamChild(CVID cvid);

    /// returns true iff cvParams contains exact pwiz::msdata::CVID (recursive)
    bool hasCVParam(CVID cvid);

    /// returns true iff cvParams contains a child (is_a) of pwiz::msdata::CVID (recursive)
    bool hasCVParamChild(CVID cvid);

    /// Finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam^ userParam(System::String^ name);

    /// set/add a CVParam (not recursive)
    void set(CVID cvid);
    void set(CVID cvid, System::String^ value);
    void set(CVID cvid, System::String^ value, CVID units);

    void set(CVID cvid, bool value);

    /// set/add a CVParam (not recursive)
    void set(CVID cvid, System::Int32 value);
    void set(CVID cvid, System::Int64 value);
    void set(CVID cvid, System::UInt32 value);
    void set(CVID cvid, System::UInt64 value);
    void set(CVID cvid, System::Single value);
    void set(CVID cvid, System::Double value);

    void set(CVID cvid, System::Int32 value, CVID units);
    void set(CVID cvid, System::Int64 value, CVID units);
    void set(CVID cvid, System::UInt32 value, CVID units);
    void set(CVID cvid, System::UInt64 value, CVID units);
    void set(CVID cvid, System::Single value, CVID units);
    void set(CVID cvid, System::Double value, CVID units);

    bool empty();
};


public ref class ParamGroup : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ParamGroup, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }


    ParamGroup();
    ParamGroup(System::String^ _id);

    bool empty() new;
};


// Preprocessed version for debugging
/*public ref class ParamGroupList : public System::Collections::Generic::IList<ParamGroup^> \
{ \
    internal: ParamGroupList(std::vector<pwiz::msdata::ParamGroupPtr>* base) : base_(base) {} \
              virtual ~ParamGroupList() {if (base_) delete base_;} \
              std::vector<pwiz::msdata::ParamGroupPtr>* base_; \
    \
    public: ParamGroupList() : base_(new std::vector<pwiz::msdata::ParamGroupPtr>()) {} \
    \
    public: \
    System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
    void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    \
    property ParamGroup^ Item[int] \
    { \
        virtual ParamGroup^ get(int index) {return NATIVE_SHARED_PTR_TO_CLI(ParamGroup, base_->at((size_t) index));} \
        virtual void set(int index, ParamGroup^ value) {} \
    } \
    \
    virtual void Add(ParamGroup^ item) {base_->push_back(CLI_TO_NATIVE_SHARED_PTR(pwiz::msdata::ParamGroupPtr, item));} \
    virtual void Clear() {base_->clear();} \
    virtual bool Contains(ParamGroup^ item) {return std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::msdata::ParamGroupPtr, item)) != base_->end();} \
    virtual void CopyTo(array<ParamGroup^>^ arrayTarget, int arrayIndex) {} \
    virtual bool Remove(ParamGroup^ item) {std::vector<pwiz::msdata::ParamGroupPtr>::iterator itr = std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::msdata::ParamGroupPtr, item)); if(itr == base_->end()) return false; base_->erase(itr); return true;} \
    virtual int IndexOf(ParamGroup^ item) {return (int) (std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::msdata::ParamGroupPtr, item))-base_->begin());} \
    virtual void Insert(int index, ParamGroup^ item) {base_->insert(base_->begin() + index, CLI_TO_NATIVE_SHARED_PTR(pwiz::msdata::ParamGroupPtr, item));} \
    virtual void RemoveAt(int index) {base_->erase(base_->begin() + index);} \
    \
    ref class Enumerator : System::Collections::Generic::IEnumerator<ParamGroup^> \
    { \
        public: Enumerator(std::vector<pwiz::msdata::ParamGroupPtr>* base) : base_(base) {} \
        internal: std::vector<pwiz::msdata::ParamGroupPtr>* base_; \
        internal: std::vector<pwiz::msdata::ParamGroupPtr>::iterator* itr_; \
        \
        public: \
        property ParamGroup^ Current { virtual ParamGroup^ get(); } \
        property System::Object^ Current2 { virtual System::Object^ get() sealed = System::Collections::IEnumerator::Current::get {return (System::Object^) NATIVE_SHARED_PTR_TO_CLI(ParamGroup, **itr_);} } \
        virtual bool MoveNext() \
        { \
            if (*itr_ == base_->end()) return false; \
            else ++*itr_; return true; \
        } \
        virtual void Reset() {*itr_ = base_->begin();} \
        ~Enumerator() {} \
    }; \
    virtual System::Collections::Generic::IEnumerator<ParamGroup^>^ GetEnumerator() {return gcnew Enumerator(base_);} \
    virtual System::Collections::IEnumerator^ GetEnumerator2() sealed = System::Collections::IEnumerable::GetEnumerator {return gcnew Enumerator(base_);} \
};*/


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ParamGroupList, pwiz::msdata::ParamGroupPtr, ParamGroup, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CVParamList, pwiz::msdata::CVParam, CVParam, NATIVE_REFERENCE_TO_CLI, CLI_SHARED_PTR_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(UserParamList, pwiz::msdata::UserParam, UserParam, NATIVE_REFERENCE_TO_CLI, CLI_SHARED_PTR_TO_NATIVE_REFERENCE);


public ref class FileContent : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, FileContent, ParamContainer);
    public: FileContent();
};


public ref class SourceFile : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, SourceFile, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ location
    {
        System::String^ get();
        void set(System::String^ value);
    }


    SourceFile();
    SourceFile(System::String^ _id);
    SourceFile(System::String^ _id, System::String^ _name);
    SourceFile(System::String^ _id, System::String^ _name, System::String^ _location);

    bool empty() new;
};


public ref class Contact : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Contact, ParamContainer);
    public: Contact();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SourceFileList, pwiz::msdata::SourceFilePtr, SourceFile, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ContactList, pwiz::msdata::Contact, Contact, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class FileDescription
{
    DEFINE_INTERNAL_BASE_CODE(FileDescription, pwiz::msdata::FileDescription);

    public:
    property FileContent^ fileContent
    {
        FileContent^ get();
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get();
    }

    property ContactList^ contacts
    {
        ContactList^ get();
    }


    FileDescription();


    bool empty();
};


public ref class Sample : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Sample, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }


    Sample();
    Sample(System::String^ _id);
    Sample(System::String^ _id, System::String^ _name);

    bool empty() new;
};


public enum class ComponentType
{
    ComponentType_Unknown = -1,
    ComponentType_Source = 0,
    ComponentType_Analyzer,
    ComponentType_Detector
};


public ref class Component : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Component, ParamContainer);

    public:
    property ComponentType type
    {
        ComponentType get();
        void set(ComponentType value);
    }

    property int order
    {
        int get();
        void set(int value);
    }


    Component();
    Component(ComponentType type, int order);
    Component(CVID cvid, int order);

    void define(CVID cvid, int order);
    bool empty() new;
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ComponentBaseList, pwiz::msdata::Component, Component, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class ComponentList : public ComponentBaseList
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ComponentList, ComponentBaseList);

    public:
    ComponentList();

    Component^ source(int index);
    Component^ analyzer(int index);
    Component^ detector(int index);
};


public ref class Software
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, Software);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property CVParam^ softwareParam
    {
        CVParam^ get();
        void set(CVParam^ value);
    }

    property System::String^ softwareParamVersion
    {
        System::String^ get();
        void set(System::String^ value);
    }


    Software();
    Software(System::String^ _id);
    Software(System::String^ _id, CVParam^ _softwareParam, System::String^ _softwareParamVersion);

    bool empty();
};


public ref class InstrumentConfiguration : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, InstrumentConfiguration, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property ComponentList^ componentList
    {
        ComponentList^ get();
    }

    property Software^ software
    {
        Software^ get();
    }


    InstrumentConfiguration();
    InstrumentConfiguration(System::String^ _id);

    bool empty() new;
};


public ref class ProcessingMethod : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ProcessingMethod, ParamContainer);

    public:
    property int order
    {
        int get();
        void set(int value);
    }


    ProcessingMethod();

    bool empty() new;
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ProcessingMethodList, pwiz::msdata::ProcessingMethod, ProcessingMethod, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class DataProcessing
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, DataProcessing);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property Software^ software
    {
        Software^ get();
    }

    property ProcessingMethodList^ processingMethods
    {
        ProcessingMethodList^ get();
    }


    DataProcessing();
    DataProcessing(System::String^ _id);

    bool empty();
};


public ref class Target : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Target, ParamContainer);
    public: Target();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TargetList, pwiz::msdata::Target, Target, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class AcquisitionSettings
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, AcquisitionSettings);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property InstrumentConfiguration^ instrumentConfiguration
    {
        InstrumentConfiguration^ get();
        void set(InstrumentConfiguration^ value);
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get();
    }

    property TargetList^ targets
    {
        TargetList^ get();
    }


    AcquisitionSettings();
    AcquisitionSettings(System::String^ _id);

    bool empty();
};


public ref class Acquisition : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Acquisition, ParamContainer);

    public:
    property int number
    {
        int get();
        void set(int value);
    }

    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    property System::String^ spectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ externalSpectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ externalNativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }


    Acquisition();

    bool empty() new;
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Acquisitions, pwiz::msdata::Acquisition, Acquisition, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class AcquisitionList : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, AcquisitionList, ParamContainer);

    public:
    property Acquisitions^ acquisitions
    {
        Acquisitions^ get();
    }


    AcquisitionList();

    bool empty() new;
};


public ref class IsolationWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, IsolationWindow, ParamContainer);
    public: IsolationWindow();
};


public ref class SelectedIon : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, SelectedIon, ParamContainer);
    public: SelectedIon();
};


public ref class Activation : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Activation, ParamContainer);
    public: Activation();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SelectedIonList, pwiz::msdata::SelectedIon, SelectedIon, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Precursor : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Precursor, ParamContainer);

    public:
    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    property System::String^ spectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ externalSpectrumID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ externalNativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property IsolationWindow^ isolationWindow
    {
        IsolationWindow^ get();
        void set(IsolationWindow^ value);
    }

    property SelectedIonList^ selectedIons
    {
        SelectedIonList^ get();
    }

    property Activation^ activation
    {
        Activation^ get();
        void set(Activation^ value);
    }


    Precursor();

    bool empty() new;
};


public ref class ScanWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, ScanWindow, ParamContainer);

    public:
    ScanWindow();
    ScanWindow(double mzLow, double mzHigh);
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ScanWindowList, pwiz::msdata::ScanWindow, ScanWindow, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Scan : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Scan, ParamContainer);

    public:
    property InstrumentConfiguration^ instrumentConfiguration
    {
        InstrumentConfiguration^ get();
        void set(InstrumentConfiguration^ value);
    }

    property ScanWindowList^ scanWindows
    {
        ScanWindowList^ get();
    }


    Scan();

    bool empty() new;
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(PrecursorList, pwiz::msdata::Precursor, Precursor, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class SpectrumDescription : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, SpectrumDescription, ParamContainer);

    public:
    property AcquisitionList^ acquisitionList
    {
        AcquisitionList^ get();
    }

    property PrecursorList^ precursors
    {
        PrecursorList^ get();
    }

    property Scan^ scan
    {
        Scan^ get();
        void set(Scan^ value);
    }


    SpectrumDescription();

    bool empty() new;
};


DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(BinaryData, double, double, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE);


public ref class BinaryDataArray : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, BinaryDataArray, ParamContainer);

    public:
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        void set(DataProcessing^ value);
    }

    property BinaryData^ data
    {
        BinaryData^ get();
        void set(BinaryData^ value);
    }


    BinaryDataArray();

    bool empty() new;
};


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


public ref class SpectrumIdentity
{
    DEFINE_INTERNAL_BASE_CODE(SpectrumIdentity, pwiz::msdata::SpectrumIdentity);

    public:
    property int index
    {
        int get();
        void set(int value);
    }

    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ nativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ spotID
    {
        System::String^ get();
        void set(System::String^ value);
    }

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
    property int index
    {
        int get();
        void set(int value);
    }

    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ nativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    ChromatogramIdentity();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(BinaryDataArrayList, pwiz::msdata::BinaryDataArrayPtr, BinaryDataArray, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MZIntensityPairList, pwiz::msdata::MZIntensityPair, MZIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TimeIntensityPairList, pwiz::msdata::TimeIntensityPair, TimeIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Spectrum : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Spectrum, ParamContainer);

    public:

    // SpectrumIdentity
    property int index
    {
        int get();
        void set(int value);
    }

    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ nativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ spotID
    {
        System::String^ get();
        void set(System::String^ value);
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }


    // Spectrum
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }
 
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        void set(DataProcessing^ value);
    }

    property SourceFile^ sourceFile
    {
        SourceFile^ get();
        void set(SourceFile^ value);
    }

    property SpectrumDescription^ spectrumDescription
    {
        SpectrumDescription^ get();
        void set(SpectrumDescription^ value);
    }

    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get();
        void set(BinaryDataArrayList^ value);
    }
 

    Spectrum();

    bool empty() new;

    /// copy binary data arrays into m/z-intensity pair array
    void getMZIntensityPairs(MZIntensityPairList^% output);

    /// get m/z array (may be null)
    BinaryDataArray^ getMZArray();

    /// get intensity array (may be null)
    BinaryDataArray^ getIntensityArray();

    /// set binary data arrays 
    void setMZIntensityPairs(MZIntensityPairList^ input);

    /// set binary data arrays 
    void setMZIntensityPairs(MZIntensityPairList^ input, CVID intensityUnits);

    /// set m/z and intensity arrays separately (they must be the same size)
    void setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                              System::Collections::Generic::List<double>^ intensityArray);

    /// set m/z and intensity arrays separately (they must be the same size)
    void setMZIntensityArrays(System::Collections::Generic::List<double>^ mzArray,
                              System::Collections::Generic::List<double>^ intensityArray,
                              CVID intensityUnits);
};


public ref class Chromatogram : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Chromatogram, ParamContainer);

    public:
    // ChromatogramIdentity
    property int index
    {
        int get();
        void set(int value);
    }

    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ nativeID
    {
        System::String^ get();
        void set(System::String^ value);
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }

    
    // Chromatogram
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get();
        void set(System::UInt64 value);
    }
 
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get();
        //void set(DataProcessing^ value);
    }

    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get();
        void set(BinaryDataArrayList^ value);
    }
 

    Chromatogram();

    bool empty() new;

    /// copy binary data arrays into time-intensity pair array
    void getTimeIntensityPairs(TimeIntensityPairList^% output);

    /// set binary data arrays 
    void setTimeIntensityPairs(TimeIntensityPairList^ input);
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
/// - It is the implementation's responsibility to return a valid Spectrum^ from spectrum().
///   If this cannot be done, an exception must be thrown. 
/// 
/// - The 'getBinaryData' flag is a hint if false : implementations may provide valid 
///   BinaryDataArrayPtrs on spectrum(index, false);  implementations *must* provide 
///   valid BinaryDataArrayPtrs on spectrum(index, true).
///
public ref class SpectrumList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, SpectrumList);

    public:
    
    /// returns the number of spectra
    virtual int size();

    /// returns true iff (size() == 0)
    virtual bool empty();

    /// access to a spectrum index
    virtual SpectrumIdentity^ spectrumIdentity(int index);

    /// find id in the spectrum index (returns size() on failure)
    virtual int find(System::String^ id);

    /// find nativeID in the spectrum index (returns size() on failure)
    virtual int findNative(System::String^ nativeID);

    /// retrieve a spectrum by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Spectrum* is valid
    virtual Spectrum^ spectrum(int index);
    virtual Spectrum^ spectrum(int index, bool getBinaryData);
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Spectra, pwiz::msdata::SpectrumPtr, Spectrum, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// Simple writeable in-memory implementation of SpectrumList.
/// Note:  This spectrum() implementation returns internal SpectrumPtrs.
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


public ref class ChromatogramList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(pwiz::msdata, ChromatogramList);

    public:
    
    /// returns the number of chromatograms 
    virtual int size();

    /// returns true iff (size() == 0)
    virtual bool empty();

    /// access to a chromatogram index
    virtual ChromatogramIdentity^ chromatogramIdentity(int index);

    /// find id in the chromatogram index (returns size() on failure)
    virtual int find(System::String^ id);

    /// find nativeID in the chromatogram index (returns size() on failure)
    virtual int findNative(System::String^ nativeID);

    /// retrieve a chromatogram by index
    /// - binary data arrays will be provided if (getBinaryData == true);
    /// - client may assume the underlying Chromatogram* is valid
    virtual Chromatogram^ chromatogram(int index);
    virtual Chromatogram^ chromatogram(int index, bool getBinaryData);
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Chromatograms, pwiz::msdata::ChromatogramPtr, Chromatogram, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// Simple writeable in-memory implementation of ChromatogramList.
/// Note:  This chromatogram() implementation returns internal ChromatogramPtrs.
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


public ref class Run : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(pwiz::msdata, Run, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property InstrumentConfiguration^ defaultInstrumentConfiguration
    {
        InstrumentConfiguration^ get();
        void set(InstrumentConfiguration^ value);
    }

    property Sample^ sample
    {
        Sample^ get();
        void set(Sample^ value);
    }

    property System::String^ startTimeStamp
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get();
        void set(SourceFileList^ value);
    }

    property SpectrumList^ spectrumList
    {
        SpectrumList^ get();
        void set(SpectrumList^ value);
    }

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


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CVList, pwiz::msdata::CV, CV, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SampleList, pwiz::msdata::SamplePtr, Sample, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(InstrumentConfigurationList, pwiz::msdata::InstrumentConfigurationPtr, InstrumentConfiguration, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SoftwareList, pwiz::msdata::SoftwarePtr, Software, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(DataProcessingList, pwiz::msdata::DataProcessingPtr, DataProcessing, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(AcquisitionSettingsList, pwiz::msdata::AcquisitionSettingsPtr, AcquisitionSettings, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


public ref class MSData
{
    DEFINE_INTERNAL_BASE_CODE(MSData, pwiz::msdata::MSData);

    public:
    property System::String^ accession
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property System::String^ version
    {
        System::String^ get();
        void set(System::String^ value);
    }

    property CVList^ cvs
    {
        CVList^ get();
        void set(CVList^ value);
    }

    property FileDescription^ fileDescription
    {
        FileDescription^ get();
        void set(FileDescription^ value);
    }

    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get();
        void set(ParamGroupList^ value);
    }

    property SampleList^ samples
    {
        SampleList^ get();
        void set(SampleList^ value);
    }

    property InstrumentConfigurationList^ instrumentConfigurationList
    {
        InstrumentConfigurationList^ get();
        void set(InstrumentConfigurationList^ value);
    }

    property SoftwareList^ softwareList
    {
        SoftwareList^ get();
        void set(SoftwareList^ value);
    }

    property DataProcessingList^ dataProcessingList
    {
        DataProcessingList^ get();
        void set(DataProcessingList^ value);
    }

    property AcquisitionSettingsList^ acquisitionSettingList
    {
        AcquisitionSettingsList^ get();
        void set(AcquisitionSettingsList^ value);
    }

    property Run^ run
    {
        Run^ get();
        //void set(Run^ value);
    }


    MSData();

    bool empty();

    internal:
    // no copying
    //MSData(MSData&);
    //MSData& operator=(MSData&);
};


} // namespace msdata
} // namespace CLI
} // namespace pwiz

#endif // _MSDATA_HPP_CLI_
