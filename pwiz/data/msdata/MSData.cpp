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

#define PWIZ_SOURCE

#include "MSData.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/lexical_cast.hpp>
#include "Diff.hpp"

namespace pwiz {
namespace msdata {


using namespace pwiz::cv;
using namespace pwiz::data;


PWIZ_API_DECL vector<CV> defaultCVList()
{
    vector<CV> result; 
    result.resize(2);

    result[0] = cv::cv("MS");
    result[1] = cv::cv("UO");

    return result;
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
        throw runtime_error(("[Component::define] Error determining component type for term \"" + cvTermInfo(cvid).name + "\""));
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
// SelectedIon
//


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz)
{
    set(MS_selected_ion_m_z, mz, MS_m_z);
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, double intensity, CVID intensityUnit)
{
    set(MS_selected_ion_m_z, mz, MS_m_z);
    set(MS_peak_intensity, intensity, intensityUnit);
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, int chargeState)
{
    set(MS_selected_ion_m_z, mz, MS_m_z);
    set(MS_charge_state, boost::lexical_cast<string>(chargeState));
}


PWIZ_API_DECL
SelectedIon::SelectedIon(double mz, double intensity, int chargeState, CVID intensityUnit)
{
    set(MS_selected_ion_m_z, mz, MS_m_z);
    set(MS_peak_intensity, intensity, intensityUnit);
    set(MS_charge_state, boost::lexical_cast<string>(chargeState));
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
Precursor::Precursor(double mz, double intensity, CVID intensityUnit)
{
    selectedIons.push_back(SelectedIon(mz, intensity, intensityUnit));
}


PWIZ_API_DECL
Precursor::Precursor(double mz, int chargeState)
{
    selectedIons.push_back(SelectedIon(mz, chargeState));
}


PWIZ_API_DECL
Precursor::Precursor(double mz, double intensity, int chargeState, CVID intensityUnit)
{
    selectedIons.push_back(SelectedIon(mz, intensity, chargeState, intensityUnit));
}


PWIZ_API_DECL bool Precursor::empty() const
{
    return (!sourceFilePtr.get() || sourceFilePtr->empty()) && spectrumID.empty() &&
           isolationWindow.empty() && selectedIons.empty() &&
           activation.empty() && ParamContainer::empty();
}


//
// Product
//


PWIZ_API_DECL bool Product::empty() const
{
    return (isolationWindow.empty());
}

PWIZ_API_DECL bool Product::operator==(const Product& that) const
{
    return isolationWindow == that.isolationWindow;
}


//
// ScanWindow
//


PWIZ_API_DECL ScanWindow::ScanWindow(double low, double high, CVID unit)
{
    set(MS_scan_window_lower_limit, low, unit);
    set(MS_scan_window_upper_limit, high, unit);
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
// ScanList
//


PWIZ_API_DECL bool ScanList::empty() const
{
    return scans.empty() && ParamContainer::empty();
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


PWIZ_API_DECL bool IntegerDataArray::empty() const
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
// id
//


namespace id {


pair<string,string> stringToPair(const string& nameValuePair)
{
    string::size_type indexEquals = nameValuePair.find('=');
    if (indexEquals==string::npos || indexEquals+1>=nameValuePair.size())
        throw runtime_error(("[MSData::stringToPair] Bad format: " + nameValuePair).c_str());

    return make_pair(nameValuePair.substr(0,indexEquals), nameValuePair.substr(indexEquals+1)); 
}


PWIZ_API_DECL map<string,string> parse(const string& id)
{
    if (id.empty())
        throw runtime_error("[MSData::parse] Empty id");

    vector<string> pairs;
    boost::split(pairs, id, boost::is_any_of(" \t\n\r"));

    map<string,string> result;
    transform(pairs.begin(), pairs.end(), inserter(result, result.end()), stringToPair);

    return result;
}


PWIZ_API_DECL string value(const string& id, const string& name)
{
    map<string,string> temp = parse(id);
    return temp[name];
}


PWIZ_API_DECL CVID getDefaultNativeIDFormat(const MSData& msd)
{
    CVID result = CVID_Unknown;
    if (msd.run.defaultSourceFilePtr.get())
        result = msd.run.defaultSourceFilePtr->cvParamChild(MS_nativeID_format).cvid;
    else if (!msd.fileDescription.sourceFilePtrs.empty())
        result = msd.fileDescription.sourceFilePtrs[0]->cvParamChild(MS_nativeID_format).cvid;
    if (( CVID_Unknown == result ) && (msd.version().find("1.0") == 0))
        result = MS_scan_number_only_nativeID_format;
    return result;
}


PWIZ_API_DECL string translateScanNumberToNativeID(CVID nativeIdFormat, const string& scanNumber)
{
    switch (nativeIdFormat)
    {
        case MS_Thermo_nativeID_format:
            return "controllerType=0 controllerNumber=1 scan=" + scanNumber;

        case MS_spectrum_identifier_nativeID_format:
            return "spectrum=" + scanNumber;

        case MS_multiple_peak_list_nativeID_format:
            return "index=" + scanNumber;

        case MS_Agilent_MassHunter_nativeID_format:
            return "scanId=" + scanNumber;

        case MS_Bruker_Agilent_YEP_nativeID_format:
        case MS_Bruker_BAF_nativeID_format:
        case MS_scan_number_only_nativeID_format:
            return "scan=" + scanNumber;

        default:
            return "";
    }
}

PWIZ_API_DECL string translateNativeIDToScanNumber(CVID nativeIdFormat, const string& id)
{
    switch (nativeIdFormat)
    {
        case MS_spectrum_identifier_nativeID_format: // mzData
            return value(id, "spectrum");

        case MS_multiple_peak_list_nativeID_format: // MGF
            return value(id, "index");

        case MS_Agilent_MassHunter_nativeID_format:
            return value(id, "scanId");

        case MS_Thermo_nativeID_format:
            // conversion from Thermo nativeIDs assumes default controller information
            if (id.find("controllerType=0 controllerNumber=1") != 0)
                return "";

            // fall through to get scan

        case MS_Bruker_Agilent_YEP_nativeID_format:
        case MS_Bruker_BAF_nativeID_format:
        case MS_scan_number_only_nativeID_format:
            return value(id, "scan");

        default:
            if (bal::starts_with(id, "scan=")) return value(id, "scan");
            else if (bal::starts_with(id, "index=")) return value(id, "index");
            return "";
    }
}

string abbreviate(const string& id, char delimiter /*= '.'*/)
{
    string result;
    size_t indexEquals, indexSpace = 0;
    do
    {
        if (!result.empty())
            result += delimiter;

        indexEquals = id.find('=', indexSpace);
        if (indexEquals == string::npos)
            throw runtime_error("[MSData::id::abbreviate] Bad format: " + id);

        indexSpace = id.find(' ', indexEquals+1);
        if (indexSpace == string::npos)
            result += id.substr(indexEquals+1);
        else
            result += id.substr(indexEquals+1, indexSpace-indexEquals-1);
    } while (indexSpace != string::npos);

    return result;
}


} // namespace id


//
// Spectrum 
//


PWIZ_API_DECL bool Spectrum::empty() const
{
    return index==IDENTITY_INDEX_NONE &&
           id.empty() &&
           defaultArrayLength==0 &&
           (!dataProcessingPtr.get() || dataProcessingPtr->empty()) && 
           (!sourceFilePtr.get() || sourceFilePtr->empty()) && 
           scanList.empty() &&
           precursors.empty() && 
           products.empty() && 
           binaryDataArrayPtrs.empty() &&
           ParamContainer::empty();
}

PWIZ_API_DECL bool Spectrum::hasBinaryData() const
{
    return (binaryDataArrayPtrs.size() &&
            binaryDataArrayPtrs[0] &&
            !binaryDataArrayPtrs[0]->data.empty())
           ||
           (integerDataArrayPtrs.size() &&
            integerDataArrayPtrs[0] &&
            !integerDataArrayPtrs[0]->data.empty());
};

namespace {

pair<BinaryDataArrayPtr,BinaryDataArrayPtr> 
getMZIntensityArrays(const vector<BinaryDataArrayPtr>& ptrs)
{
    BinaryDataArrayPtr mzArray;
    BinaryDataArrayPtr intensityArray;

    for (vector<BinaryDataArrayPtr>::const_iterator it=ptrs.begin(); it!=ptrs.end(); ++it)
    {
        if (((*it)->hasCVParam(MS_m_z_array) || (*it)->hasCVParam(MS_wavelength_array)) && !mzArray.get()) mzArray = *it;
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

PWIZ_API_DECL BinaryDataArrayPtr Spectrum::getArrayByCVID(CVID arrayType) const
{
    for (const auto& arrayPtr : binaryDataArrayPtrs) 
        if (arrayPtr->hasCVParam(arrayType)) return arrayPtr;
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL BinaryDataArrayPtr Spectrum::getMZArray() const
{
    for (vector<BinaryDataArrayPtr>::const_iterator it = binaryDataArrayPtrs.begin();
         it != binaryDataArrayPtrs.end();
         ++it)
    {
        if ((*it)->hasCVParam(MS_m_z_array) || (*it)->hasCVParam(MS_wavelength_array)) return *it;
    }
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL BinaryDataArrayPtr Spectrum::getIntensityArray() const
{
    return getArrayByCVID(MS_intensity_array);
}


PWIZ_API_DECL void Spectrum::setMZIntensityPairs(const vector<MZIntensityPair>& input, CVID intensityUnits)
{
    if (!input.empty())    
        setMZIntensityPairs(&input[0], input.size(), intensityUnits);
    else
        setMZIntensityArrays(vector<double>(), vector<double>(), intensityUnits);
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


/// set m/z and intensity arrays separately (they must be the same size) by swapping the vector contents
/// this allows for a more nearly zero copy setup.  Contents of mzArray and intensityArray are undefined after calling.
PWIZ_API_DECL void Spectrum::swapMZIntensityArrays(pwiz::util::BinaryData<double>& mzArray, pwiz::util::BinaryData<double>& intensityArray, CVID intensityUnits)
{
    if (mzArray.size() != intensityArray.size())
        throw runtime_error("[MSData::Spectrum::swapMZIntensityArrays()] Sizes do not match.");

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

    bd_mz->data.swap(mzArray);
    bd_intensity->data.swap(intensityArray);
}


PWIZ_API_DECL void Spectrum::setMZIntensityArrays(const vector<double>& mzArray, const vector<double>& intensityArray, CVID intensityUnits)
{
    pwiz::util::BinaryData<double> mz, intensity;
    mz = mzArray, intensity = intensityArray;
    setMZIntensityArrays(mz, intensity, intensityUnits);
}


PWIZ_API_DECL void Spectrum::setMZIntensityArrays(const pwiz::util::BinaryData<double>& mzArray, const pwiz::util::BinaryData<double>& intensityArray, CVID intensityUnits)
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
    return index==IDENTITY_INDEX_NONE &&
           id.empty() &&
           defaultArrayLength==0 &&
           (!dataProcessingPtr.get() || dataProcessingPtr->empty()) &&
           precursor.empty() &&
           product.empty() &&
           binaryDataArrayPtrs.empty() &&
           ParamContainer::empty();
}


namespace {

pair<BinaryDataArrayPtr,BinaryDataArrayPtr> 
getTimeIntensityArrays(const vector<BinaryDataArrayPtr>& ptrs)
{
    BinaryDataArrayPtr timeArray;
    BinaryDataArrayPtr intensityArray;

    for (vector<BinaryDataArrayPtr>::const_iterator it=ptrs.begin(); it!=ptrs.end(); ++it)
    {
        if ((*it)->hasCVParam(MS_time_array) && !timeArray.get()) timeArray = *it;
        if ((*it)->hasCVParam(MS_intensity_array) && !intensityArray.get()) intensityArray = *it;
    }

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
        getTimeIntensityArrays(binaryDataArrayPtrs); 

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


PWIZ_API_DECL BinaryDataArrayPtr Chromatogram::getTimeArray() const
{
    for (vector<BinaryDataArrayPtr>::const_iterator it = binaryDataArrayPtrs.begin();
         it != binaryDataArrayPtrs.end();
         ++it)
    {
        if ((*it)->hasCVParam(MS_time_array)) return *it;
    }
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL BinaryDataArrayPtr Chromatogram::getIntensityArray() const
{
    for (vector<BinaryDataArrayPtr>::const_iterator it = binaryDataArrayPtrs.begin();
         it != binaryDataArrayPtrs.end();
         ++it)
    {
        if ((*it)->hasCVParam(MS_intensity_array)) return *it;
    }
    return BinaryDataArrayPtr();
}


PWIZ_API_DECL void Chromatogram::setTimeIntensityPairs(const vector<TimeIntensityPair>& input, CVID timeUnits, CVID intensityUnits)
{
    if (!input.empty())    
        setTimeIntensityPairs(&input[0], input.size(), timeUnits, intensityUnits);
}


PWIZ_API_DECL void Chromatogram::setTimeIntensityPairs(const TimeIntensityPair* input, size_t size, CVID timeUnits, CVID intensityUnits)
{
    BinaryDataArrayPtr bd_time(new BinaryDataArray);
    BinaryDataArrayPtr bd_intensity(new BinaryDataArray);

    binaryDataArrayPtrs.clear();
    binaryDataArrayPtrs.push_back(bd_time);
    binaryDataArrayPtrs.push_back(bd_intensity);

    bd_time->cvParams.push_back(CVParam(MS_time_array, "", timeUnits));
    bd_intensity->cvParams.push_back(CVParam(MS_intensity_array, "", intensityUnits));

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


PWIZ_API_DECL void Chromatogram::setTimeIntensityArrays(const std::vector<double>& timeArray, const std::vector<double>& intensityArray, CVID timeUnits, CVID intensityUnits)
{
    if (timeArray.size() != intensityArray.size())
        throw runtime_error("[MSData::Chromatogram::setTimeIntensityArrays()] Sizes do not match.");

    pair<BinaryDataArrayPtr,BinaryDataArrayPtr> arrays = 
            getTimeIntensityArrays(binaryDataArrayPtrs);

    BinaryDataArrayPtr& bd_time = arrays.first;
    BinaryDataArrayPtr& bd_intensity = arrays.second;

    if (!bd_time.get())
    {
        bd_time = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_time_array);
        arrayType.units = timeUnits;
        bd_time->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_time);
    }

    if (!bd_intensity.get())
    {
        bd_intensity = BinaryDataArrayPtr(new BinaryDataArray);
        CVParam arrayType(MS_intensity_array);
        arrayType.units = intensityUnits;
        bd_intensity->cvParams.push_back(arrayType);
        binaryDataArrayPtrs.push_back(bd_intensity);
    }

    defaultArrayLength = timeArray.size();

    bd_time->data.assign(timeArray.begin(), timeArray.end());
    bd_intensity->data.assign(intensityArray.begin(), intensityArray.end());
}


//
// SpectrumList (default implementations)
//


PWIZ_API_DECL bool SpectrumList::empty() const 
{
    return size()==0 && !dataProcessingPtr().get();
}


PWIZ_API_DECL size_t SpectrumList::find(const string& id) const
{
    for (size_t index=0; index<size(); ++index)
        if (spectrumIdentity(index).id == id) 
            return index;
    return size();
}


PWIZ_API_DECL size_t SpectrumList::findAbbreviated(const string& abbreviatedId, char delimiter) const
{
    vector<string> abbreviatedTokens, actualTokens;

    // "1.1.123.2" splits to { 1, 1, 123, 2 }
    bal::split(abbreviatedTokens, abbreviatedId, bal::is_any_of(string(1, delimiter)));

    if (empty()) return size();

    // "sample=1 period=1 cycle=123 experiment=2" splits to { sample, 1, period, 1, cycle, 123, experiment, 2 }
    string firstId = spectrumIdentity(0).id;
    bal::split(actualTokens, firstId, bal::is_any_of(" ="));

    if (actualTokens.size() != abbreviatedTokens.size() * 2)
    {
        // TODO log this since I assume Skyline devs/uers don't want to see it
        //warn_once(("[SpectrumList::findAbbreviated] abbreviated id (" + abbreviatedId + ") has different number of terms from spectrum list (" + firstId + ")").c_str());
        return size();
    }

    string fullId(actualTokens[0] + "=" + abbreviatedTokens[0]);
    for (size_t i = 1; i < abbreviatedTokens.size(); ++i)
        fullId += " " + actualTokens[2*i] + "=" + abbreviatedTokens[i];

    return find(fullId);
}


PWIZ_API_DECL IndexList SpectrumList::findNameValue(const string& name, const string& value) const
{
    IndexList result;
    for (size_t index=0; index<size(); ++index)
        if (id::value(spectrumIdentity(index).id, name) == value)
            result.push_back(index);
    return result;
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


PWIZ_API_DECL SpectrumPtr SpectrumList::spectrum(const SpectrumPtr& seed, bool getBinaryData) const
{
    return spectrum(seed->index, getBinaryData);
};


PWIZ_API_DECL SpectrumPtr SpectrumList::spectrum(size_t index, DetailLevel detailLevel) const
{
    // By default faster metadeta access is not implemented
    if (detailLevel == DetailLevel_FastMetadata || detailLevel == DetailLevel_InstantMetadata)
        return SpectrumPtr(new Spectrum);

    return spectrum(index, detailLevel == DetailLevel_FullData);
}


PWIZ_API_DECL void SpectrumList::warn_once(const char *msg) const
{
}

PWIZ_API_DECL DetailLevel SpectrumList::min_level_accepted(std::function<boost::tribool(const Spectrum&)> predicate) const
{
    DetailLevel result = DetailLevel_InstantMetadata;

    for (size_t i = 0, end = size(); i < end; ++i)
    {
        boost::tribool accepted;

        do
        {
            SpectrumPtr s = spectrum(i, result);
            accepted = predicate(*s);

            if (accepted)
                return result;
            if (!accepted && (int)result < (int)DetailLevel_FullData)
                result = DetailLevel(int(result) + 1);
            else if (boost::logic::indeterminate(accepted))
            {
                break;
            }
        } while ((int)result < (int)DetailLevel_FullData);
    }

    // if we reach this point, no spectrum satisfied the predicate even at the highest detail level
    throw runtime_error("[SpectrumList::min_level_accepted] no spectrum satisfied the given predicate at any DetailLevel");
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
    if (index >= size())
        throw runtime_error("[MSData::SpectrumListSimple::spectrum()] Invalid index.");

    // validate Spectrum* 
    if (!spectra[index].get())
        throw runtime_error("[MSData::SpectrumListSimple::spectrum()] Null SpectrumPtr.");

    return spectra[index];
}


PWIZ_API_DECL const shared_ptr<const DataProcessing> SpectrumListSimple::dataProcessingPtr() const
{
    return dp;
}


//
// ChromatogramList (default implementations)
//


PWIZ_API_DECL bool ChromatogramList::empty() const
{
    return size()==0 && !dataProcessingPtr().get();
}


PWIZ_API_DECL size_t ChromatogramList::find(const string& id) const
{
    for (size_t index=0; index<size(); ++index)
        if (chromatogramIdentity(index).id == id) 
            return index;
    return size();
}


PWIZ_API_DECL ChromatogramPtr ChromatogramList::chromatogram(size_t index, DetailLevel detailLevel) const
{
    // By default faster metadata access is not implemented
    if (detailLevel == DetailLevel_FastMetadata || detailLevel == DetailLevel_InstantMetadata)
        return ChromatogramPtr(new Chromatogram);

    return chromatogram(index, detailLevel == DetailLevel_FullData);
}


PWIZ_API_DECL const shared_ptr<const DataProcessing> ChromatogramList::dataProcessingPtr() const
{
    return shared_ptr<const DataProcessing>();
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
    if (index >= size())
        throw runtime_error("[MSData::ChromatogramListSimple::chromatogram()] Invalid index.");

    // validate Chromatogram* 
    if (!chromatograms[index].get())
        throw runtime_error("[MSData::ChromatogramListSimple::chromatogram()] Null ChromatogramPtr.");

    return chromatograms[index];
}


PWIZ_API_DECL const shared_ptr<const DataProcessing> ChromatogramListSimple::dataProcessingPtr() const
{
    return dp;
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
           (!defaultSourceFilePtr.get() || defaultSourceFilePtr->empty()) &&
           (!spectrumListPtr.get() || spectrumListPtr->empty()) &&
           (!chromatogramListPtr.get() || chromatogramListPtr->empty()) &&
           ParamContainer::empty();
}


//
// MSData
//


PWIZ_API_DECL MSData::MSData() : version_("1.1.0"),nFiltersApplied_(0) {}
PWIZ_API_DECL MSData::~MSData() {}


PWIZ_API_DECL bool MSData::empty() const
{
    return accession.empty() &&
           id.empty() &&
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


PWIZ_API_DECL const string& MSData::version() const {return version_;}


namespace {

template <typename object_type>
struct HasID
{
    const string& id_;
    HasID(const string& id) : id_(id) {}

    bool operator()(const shared_ptr<object_type>& objectPtr)
    {
        return objectPtr.get() && objectPtr->id == id_;
    }
};

} // namespace


PWIZ_API_DECL vector<DataProcessingPtr> MSData::allDataProcessingPtrs() const
{
    vector<DataProcessingPtr> result(dataProcessingPtrs);

    if (run.spectrumListPtr.get())
    {
        // if SpectrumList::dataProcessingPtr() is not in MSData::dataProcessingPtrs, add it
        const shared_ptr<const DataProcessing> sldp = run.spectrumListPtr->dataProcessingPtr();
        if (sldp.get() && std::find_if(result.begin(), result.end(), HasID<DataProcessing>(sldp->id)) == result.end())
            result.push_back(boost::const_pointer_cast<DataProcessing>(sldp));
    }

    if (run.chromatogramListPtr.get())
    {
        // if ChromatogramList::dataProcessingPtr() is not in MSData::dataProcessingPtrs, add it
        const shared_ptr<const DataProcessing> cldp = run.chromatogramListPtr->dataProcessingPtr();
        if (cldp.get() && std::find_if(result.begin(), result.end(), HasID<DataProcessing>(cldp->id)) == result.end())
            result.push_back(boost::const_pointer_cast<DataProcessing>(cldp));
    }

    return result;
}


} // namespace msdata
} // namespace pwiz

