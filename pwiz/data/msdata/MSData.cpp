//
// MSData.cpp
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

#define PWIZ_SOURCE

#include "MSData.hpp"
#include <iostream>
#include <stdexcept>
#include <algorithm>
#include <iterator>
#include "boost/lexical_cast.hpp"
#include "boost/format.hpp"
#include "Diff.hpp"

namespace pwiz {
namespace msdata {


using namespace std;
using boost::lexical_cast;
using boost::shared_ptr;


//
// CV
//


PWIZ_API_DECL bool CV::empty() const 
{
    return id.empty() && URI.empty() && fullName.empty() && version.empty();
}


PWIZ_API_DECL bool CV::operator==(const CV& that) const
{
    return id == that.id && fullName == that.fullName && URI == that.URI && version == that.version;
}


PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> result; 
    result.resize(2);

    CV& cv_MS = result[0];
    cv_MS.URI = "http://psidev.info/ms/mzML/psi-ms.obo"; 
    cv_MS.id = "MS";
    cv_MS.fullName = cvinfo(MS_Proteomics_Standards_Initiative_Mass_Spectrometry_Ontology).name;
    cv_MS.version = "1.3.1"; // TODO: generate from cv.hpp

    CV& cv_UO = result[1];
    cv_UO.URI = "http://obo.cvs.sourceforge.net/obo/obo/ontology/phenotype/unit.obo"; 
    cv_UO.id = "UO";
    cv_UO.fullName = "Unit Ontology";
    cv_UO.version = "1.15"; // TODO: generate from cv.hpp

    return result;
}


//
// UserParam
//


PWIZ_API_DECL
UserParam::UserParam(const string& _name, 
                     const string& _value, 
                     const string& _type,
                     CVID _units)
:   name(_name), value(_value), type(_type), units(_units)
{}


PWIZ_API_DECL bool UserParam::empty() const 
{
    return name.empty() && value.empty() && type.empty() && units==CVID_Unknown;
}


PWIZ_API_DECL bool UserParam::operator==(const UserParam& that) const
{
    return (name==that.name && value==that.value && type==that.type && units==that.units);
}


PWIZ_API_DECL bool UserParam::operator!=(const UserParam& that) const
{
    return !operator==(that); 
}


//
// ParamContainer
//


PWIZ_API_DECL CVParam ParamContainer::cvParam(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
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


PWIZ_API_DECL CVParam ParamContainer::cvParamChild(CVID cvid) const
{
    // first look in our own cvParams

    vector<CVParam>::const_iterator it = 
        find_if(cvParams.begin(), cvParams.end(), CVParamIsChildOf(cvid));
   
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


PWIZ_API_DECL bool ParamContainer::hasCVParam(CVID cvid) const
{
    CVParam param = cvParam(cvid);
    return (param.cvid != CVID_Unknown);
}


PWIZ_API_DECL bool ParamContainer::hasCVParamChild(CVID cvid) const
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


PWIZ_API_DECL UserParam ParamContainer::userParam(const string& name) const
{
    vector<UserParam>::const_iterator it = 
        find_if(userParams.begin(), userParams.end(), HasName(name));
    return it!=userParams.end() ? *it : UserParam();
}


PWIZ_API_DECL void ParamContainer::set(CVID cvid, const string& value, CVID units)
{
    vector<CVParam>::iterator it = find_if(cvParams.begin(), cvParams.end(), CVParamIs(cvid));
   
    if (it!=cvParams.end())
    {
        it->value = value;
        it->units = units;
        return;
    }

    cvParams.push_back(CVParam(cvid, value, units));
}


PWIZ_API_DECL bool ParamContainer::empty() const
{
    return paramGroupPtrs.empty() && cvParams.empty() && userParams.empty();
}


PWIZ_API_DECL void ParamContainer::clear()
{
    paramGroupPtrs.clear();
    cvParams.clear();
    userParams.clear();
}


PWIZ_API_DECL bool ParamContainer::operator==(const ParamContainer& that) const
{
    return !Diff<ParamContainer>(*this, that);
}


PWIZ_API_DECL bool ParamContainer::operator!=(const ParamContainer& that) const
{
    return !(*this == that);
}


//
// ParamGroup
//


PWIZ_API_DECL ParamGroup::ParamGroup(const string& _id)
: id(_id) 
{}


PWIZ_API_DECL bool ParamGroup::empty() const 
{
    return id.empty() && ParamContainer::empty();
}


//
// SourceFile
//


PWIZ_API_DECL
SourceFile::SourceFile(const string _id,
                       const string _name,
                       const string _location)
:   id(_id), name(_name), location(_location)
{}


PWIZ_API_DECL bool SourceFile::empty() const
{
    return id.empty() && name.empty() && location.empty() && ParamContainer::empty();
}


//
// FileDescription
//


PWIZ_API_DECL bool FileDescription::empty() const
{
    return fileContent.empty() && sourceFilePtrs.empty() && contacts.empty();
}


//
// Sample
//


PWIZ_API_DECL
Sample::Sample(const string _id,
               const string _name)
:   id(_id), name(_name)
{}


PWIZ_API_DECL bool Sample::empty() const
{
    return id.empty() && name.empty() && ParamContainer::empty();
}


//
// Component
//


PWIZ_API_DECL void Component::define(CVID cvid, int order)
{
    cvParams.clear();
    cvParams.push_back(cvid);
    this->order = order;

    if (cvIsA(cvid, MS_ionization_type))
        type = ComponentType_Source;
    else if (cvIsA(cvid, MS_mass_analyzer_type))
        type = ComponentType_Analyzer;
    else if (cvIsA(cvid, MS_detector_type))
        type = ComponentType_Detector;
    else
        throw runtime_error(("[Component::define] Error determining component type for term \"" + cvinfo(cvid).name + "\""));
}


PWIZ_API_DECL bool Component::empty() const
{
    return order==0 && ParamContainer::empty();
}


//
// ComponentList
//


PWIZ_API_DECL Component& ComponentList::source(size_t index)
{
    size_t count = 0;
    for (size_t i=0; i < size(); ++i)
    {
        Component& c = at(i);
        if (c.type == ComponentType_Source)
        {
            if (count == index)
                return c;
            ++count;
        }
    }
    throw out_of_range((boost::format("[ComponentList::source] Source %d is out of range; only found %d sources") % index % count).str());
}


PWIZ_API_DECL Component& ComponentList::analyzer(size_t index)
{
    size_t count = 0;
    for (size_t i=0; i < size(); ++i)
    {
        Component& c = at(i);
        if (c.type == ComponentType_Analyzer)
        {
            if (count == index)
                return c;
            ++count;
        }
    }
    throw out_of_range((boost::format("[ComponentList::analyzer] Analyzer %d is out of range; only found %d analyzers") % index % count).str());
}


PWIZ_API_DECL Component& ComponentList::detector(size_t index)
{
    size_t count = 0;
    for (size_t i=0; i < size(); ++i)
    {
        Component& c = at(i);
        if (c.type == ComponentType_Detector)
        {
            if (count == index)
                return c;
            ++count;
        }
    }
    throw out_of_range((boost::format("[ComponentList::detector] Detector %d is out of range; only found %d detectors") % index % count).str());
}


PWIZ_API_DECL const Component& ComponentList::source(size_t index) const
{
    return const_cast<ComponentList*>(this)->source(index);
}


PWIZ_API_DECL const Component& ComponentList::analyzer(size_t index) const
{
    return const_cast<ComponentList*>(this)->analyzer(index);
}


PWIZ_API_DECL const Component& ComponentList::detector(size_t index) const
{
    return const_cast<ComponentList*>(this)->detector(index);
}


//
// Software
//


PWIZ_API_DECL bool Software::empty() const
{
    return id.empty() && version.empty() && ParamContainer::empty();
}


//
// This constructor is a workaround for an MSVC internal compiler error.
// For some reason MSVC doesn't like this default argument, but won't say why:
//   const CVParam& _cvParam = CVParam(),
//
PWIZ_API_DECL Software::Software(const string& _id)
:   id(_id)
{}


PWIZ_API_DECL
Software::Software(const string& _id,
                   const CVParam& _param,
                   const string& _version)
:   id(_id), version(_version)
{
    cvParams.push_back(_param);    
}


//
// InstrumentConfiguration
//


PWIZ_API_DECL InstrumentConfiguration::InstrumentConfiguration(const string& _id)
:   id(_id)
{}


PWIZ_API_DECL bool InstrumentConfiguration::empty() const
{
    return id.empty() && componentList.empty() && 
           (!softwarePtr.get() || softwarePtr->empty()) && 
           ParamContainer::empty();
}


//
// ProcessingMethod 
//


PWIZ_API_DECL bool ProcessingMethod::empty() const
{
    return order==0 && 
        (!softwarePtr.get() || softwarePtr->empty()) && 
        ParamContainer::empty();
}


//
// DataProcessing 
//


PWIZ_API_DECL DataProcessing::DataProcessing(const string& _id)
:   id(_id)
{}


PWIZ_API_DECL bool DataProcessing::empty() const
{
    return id.empty() && 
           processingMethods.empty(); 
}


//
// ScanSettings 
//


PWIZ_API_DECL ScanSettings::ScanSettings(const string& _id)
:   id(_id)
{}


PWIZ_API_DECL bool ScanSettings::empty() const
{
    return id.empty() && 
           sourceFilePtrs.empty() &&
           targets.empty();
}



//
// Acquisition
//


PWIZ_API_DECL bool Acquisition::empty() const
{
    return number==0 && 
           (!sourceFilePtr.get() || sourceFilePtr->empty()) && 
           spectrumID.empty() &&
           ParamContainer::empty();
}


//
// AcquisitionList
//


PWIZ_API_DECL bool AcquisitionList::empty() const
{
    return acquisitions.empty() && ParamContainer::empty();
}


//
// SelectedIon
//


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz)
{
    set(MS_m_z, mz);
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, double intensity)
{
    set(MS_m_z, mz);
    set(MS_intensity, intensity);
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, int chargeState)
{
    set(MS_m_z, mz);
    set(MS_charge_state, chargeState);
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, double intensity, int chargeState)
{
    set(MS_m_z, mz);
    set(MS_intensity, intensity);
    set(MS_charge_state, chargeState);
}


//
// Precursor
//


PWIZ_API_DECL
Precursor::Precursor(double mz)
{
    selectedIons.push_back(SelectedIon(mz));
}


PWIZ_API_DECL
Precursor::Precursor(double mz, double intensity)
{
    selectedIons.push_back(SelectedIon(mz, intensity));
}


PWIZ_API_DECL
Precursor::Precursor(double mz, int chargeState)
{
    selectedIons.push_back(SelectedIon(mz, chargeState));
}


PWIZ_API_DECL
Precursor::Precursor(double mz, double intensity, int chargeState)
{
    selectedIons.push_back(SelectedIon(mz, intensity, chargeState));
}


PWIZ_API_DECL bool Precursor::empty() const
{
    return (!sourceFilePtr.get() || sourceFilePtr->empty()) && spectrumID.empty() &&
           isolationWindow.empty() && selectedIons.empty() &&
           activation.empty() && ParamContainer::empty();
}


//
// ScanWindow
//


PWIZ_API_DECL ScanWindow::ScanWindow(double mzLow, double mzHigh)
{
    cvParams.push_back(CVParam(MS_scan_m_z_lower_limit, mzLow));
    cvParams.push_back(CVParam(MS_scan_m_z_upper_limit, mzHigh));
}


//
// Scan
//


PWIZ_API_DECL bool Scan::empty() const
{
    return (!instrumentConfigurationPtr.get() || instrumentConfigurationPtr->empty()) &&
           scanWindows.empty() && 
           ParamContainer::empty();
}


//
// SpectrumDescription
//


PWIZ_API_DECL bool SpectrumDescription::empty() const
{
    return acquisitionList.empty() &&
           precursors.empty() && 
           scan.empty() &&
           ParamContainer::empty();
}


//
// BinaryDataArray
//


PWIZ_API_DECL bool BinaryDataArray::empty() const
{
    return (!dataProcessingPtr.get() || dataProcessingPtr->empty()) && 
           data.empty() && 
           ParamContainer::empty();
}


//
// MZIntensityPair 
//


PWIZ_API_DECL ostream& operator<<(ostream& os, const MZIntensityPair& mzi)
{
    os << "(" << mzi.mz << "," << mzi.intensity << ")";
    return os;
}


PWIZ_API_DECL bool MZIntensityPair::operator==(const MZIntensityPair& that) const
{
    return mz == that.mz && intensity == that.intensity;
}


//
// TimeIntensityPair 
//


PWIZ_API_DECL ostream& operator<<(ostream& os, const TimeIntensityPair& ti)
{
    os << "(" << ti.time << "," << ti.intensity << ")";
    return os;
}


PWIZ_API_DECL bool TimeIntensityPair::operator==(const TimeIntensityPair& that) const
{
    return time == that.time && intensity == that.intensity;
}


//
// Spectrum 
//


PWIZ_API_DECL bool Spectrum::empty() const
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
getMZIntensityArrays(const vector<BinaryDataArrayPtr>& ptrs)
{
    BinaryDataArrayPtr mzArray;
    BinaryDataArrayPtr intensityArray;

    for (vector<BinaryDataArrayPtr>::const_iterator it=ptrs.begin(); it!=ptrs.end(); ++it)
    {
        if ((*it)->hasCVParam(MS_m_z_array) && !mzArray.get()) mzArray = *it;
        if ((*it)->hasCVParam(MS_intensity_array) && !intensityArray.get()) intensityArray = *it;
    }

    return make_pair(mzArray, intensityArray);
}

} // namespace


PWIZ_API_DECL void Spectrum::getMZIntensityPairs(vector<MZIntensityPair>& output) const 
{
    // retrieve and validate m/z and intensity arrays

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
        getMZIntensityArrays(binaryDataArrayPtrs);

    if (!arrays.first.get() || !arrays.second.get()) 
        return;

    if (arrays.first->data.size() != arrays.second->data.size())
        throw runtime_error("[MSData::Spectrum::getMZIntensityPairs()] Sizes do not match.");

    output.clear();
    output.resize(arrays.first->data.size());

    if (!output.empty())
    {
        double* mz = &arrays.first->data[0];
        double* intensity = &arrays.second->data[0];
        MZIntensityPair* start = &output[0];
        for (MZIntensityPair* p = start; p != start + output.size(); ++p) 
        {
            p->mz = *mz++;
            p->intensity = *intensity++;
        }
    }
}


PWIZ_API_DECL void Spectrum::getMZIntensityPairs(MZIntensityPair* output, size_t expectedSize) const
{
    // retrieve and validate m/z and intensity arrays

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
        getMZIntensityArrays(binaryDataArrayPtrs);

    // if either array is absent, do not modify the output
    if (!arrays.first.get() || !arrays.second.get()) 
        return;

    if (arrays.first->data.size() != expectedSize)
        throw runtime_error("[MSData::Spectrum::getMZIntensityPairs()] m/z array invalid size.");

    if (arrays.second->data.size() != expectedSize)
        throw runtime_error("[MSData::Spectrum::getMZIntensityPairs()] Intensity array invalid size.");

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


PWIZ_API_DECL BinaryDataArrayPtr Spectrum::getMZArray() const
{
    for (vector<BinaryDataArrayPtr>::const_iterator it = binaryDataArrayPtrs.begin();
         it != binaryDataArrayPtrs.end();
         ++it)
    {
        if ((*it)->hasCVParam(MS_m_z_array)) return *it;
    }
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL BinaryDataArrayPtr Spectrum::getIntensityArray() const
{
    for (vector<BinaryDataArrayPtr>::const_iterator it = binaryDataArrayPtrs.begin();
         it != binaryDataArrayPtrs.end();
         ++it)
    {
        if ((*it)->hasCVParam(MS_intensity_array)) return *it;
    }
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL void Spectrum::setMZIntensityPairs(const vector<MZIntensityPair>& input, CVID intensityUnit)
{
    // TODO: setting the arrays with an empty vector is a valid use case!
    if (!input.empty())    
        setMZIntensityPairs(&input[0], input.size());
}


PWIZ_API_DECL void Spectrum::setMZIntensityPairs(const MZIntensityPair* input, size_t size, CVID intensityUnits)
{
    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
            getMZIntensityArrays(binaryDataArrayPtrs);

    BinaryDataArrayPtr& bd_mz = arrays.first;
    BinaryDataArrayPtr& bd_intensity = arrays.second;

    if (!bd_mz.get())
    {
        bd_mz = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_m_z_array);
        arrayType.units = MS_m_z;
        bd_mz->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_mz);
    }

    if (!bd_intensity.get())
    {
        bd_intensity = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_intensity_array);
        arrayType.units = intensityUnits;
        bd_intensity->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_intensity);
    }

    bd_mz->data.clear();
    bd_intensity->data.clear();

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


PWIZ_API_DECL void Spectrum::setMZIntensityArrays(const std::vector<double>& mzArray, const std::vector<double>& intensityArray, CVID intensityUnits)
{
    if (mzArray.size() != intensityArray.size())
        throw runtime_error("[MSData::Spectrum::setMZIntensityArrays()] Sizes do not match.");

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
            getMZIntensityArrays(binaryDataArrayPtrs);

    BinaryDataArrayPtr& bd_mz = arrays.first;
    BinaryDataArrayPtr& bd_intensity = arrays.second;

    if (!bd_mz.get())
    {
        bd_mz = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_m_z_array);
        arrayType.units = MS_m_z;
        bd_mz->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_mz);
    }

    if (!bd_intensity.get())
    {
        bd_intensity = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_intensity_array);
        arrayType.units = intensityUnits;
        bd_intensity->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_intensity);
    }

    defaultArrayLength = mzArray.size();

    bd_mz->data.clear();
    bd_intensity->data.clear();

    bd_mz->data.insert(bd_mz->data.end(), mzArray.begin(), mzArray.end());
    bd_intensity->data.insert(bd_intensity->data.end(), intensityArray.begin(), intensityArray.end());
}


//
// Chromatogram 
//


PWIZ_API_DECL bool Chromatogram::empty() const
{
    return index==0 &&
           id.empty() &&
           nativeID.empty() &&
           defaultArrayLength==0 &&
           (!dataProcessingPtr.get() || dataProcessingPtr->empty()) && 
           binaryDataArrayPtrs.empty() &&
           ParamContainer::empty();
}


namespace {

pair<BinaryDataArrayPtr,BinaryDataArrayPtr> 
getTimeIntensityArrays(const vector<BinaryDataArrayPtr>& ptrs, size_t expectedSize)
{
    BinaryDataArrayPtr timeArray;
    BinaryDataArrayPtr intensityArray;

    for (vector<BinaryDataArrayPtr>::const_iterator it=ptrs.begin(); it!=ptrs.end(); ++it)
    {
        if ((*it)->hasCVParam(MS_time_array) && !timeArray.get()) timeArray = *it;
        if ((*it)->hasCVParam(MS_intensity_array) && !intensityArray.get()) intensityArray = *it;
    }

    if (!timeArray.get()) 
        throw runtime_error("[MSData::getTimeIntensityArrays()] Time array not found.");

    if (!intensityArray.get()) 
        throw runtime_error("[MSData::getTimeIntensityArrays()] Intensity array not found.");

    if (timeArray->data.size() != expectedSize)
        throw runtime_error("[MSData::getTimeIntensityArrays()] Time array invalid size.");
         
    if (intensityArray->data.size() != expectedSize)
        throw runtime_error("[MSData::getTimeIntensityArrays()] Intensity array invalid size.");
         
    return make_pair(timeArray, intensityArray);
}

} // namespace


PWIZ_API_DECL void Chromatogram::getTimeIntensityPairs(vector<TimeIntensityPair>& output) const 
{
    output.clear();
    output.resize(defaultArrayLength);
    if (!output.empty())
        getTimeIntensityPairs(&output[0], output.size());
}


PWIZ_API_DECL void Chromatogram::getTimeIntensityPairs(TimeIntensityPair* output, size_t expectedSize) const
{
    // retrieve and validate time and intensity arrays

    if (expectedSize == 0) return;

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
        getTimeIntensityArrays(binaryDataArrayPtrs, expectedSize); 

    if (!output)
        throw runtime_error("[MSData::Chromatogram::getTimeIntensityPairs()] Null output buffer.");

    // copy data into return buffer

    double* time = &arrays.first->data[0];
    double* intensity = &arrays.second->data[0];
    for (TimeIntensityPair* p=output; p!=output+expectedSize; ++p) 
    {
        p->time = *time++;
        p->intensity = *intensity++;
    }
}


PWIZ_API_DECL void Chromatogram::setTimeIntensityPairs(const vector<TimeIntensityPair>& input)
{
    if (!input.empty())    
        setTimeIntensityPairs(&input[0], input.size());
}


PWIZ_API_DECL void Chromatogram::setTimeIntensityPairs(const TimeIntensityPair* input, size_t size)
{
    BinaryDataArrayPtr bd_time(new BinaryDataArray);
    BinaryDataArrayPtr bd_intensity(new BinaryDataArray);

    binaryDataArrayPtrs.clear();
    binaryDataArrayPtrs.push_back(bd_time);
    binaryDataArrayPtrs.push_back(bd_intensity);

    bd_time->cvParams.push_back(MS_time_array);
    bd_intensity->cvParams.push_back(MS_intensity_array);

    bd_time->data.resize(size);
    bd_intensity->data.resize(size);
    defaultArrayLength = size;

    if (size == 0) return;

    double* time = &bd_time->data[0];
    double* intensity = &bd_intensity->data[0];
    for (const TimeIntensityPair* p=input; p!=input+size; ++p)
    {
        *time++ = p->time;
        *intensity++ = p->intensity;
    }
}


//
// SpectrumList (default implementations)
//


PWIZ_API_DECL bool SpectrumList::empty() const {return size()==0;}


PWIZ_API_DECL size_t SpectrumList::find(const string& id) const
{
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).id == id) 
            return index;
    return size();
}


PWIZ_API_DECL size_t SpectrumList::findNative(const string& nativeID) const
{
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).nativeID == nativeID) 
            return index;
    return size();
}


PWIZ_API_DECL IndexList SpectrumList::findSpotID(const string& spotID) const
{
    IndexList result;
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).spotID == spotID) 
            result.push_back(index);
    return result;
}


PWIZ_API_DECL const shared_ptr<const DataProcessing> SpectrumList::dataProcessingPtr() const
{
    return shared_ptr<const DataProcessing>();
}


//
// SpectrumListSimple
//


PWIZ_API_DECL const SpectrumIdentity& SpectrumListSimple::spectrumIdentity(size_t index) const
{
    return *spectrum(index, false);
}


PWIZ_API_DECL SpectrumPtr SpectrumListSimple::spectrum(size_t index, bool getBinaryData) const
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
// ChromatogramList (default implementations)
//


PWIZ_API_DECL bool ChromatogramList::empty() const {return size()==0;}


PWIZ_API_DECL size_t ChromatogramList::find(const string& id) const
{
    for (size_t index=0; index<size(); ++index)
        if (chromatogramIdentity(index).id == id) 
            return index;
    return size();
}


PWIZ_API_DECL size_t ChromatogramList::findNative(const string& nativeID) const
{
    for (size_t index=0; index<size(); ++index)
        if (chromatogramIdentity(index).nativeID == nativeID) 
            return index;
    return size();
}


//
// ChromatogramListSimple
//


PWIZ_API_DECL const ChromatogramIdentity& ChromatogramListSimple::chromatogramIdentity(size_t index) const
{
    return *chromatogram(index, false);
}


PWIZ_API_DECL ChromatogramPtr ChromatogramListSimple::chromatogram(size_t index, bool getBinaryData) const
{
    // validate index
    if (index > size())
        throw runtime_error("[MSData::ChromatogramListSimple::chromatogram()] Invalid index.");

    // validate Chromatogram* 
    if (!chromatograms[index].get())
        throw runtime_error("[MSData::ChromatogramListSimple::chromatogram()] Null ChromatogramPtr.");

    return chromatograms[index];
} 


//
// Run
//


PWIZ_API_DECL bool Run::empty() const
{
    return id.empty() &&
           (!defaultInstrumentConfigurationPtr.get() || defaultInstrumentConfigurationPtr->empty()) &&
           (!samplePtr.get() || samplePtr->empty()) &&
           startTimeStamp.empty() &&
           sourceFilePtrs.empty() &&
           (!spectrumListPtr.get() || spectrumListPtr->empty()) &&
           (!chromatogramListPtr.get() || chromatogramListPtr->empty()) &&
           ParamContainer::empty();
}


//
// MSData
//

PWIZ_API_DECL MSData::MSData() {}
PWIZ_API_DECL MSData::~MSData() {}

PWIZ_API_DECL bool MSData::empty() const
{
    return accession.empty() &&
           id.empty() &&
           version.empty() &&
           cvs.empty() &&
           fileDescription.empty() &&
           paramGroupPtrs.empty() &&
           samplePtrs.empty() &&
           softwarePtrs.empty() &&
           scanSettingsPtrs.empty() &&
           instrumentConfigurationPtrs.empty() && 
           dataProcessingPtrs.empty() &&
           run.empty();
}


} // namespace msdata
} // namespace pwiz


