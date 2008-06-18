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
    DEFINE_INTERNAL_BASE_CODE(CV);
             
    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String(base_->id.c_str());}
        void set(System::String^ value) {base_->id = ToStdString(value);}
    }

    property System::String^ URI
    {
        System::String^ get() {return gcnew System::String(base_->URI.c_str());}
        void set(System::String^ value) {base_->URI = ToStdString(value);}
    }

    property System::String^ fullName
    {
        System::String^ get() {return gcnew System::String(base_->fullName.c_str());}
        void set(System::String^ value) {base_->fullName = ToStdString(value);}
    }

    property System::String^ version
    {
        System::String^ get() {return gcnew System::String(base_->version.c_str());}
        void set(System::String^ value) {base_->version = ToStdString(value);}
    }


    CV();

    bool empty() {return base_->empty();}
};


public ref class UserParamValue
{
    internal: UserParamValue(pwiz::msdata::UserParam* base) : base_(base) {} \
              virtual ~UserParamValue() {if (base_) delete base_;} \
              pwiz::msdata::UserParam* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(UserParamValue^ value) {return gcnew System::String(value->base_->value.c_str());};
    static explicit operator float(UserParamValue^ value) {return value->base_->valueAs<float>();}
    static operator double(UserParamValue^ value) {return value->base_->valueAs<double>();}
    static explicit operator int(UserParamValue^ value) {return value->base_->valueAs<int>();}
    static explicit operator System::UInt64(UserParamValue^ value) {return (System::UInt64) value->base_->valueAs<size_t>();}
    static explicit operator bool(UserParamValue^ value) {return value->base_->value == "true";}
    UserParamValue^ operator=(System::String^ value) {base_->value = ToStdString(value); return this;} 
};


public ref class UserParam
{
    internal: UserParam(pwiz::msdata::UserParam* base) : base_(base), value_(gcnew UserParamValue(base)) {} \
          virtual ~UserParam() {if (base_) delete base_;} \
          pwiz::msdata::UserParam* base_;
          UserParamValue^ value_;

    public:
    property System::String^ name
    {
        System::String^ get() {return gcnew System::String(base_->name.c_str());}
        void set(System::String^ value) {base_->name = ToStdString(value);}
    }

    property UserParamValue^ value
    {
        UserParamValue^ get() {return value_;}
    }

    property System::String^ type
    {
        System::String^ get() {return gcnew System::String(base_->type.c_str());}
        void set(System::String^ value) {base_->type = ToStdString(value);}
    }

    property CVID units
    {
        CVID get() {return (CVID) base_->units;}
        void set(CVID value) {base_->units = (pwiz::msdata::CVID) value;}
    }

    UserParam();
    UserParam(System::String^ _name);
    UserParam(System::String^ _name, System::String^ _value);
    UserParam(System::String^ _name, System::String^ _value, System::String^ _type);
    UserParam(System::String^ _name, System::String^ _value, System::String^ _type, CVID _units);

    bool empty() {return base_->empty();}
    bool operator==(UserParam^ that) {return base_ == that->base_;}
    bool operator!=(UserParam^ that) {return base_ != that->base_;}
};


ref class ParamGroupList;
ref class CVParamList;
ref class UserParamList;


public ref class ParamContainer
{
    internal: ParamContainer(pwiz::msdata::ParamContainer* base) : base_(base) {}
              virtual ~ParamContainer() {if (base_) delete base_;}
              pwiz::msdata::ParamContainer* base_;

    ParamGroupList^ getParamGroups();
    CVParamList^ getCVParams();
    UserParamList^ getUserParams();

    public:
    ParamContainer() : base_(new pwiz::msdata::ParamContainer()) {}

    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get() {return getParamGroups();}
    }

    property CVParamList^ cvParams
    {
        CVParamList^ get() {return getCVParams();}
    }

    property UserParamList^ userParams
    {
        UserParamList^ get() {return getUserParams();}
    }

    
    /// Finds pwiz::msdata::CVID in the container:
    /// - returns first CVParam result such that (result.pwiz::msdata::CVID == pwiz::msdata::CVID); 
    /// - if not found, returns CVParam(pwiz::msdata::CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam^ cvParam(CVID cvid) {return gcnew CVParam(new pwiz::msdata::CVParam(base_->cvParam((pwiz::msdata::CVID) cvid)));}

    /// Finds child of pwiz::msdata::CVID in the container:
    /// - returns first CVParam result such that (result.pwiz::msdata::CVID is_a pwiz::msdata::CVID); 
    /// - if not found, CVParam(pwiz::msdata::CVID_Unknown)
    /// - recursive: looks into paramGroupPtrs
    CVParam^ cvParamChild(CVID cvid) {return gcnew CVParam(new pwiz::msdata::CVParam(base_->cvParamChild((pwiz::msdata::CVID) cvid)));}

    /// returns true iff cvParams contains exact pwiz::msdata::CVID (recursive)
    bool hasCVParam(CVID cvid) {return base_->hasCVParam((pwiz::msdata::CVID) cvid);}

    /// returns true iff cvParams contains a child (is_a) of pwiz::msdata::CVID (recursive)
    bool hasCVParamChild(CVID cvid) {return base_->hasCVParamChild((pwiz::msdata::CVID) cvid);}

    /// Finds UserParam with specified name 
    /// - returns UserParam() if name not found 
    /// - not recursive: looks only at local userParams
    UserParam^ userParam(System::String^ name) {return gcnew UserParam(new pwiz::msdata::UserParam(base_->userParam(ToStdString(name))));}

    /// set/add a CVParam (not recursive)
    void set(CVID cvid) {base_->set((pwiz::msdata::CVID) cvid);}
    void set(CVID cvid, System::String^ value) {base_->set((pwiz::msdata::CVID) cvid, ToStdString(value));}
    void set(CVID cvid, System::String^ value, CVID units) {base_->set((pwiz::msdata::CVID) cvid, ToStdString(value), (pwiz::msdata::CVID) units);}

    void set(CVID cvid, bool value) {set(cvid, (value ? "true" : "false"));}

    /// set/add a CVParam (not recursive)
    generic <typename value_type>
    void set(CVID cvid, value_type value) {set(cvid, value->ToString());}

    generic <typename value_type>
    void set(CVID cvid, value_type value, CVID units) {set(cvid, value->ToString(), units);}

    bool empty() {return base_->empty();}
};


public ref class ParamGroup : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(ParamGroup, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }


    ParamGroup();
    ParamGroup(System::String^ _id);

    bool empty() new {return (*base_)->empty();}
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
    property int Count { virtual int get() {return (int) base_->size();} } \
    property bool IsReadOnly { virtual bool get() {return false;} } \
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
        property ParamGroup^ Current { virtual ParamGroup^ get() {return NATIVE_SHARED_PTR_TO_CLI(ParamGroup, **itr_);} } \
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
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CVParamList, pwiz::msdata::CVParam, CVParam, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(UserParamList, pwiz::msdata::UserParam, UserParam, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class FileContent : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(FileContent, ParamContainer);
    public: FileContent();
};


public ref class SourceFile : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(SourceFile, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property System::String^ name
    {
        System::String^ get() {return gcnew System::String((*base_)->name.c_str());}
        void set(System::String^ value) {(*base_)->name = ToStdString(value);}
    }

    property System::String^ location
    {
        System::String^ get() {return gcnew System::String((*base_)->location.c_str());}
        void set(System::String^ value) {(*base_)->location = ToStdString(value);}
    }


    SourceFile();
    SourceFile(System::String^ _id);
    SourceFile(System::String^ _id, System::String^ _name);
    SourceFile(System::String^ _id, System::String^ _name, System::String^ _location);

    bool empty() new {return (*base_)->empty();}
};


public ref class Contact : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Contact, ParamContainer);
    public: Contact();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SourceFileList, pwiz::msdata::SourceFilePtr, SourceFile, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ContactList, pwiz::msdata::Contact, Contact, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class FileDescription
{
    DEFINE_INTERNAL_BASE_CODE(FileDescription);

    public:
    property FileContent^ fileContent
    {
        FileContent^ get() {return gcnew FileContent(&base_->fileContent);}
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get() {return gcnew SourceFileList(&base_->sourceFilePtrs);}
    }

    property ContactList^ contacts
    {
        ContactList^ get() {return gcnew ContactList(&base_->contacts);}
    }


    FileDescription();


    bool empty() {return base_->empty();}
};


public ref class Sample : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(Sample, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property System::String^ name
    {
        System::String^ get() {return gcnew System::String((*base_)->name.c_str());}
        void set(System::String^ value) {(*base_)->name = ToStdString(value);}
    }


    Sample();
    Sample(System::String^ _id);
    Sample(System::String^ _id, System::String^ _name);

    bool empty() new {return (*base_)->empty();}
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
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Component, ParamContainer);

    public:
    property ComponentType type
    {
        ComponentType get() {return (ComponentType) base_->type;}
        void set(ComponentType value) {base_->type = (pwiz::msdata::ComponentType) value;}
    }

    property int order
    {
        int get() {return base_->order;}
        void set(int value) {base_->order = value;}
    }


    Component();
    Component(ComponentType type, int order);
    Component(CVID cvid, int order);

    void define(CVID cvid, int order) {base_->define((pwiz::msdata::CVID) cvid, order);}
    bool empty() new {return base_->empty();}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ComponentBaseList, pwiz::msdata::Component, Component, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class ComponentList : public ComponentBaseList
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(ComponentList, ComponentBaseList);

    public:
    ComponentList();

    Component^ source(int index) {return gcnew Component(&base_->source((size_t) index));}
    Component^ analyzer(int index) {return gcnew Component(&base_->analyzer((size_t) index));}
    Component^ detector(int index) {return gcnew Component(&base_->detector((size_t) index));}
};


public ref class Software
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(Software);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property CVParam^ softwareParam
    {
        CVParam^ get() {return gcnew CVParam(&(*base_)->softwareParam);}
        void set(CVParam^ value) {(*base_)->softwareParam = *value->base_;}
    }

    property System::String^ softwareParamVersion
    {
        System::String^ get() {return gcnew System::String((*base_)->softwareParamVersion.c_str());}
        void set(System::String^ value) {(*base_)->softwareParamVersion = ToStdString(value);}
    }


    Software();
    Software(System::String^ _id);
    Software(System::String^ _id, CVParam^ _softwareParam, System::String^ _softwareParamVersion);

    bool empty() {return (*base_)->empty();}
};


public ref class InstrumentConfiguration : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(InstrumentConfiguration, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property ComponentList^ componentList
    {
        ComponentList^ get() {return gcnew ComponentList(&(*base_)->componentList);}
    }

    property Software^ software
    {
        Software^ get() {return NATIVE_SHARED_PTR_TO_CLI(Software, (*base_)->softwarePtr);}
    }


    InstrumentConfiguration();
    InstrumentConfiguration(System::String^ _id);

    bool empty() new {return (*base_)->empty();}
};


public ref class ProcessingMethod : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(ProcessingMethod, ParamContainer);

    public:
    property int order
    {
        int get() {return base_->order;}
        void set(int value) {base_->order = value;}
    }


    ProcessingMethod();

    bool empty() new {return base_->empty();}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ProcessingMethodList, pwiz::msdata::ProcessingMethod, ProcessingMethod, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class DataProcessing
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(DataProcessing);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property Software^ software
    {
        Software^ get() {return NATIVE_SHARED_PTR_TO_CLI(Software, (*base_)->softwarePtr);}
    }

    property ProcessingMethodList^ processingMethods
    {
        ProcessingMethodList^ get() {return gcnew ProcessingMethodList(&(*base_)->processingMethods);}
    }


    DataProcessing();
    DataProcessing(System::String^ _id);

    bool empty() {return (*base_)->empty();}
};


public ref class Target : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Target, ParamContainer);
    public: Target();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TargetList, pwiz::msdata::Target, Target, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class AcquisitionSettings
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(AcquisitionSettings);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property InstrumentConfiguration^ instrumentConfiguration
    {
        InstrumentConfiguration^ get() {return NATIVE_SHARED_PTR_TO_CLI(InstrumentConfiguration, (*base_)->instrumentConfigurationPtr);}
        void set(InstrumentConfiguration^ value) {(*base_)->instrumentConfigurationPtr = *value->base_;}
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get() {return gcnew SourceFileList(&(*base_)->sourceFilePtrs);}
    }

    property TargetList^ targets
    {
        TargetList^ get() {return gcnew TargetList(&(*base_)->targets);}
    }


    AcquisitionSettings();
    AcquisitionSettings(System::String^ _id);

    bool empty() {return (*base_)->empty();}
};


public ref class Acquisition : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Acquisition, ParamContainer);

    public:
    property int number
    {
        int get() {return base_->number;}
        void set(int value) {base_->number = value;}
    }

    property SourceFile^ sourceFile
    {
        SourceFile^ get() {return NATIVE_SHARED_PTR_TO_CLI(SourceFile, base_->sourceFilePtr);}
        void set(SourceFile^ value) {base_->sourceFilePtr = *value->base_;}
    }

    property System::String^ spectrumID
    {
        System::String^ get() {return gcnew System::String(base_->spectrumID.c_str());}
        void set(System::String^ value) {base_->spectrumID = ToStdString(value);}
    }

    property System::String^ externalSpectrumID
    {
        System::String^ get() {return gcnew System::String(base_->externalSpectrumID.c_str());}
        void set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}
    }

    property System::String^ externalNativeID
    {
        System::String^ get() {return gcnew System::String(base_->externalNativeID.c_str());}
        void set(System::String^ value) {base_->externalNativeID = ToStdString(value);}
    }


    Acquisition();

    bool empty() new {return base_->empty();}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(Acquisitions, pwiz::msdata::Acquisition, Acquisition, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class AcquisitionList : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(AcquisitionList, ParamContainer);

    public:
    property Acquisitions^ acquisitions
    {
        Acquisitions^ get() {return gcnew Acquisitions(&base_->acquisitions);}
    }


    AcquisitionList();

    bool empty() new {return base_->empty();}
};


public ref class IsolationWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(IsolationWindow, ParamContainer);
    public: IsolationWindow();
};


public ref class SelectedIon : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(SelectedIon, ParamContainer);
    public: SelectedIon();
};


public ref class Activation : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Activation, ParamContainer);
    public: Activation();
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(SelectedIonList, pwiz::msdata::SelectedIon, SelectedIon, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Precursor : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Precursor, ParamContainer);

    public:
    property SourceFile^ sourceFile
    {
        SourceFile^ get() {return NATIVE_SHARED_PTR_TO_CLI(SourceFile, base_->sourceFilePtr);}
        void set(SourceFile^ value) {base_->sourceFilePtr = *value->base_;}
    }

    property System::String^ spectrumID
    {
        System::String^ get() {return gcnew System::String(base_->spectrumID.c_str());}
        void set(System::String^ value) {base_->spectrumID = ToStdString(value);}
    }

    property System::String^ externalSpectrumID
    {
        System::String^ get() {return gcnew System::String(base_->externalSpectrumID.c_str());}
        void set(System::String^ value) {base_->externalSpectrumID = ToStdString(value);}
    }

    property System::String^ externalNativeID
    {
        System::String^ get() {return gcnew System::String(base_->externalNativeID.c_str());}
        void set(System::String^ value) {base_->externalNativeID = ToStdString(value);}
    }

    property IsolationWindow^ isolationWindow
    {
        IsolationWindow^ get() {return gcnew IsolationWindow(&base_->isolationWindow);}
        void set(IsolationWindow^ value) {base_->isolationWindow = *value->base_;}
    }

    property SelectedIonList^ selectedIons
    {
        SelectedIonList^ get() {return gcnew SelectedIonList(&base_->selectedIons);}
    }

    property Activation^ activation
    {
        Activation^ get() {return gcnew Activation(&base_->activation);}
        void set(Activation^ value) {base_->activation = *value->base_;}
    }


    Precursor();

    bool empty() new {return base_->empty();}
};


public ref class ScanWindow : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(ScanWindow, ParamContainer);

    public:
    ScanWindow();
    ScanWindow(double mzLow, double mzHigh);
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ScanWindowList, pwiz::msdata::ScanWindow, ScanWindow, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Scan : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Scan, ParamContainer);

    public:
    property InstrumentConfiguration^ instrumentConfiguration
    {
        InstrumentConfiguration^ get() {return NATIVE_SHARED_PTR_TO_CLI(InstrumentConfiguration, base_->instrumentConfigurationPtr);}
        void set(InstrumentConfiguration^ value) {base_->instrumentConfigurationPtr = *value->base_;}
    }

    property ScanWindowList^ scanWindows
    {
        ScanWindowList^ get() {return gcnew ScanWindowList(&base_->scanWindows);}
    }


    Scan();

    bool empty() new {return base_->empty();}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(PrecursorList, pwiz::msdata::Precursor, Precursor, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class SpectrumDescription : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(SpectrumDescription, ParamContainer);

    public:
    property AcquisitionList^ acquisitionList
    {
        AcquisitionList^ get() {return gcnew AcquisitionList(&base_->acquisitionList);}
    }

    property PrecursorList^ precursors
    {
        PrecursorList^ get() {return gcnew PrecursorList(&base_->precursors);}
    }

    property Scan^ scan
    {
        Scan^ get() {return gcnew Scan(&base_->scan);}
        void set(Scan^ value) {base_->scan = *value->base_;}
    }


    SpectrumDescription();

    bool empty() new {return base_->empty();}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_VALUE_TYPE(BinaryData, double, double, NATIVE_VALUE_TO_CLI, CLI_VALUE_TO_NATIVE_VALUE);


public ref class BinaryDataArray : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(BinaryDataArray, ParamContainer);

    public:
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get() {return NATIVE_SHARED_PTR_TO_CLI(DataProcessing, (*base_)->dataProcessingPtr);}
        void set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}
    }

    property BinaryData^ data
    {
        BinaryData^ get() {return gcnew BinaryData(&(*base_)->data);}
        void set(BinaryData^ value) {(*base_)->data = *value->base_;}
    }


    BinaryDataArray();

    bool empty() new {return (*base_)->empty();}
};


public ref class MZIntensityPair
{
    DEFINE_INTERNAL_BASE_CODE(MZIntensityPair);

    public:
    property double mz
    {
        double get() {return base_->mz;}
        void set(double value) {base_->mz = value;}
    }

    property double intensity
    {
        double get() {return base_->intensity;}
        void set(double value) {base_->intensity = value;}
    }


    MZIntensityPair() : base_(new pwiz::msdata::MZIntensityPair()) {}
    MZIntensityPair(double _mz, double _intensity) : base_(new pwiz::msdata::MZIntensityPair(_mz, _intensity)) {}
};


public ref class TimeIntensityPair
{
    DEFINE_INTERNAL_BASE_CODE(TimeIntensityPair);

    public:
    property double time
    {
        double get() {return base_->time;}
        void set(double value) {base_->time = value;}
    }

    property double intensity
    {
        double get() {return base_->intensity;}
        void set(double value) {base_->intensity = value;}
    }


    TimeIntensityPair() : base_(new pwiz::msdata::TimeIntensityPair()) {}
    TimeIntensityPair(double _mz, double _intensity) : base_(new pwiz::msdata::TimeIntensityPair(_mz, _intensity)) {}
};


public ref class SpectrumIdentity
{
    DEFINE_INTERNAL_BASE_CODE(SpectrumIdentity);

    public:
    property int index
    {
        int get() {return (int) base_->index;}
        void set(int value) {base_->index = (size_t) value;}
    }

    property System::String^ id
    {
        System::String^ get() {return gcnew System::String(base_->id.c_str());}
        void set(System::String^ value) {base_->id = ToStdString(value);}
    }

    property System::String^ nativeID
    {
        System::String^ get() {return gcnew System::String(base_->nativeID.c_str());}
        void set(System::String^ value) {base_->nativeID = ToStdString(value);}
    }

    property System::String^ spotID
    {
        System::String^ get() {return gcnew System::String(base_->spotID.c_str());}
        void set(System::String^ value) {base_->spotID = ToStdString(value);}
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get() {return (System::UInt64) base_->sourceFilePosition;}
        void set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}
    }


    SpectrumIdentity() : base_(new pwiz::msdata::SpectrumIdentity()) {}
};


public ref class ChromatogramIdentity
{
    DEFINE_INTERNAL_BASE_CODE(ChromatogramIdentity);

    public:
    property int index
    {
        int get() {return (int) base_->index;}
        void set(int value) {base_->index = (size_t) value;}
    }

    property System::String^ id
    {
        System::String^ get() {return gcnew System::String(base_->id.c_str());}
        void set(System::String^ value) {base_->id = ToStdString(value);}
    }

    property System::String^ nativeID
    {
        System::String^ get() {return gcnew System::String(base_->nativeID.c_str());}
        void set(System::String^ value) {base_->nativeID = ToStdString(value);}
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get() {return (System::UInt64) base_->sourceFilePosition;}
        void set(System::UInt64 value) {base_->sourceFilePosition = (size_t) value;}
    }


    ChromatogramIdentity() : base_(new pwiz::msdata::ChromatogramIdentity()) {}
};


DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(BinaryDataArrayList, pwiz::msdata::BinaryDataArrayPtr, BinaryDataArray, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(MZIntensityPairList, pwiz::msdata::MZIntensityPair, MZIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);
DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(TimeIntensityPairList, pwiz::msdata::TimeIntensityPair, TimeIntensityPair, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


public ref class Spectrum : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(Spectrum, ParamContainer);

    public:

    // SpectrumIdentity
    property int index
    {
        int get() {return (int) (*base_)->index;}
        void set(int value) {(*base_)->index = (size_t) value;}
    }

    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property System::String^ nativeID
    {
        System::String^ get() {return gcnew System::String((*base_)->nativeID.c_str());}
        void set(System::String^ value) {(*base_)->nativeID = ToStdString(value);}
    }

    property System::String^ spotID
    {
        System::String^ get() {return gcnew System::String((*base_)->spotID.c_str());}
        void set(System::String^ value) {(*base_)->spotID = ToStdString(value);}
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get() {return (System::UInt64) (*base_)->sourceFilePosition;}
        void set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}
    }


    // Spectrum
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get() {return (System::UInt64) (*base_)->defaultArrayLength;}
        void set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
    }
 
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get() {return NATIVE_SHARED_PTR_TO_CLI(DataProcessing, (*base_)->dataProcessingPtr);}
        void set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}
    }

    property SourceFile^ sourceFile
    {
        SourceFile^ get() {return NATIVE_SHARED_PTR_TO_CLI(SourceFile, (*base_)->sourceFilePtr);}
        void set(SourceFile^ value) {(*base_)->sourceFilePtr = *value->base_;}
    }

    property SpectrumDescription^ spectrumDescription
    {
        SpectrumDescription^ get() {return gcnew SpectrumDescription(&(*base_)->spectrumDescription);}
        void set(SpectrumDescription^ value) {(*base_)->spectrumDescription = *value->base_;}
    }

    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs);}
        void set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}
    }
 

    Spectrum();

    bool empty() new {return (*base_)->empty();}

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
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(Chromatogram, ParamContainer);

    public:
    // ChromatogramIdentity
    property int index
    {
        int get() {return (int) (*base_)->index;}
        void set(int value) {(*base_)->index = (size_t) value;}
    }

    property System::String^ id
    {
        System::String^ get() {return gcnew System::String((*base_)->id.c_str());}
        void set(System::String^ value) {(*base_)->id = ToStdString(value);}
    }

    property System::String^ nativeID
    {
        System::String^ get() {return gcnew System::String((*base_)->nativeID.c_str());}
        void set(System::String^ value) {(*base_)->nativeID = ToStdString(value);}
    }

	property System::UInt64 sourceFilePosition
    {
        System::UInt64 get() {return (System::UInt64) (*base_)->sourceFilePosition;}
        void set(System::UInt64 value) {(*base_)->sourceFilePosition = (size_t) value;}
    }

    
    // Chromatogram
    property System::UInt64 defaultArrayLength
    {
        System::UInt64 get() {return (*base_)->defaultArrayLength;}
        void set(System::UInt64 value) {(*base_)->defaultArrayLength = (size_t) value;}
    }
 
    property DataProcessing^ dataProcessing
    {
        DataProcessing^ get() {return NATIVE_SHARED_PTR_TO_CLI(DataProcessing, (*base_)->dataProcessingPtr);}
        //void set(DataProcessing^ value) {(*base_)->dataProcessingPtr = *value->base_;}
    }

    property BinaryDataArrayList^ binaryDataArrays
    {
        BinaryDataArrayList^ get() {return gcnew BinaryDataArrayList(&(*base_)->binaryDataArrayPtrs);}
        void set(BinaryDataArrayList^ value) {(*base_)->binaryDataArrayPtrs = *value->base_;}
    }
 

    Chromatogram();

    bool empty() new {return (*base_)->empty();}

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
    DEFINE_SHARED_INTERNAL_BASE_CODE(SpectrumList);

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
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(SpectrumListSimple, SpectrumList);

    public:
    property Spectra^ spectra
    {
        Spectra^ get() {return gcnew Spectra(&(*base_)->spectra);}
        void set(Spectra^ value) {(*base_)->spectra = *value->base_;}
    }


    SpectrumListSimple();

    // SpectrumList implementation

    virtual int size() override {return (*base_)->size();}
    virtual bool empty() override {return (*base_)->empty();}
    virtual SpectrumIdentity^ spectrumIdentity(int index) override;
    virtual Spectrum^ spectrum(int index) override;
    virtual Spectrum^ spectrum(int index, bool getBinaryData) override;
};


public ref class ChromatogramList
{
    DEFINE_SHARED_INTERNAL_BASE_CODE(ChromatogramList);

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
    DEFINE_SHARED_DERIVED_INTERNAL_SHARED_BASE_CODE(ChromatogramListSimple, ChromatogramList);

    public:
    property Chromatograms^ chromatograms
    {
        Chromatograms^ get() {return gcnew Chromatograms(&(*base_)->chromatograms);}
        void set(Chromatograms^ value) {(*base_)->chromatograms = *value->base_;}
    }


    ChromatogramListSimple();

    // ChromatogramList implementation

    virtual int size() override {return (*base_)->size();}
    virtual bool empty() override {return (*base_)->empty();}
    virtual ChromatogramIdentity^ chromatogramIdentity(int index) override;
    virtual Chromatogram^ chromatogram(int index) override;
    virtual Chromatogram^ chromatogram(int index, bool getBinaryData) override;
};


public ref class Run : public ParamContainer
{
    DEFINE_DERIVED_INTERNAL_BASE_CODE(Run, ParamContainer);

    public:
    property System::String^ id
    {
        System::String^ get() {return gcnew System::String(base_->id.c_str());}
        void set(System::String^ value) {base_->id = ToStdString(value);}
    }

    property InstrumentConfiguration^ defaultInstrumentConfiguration
    {
        InstrumentConfiguration^ get() {return NATIVE_SHARED_PTR_TO_CLI(InstrumentConfiguration, base_->defaultInstrumentConfigurationPtr);}
        void set(InstrumentConfiguration^ value) {base_->defaultInstrumentConfigurationPtr = *value->base_;}
    }

    property Sample^ sample
    {
        Sample^ get() {return NATIVE_SHARED_PTR_TO_CLI(Sample, base_->samplePtr);}
        void set(Sample^ value) {base_->samplePtr = *value->base_;}
    }

    property System::String^ startTimeStamp
    {
        System::String^ get() {return gcnew System::String(base_->startTimeStamp.c_str());}
        void set(System::String^ value) {base_->startTimeStamp = ToStdString(value);}
    }

    property SourceFileList^ sourceFiles
    {
        SourceFileList^ get() {return gcnew SourceFileList(&base_->sourceFilePtrs);}
        void set(SourceFileList^ value) {base_->sourceFilePtrs = *value->base_;}
    }

    property SpectrumList^ spectrumList
    {
        SpectrumList^ get() {return NATIVE_SHARED_PTR_TO_CLI(SpectrumList, base_->spectrumListPtr);}
        void set(SpectrumList^ value) {base_->spectrumListPtr = *value->base_;}
    }

    property ChromatogramList^ chromatogramList
    {
        ChromatogramList^ get() {return NATIVE_SHARED_PTR_TO_CLI(ChromatogramList, base_->chromatogramListPtr);}
        void set(ChromatogramList^ value) {base_->chromatogramListPtr = *value->base_;}
    }


    Run();
    bool empty() new {return base_->empty();}

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
    DEFINE_INTERNAL_BASE_CODE(MSData);

    public:
    property System::String^ accession
    {
        System::String^ get() {return gcnew System::String(base_->accession.c_str());}
        void set(System::String^ value) {base_->accession = ToStdString(value);}
    }

    property System::String^ id
    {
        System::String^ get() {return gcnew System::String(base_->id.c_str());}
        void set(System::String^ value) {base_->id = ToStdString(value);}
    }

    property System::String^ version
    {
        System::String^ get() {return gcnew System::String(base_->version.c_str());}
        void set(System::String^ value) {base_->version = ToStdString(value);}
    }

    property CVList^ cvs
    {
        CVList^ get() {return gcnew CVList(&base_->cvs);}
        void set(CVList^ value) {base_->cvs = *value->base_;}
    }

    property FileDescription^ fileDescription
    {
        FileDescription^ get() {return gcnew FileDescription(&base_->fileDescription);}
        void set(FileDescription^ value) {base_->fileDescription = *value->base_;}
    }

    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get() {return gcnew ParamGroupList(&base_->paramGroupPtrs);}
        void set(ParamGroupList^ value) {base_->paramGroupPtrs = *value->base_;}
    }

    property SampleList^ samples
    {
        SampleList^ get() {return gcnew SampleList(&base_->samplePtrs);}
        void set(SampleList^ value) {base_->samplePtrs = *value->base_;}
    }

    property InstrumentConfigurationList^ instrumentConfigurationList
    {
        InstrumentConfigurationList^ get() {return gcnew InstrumentConfigurationList(&base_->instrumentConfigurationPtrs);}
        void set(InstrumentConfigurationList^ value) {base_->instrumentConfigurationPtrs = *value->base_;}
    }

    property SoftwareList^ softwareList
    {
        SoftwareList^ get() {return gcnew SoftwareList(&base_->softwarePtrs);}
        void set(SoftwareList^ value) {base_->softwarePtrs = *value->base_;}
    }

    property DataProcessingList^ dataProcessingList
    {
        DataProcessingList^ get() {return gcnew DataProcessingList(&base_->dataProcessingPtrs);}
        void set(DataProcessingList^ value) {base_->dataProcessingPtrs = *value->base_;}
    }

    property AcquisitionSettingsList^ acquisitionSettingList
    {
        AcquisitionSettingsList^ get() {return gcnew AcquisitionSettingsList(&base_->acquisitionSettingsPtrs);}
        void set(AcquisitionSettingsList^ value) {base_->acquisitionSettingsPtrs = *value->base_;}
    }

    property Run^ run
    {
        Run^ get() {return gcnew Run(&base_->run);}
        //void set(Run^ value) {base_->run = *value->base_;}
    }


    MSData();

    bool empty() {return base_->empty();}

    internal:
    // no copying
    //MSData(MSData&);
    //MSData& operator=(MSData&);
};


} // namespace msdata
} // namespace CLI
} // namespace pwiz

#endif // _MSDATA_HPP_CLI_
