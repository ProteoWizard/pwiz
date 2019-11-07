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

#include "Reader_UIMF.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"


PWIZ_API_DECL std::string pwiz::msdata::Reader_UIMF::identify(const std::string& filename, const std::string& head) const
{
    return bfs::is_regular_file(filename) && bal::iends_with(filename, ".uimf") ? getType() : "";
}


#ifdef PWIZ_READER_UIMF
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "SpectrumList_UIMF.hpp"
#include "ChromatogramList_UIMF.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_UIMF
//


namespace {

void fillInMetadata(const string& rawpath, UIMFReaderPtr rawfile, MSData& msd)
{
    msd.cvs = defaultCVList();

    const set<FrameType>& frameTypes = rawfile->getFrameTypes();
    if (frameTypes.count(FrameType_MS1) > 0) msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    if (frameTypes.count(FrameType_Calibration) > 0) msd.fileDescription.fileContent.set(MS_calibration_spectrum);
    if (frameTypes.count(FrameType_Prescan) > 0) msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    if (frameTypes.count(FrameType_MS2) > 0) msd.fileDescription.fileContent.set(MS_MSn_spectrum);


    msd.fileDescription.fileContent.set(MS_profile_spectrum);

    msd.fileDescription.fileContent.set(MS_TIC_chromatogram);
    /*if (scanTypes & MSScanType_SelectedIon)
        msd.fileDescription.fileContent.set(MS_SIM_chromatogram);
    if (scanTypes & MSScanType_MultipleReaction)
        msd.fileDescription.fileContent.set(MS_SRM_chromatogram);*/

    // iterate over all files in AcqData

    bfs::path sourcePath(rawpath);

    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = BFS_STRING(sourcePath.leaf());
    sourceFile->name = BFS_STRING(sourcePath.leaf());
    sourceFile->location = "file:///" + BFS_GENERIC_STRING(BFS_COMPLETE(sourcePath.branch_path()));
    sourceFile->set(MS_UIMF_nativeID_format);
    sourceFile->set(MS_UIMF_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.id = bfs::basename(sourcePath);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_UIMF_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_UIMF* sl = dynamic_cast<SpectrumList_UIMF*>(msd.run.spectrumListPtr.get());
    ChromatogramList_UIMF* cl = dynamic_cast<ChromatogramList_UIMF*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    // add dummy IC
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("IC")));
    msd.instrumentConfigurationPtrs.back()->set(MS_instrument_model);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(rawfile->getAcquisitionTime());
}

} // namespace


PWIZ_API_DECL
void Reader_UIMF::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex /* = 0 */,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_UIMF::read] multiple runs not supported");

    // instantiate RawFile, share ownership with SpectrumList_UIMF

    UIMFReaderPtr dataReader(UIMFReader::create(filename));

    shared_ptr<SpectrumList_UIMF> sl(new SpectrumList_UIMF(result, dataReader, config));
    shared_ptr<ChromatogramList_UIMF> cl(new ChromatogramList_UIMF(dataReader));
    result.run.spectrumListPtr = sl;
    result.run.chromatogramListPtr = cl;

    fillInMetadata(filename, dataReader, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_UIMF /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {


PWIZ_API_DECL void Reader_UIMF::read(const string& filename, const string& head, MSData& result,	int sampleIndex /* = 0 */, const Config& config) const
{
	throw ReaderFail("[Reader_UIMF::read()] UIMF reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
		"support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
		"program was built without COM support and cannot access UIMF DLLs - try building with MSVC instead of GCC"
#else // wrong platform
		"UIMF DLLs only work on Windows"
#endif
		);
}


} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_UIMF /////////////////////////////////////////////////////////////////////////////

