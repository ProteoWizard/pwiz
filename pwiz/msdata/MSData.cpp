//
// MSData.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "MSData.hpp"
#include <iostream>
#include <stdexcept>
#include <algorithm>
#include <iterator>
#include "boost/lexical_cast.hpp"


namespace pwiz {
namespace msdata {


using namespace std;
using boost::lexical_cast;


//
// CV
//


bool CV::empty() const 
{
    return URI.empty() && cvLabel.empty() && fullName.empty() && version.empty();
}


//
// UserParam
//


UserParam::UserParam(const string& _name, 
                     const string& _value, 
                     const string& _type)
:   name(_name), value(_value), type(_type)
{}


bool UserParam::empty() const 
{
    return name.empty() && value.empty() && type.empty();
}


bool UserParam::operator==(const UserParam& that) const
{
    return (name==that.name && value==that.value && type==that.type);
}


bool UserParam::operator!=(const UserParam& that) const
{
    return !operator==(that); 
}


//
// ParamContainer
//


CVParam ParamContainer::cvParam(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParam::Is(cvid));
   
    if (it!=cvParams.end()) return *it;

    // then recurse into paramGroupPtrs

    for (vector<ParamGroupPtr>::const_iterator jt=paramGroupPtrs.begin();
         jt!=paramGroupPtrs.end(); ++jt)
    {
        CVParam result = jt->get() ? (*jt)->cvParam(cvid) : CVParam();
        if (result.cvid != CVID_Unknown)
            return result;
    }

    return CVParam();
}


CVParam ParamContainer::cvParamChild(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParam::IsChildOf(cvid));
   
    if (it!=cvParams.end()) return *it;

    // then recurse into paramGroupPtrs

    for (vector<ParamGroupPtr>::const_iterator jt=paramGroupPtrs.begin();
         jt!=paramGroupPtrs.end(); ++jt)
    {
        CVParam result = jt->get() ? (*jt)->cvParamChild(cvid) : CVParam();
        if (result.cvid != CVID_Unknown)
            return result;
    }

    return CVParam();
}


bool ParamContainer::hasCVParam(CVID cvid) const
{
    CVParam param = cvParam(cvid);
    return (param.cvid != CVID_Unknown);
}


bool ParamContainer::hasCVParamChild(CVID cvid) const
{
    CVParam param = cvParamChild(cvid);
    return (param.cvid != CVID_Unknown);
}


namespace {
struct HasName
{
    string name_;
    HasName(const string& name) : name_(name) {}
    bool operator()(const UserParam& userParam) {return name_ == userParam.name;}
};
} // namespace


UserParam ParamContainer::userParam(const string& name) const
{
    vector<UserParam>::const_iterator it = 
        find_if(userParams.begin(), userParams.end(), HasName(name));
    return it!=userParams.end() ? *it : UserParam();
}


void ParamContainer::set(CVID cvid, const string& value, CVID units)
{
    vector<CVParam>::iterator it = find_if(cvParams.begin(), cvParams.end(), CVParam::Is(cvid));
   
    if (it!=cvParams.end())
    {
        it->value = value;
        it->units = units;
        return;
    }

    cvParams.push_back(CVParam(cvid, value, units));
}


bool ParamContainer::empty() const
{
    return paramGroupPtrs.empty() && cvParams.empty() && userParams.empty();
}


//
// ParamGroup
//


ParamGroup::ParamGroup(const string& _id)
: id(_id) 
{}


bool ParamGroup::empty() const 
{
    return id.empty() && ParamContainer::empty();
}


//
// SourceFile
//


SourceFile::SourceFile(const string _id,
                       const string _name,
                       const string _location)
:   id(_id), name(_name), location(_location)
{}


bool SourceFile::empty() const
{
    return id.empty() && name.empty() && location.empty() && ParamContainer::empty();
}


//
// FileDescription
//


bool FileDescription::empty() const
{
    return fileContent.empty() && sourceFilePtrs.empty() && contacts.empty();
}


//
// Sample
//


Sample::Sample(const string _id,
               const string _name)
:   id(_id), name(_name)
{}


bool Sample::empty() const
{
    return id.empty() && name.empty() && ParamContainer::empty();
}


//
// Component
//


bool Component::empty() const
{
    return order==0 && ParamContainer::empty();
}


//
// ComponentList
//


bool ComponentList::empty() const
{
    return source.empty() && analyzer.empty() && detector.empty();
}


//
// Software
//


bool Software::empty() const
{
    return id.empty() && softwareParam.cvid==CVID_Unknown && 
           softwareParamVersion.empty();
}


//
// This constructor is a workaround for an MSVC internal compiler error.
// For some reason MSVC doesn't like this default argument, but won't say why:
//   const CVParam& _cvParam = CVParam(),
//
Software::Software(const string& _id)
:   id(_id)
{}


Software::Software(const string& _id,
                   const CVParam& _softwareParam,
                   const string& _softwareParamVersion)
:   id(_id), softwareParam(_softwareParam), softwareParamVersion(_softwareParamVersion)
{}


//
// Instrument
//


Instrument::Instrument(const string& _id)
:   id(_id)
{}


bool Instrument::empty() const
{
    return id.empty() && componentList.empty() && 
           (!softwarePtr.get() || softwarePtr->empty()) && 
           ParamContainer::empty();
}


//
// ProcessingMethod 
//


bool ProcessingMethod::empty() const
{
    return order==0 && ParamContainer::empty();
}


//
// DataProcessing 
//


DataProcessing::DataProcessing(const string& _id)
:   id(_id)
{}


bool DataProcessing::empty() const
{
    return id.empty() && 
           (!softwarePtr.get() || softwarePtr->empty()) && 
           processingMethods.empty(); 
}


//
// Acquisition
//


bool Acquisition::empty() const
{
    return number==0 && 
           (!sourceFilePtr.get() || sourceFilePtr->empty()) && 
           spectrumID.empty() &&
           ParamContainer::empty();
}


//
// AcquisitionList
//


bool AcquisitionList::empty() const
{
    return acquisitions.empty() && ParamContainer::empty();
}


//
// Precursor
//


bool Precursor::empty() const
{
    return spectrumID.empty() && ionSelection.empty() &&
           activation.empty() && ParamContainer::empty();
}


//
// SelectionWindow
//


SelectionWindow::SelectionWindow(double mzLow, double mzHigh)
{
    cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, mzLow));
    cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, mzHigh));
}


//
// Scan
//


bool Scan::empty() const
{
    return (!instrumentPtr.get() || instrumentPtr->empty()) &&
           selectionWindows.empty() && 
           ParamContainer::empty();
}


//
// SpectrumDescription
//


bool SpectrumDescription::empty() const
{
    return acquisitionList.empty() &&
           precursors.empty() && 
           scan.empty() &&
           ParamContainer::empty();
}


//
// BinaryDataArray
//


bool BinaryDataArray::empty() const
{
    return (!dataProcessingPtr.get() || dataProcessingPtr->empty()) && 
           data.empty() && 
           ParamContainer::empty();
}


//
// MZIntensityPair 
//


ostream& operator<<(ostream& os, const MZIntensityPair& mzi)
{
    os << "(" << mzi.mz << "," << mzi.intensity << ")";
    return os;
}


//
// Spectrum 
//


bool Spectrum::empty() const
{
    return index==0 &&
           id.empty() &&
           nativeID.empty() &&
           defaultArrayLength==0 &&
           (!dataProcessingPtr.get() || dataProcessingPtr->empty()) && 
           (!sourceFilePtr.get() || sourceFilePtr->empty()) && 
           spectrumDescription.empty() &&
           binaryDataArrayPtrs.empty() &&
           ParamContainer::empty();
}


namespace {

pair<BinaryDataArrayPtr,BinaryDataArrayPtr> 
getMZIntensityArrays(const vector<BinaryDataArrayPtr>& ptrs, size_t expectedSize)
{
    BinaryDataArrayPtr mzArray;
    BinaryDataArrayPtr intensityArray;

    for (vector<BinaryDataArrayPtr>::const_iterator it=ptrs.begin(); it!=ptrs.end(); ++it)
    {
        if ((*it)->hasCVParam(MS_m_z_array) && !mzArray.get()) mzArray = *it;
        if ((*it)->hasCVParam(MS_intensity_array) && !intensityArray.get()) intensityArray = *it;
    }

    if (!mzArray.get()) 
        throw runtime_error("[MSData::getMZIntensityArrays()] m/z array not found.");

    if (!intensityArray.get()) 
        throw runtime_error("[MSData::getMZIntensityArrays()] Intensity array not found.");

    if (mzArray->data.size() != expectedSize)
        throw runtime_error("[MSData::getMZIntensityArrays()] m/z array invalid size.");
         
    if (intensityArray->data.size() != expectedSize)
        throw runtime_error("[MSData::getMZIntensityArrays()] Intensity array invalid size.");
         
    return make_pair(mzArray, intensityArray);
}

} // namespace


void Spectrum::getMZIntensityPairs(vector<MZIntensityPair>& output) const 
{
    output.clear();
    output.resize(defaultArrayLength);
    getMZIntensityPairs(!output.empty() ? &output[0] : 0, defaultArrayLength);
}


void Spectrum::getMZIntensityPairs(MZIntensityPair* output, size_t expectedSize) const
{
    // retrieve and validate m/z and intensity arrays

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
        getMZIntensityArrays(binaryDataArrayPtrs, expectedSize); 

    if (expectedSize == 0) return;

    if (!output)
        throw runtime_error("[MSData::Spectrum::getMZIntensityPairs()] Null output buffer.");

    // copy data into return buffer

    double* mz = &arrays.first->data[0];
    double* intensity = &arrays.second->data[0];
    for (MZIntensityPair* p=output; p!=output+expectedSize; ++p) 
    {
        p->mz = *mz++;
        p->intensity = *intensity++;
    }
}


void Spectrum::setMZIntensityPairs(const vector<MZIntensityPair>& input)
{
    if (!input.empty())    
        setMZIntensityPairs(&input[0], input.size());
}


void Spectrum::setMZIntensityPairs(const MZIntensityPair* input, size_t size)
{
    BinaryDataArrayPtr bd_mz(new BinaryDataArray);
    BinaryDataArrayPtr bd_intensity(new BinaryDataArray);

    binaryDataArrayPtrs.clear();
    binaryDataArrayPtrs.push_back(bd_mz);
    binaryDataArrayPtrs.push_back(bd_intensity);

    bd_mz->cvParams.push_back(MS_m_z_array);
    bd_intensity->cvParams.push_back(MS_intensity_array);

    bd_mz->data.resize(size);
    bd_intensity->data.resize(size);
    defaultArrayLength = size;

    if (size == 0) return;

    double* mz = &bd_mz->data[0];
    double* intensity = &bd_intensity->data[0];
    for (const MZIntensityPair* p=input; p!=input+size; ++p)
    {
        *mz++ = p->mz;
        *intensity++ = p->intensity;
    }
}


//
// SpectrumList (default implementations)
//


bool SpectrumList::empty() const {return size()==0;}


size_t SpectrumList::find(const string& id) const
{
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).id == id) 
            return index;
    return size();
}


size_t SpectrumList::findNative(const string& nativeID) const
{
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).nativeID == nativeID) 
            return index;
    return size();
}


//
// SpectrumListSimple
//


const SpectrumIdentity& SpectrumListSimple::spectrumIdentity(size_t index) const
{
    return *spectrum(index, false);
}


SpectrumPtr SpectrumListSimple::spectrum(size_t index, bool getBinaryData) const
{
    // validate index
    if (index > size())
        throw runtime_error("[MSData::SpectrumListSimple::spectrum()] Invalid index.");

    // validate Spectrum* 
    if (!spectra[index].get())
        throw runtime_error("[MSData::SpectrumListSimple::spectrum()] Null SpectrumPtr.");

    return spectra[index];
} 


//
// Run
//


bool Run::empty() const
{
    return id.empty() &&
           (!instrumentPtr.get() || instrumentPtr->empty()) &&
           (!samplePtr.get() || samplePtr->empty()) &&
           startTimeStamp.empty() &&
           sourceFilePtrs.empty() &&
           (!spectrumListPtr.get() || spectrumListPtr->empty()) &&
           ParamContainer::empty();
}


//
// MSData
//


bool MSData::empty() const
{
    return accession.empty() &&
           id.empty() &&
           version.empty() &&
           cvs.empty() &&
           fileDescription.empty() &&
           paramGroupPtrs.empty() &&
           samplePtrs.empty() &&
           instrumentPtrs.empty() && 
           softwarePtrs.empty() &&
           dataProcessingPtrs.empty() &&
           run.empty();
}


} // namespace msdata
} // namespace pwiz


