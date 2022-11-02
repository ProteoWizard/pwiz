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

#include "Reader_Mobilion.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/data/msdata/Version.hpp"


// A Mobilion MBI file is HDF5

// TODO: add MBI specific header and check this. This version only checks whether the file is a HDF5 file.
namespace {

    const char mz5Header[] = { '\x89', '\x48', '\x44', '\x46', '\x0d', '\x0a', '\x1a', '\x0a' };
    const size_t mz5HeaderSize = sizeof(mz5Header) / sizeof(char);

} // namespace

PWIZ_API_DECL std::string pwiz::msdata::Reader_Mobilion::identify(const std::string& filename, const std::string& head) const
{

    if (head.length() < mz5HeaderSize)
        return "";

    for (size_t i = 0; i < mz5HeaderSize; ++i)
        if (head[i] != mz5Header[i])
            return "";

    return bal::iends_with(filename, ".mbi") ? getType() : "";
}


#ifdef PWIZ_READER_MOBILION
#include "pwiz/utility/misc/SHA1Calculator.hpp"
//#include <boost/date_time/gregorian/gregorian.hpp>
#include "Reader_Mobilion_Detail.hpp"
#include "SpectrumList_Mobilion.hpp"
#include "ChromatogramList_Mobilion.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::Mobilion;


//
// Reader_Mobilion
//

namespace {

void fillInMetadata(const string& rawpath, const MBIFilePtr& rawdata, MSData& msd)
{
    msd.cvs = defaultCVList();

    bool hasMS1 = false, hasMS2 = false;
    for (size_t i = 0; i < rawdata->NumFrames() && (!hasMS1 || !hasMS2); ++i)
        if (rawdata->GetFrame(i)->IsFragmentationData())
            hasMS2 = true;
        else
            hasMS1 = true;

    if (hasMS1) msd.fileDescription.fileContent.set(MS_MS1_spectrum);
    if (hasMS2) msd.fileDescription.fileContent.set(MS_MSn_spectrum);

    msd.fileDescription.fileContent.set(MS_profile_spectrum);

    msd.fileDescription.fileContent.set(MS_TIC_chromatogram);

    bfs::path sourcePath(rawpath);

    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = BFS_STRING(sourcePath.leaf());
    sourceFile->name = BFS_STRING(sourcePath.leaf());
    sourceFile->location = "file:///" + BFS_GENERIC_STRING(BFS_COMPLETE(sourcePath.branch_path()));
    sourceFile->set(MS_Bruker_TDF_nativeID_format);
    sourceFile->set(MS_file_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.id = bfs::basename(sourcePath);
    msd.run.id = msd.id;

    auto metadata = rawdata->Metadata();
    
    SoftwarePtr softwareMobilion(new Software);
    softwareMobilion->id = "MOBILion";
    softwareMobilion->set(MS_acquisition_software);
    softwareMobilion->version = metadata.ReadString("acq-software-version");
    msd.softwarePtrs.push_back(softwareMobilion);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_Mobilion";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Mobilion_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Mobilion* sl = dynamic_cast<SpectrumList_Mobilion*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Mobilion* cl = dynamic_cast<ChromatogramList_Mobilion*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    //initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("IC")));
    auto& ic = *msd.instrumentConfigurationPtrs.back();

    ic.set(MS_Agilent_instrument_model);
    ic.userParams.push_back(UserParam("instrument model", metadata.ReadString("acq-ms-model")));

    ic.componentList.push_back(Component(MS_electrospray_ionization, 1));
    ic.componentList.push_back(Component(MS_quadrupole, 2));
    ic.componentList.push_back(Component(MS_quadrupole, 3));
    ic.componentList.push_back(Component(MS_time_of_flight, 4));
    ic.componentList.push_back(Component(MS_multichannel_plate, 5));

    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    string timestamp = metadata.ReadString("acq-timestamp");
    if (!timestamp.empty())
    {
        vector<string> tokens;
        bal::split(tokens, timestamp, bal::is_any_of(".")); // trim fractional seconds
        blt::local_date_time dateTime = parse_date_time("%Y-%m-%d %H:%M:%S", tokens[0]);
        if (!dateTime.is_not_a_date_time())
            msd.run.startTimeStamp = encode_xml_datetime(dateTime);
    }
}

} // namespace

PWIZ_API_DECL
void Reader_Mobilion::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_Mobilion::read] multiple runs not supported");

    string::const_iterator unicodeCharItr = std::find_if(filename.begin(), filename.end(), [](char ch) { return !isprint(ch) || static_cast<int>(ch) < 0; });
    if (unicodeCharItr != filename.end())
    {
        auto utf8CharAsString = [](string::const_iterator ch, string::const_iterator end) { string utf8; while (ch != end && *ch < 0) { utf8 += *ch; ++ch; }; return utf8; };
        throw ReaderFail(string("[Reader_Mobilion::read()] Mobilion API does not support Unicode in filepaths ('") + utf8CharAsString(unicodeCharItr, filename.end()) + "')");
    }

    MBIFilePtr rawdata(new MBIFile(filename.c_str()));

    result.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_Mobilion(result, rawdata, config));
    result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramList_Mobilion(rawdata, config));

    fillInMetadata(filename, rawdata, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_MOBILION

//
// non-MSVC implementation
//

#include "Reader_Mobilion.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {
    
PWIZ_API_DECL void Reader_Mobilion::read(const string& filename, const string& head, MSData& result, int runIndex, const Config& config) const
{
    throw ReaderFail("[Reader_Mobilion::read()] Mobilion MBI reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access MassLynx DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires MassLynx which only work on Windows"
#endif
    );
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_MOBILION

