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

#include "Reader_Waters.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/data/msdata/Version.hpp"


// A Waters RAW source (representing a "run") is actually a directory
// It may have multiple functions (scan events), e.g. _FUNC001.DAT, _FUNC002.DAT, etc.
// It may also contain some useful metadata in _extern.inf

PWIZ_API_DECL std::string pwiz::msdata::Reader_Waters::identify(const std::string& filename, const std::string& head) const
{

	std::string result;
    // Make sure target "filename" is actually a directory
    if (!bfs::is_directory(filename))
        return result;

    // Count the number of _FUNC[0-9]{3}.DAT files, starting with _FUNC001.DAT
    string functionPathmask = filename + "/_FUNC*.DAT";
    vector<bfs::path> functionFilepaths;
    pwiz::util::expand_pathmask(functionPathmask, functionFilepaths);
    if (!functionFilepaths.empty())
        result = getType();
    return result;
}


#ifdef PWIZ_READER_WATERS
#include "pwiz/utility/misc/SHA1Calculator.hpp"
//#include <boost/date_time/gregorian/gregorian.hpp>
#include "Reader_Waters_Detail.hpp"
#include "SpectrumList_Waters.hpp"
#include "ChromatogramList_Waters.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::Waters;


//
// Reader_Waters
//

namespace {

void fillInMetadata(const string& rawpath, RawDataPtr rawdata, MSData& msd)
{
    msd.cvs = defaultCVList();

    string functionPathmask = rawpath + "/_FUNC*.DAT";
    vector<bfs::path> functionFilepaths;
    expand_pathmask(functionPathmask, functionFilepaths);

    // first list all the function DAT files as sources
    for (size_t i=0; i < functionFilepaths.size(); ++i)
    {
        bfs::path sourcePath = functionFilepaths[i];
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = BFS_STRING(sourcePath.leaf());
        sourceFile->name = BFS_STRING(sourcePath.leaf());
        sourceFile->location = "file://" + BFS_COMPLETE(sourcePath.branch_path()).string();
        sourceFile->set(MS_Waters_nativeID_format);
        sourceFile->set(MS_Waters_raw_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }

    if (!functionFilepaths.empty())
        msd.run.defaultSourceFilePtr = msd.fileDescription.sourceFilePtrs.front();

    // next iterate over any other files
    bfs::path p(rawpath);
    for (bfs::directory_iterator itr(p); itr != bfs::directory_iterator(); ++itr)
    {
        bfs::path sourcePath = itr->path();
        if (bfs::is_directory(sourcePath))
            continue;

        // skip the function filepaths
        if (find(functionFilepaths.begin(), functionFilepaths.end(), sourcePath) != functionFilepaths.end())
            continue;

        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = BFS_STRING(sourcePath.leaf());
        sourceFile->name = BFS_STRING(sourcePath.leaf());
        sourceFile->location = string("file://") + BFS_COMPLETE(sourcePath.branch_path()).string();
        sourceFile->set(MS_no_nativeID_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }

    SoftwarePtr softwareMassLynx(new Software);
    softwareMassLynx->id = "MassLynx";
    softwareMassLynx->set(MS_MassLynx);
    softwareMassLynx->version = "4.1";
    msd.softwarePtrs.push_back(softwareMassLynx);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_Waters";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Waters_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_Waters* sl = dynamic_cast<SpectrumList_Waters*>(msd.run.spectrumListPtr.get());
    ChromatogramList_Waters* cl = dynamic_cast<ChromatogramList_Waters*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    //initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    msd.instrumentConfigurationPtrs.push_back(InstrumentConfigurationPtr(new InstrumentConfiguration("IC")));
    msd.instrumentConfigurationPtrs.back()->set(MS_Waters_instrument_model);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.id = bfs::basename(p);
    msd.run.id = msd.id;
    string dateStamp = rawdata->GetHeaderProp("Acquired Date");
    if (!dateStamp.empty())
    {
        string timeStamp = rawdata->GetHeaderProp("Acquired Time");
        if (!timeStamp.empty())
            dateStamp += " " + timeStamp;

        blt::local_date_time dateTime = parse_date_time("%d-%b-%Y %H:%M:%S", dateStamp);
        if (!dateTime.is_not_a_date_time())
            msd.run.startTimeStamp = encode_xml_datetime(dateTime);
    }
}

} // namespace

PWIZ_API_DECL
void Reader_Waters::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int runIndex,
                         const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_Waters::read] multiple runs not supported");

    string::const_iterator unicodeCharItr = std::find_if(filename.begin(), filename.end(), [](char ch) { return !isprint(ch) || static_cast<int>(ch) < 0; });
    if (unicodeCharItr != filename.end())
    {
        auto utf8CharAsString = [](string::const_iterator ch, string::const_iterator end) { string utf8; while (ch != end && *ch < 0) { utf8 += *ch; ++ch; }; return utf8; };
        throw ReaderFail(string("[Reader_Waters::read()] Waters API does not support Unicode in filepaths ('") + utf8CharAsString(unicodeCharItr, filename.end()) + "')");
    }

    try
    {
        RawDataPtr rawdata = RawDataPtr(new RawData(filename, config.iterationListenerRegistry));

        result.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_Waters(result, rawdata, config));
        result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramList_Waters(rawdata, config));

        fillInMetadata(filename, rawdata, result);
    }
    catch (exception&)
    {
        throw;
    }
    catch (...)
    {
        throw runtime_error("Unknown error and possible memory corruption when opening Waters RAW: " + filename);
    }
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_WATERS

//
// non-MSVC implementation
//

#include "Reader_Waters.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {
    
PWIZ_API_DECL void Reader_Waters::read(const string& filename, const string& head, MSData& result, int runIndex, const Config& config) const
{
    throw ReaderFail("[Reader_Waters::read()] Waters RAW reader not implemented: "
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

#endif // PWIZ_READER_WATERS

