//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

#include "Reader_Agilent.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"


PWIZ_API_DECL std::string pwiz::msdata::Reader_Agilent::identify(const std::string& filename, const std::string& head) const
{
    if (bfs::is_directory(filename))
    {
        auto filepath = bfs::path(filename) / "AcqData";
        if (bfs::exists(filepath) &&
            (bfs::exists(filepath / "mspeak.bin") ||
             bfs::exists(filepath / "msprofile.bin")))
            return getType();
    }
    else if (bal::icontains(filename, ".d/AcqData/mspeak.bin") ||
             bal::icontains(filename, ".d/AcqData/msprofile.bin"))
         return getType();

    return "";
}


#ifdef PWIZ_READER_AGILENT
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "Reader_Agilent_Detail.hpp"
#include "SpectrumList_Agilent.hpp"
#include "ChromatogramList_Agilent.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::Agilent;


//
// Reader_Agilent
//


namespace {

void initializeInstrumentConfigurationPtrs(MSData& msd,
                                           MassHunterDataPtr rawfile,
                                           const SoftwarePtr& instrumentSoftware)
{
    DeviceType deviceType = rawfile->getDeviceType();
    CVID cvidModel = translateAsInstrumentModel(deviceType);

    // set common instrument parameters
    ParamGroupPtr commonInstrumentParams(new ParamGroup);
    commonInstrumentParams->id = "CommonInstrumentParams";
    msd.paramGroupPtrs.push_back(commonInstrumentParams);

    if (cvidModel == MS_Agilent_instrument_model)
        commonInstrumentParams->userParams.push_back(UserParam("instrument model", rawfile->getDeviceName(deviceType)));
    commonInstrumentParams->set(cvidModel);

    auto serialNumber = rawfile->getDeviceSerialNumber(deviceType);
    if (!serialNumber.empty())
        commonInstrumentParams->set(MS_instrument_serial_number, serialNumber);

    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(rawfile);
    if (configurations.empty())
        configurations.resize(1); // provide at least one configuration

    for (size_t i=0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration(configurations[i]));

        ic->id = (format("IC%d") % (i+1)).str();
        ic->paramGroupPtrs.push_back(commonInstrumentParams);
        ic->softwarePtr = instrumentSoftware;

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}


void fillInMetadata(const string& rawpath, MassHunterDataPtr rawfile, MSData& msd, const Reader::Config& config)
{
    msd.cvs = defaultCVList();

    MSScanType scanTypes = rawfile->getScanTypes();
    if (scanTypes & MSScanType_Scan)         msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    if (scanTypes & MSScanType_ProductIon)   msd.fileDescription.fileContent.set(MS_MSn_spectrum);
    if (scanTypes & MSScanType_PrecursorIon) msd.fileDescription.fileContent.set(MS_precursor_ion_spectrum);
    // other scan types are not enumerated

    if (!msd.fileDescription.fileContent.empty())
    {
        // determine which spectrum representations are available
        // TODO: adjust this list according to PeakPicker settings?
        switch (rawfile->getSpectraFormat())
        {
            case MSStorageMode_Mixed:
                msd.fileDescription.fileContent.set(MS_centroid_spectrum);
                msd.fileDescription.fileContent.set(MS_profile_spectrum);
                break;

            case MSStorageMode_ProfileSpectrum:
                msd.fileDescription.fileContent.set(MS_profile_spectrum);
                break;

            case MSStorageMode_PeakDetectedSpectrum:
                msd.fileDescription.fileContent.set(MS_centroid_spectrum);
                break;
        }
    }

    msd.fileDescription.fileContent.set(MS_TIC_chromatogram);
    if (scanTypes & MSScanType_SelectedIon)
        msd.fileDescription.fileContent.set(MS_SIM_chromatogram);
    if (scanTypes & MSScanType_MultipleReaction)
        msd.fileDescription.fileContent.set(MS_SRM_chromatogram);

    // iterate over all files in AcqData
    bfs::path p(rawpath);
    for (bfs::directory_iterator itr(p / "AcqData"); itr != bfs::directory_iterator(); ++itr)
    {
        bfs::path sourcePath = itr->path();
        if (bfs::is_directory(sourcePath))
            continue;

        // skip non-native files that might be cluttering up the directory
        string ext = bfs::extension(sourcePath);
        bal::to_lower(ext);
        if (ext == ".mzxml" || ext == ".mzdata" || ext == ".mgf" || ext == ".ms2" || ext == ".txt")
            continue;

        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = BFS_STRING(sourcePath.leaf());
        sourceFile->name = BFS_STRING(sourcePath.leaf());
        sourceFile->location = "file:///" + BFS_GENERIC_STRING(BFS_COMPLETE(sourcePath.branch_path()));
        sourceFile->set(MS_Agilent_MassHunter_nativeID_format);
        sourceFile->set(MS_Agilent_MassHunter_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

        if (ext == ".bin")
            msd.run.defaultSourceFilePtr = sourceFile;
    }

    msd.id = bfs::basename(p);

    SoftwarePtr softwareMassHunter(new Software);
    softwareMassHunter->id = "MassHunter";
    softwareMassHunter->set(MS_MassHunter_Data_Acquisition);
    softwareMassHunter->version = rawfile->getVersion();
    msd.softwarePtrs.push_back(softwareMassHunter);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Agilent_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Agilent* sl = dynamic_cast<SpectrumList_Agilent*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Agilent* cl = dynamic_cast<ChromatogramList_Agilent*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    initializeInstrumentConfigurationPtrs(msd, rawfile, softwareMassHunter);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(rawfile->getAcquisitionTime(false));  // All but the oldest Agilent formats state their time zones
}

} // namespace


PWIZ_API_DECL
void Reader_Agilent::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex /* = 0 */,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_Agilent::read] multiple runs not supported");

    // instantiate RawFile, share ownership with SpectrumList_Agilent

    MassHunterDataPtr dataReader(MassHunterData::create(filename));

    shared_ptr<SpectrumList_Agilent> sl(new SpectrumList_Agilent(result, dataReader, config));
    shared_ptr<ChromatogramList_Agilent> cl(new ChromatogramList_Agilent(dataReader, config));
    result.run.spectrumListPtr = sl;
    result.run.chromatogramListPtr = cl;

    fillInMetadata(filename, dataReader, result, config);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_AGILENT /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {


PWIZ_API_DECL void Reader_Agilent::read(const string& filename, const string& head, MSData& result,	int sampleIndex /* = 0 */, const Config& config) const
{
	throw ReaderFail("[Reader_Agilent::read()] Agilent MassHunter reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
		"support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
		"program was built without COM support and cannot access Agilent DLLs - try building with MSVC instead of GCC"
#else // wrong platform
		"Agilent DLLs only work on Windows"
#endif
		);
}


} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_AGILENT /////////////////////////////////////////////////////////////////////////////

