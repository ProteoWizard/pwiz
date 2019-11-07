//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#define PWIZ_SOURCE

#include "Reader_Thermo.hpp"
#include "pwiz/utility/misc/String.hpp"


namespace {
// helper function used by both forms (real and stubbed) of Reader_Thermo
bool _hasRAWHeader(const std::string& head)
{
    const char rawHeader[] =
    {
        '\x01', '\xA1',
        'F', '\0', 'i', '\0', 'n', '\0', 'n', '\0',
        'i', '\0', 'g', '\0', 'a', '\0', 'n', '\0'
    };

    if (head.length() < sizeof(rawHeader)) // as with input "files" that are actually directories
        return false;

    for (size_t i=0; i<sizeof(rawHeader); i++)
        if (head[i] != rawHeader[i])
            return false;

    return true;
}
} // namespace


#ifdef PWIZ_READER_THERMO
#include "pwiz_aux/msrc/utility/vendor_api/thermo/RawFile.h"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include "ChromatogramList_Thermo.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::vendor_api::Thermo;
using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::Thermo;


//
// Reader_Thermo
//


PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}


namespace {

void initializeInstrumentConfigurationPtrs(MSData& msd,
                                           RawFile& rawfile,
                                           const SoftwarePtr& instrumentSoftware,
                                           const InstrumentData& instData)
{
    CVID cvidModel = translateAsInstrumentModel(rawfile.getInstrumentModel());

    // set common instrument parameters
    ParamGroupPtr commonInstrumentParams(new ParamGroup);
    commonInstrumentParams->id = "CommonInstrumentParams";
    msd.paramGroupPtrs.push_back(commonInstrumentParams);

    if (cvidModel == MS_Thermo_Electron_instrument_model)
        commonInstrumentParams->userParams.push_back(UserParam("instrument model", instData.Model));
    commonInstrumentParams->set(cvidModel);

    if (!instData.SerialNumber.empty())
        commonInstrumentParams->set(MS_instrument_serial_number, instData.SerialNumber);

    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(rawfile);

    for (size_t i=0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration(configurations[i]));

        ic->id = (format("IC%d") % (i+1)).str();
        ic->paramGroupPtrs.push_back(commonInstrumentParams);
        ic->softwarePtr = instrumentSoftware;

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}


void fillInMetadata(const string& filename, RawFile& rawfile, MSData& msd, const Reader::Config& config)
{
    msd.cvs = defaultCVList();

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = "RAW1";
    sourceFile->name = BFS_STRING(p.leaf());
    sourceFile->location = "file:///" + BFS_COMPLETE(p.branch_path()).string();
    sourceFile->set(MS_Thermo_nativeID_format);
    sourceFile->set(MS_Thermo_RAW_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    msd.id = bfs::basename(p);

    // reset controller which may have been changed by Spectrum/ChromatogramList index enumeration
    rawfile.setCurrentController(Controller_MS, 1);

    auto instData = rawfile.getInstrumentData();

    string sampleID = rawfile.getSampleID();
    if (!sampleID.empty())
    {
        SamplePtr samplePtr(new Sample(sampleID));
        samplePtr->set(MS_sample_name, sampleID);
        msd.samplePtrs.push_back(samplePtr);
    }

    /*for (int i=0; i < (int) ValueID_Double_Count; ++i)
        if (rawfile.value((ValueID_Double) i) > 0)
            samplePtr->userParams.push_back(UserParam(rawfile.name((ValueID_Double) i),
                                                      lexical_cast<string>(rawfile.value((ValueID_Double) i)),
                                                      "xsd:double"));
    for (int i=0; i < (int) ValueID_Long_Count; ++i)
        if (rawfile.value((ValueID_Long) i) > 0)
            samplePtr->userParams.push_back(UserParam(rawfile.name((ValueID_Long) i),
                                                      lexical_cast<string>(rawfile.value((ValueID_Long) i)),
                                                      "xsd:long"));
    for (int i=0; i < (int) ValueID_String_Count; ++i)
        if (!rawfile.value((ValueID_String) i).empty())
            samplePtr->userParams.push_back(UserParam(rawfile.name((ValueID_String) i),
                                                      rawfile.value((ValueID_String) i),
                                                      "xsd:string"));*/

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->set(MS_Xcalibur);
    softwareXcalibur->version = instData.SoftwareVersion;
    msd.softwarePtrs.push_back(softwareXcalibur);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Thermo_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Thermo* sl = dynamic_cast<SpectrumList_Thermo*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Thermo* cl = dynamic_cast<ChromatogramList_Thermo*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    // add file content metadata

    if (sl->numSpectraOfMSOrder(MSOrder_NeutralLoss) > 0)
        msd.fileDescription.fileContent.set(MS_constant_neutral_loss_spectrum);
    if (sl->numSpectraOfMSOrder(MSOrder_NeutralGain) > 0)
        msd.fileDescription.fileContent.set(MS_constant_neutral_gain_spectrum);
    if (sl->numSpectraOfMSOrder(MSOrder_ParentScan) > 0)
        msd.fileDescription.fileContent.set(MS_precursor_ion_spectrum);

    //if (sl->numSpectraOfScanType(ScanType_Zoom) > 0)
    //    msd.fileDescription.fileContent.set(MS_zoom_scan);

    int simScanCount = sl->numSpectraOfScanType(ScanType_SIM); // MS1
    int srmScanCount = sl->numSpectraOfScanType(ScanType_SRM); // MS2

    if (sl->numSpectraOfScanType(ScanType_Full) > 0)
    {
        // MS can be either Full scans or SIM scans so we compare against the SIM scan count
        if (sl->numSpectraOfMSOrder(MSOrder_MS) > simScanCount)
            msd.fileDescription.fileContent.set(MS_MS1_spectrum);

        // MS2 can be either Full or SRM scans so we compare against the SRM scan count
        if (sl->numSpectraOfMSOrder(MSOrder_MS2) > srmScanCount)
            msd.fileDescription.fileContent.set(MS_MSn_spectrum);
        
        // MS3+ scans are definitely MSn
        for (int msOrder = (int) MSOrder_MS3; msOrder < (int) MSOrder_Count; ++msOrder)
            if (sl->numSpectraOfMSOrder(static_cast<MSOrder>(msOrder)) > 0)
            {
                msd.fileDescription.fileContent.set(MS_MSn_spectrum);
                break;
            }
    }

    // these scan types should be represented as chromatograms
    if (simScanCount > 0)
        msd.fileDescription.fileContent.set(MS_SIM_chromatogram);
    if (srmScanCount > 0)
        msd.fileDescription.fileContent.set(MS_SRM_chromatogram);

    // add instrument configuration metadata
    initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur, instData);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];
    else
    {
        if (config.unknownInstrumentIsError)
            throw runtime_error("[Reader_Thermo::fillInMetadata] unable to parse instrument model; please report this error to the ProteoWizard developers with this information: model(" + instData.Model + ") name(" + instData.Name + "); if want to convert the file anyway, use the ignoreUnknownInstrumentError flag");
        // TODO: else log warning
    }

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(rawfile.getCreationDate(config.adjustUnknownTimeZonesToHostTimeZone));
}

} // namespace


PWIZ_API_DECL std::string Reader_Thermo::identify(const string& filename, const string& head) const
{
	return std::string(hasRAWHeader(head)?getType():"");
}


PWIZ_API_DECL
void Reader_Thermo::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int sampleIndex /* = 0 */,
                         const Config& config) const
{
    if (sampleIndex != 0)
        throw ReaderFail("[Reader_Thermo::read] multiple samples not supported");

    // instantiate RawFile, share ownership with SpectrumList_Thermo

    RawFilePtr rawfile = RawFile::create(filename);

    shared_ptr<SpectrumList_Thermo> sl(new SpectrumList_Thermo(result, rawfile, config));
    shared_ptr<ChromatogramList_Thermo> cl(new ChromatogramList_Thermo(result, rawfile, config));
    result.run.spectrumListPtr = sl;
    result.run.chromatogramListPtr = cl;

    fillInMetadata(filename, *rawfile, result, config);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_THERMO

//
// non-MSVC implementation
//

#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {


PWIZ_API_DECL std::string Reader_Thermo::identify(const string& filename, const string& head) const
{
   // we know what this is, but we'll throw an exception on read
	return std::string(hasRAWHeader(head)?getType():"");
}

PWIZ_API_DECL void Reader_Thermo::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */, const Config& config) const
{
	throw ReaderFail("[Reader_Thermo::read()] Thermo RAW reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
		"support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
		"program was built without COM support and cannot access Thermo DLLs - try building with MSVC instead of GCC"
#else // wrong platform
		"Thermo DLLs only work on Windows"
#endif
		);
}

PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_THERMO

