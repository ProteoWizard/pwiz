//
// Reader_RAW.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "Reader_RAW.hpp"


namespace {
// helper function used by both forms (real and stubbed) of ReadRAW
bool _hasRAWHeader(const std::string& head)
{
    const char rawHeader[] =
    {
        '\x01', '\xA1', 
        'F', '\0', 'i', '\0', 'n', '\0', 'n', '\0', 
        'i', '\0', 'g', '\0', 'a', '\0', 'n', '\0'
    };

    for (size_t i=0; i<sizeof(rawHeader); i++)
        if (head[i] != rawHeader[i]) 
            return false;

    return true;
}
} // namespace


#ifndef PWIZ_NO_READER_RAW
#include "CVTranslator.hpp"
#include "rawfile/RawFile.h"
#include "util/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/filesystem/path.hpp"
#include <iostream>
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::raw;
using namespace pwiz::util;
namespace bfs = boost::filesystem;


//
// SpectrumList_RAW
//


namespace {


class SpectrumList_RAW : public SpectrumList
{
    public:

    SpectrumList_RAW(const MSData& msd, shared_ptr<RawFile> rawfile);
    virtual size_t size() const;
    virtual const SpectrumIdentity& spectrumIdentity(size_t index) const;
    virtual size_t find(const string& id) const;
    virtual size_t findNative(const string& nativeID) const;
    virtual SpectrumPtr spectrum(size_t index, bool getBinaryData) const;

    private:

    const MSData& msd_;
    shared_ptr<RawFile> rawfile_;
    size_t size_;
    mutable vector<SpectrumPtr> spectrumCache_;
    vector<SpectrumIdentity> index_;

    void createIndex();
    string findPrecursorID(size_t index) const;
};


SpectrumList_RAW::SpectrumList_RAW(const MSData& msd, shared_ptr<RawFile> rawfile)
:   msd_(msd), rawfile_(rawfile),
    size_(rawfile->value(NumSpectra)),
    spectrumCache_(size_), index_(size_)
{
    createIndex();
}


size_t SpectrumList_RAW::size() const
{
    return size_;
}


const SpectrumIdentity& SpectrumList_RAW::spectrumIdentity(size_t index) const
{
    if (index>size_)
        throw runtime_error(("[SpectrumList_RAW::spectrumIdentity()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());
    return index_[index];
}


size_t SpectrumList_RAW::find(const string& id) const
{
    try
    {
        size_t scanNumber = lexical_cast<size_t>(id);
        if (scanNumber>=1 && scanNumber<=size()) 
            return scanNumber-1;
    }
    catch (bad_lexical_cast&) {}

    return size();
}


size_t SpectrumList_RAW::findNative(const string& nativeID) const
{
    return find(nativeID);
}


CVID filterStringToMassAnalyzer(const string& filterString)
{
    if (filterString.find("ITMS") != string::npos)
        return MS_ion_trap;
    else if (filterString.find("FTMS") != string::npos)
        return MS_FT_ICR;
    else
        return CVID_Unknown;
}


CVParam translate(ScanType scanType)
{
    switch (scanType)
    {
        case ScanType_Full:
            return MS_full_scan;
        case ScanType_Zoom:
            return MS_zoom_scan;
        case ScanType_Unknown:
        default:
            return CVParam();
    }
}


CVParam translate(PolarityType scanType)
{
    switch (scanType)
    {
        case PolarityType_Positive:
            return MS_positive_scan;
        case PolarityType_Negative:
            return MS_negative_scan;
        case PolarityType_Unknown:
        default:
            return CVParam();
    }
}


SpectrumPtr SpectrumList_RAW::spectrum(size_t index, bool getBinaryData) const 
{ 
    if (index>size_)
        throw runtime_error(("[SpectrumList_RAW::spectrum()] Bad index: " 
                            + lexical_cast<string>(index)).c_str());

    // returned cached Spectrum if possible

    if (!getBinaryData && spectrumCache_[index].get())
        return spectrumCache_[index];

    // allocate a new Spectrum

    SpectrumPtr result(new Spectrum);
    if (!result.get())
        throw runtime_error("[SpectrumList_RAW::spectrum()] Allocation error.");

    // get rawfile::ScanInfo and translate

    long scanNumber = static_cast<int>(index) + 1;
    auto_ptr<ScanInfo> scanInfo = rawfile_->getScanInfo(scanNumber);
    if (!scanInfo.get())
        throw runtime_error("[SpectrumList_RAW::spectrum()] Error retrieving ScanInfo.");

    result->index = index;
    result->id = result->nativeID = lexical_cast<string>(scanNumber);

    SpectrumDescription& sd = result->spectrumDescription;
    Scan& scan = sd.scan;

    if (msd_.instrumentPtrs.empty())
        throw runtime_error("[SpectrumList_RAW::spectrum()] No instruments defined.");
    scan.instrumentPtr = msd_.instrumentPtrs[0];

    string filterString = scanInfo->filter();

    scan.cvParams.push_back(CVParam(MS_filter_string, filterString));

    string scanEvent = scanInfo->trailerExtraValue("Scan Event:");
    scan.cvParams.push_back(CVParam(MS_preset_scan_configuration, scanEvent));

    CVID massAnalyzer = filterStringToMassAnalyzer(filterString);
    if (massAnalyzer != CVID_Unknown)
        scan.cvParams.push_back(massAnalyzer);
     
    result->set(MS_ms_level, scanInfo->msLevel());

    ScanType scanType = scanInfo->scanType();
    if (scanType!=ScanType_Unknown) scan.cvParams.push_back(translate(scanType));

    PolarityType polarityType = scanInfo->polarityType();
    if (polarityType!=PolarityType_Unknown) scan.cvParams.push_back(translate(polarityType));

    if (scanInfo->isProfileScan()) sd.cvParams.push_back(MS_profile_mass_spectrum); 
    if (scanInfo->isCentroidScan()) sd.cvParams.push_back(MS_centroid_mass_spectrum); 

    scan.cvParams.push_back(CVParam(MS_scan_time, scanInfo->startTime(), MS_minute));
    sd.cvParams.push_back(CVParam(MS_lowest_m_z_value, scanInfo->lowMass()));
    sd.cvParams.push_back(CVParam(MS_highest_m_z_value, scanInfo->highMass()));
    sd.cvParams.push_back(CVParam(MS_base_peak_m_z, scanInfo->basePeakMass()));
    sd.cvParams.push_back(CVParam(MS_base_peak_intensity, scanInfo->basePeakIntensity()));
    sd.cvParams.push_back(CVParam(MS_total_ion_current, scanInfo->totalIonCurrent()));

    for (long i=0, precursorCount=scanInfo->parentCount(); i<precursorCount; i++)
    {
        // Note: we report what RawFile gives us, which comes from the filter string;
        // we can look in the trailer extra values for better (but still unreliable) 
        // info.  Precursor recalculation should be done outside the Reader.

        Precursor precursor;
        precursor.spectrumID = findPrecursorID(index);
        precursor.ionSelection.cvParams.push_back(CVParam(MS_m_z, scanInfo->parentMass(i)));
        precursor.ionSelection.cvParams.push_back(CVParam(MS_intensity, scanInfo->parentEnergy(i)));
        sd.precursors.push_back(precursor); 
    }

    if (getBinaryData)
    {
        auto_ptr<raw::MassList> massList = 
            rawfile_->getMassList(scanNumber, "", raw::Cutoff_None, 0, 0, false);

        result->setMZIntensityPairs(reinterpret_cast<MZIntensityPair*>(massList->data()), 
                                    massList->size());
    }

    // save to cache if no binary data

    if (!getBinaryData && !spectrumCache_[index].get())
        spectrumCache_[index] = result; 

    return result;
}


void SpectrumList_RAW::createIndex()
{
    for (size_t i=0; i<size_; i++)
    {
        SpectrumIdentity& si = index_[i];
        si.index = i;
        si.id = si.nativeID = lexical_cast<string>(i+1);
    }
}


string SpectrumList_RAW::findPrecursorID(size_t index) const
{
    // return most recent survey scan

    while (index>0)
    {
        index--;
        SpectrumPtr candidate = spectrum(index, false);
        if (candidate->cvParam(MS_ms_level).valueAs<int>() == 1) return candidate->id;
    }

    return "";
}


} // namespace


//
// Reader_RAW
//


bool Reader_RAW::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}

namespace {

auto_ptr<RawFileLibrary> rawFileLibrary_;


void fillInMetadata(const string& filename, RawFile& rawfile, MSData& msd)
{
    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "psi-ms.obo"; 
    cv.cvLabel = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "1.0";

    msd.fileDescription.fileContent.cvParams.push_back(MS_MSn_spectrum);

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = "rawfile";
    sourceFile->name = p.leaf();
    sourceFile->location = p.branch_path().string();
    sourceFile->cvParams.push_back(MS_Xcalibur_RAW_file);
    string sha1 = SHA1Calculator::hashFile(filename);
    sourceFile->cvParams.push_back(CVParam(MS_SHA_1, sha1));
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = rawfile.value(InstSoftwareVersion);
    msd.softwarePtrs.push_back(softwareXcalibur);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz::msdata::Reader_RAW";
    softwarePwiz->softwareParam = MS_pwiz;
    softwarePwiz->softwareParamVersion = "1.0"; 
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz::msdata::Reader_RAW conversion";
    dpPwiz->softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    CVTranslator cvTranslator;

    InstrumentPtr instrument(new Instrument);
    string model = rawfile.value(InstModel);
    CVID cvidModel = cvTranslator.translate(model);
    if (cvidModel != CVID_Unknown) 
    {
        instrument->cvParams.push_back(cvidModel);
        instrument->id = cvinfo(cvidModel).name;
    }
    else
    {
        instrument->userParams.push_back(UserParam("instrument model", model));
        instrument->id = model;
    }
    instrument->cvParams.push_back(CVParam(MS_instrument_serial_number, 
                                           rawfile.value(InstSerialNumber)));
    instrument->softwarePtr = softwareXcalibur;
    instrument->componentList.source.order = 1;
    instrument->componentList.analyzer.order = 2;
    instrument->componentList.detector.order = 3;
    msd.instrumentPtrs.push_back(instrument);

    msd.run.id = filename;
    //msd.run.startTimeStamp = rawfile.getCreationDate(); // TODO format: 2007-06-27T15:23:45.00035
    msd.run.instrumentPtr = instrument;
}


} // namespace


bool Reader_RAW::accept(const string& filename, const string& head) const
{
    return hasRAWHeader(head);
}


void Reader_RAW::read(const string& filename, 
                      const string& head,
                      MSData& result) const
{
    // initialize RawFileLibrary

	if (!rawFileLibrary_.get()) {
		rawFileLibrary_.reset(new RawFileLibrary());
	}

    // instantiate RawFile, share ownership with SpectrumList_RAW

    shared_ptr<RawFile> rawfile(RawFile::create(filename).release());
    rawfile->setCurrentController(Controller_MS, 1);
    result.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_RAW(result, rawfile));

    fillInMetadata(filename, *rawfile, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "Reader_RAW.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

bool Reader_RAW::accept(const string& filename, const string& head) const
{
    return false;
}

void Reader_RAW::read(const string& filename, MSData& result) const
{
    throw runtime_error("[Reader_RAW::read()] Not implemented."); 
}

bool Reader_RAW::hasRAWHeader(const string& head)
{   
    return _hasRAWHeader(head);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

