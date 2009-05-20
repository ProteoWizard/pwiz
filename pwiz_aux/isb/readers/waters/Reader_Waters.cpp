#define PWIZ_SOURCE

#include "Reader_Waters.hpp"
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

    // Count the number of _FUNC[0-9]{3}.DAT files
    int functionCount = 0;
    while (bfs::exists(bfs::path(filename) / (boost::format("_FUNC%03d.DAT") % (functionCount+1)).str()))
        ++functionCount;
    if (functionCount > 0)
		result = getType();
    return result;
}


#ifdef PWIZ_READER_WATERS
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/COMInitializer.hpp"
#include "boost/shared_ptr.hpp"
//#include <boost/date_time/gregorian/gregorian.hpp>
#include <boost/foreach.hpp>
#include "dacserver_4-1.h"
#include "Reader_Waters_Detail.hpp"
#include "SpectrumList_Waters.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_Waters
//

namespace {


inline char idref_allowed(char c)
{
    return isalnum(c) || c=='-' ?
           c :
           '_';
}


string stringToIDREF(const string& s)
{
    string result = s;
    transform(result.begin(), result.end(), result.begin(), idref_allowed);
    return result;
}


void fillInMetadata(const string& rawpath, MSData& msd)
{
    msd.cvs = defaultCVList();

    bfs::path p(rawpath);
    for (bfs::directory_iterator itr(p); itr != bfs::directory_iterator(); ++itr)
    {
        bfs::path sourcePath = itr->path();
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = stringToIDREF(sourcePath.leaf());
        sourceFile->name = sourcePath.leaf();
        sourceFile->location = string("file://") + bfs::complete(sourcePath.branch_path()).string();
        if (bal::to_lower_copy(bfs::extension(sourcePath)) == ".dat")
        {
            sourceFile->set(MS_Waters_nativeID_format);
            sourceFile->set(MS_Waters_raw_file);
        }
        else
            sourceFile->set(MS_no_nativeID_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }
    msd.id = stringToIDREF(p.leaf());

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
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    //initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    //if (!msd.instrumentConfigurationPtrs.empty())
    //    msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = boost::to_lower_copy(stringToIDREF(rawpath));
    //msd.run.startTimeStamp = creationDateToStartTimeStamp(rawfile.getCreationDate());
}

} // namespace


PWIZ_API_DECL Reader_Waters::Reader_Waters()
{
    COMInitializer::initialize();
}

PWIZ_API_DECL Reader_Waters::~Reader_Waters()
{
    COMInitializer::uninitialize();
}

PWIZ_API_DECL
void Reader_Waters::read(const string& filename,
                         const string& head,
                         MSData& result,
                         int sampleIndex /* = 0 */) const
{
    if (sampleIndex != 0)
        throw ReaderFail("[Reader_Waters::read] multiple samples not supported");

    SpectrumList_Waters* sl = new SpectrumList_Waters(result, filename);
    result.run.spectrumListPtr = SpectrumListPtr(sl);
    //result.run.chromatogramListPtr = sl->Chromatograms();

    fillInMetadata(filename, result);
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

using namespace std;

PWIZ_API_DECL Reader_Waters::Reader_Waters() {}
PWIZ_API_DECL Reader_Waters::~Reader_Waters() {}

PWIZ_API_DECL void Reader_Waters::read(const string& filename, const string& head, MSData& result, int sampleIndex /* = 0 */) const
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

