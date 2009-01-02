//
// Reader_Bruker.cpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#define PWIZ_SOURCE

#include "Reader_Bruker.hpp"
#include "Reader_Bruker_Detail.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/data/msdata/Version.hpp"


// A Bruker Analysis source (representing a "run") is actually a directory
// It contains several files related to a single acquisition, e.g.:
// fid, acqu, acqus, Analysis.FAMethod, AnalysisParameter.xml, sptype

PWIZ_API_DECL
std::string pwiz::msdata::Reader_Bruker::identify(const std::string& filename,
                                                  const std::string& head) const
{
    switch (detail::format(filename))
    {
        case pwiz::msdata::detail::SpectrumList_Bruker_Format_FID: return "Bruker FID";
        case pwiz::msdata::detail::SpectrumList_Bruker_Format_YEP: return "Bruker YEP";
        case pwiz::msdata::detail::SpectrumList_Bruker_Format_BAF: return "Bruker BAF";

        case pwiz::msdata::detail::SpectrumList_Bruker_Format_Unknown:
        default:
            return "";
    }
}


#ifndef PWIZ_NO_READER_BRUKER
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/COMInitializer.hpp"
#include "boost/shared_ptr.hpp"
#include <boost/foreach.hpp>
//#include "Reader_Bruker_Detail.hpp"
#include "SpectrumList_Bruker.hpp"
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
// Reader_Bruker
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


void fillInMetadata(const string& rootpath, MSData& msd)
{
    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "psi-ms.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "1.0";

    bfs::path p(rootpath);
    for (bfs::directory_iterator itr(p); itr != bfs::directory_iterator(); ++itr)
    {
        bfs::path sourcePath = itr->path();
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = stringToIDREF(sourcePath.leaf());
        sourceFile->name = sourcePath.leaf();
        sourceFile->location = string("file://") + bfs::complete(sourcePath.branch_path()).string();
        //sourceFile->cvParams.push_back(MS_Bruker_RAW_file);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }
    msd.id = stringToIDREF(p.leaf());

    SoftwarePtr software(new Software);
    software->id = "CompassXtract";
    software->set(MS_CompassXtract);
    software->version = "1.0";
    msd.softwarePtrs.push_back(software);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_Bruker";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Bruker_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    //initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    //if (!msd.instrumentConfigurationPtrs.empty())
    //    msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = boost::to_lower_copy(stringToIDREF(rootpath));
    //msd.run.startTimeStamp = creationDateToStartTimeStamp(rawfile.getCreationDate());
}

} // namespace


class Reader_Bruker::Impl
{
    public:
    Impl()
    {
        COMInitializer::initialize();
    }

    ~Impl()
    {
        COMInitializer::uninitialize();
    }

    // EDAL is CompassXtract's namespace
    EDAL::IMSAnalysisPtr pAnalysis;
};

PWIZ_API_DECL Reader_Bruker::Reader_Bruker()
:   impl_(new Impl)
{
}

PWIZ_API_DECL Reader_Bruker::~Reader_Bruker()
{
}

PWIZ_API_DECL
void Reader_Bruker::read(const string& filename, 
                         const string& head,
                         MSData& result) const
{
    SpectrumList_Bruker_Format format = detail::format(filename);
    if (format == SpectrumList_Bruker_Format_Unknown)
        throw ReaderFail("[Reader_Bruker::read()] Path given is not a recognized Bruker format");

    // use and check for a successful creation with HRESULT
    HRESULT hr = impl_->pAnalysis.CreateInstance("EDAL.MSAnalysis");
    if (FAILED(hr))
    {
        // No success when creating the analysis pointer - we decrypt the error from hr.
        LPVOID lpMsgBuf;

        ::FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER |
	                   FORMAT_MESSAGE_FROM_SYSTEM,
	                   NULL,
	                   hr,
	                   MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
	                   (LPTSTR) &lpMsgBuf,
	                   0,
	                   NULL );

        string error((const char*) lpMsgBuf);
        LocalFree(lpMsgBuf);
        throw ReaderFail("[Reader_Bruker::read()] Error initializing CompassXtract: " + error);
    }

    SpectrumList_Bruker* sl = new SpectrumList_Bruker(result, filename, format, impl_->pAnalysis);
    result.run.spectrumListPtr = SpectrumListPtr(sl);
    //result.run.chromatogramListPtr = sl->Chromatograms();

    switch (format)
    {
        case SpectrumList_Bruker_Format_FID: result.fileDescription.fileContent.set(MS_Bruker_FID_nativeID_format);
        case SpectrumList_Bruker_Format_YEP: result.fileDescription.fileContent.set(MS_Bruker_Agilent_YEP_nativeID_format);
        case SpectrumList_Bruker_Format_BAF: result.fileDescription.fileContent.set(MS_Bruker_BAF_nativeID_format);
    }

    fillInMetadata(filename, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_NO_READER_BRUKER

//
// non-MSVC implementation
//

#include "Reader_Bruker.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

class Reader_Bruker::Impl {};
PWIZ_API_DECL Reader_Bruker::Reader_Bruker() {}
PWIZ_API_DECL Reader_Bruker::~Reader_Bruker() {}

PWIZ_API_DECL void Reader_Bruker::read(const string& filename, const string& head, MSData& result) const
{
    throw ReaderFail("[Reader_Bruker::read()] Bruker Analysis reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access CompassXtract DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires CompassXtract which only work on Windows"
#endif
		);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_NO_READER_BRUKER

