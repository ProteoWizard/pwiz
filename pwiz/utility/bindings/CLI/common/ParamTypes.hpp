//
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

#ifndef _PARAMTYPES_HPP_CLI_
#define _PARAMTYPES_HPP_CLI_

#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "SharedCLI.hpp"
#include "cv.hpp"
#include "pwiz/data/common/ParamTypes.hpp"
#pragma warning( pop )


#ifndef PWIZ_BINDINGS_CLI_COMBINED
// list of friend assemblies that are permitted to access the common code's internal members
[assembly:System::Runtime::CompilerServices::InternalsVisibleTo("pwiz_bindings_cli_msdata")];
[assembly:System::Runtime::CompilerServices::InternalsVisibleTo("pwiz_bindings_cli_analysis")];
[assembly:System::Runtime::CompilerServices::InternalsVisibleTo("pwiz_bindings_cli_proteome")];
#endif


namespace pwiz {
namespace CLI {
namespace cv {


/// <summary>
/// Information about an ontology or CV source and a short 'lookup' tag to refer to.
/// </summary>
public ref class CV
{
    DEFINE_INTERNAL_BASE_CODE(CV, pwiz::cv::CV);
             
    public:

    /// <summary>
    /// the short label to be used as a reference tag with which to refer to this particular Controlled Vocabulary source description (e.g., from the cvLabel attribute, in CVParamType elements).
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the URI for the resource.
    /// </summary>
    property System::String^ URI
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the usual name for the resource (e.g. The PSI-MS Controlled Vocabulary).
    /// </summary>
    property System::String^ fullName
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the version of the CV from which the referred-to terms are drawn.
    /// </summary>
    property System::String^ version
    {
        System::String^ get();
        void set(System::String^ value);
    }


    CV();

    /// <summary>
    /// returns true iff id, URI, fullName, and version are all empty
    /// </summary>
    bool empty();


    /// <summary>
    /// returns CV term info for the specified CVID
    /// </summary>
    static CVTermInfo^ cvTermInfo(CVID cvid);


    /// <summary>
    /// returns CV term info for the specified id (accession number)
    /// </summary>
    static CVTermInfo^ cvTermInfo(System::String^ id);


    /// <summary>
    /// returns true iff child IsA parent in the CV
    /// </summary>
    static bool cvIsA(CVID child, CVID parent);


    /// <summary>
    /// returns vector of all valid CVIDs
    /// </summary>
    static System::Collections::Generic::IList<CVID>^ cvids();
};


} // namespace cv


namespace data {


using namespace cv;

#undef CATCH_AND_FORWARD_CAST
#define CATCH_AND_FORWARD_CAST(value, paramType, typeName) \
    catch (boost::bad_lexical_cast &) { throw gcnew System::InvalidCastException( \
    System::String::Format("Failed to cast " paramType " value '{0}' to " typeName ".", value->ToString())); }

/// <summary>
/// A convenient variant type for casting to non-string types
/// </summary>
public ref class CVParamValue
{
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;}
    INTERNAL: CVParamValue(boost::shared_ptr<pwiz::data::CVParam>* base);
              virtual ~CVParamValue();
              !CVParamValue();
    INTERNAL: boost::shared_ptr<pwiz::data::CVParam>* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(CVParamValue^ value) {return gcnew System::String((*value->base_)->value.c_str());};
    static explicit operator float(CVParamValue^ value)
    {
        try { return (*value->base_)->valueAs<float>(); }
        CATCH_AND_FORWARD_CAST(value, "CVParam", "float")
    }
    static operator double(CVParamValue^ value)
    {
        try { return (*value->base_)->valueAs<double>(); }
        CATCH_AND_FORWARD_CAST(value, "CVParam", "double")
    }
    static explicit operator int(CVParamValue^ value)
    {
        try { return (*value->base_)->valueAs<int>(); }
        CATCH_AND_FORWARD_CAST(value, "CVParam", "int")
    }
    static explicit operator System::UInt64(CVParamValue^ value)
    {
        try { return (System::UInt64) (*value->base_)->valueAs<size_t>(); }
        CATCH_AND_FORWARD_CAST(value, "CVParam", "uint-64")
    }
    static explicit operator bool(CVParamValue^ value) {return (*value->base_)->value == "true";}
    CVParamValue^ operator=(System::String^ value) {(*base_)->value = ToStdString(value); return this;} 
};

/// <summary>
/// represents a tag-value pair, where the tag comes from the controlled vocabulary
/// </summary>
public ref class CVParam
{
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;}
    INTERNAL: CVParam(void* base, System::Object^ owner);
              CVParam(void* base);
              virtual ~CVParam();
              !CVParam();
              boost::shared_ptr<pwiz::data::CVParam>* base_;
              pwiz::data::CVParam& base() {return **base_;}
              System::Object^ owner_;
              CVParamValue^ value_;

    public:

    /// <summary>
    /// the enumerated CV term the parameter represents
    /// </summary>
    property CVID cvid
    {
        CVID get() {return (CVID) (*base_)->cvid;}
        void set(CVID value) {(*base_)->cvid = (pwiz::cv::CVID) value;}
    }

    /// <summary>
    /// the value of the term
    /// <para>- stored as string but may represent variant types</para>
    /// <para>- must be empty for controlled value terms</para>
    /// </summary>
    property CVParamValue^ value
    {
        CVParamValue^ get() {return value_;}
    }

    /// <summary>
    /// the enumerated CV term defining the units used to represent the value
    /// </summary>
    property CVID units
    {
        CVID get() {return (CVID) (*base_)->units;}
        void set(CVID value) {(*base_)->units = (pwiz::cv::CVID) value;}
    }

    CVParam(CVID _cvid, bool _value);

    CVParam(CVID _cvid, float _value);
    CVParam(CVID _cvid, double _value);
    CVParam(CVID _cvid, System::Int32 _value);
    //CVParam(CVID _cvid, System::Int64 _value);
    CVParam(CVID _cvid, System::UInt32 _value);
    //CVParam(CVID _cvid, System::UInt64 _value);
    CVParam(CVID _cvid, System::String^ _value);

    CVParam(CVID _cvid, float _value, CVID _units);
    CVParam(CVID _cvid, double _value, CVID _units);
    CVParam(CVID _cvid, System::Int32 _value, CVID _units);
    //CVParam(CVID _cvid, System::Int64 _value, CVID _units);
    CVParam(CVID _cvid, System::UInt32 _value, CVID _units);
    //CVParam(CVID _cvid, System::UInt64 _value, CVID _units);
    CVParam(CVID _cvid, System::String^ _value, CVID _units);

    /// <summary>
    /// constructs a non-valued CVParam
    /// </summary>
    CVParam(CVID _cvid);

    /// <summary>
    /// constructs an empty/null CVParam with CVID_Unknown
    /// </summary>
    CVParam();

    /// <summary>
    /// convenience function to return string for the cvid 
    /// </summary>
    property System::String^ name { System::String^ get() {return gcnew System::String((*base_)->name().c_str());} }

    /// <summary>
    /// convenience function to return string for the units 
    /// </summary>
    property System::String^ unitsName { System::String^ get() {return gcnew System::String((*base_)->unitsName().c_str());} }

    /// <summary>
    /// convenience function to return time in seconds (throws if units not a time unit)
    /// </summary>
    double timeInSeconds();

    bool operator==(CVParam^ that);
    bool operator!=(CVParam^ that);
    bool operator==(CVID that);
    bool operator!=(CVID that);
    bool empty();
};



/// <summary>
/// A convenient variant type for casting to non-string types
/// </summary>
public ref class UserParamValue
{
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;}
    INTERNAL: UserParamValue(boost::shared_ptr<pwiz::data::UserParam>* base);
              virtual ~UserParamValue();
              !UserParamValue();
              boost::shared_ptr<pwiz::data::UserParam>* base_;

    public:
    virtual System::String^ ToString() override {return (System::String^) this;}
    static operator System::String^(UserParamValue^ value) {return gcnew System::String((*value->base_)->value.c_str());}

    static explicit operator float(UserParamValue^ value)
    {
        try { return (*value->base_)->valueAs<float>(); }
        CATCH_AND_FORWARD_CAST(value, "UserParam", "float")
    }
    static operator double(UserParamValue^ value)
    {
        try { return (*value->base_)->valueAs<double>(); }
        CATCH_AND_FORWARD_CAST(value, "UserParam", "double")
    }
    static explicit operator int(UserParamValue^ value)
    {
        try { return (*value->base_)->valueAs<int>(); }
        CATCH_AND_FORWARD_CAST(value, "UserParam", "int")
    }
    static explicit operator System::UInt64(UserParamValue^ value)
    {
        try { return (System::UInt64) (*value->base_)->valueAs<size_t>(); }
        CATCH_AND_FORWARD_CAST(value, "UserParam", "uint-64")
    }

    static explicit operator bool(UserParamValue^ value) {return (*value->base_)->value == "true";}
    UserParamValue^ operator=(System::String^ value) {(*base_)->value = ToStdString(value); return this;} 
};


/// <summary>
/// Uncontrolled user parameters (essentially allowing free text). Before using these, one should verify whether there is an appropriate CV term available, and if so, use the CV term instead
/// </summary>
public ref class UserParam
{
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;}
    INTERNAL: UserParam(void* base, System::Object^ owner);
              UserParam(void* base);
              virtual ~UserParam();
              !UserParam();
              boost::shared_ptr<pwiz::data::UserParam>* base_;
              pwiz::data::UserParam& base() {return **base_;}
              System::Object^ owner_;
              UserParamValue^ value_;

    public:

    /// <summary>
    /// the name for the parameter.
    /// </summary>
    property System::String^ name
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// the value for the parameter, where appropriate.
    /// </summary>
    property UserParamValue^ value
    {
        UserParamValue^ get();
    }

    /// <summary>
    /// the datatype of the parameter, where appropriate (e.g.: xsd:float).
    /// </summary>
    property System::String^ type
    {
        System::String^ get();
        void set(System::String^ value);
    }

    /// <summary>
    /// an optional CV parameter for the unit term associated with the value, if any (e.g. MS_electron_volt).
    /// </summary>
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

    /// <summary>
    /// returns true iff name, value, type, and units are all empty
    /// </summary>
    bool empty();

    /// <summary>
    /// convenience function to return time in seconds (throws if units not a time unit)
    /// </summary>
    double timeInSeconds();

    /// <summary>
    /// returns true iff name, value, type, and units are all pairwise equal
    /// </summary>
    bool operator==(UserParam^ that);

    /// <summary>
    /// returns !(this==that)
    /// </summary>
    bool operator!=(UserParam^ that);

};


ref class ParamGroupList;
ref class CVParamList;
ref class UserParamList;


/// <summary>
/// The base class for elements that may contain cvParams, userParams, or paramGroup references
/// </summary>
public ref class ParamContainer
{
    public:   System::IntPtr void_base() {return (System::IntPtr) base_;}
    INTERNAL: ParamContainer(pwiz::data::ParamContainer* base);
              virtual ~ParamContainer();
              pwiz::data::ParamContainer* base_;
              pwiz::data::ParamContainer& base() {return *base_;}
              System::Object^ owner_;

    public:
    ParamContainer();

    /// <summary>
    /// a collection of references to ParamGroups
    /// </summary>
    property ParamGroupList^ paramGroups
    {
        ParamGroupList^ get();
    }

    /// <summary>
    /// a collection of controlled vocabulary terms
    /// </summary>
    property CVParamList^ cvParams
    {
        CVParamList^ get();
    }

    /// <summary>
    /// a collection of uncontrolled user terms
    /// </summary>
    property UserParamList^ userParams
    {
        UserParamList^ get();
    }

    /// <summary>
    /// Finds cvid in the container
    /// <para>- returns first CVParam result such that (result.cvid == cvid)</para>
    /// <para>- if not found, returns CVParam(CVID_Unknown)</para>
    /// <para>- recursive (looks into paramGroups)</para>
    /// </summary>
    CVParam^ cvParam(CVID cvid);

    /// <summary>
    /// Finds child of cvid in the container
    /// <para>- returns first CVParam result such that (result.cvid IS_A cvid)</para>
    /// <para>- if not found, returns CVParam(CVID_Unknown)</para>
    /// <para>- recursive (looks into paramGroups)</para>
    /// </summary>
    CVParam^ cvParamChild(CVID cvid);

    /// <summary>
    /// Finds all children of cvid in the container
    /// <para>- returns all CVParam results such that (result.cvid IS_A cvid)</para>
    /// <para>- if not found, returns empty list</para>
    /// <para>- recursive (looks into paramGroups)</para>
    /// </summary>
    CVParamList^ cvParamChildren(CVID cvid);

    /// <summary>
    /// returns true iff cvParams contains exact cvid (recursive)
    /// </summary>
    bool hasCVParam(CVID cvid);

    /// <summary>
    /// returns true iff cvParams contains a child IS_A of cvid (recursive)
    /// </summary>
    bool hasCVParamChild(CVID cvid);

    /// <summary>
    /// Finds and returns UserParam with specified name 
    /// <para>- returns UserParam() if name not found</para>
    /// <para>- not recursive: looks only at local userParams</para>
    /// </summary>
    UserParam^ userParam(System::String^ name);

    /// <summary>
    /// set/add a CVParam (not recursive)
    /// </summary>
    void set(CVID cvid);

    /// <summary>
    /// set/add a CVParam (not recursive)
    /// </summary>
    void set(CVID cvid, System::String^ value);

    /// <summary>
    /// set/add a CVParam (not recursive)
    /// </summary>
    void set(CVID cvid, System::String^ value, CVID units);

    /// <summary>
    /// set/add a CVParam (not recursive)
    /// </summary>
    void set(CVID cvid, bool value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::Int32 value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::Int64 value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::UInt32 value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::UInt64 value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::Single value);

    /// <summary>
    /// set/add a CVParam with a value (not recursive)
    /// </summary>
    void set(CVID cvid, System::Double value);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::Int32 value, CVID units);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::Int64 value, CVID units);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::UInt32 value, CVID units);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::UInt64 value, CVID units);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::Single value, CVID units);

    /// <summary>
    /// set/add a CVParam with a value and units (not recursive)
    /// </summary>
    void set(CVID cvid, System::Double value, CVID units);

    /// <summary>
    /// returns true iff the element contains no params or param groups
    /// </summary>
    bool empty();
};


/// <summary>
/// A collection of CVParam and UserParam elements that can be referenced from elsewhere in this mzML document by using the 'paramGroupRef' element in that location to reference the 'id' attribute value of this element. 
/// </summary>
public ref class ParamGroup : public ParamContainer
{
    DEFINE_SHARED_DERIVED_INTERNAL_BASE_CODE(pwiz::data, ParamGroup, ParamContainer);

    public:

    /// <summary>
    /// the identifier with which to reference this ReferenceableParamGroup.
    /// </summary>
    property System::String^ id
    {
        System::String^ get();
        void set(System::String^ value);
    }


    ParamGroup();
    ParamGroup(System::String^ _id);

    /// <summary>
    /// returns true iff the element contains no params or param groups
    /// </summary>
    bool empty() new;
};


// Preprocessed version for debugging
/*public ref class ParamGroupList : public System::Collections::Generic::IList<ParamGroup^> \
{ \
    INTERNAL: ParamGroupList(std::vector<pwiz::data::ParamGroupPtr>* base) : base_(base) {} \
              virtual ~ParamGroupList() {if (base_) delete base_;} \
              std::vector<pwiz::data::ParamGroupPtr>* base_; \
    \
    public: ParamGroupList() : base_(new std::vector<pwiz::data::ParamGroupPtr>()) {} \
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
    virtual void Add(ParamGroup^ item) {base_->push_back(CLI_TO_NATIVE_SHARED_PTR(pwiz::data::ParamGroupPtr, item));} \
    virtual void Clear() {base_->clear();} \
    virtual bool Contains(ParamGroup^ item) {return std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::data::ParamGroupPtr, item)) != base_->end();} \
    virtual void CopyTo(array<ParamGroup^>^ arrayTarget, int arrayIndex) {} \
    virtual bool Remove(ParamGroup^ item) {std::vector<pwiz::data::ParamGroupPtr>::iterator itr = std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::data::ParamGroupPtr, item)); if(itr == base_->end()) return false; base_->erase(itr); return true;} \
    virtual int IndexOf(ParamGroup^ item) {return (int) (std::find(base_->begin(), base_->end(), CLI_TO_NATIVE_SHARED_PTR(pwiz::data::ParamGroupPtr, item))-base_->begin());} \
    virtual void Insert(int index, ParamGroup^ item) {base_->insert(base_->begin() + index, CLI_TO_NATIVE_SHARED_PTR(pwiz::data::ParamGroupPtr, item));} \
    virtual void RemoveAt(int index) {base_->erase(base_->begin() + index);} \
    \
    ref class Enumerator : System::Collections::Generic::IEnumerator<ParamGroup^> \
    { \
        public: Enumerator(std::vector<pwiz::data::ParamGroupPtr>* base) : base_(base) {} \
        INTERNAL: std::vector<pwiz::data::ParamGroupPtr>* base_; \
        INTERNAL: std::vector<pwiz::data::ParamGroupPtr>::iterator* itr_; \
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


/// <summary>
/// A list of CV references; implements the IList&lt;CV&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CVList, pwiz::cv::CV, CV, NATIVE_REFERENCE_TO_CLI, CLI_TO_NATIVE_REFERENCE);


/// <summary>
/// A list of ParamGroup references; implements the IList&lt;ParamGroup&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(ParamGroupList, pwiz::data::ParamGroupPtr, ParamGroup, NATIVE_SHARED_PTR_TO_CLI, CLI_TO_NATIVE_SHARED_PTR);


/// <summary>
/// A list of CVParam references; implements the IList&lt;CVParam&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(CVParamList, pwiz::data::CVParam, CVParam, NATIVE_REFERENCE_TO_CLI, CLI_SHARED_PTR_TO_NATIVE_REFERENCE);


/// <summary>
/// A list of UserParam references; implements the IList&lt;UserParam&gt; interface
/// </summary>
public DEFINE_STD_VECTOR_WRAPPER_FOR_REFERENCE_TYPE(UserParamList, pwiz::data::UserParam, UserParam, NATIVE_REFERENCE_TO_CLI, CLI_SHARED_PTR_TO_NATIVE_REFERENCE);


} // namespace data


namespace util {

public ref struct tribool
{
    enum class value_type
    {
        tribool_false = 0,
        tribool_true = 1,
        tribool_indeterminate = 2
    };

    public:
    value_type value;

    tribool() : value(value_type::tribool_indeterminate) {}
    tribool(bool value) : value(value ? value_type::tribool_true : value_type::tribool_false) {}
    tribool(value_type value) : value(value) {}

    bool operator==(tribool rhs) {return value == rhs.value;}
    operator bool() {return value == value_type::tribool_true;}
    bool operator!() {return value == value_type::tribool_false;}
};

} // namespace util
} // namespace CLI
} // namespace pwiz


#endif // _PARAMTYPES_HPP_CLI_
