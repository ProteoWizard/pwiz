//
// Reader_ABI.cpp
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

#include "Reader_ABI.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "boost/shared_ptr.hpp"
#include <boost/foreach.hpp>
#include <iostream>
#include <iomanip>
#include <stdexcept>


PWIZ_API_DECL std::string pwiz::msdata::Reader_ABI::identify(const std::string& filename, const std::string& head) const
{
	std::string result;
    // TODO: check header signature?
    if (bfs::extension(filename) == ".wiff")
		result = getType();
    return result;
}


#ifdef PWIZ_READER_ABI
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"
#include "SpectrumList_ABI.hpp"
#include "ChromatogramList_ABI.hpp"
#include "Reader_ABI_Detail.hpp"


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_ABI
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

void fillInMetadata(const string& wiffpath, MSData& msd, WiffFilePtr wifffile, int sample)
{
    msd.cvs = defaultCVList();

    string sampleName = wifffile->getSampleNames()[sample-1];

    int periodCount = wifffile->getPeriodCount(sample);
    for (int ii=1; ii <= periodCount; ++ii)
    {
        int experimentCount = wifffile->getExperimentCount(sample, ii);
        for (int iii=1; iii <= experimentCount; ++iii)
        {
            ExperimentPtr msExperiment = wifffile->getExperiment(sample, ii, iii);
            msd.fileDescription.fileContent.set(translateAsSpectrumType(msExperiment->getScanType()));
        }
    }

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(wiffpath);
    sourceFile->id = "WIFF1";
    sourceFile->name = p.leaf();
    string location = bfs::complete(p.branch_path()).string();
    if (location.empty()) location = ".";
    sourceFile->location = string("file:///") + location;
    sourceFile->set(MS_WIFF_nativeID_format);
    sourceFile->set(MS_ABI_WIFF_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.id = stringToIDREF(sampleName);

    SoftwarePtr acquisitionSoftware(new Software);
    acquisitionSoftware->id = "Analyst";
    acquisitionSoftware->set(MS_Analyst);
    acquisitionSoftware->version = "unknown";
    msd.softwarePtrs.push_back(acquisitionSoftware);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_ABI";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_ABI_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_ABI* sl = dynamic_cast<SpectrumList_ABI*>(msd.run.spectrumListPtr.get());
    ChromatogramList_ABI* cl = dynamic_cast<ChromatogramList_ABI*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    InstrumentConfigurationPtr ic(new InstrumentConfiguration(translateAsInstrumentConfiguration(wifffile)));
    ic->softwarePtr = acquisitionSoftware;
    msd.instrumentConfigurationPtrs.push_back(ic);
    msd.run.defaultInstrumentConfigurationPtr = ic;

    msd.run.id = boost::to_lower_copy(sampleName);
    msd.run.startTimeStamp = wifffile->getSampleAcquisitionTime();
}

} // namespace


PWIZ_API_DECL
void Reader_ABI::read(const string& filename, 
                      const string& head,
                      MSData& result) const
{
    try
    {
        WiffFilePtr wifffile = WiffFile::create(filename);
        if (wifffile->getSampleCount() > 1)
            throw ReaderFail("[Reader_ABI::read()] invalid call for a single run on a multi-run WIFF file");

        SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, 1);
        ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, 1);
        result.run.spectrumListPtr = SpectrumListPtr(sl);
        result.run.chromatogramListPtr = ChromatogramListPtr(cl);

        fillInMetadata(filename, result, wifffile, 1);
    }
    catch (std::exception& e)
    {
        throw std::runtime_error(e.what());
    }
    catch (...)
    {
        throw runtime_error("[Reader_ABI::read()] unhandled exception");
    }
}

PWIZ_API_DECL
void Reader_ABI::read(const string& filename,
                      const string& head,
                      vector<MSDataPtr>& results) const
{
    try
    {
        WiffFilePtr wifffile = WiffFile::create(filename);

        int sampleCount = wifffile->getSampleCount();
        for (int i=1; i <= sampleCount; ++i)
        {
            results.push_back(MSDataPtr(new MSData));
            MSData& result = *results.back();

            SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, i);
            ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, i);
            result.run.spectrumListPtr = SpectrumListPtr(sl);
            result.run.chromatogramListPtr = ChromatogramListPtr(cl);

            fillInMetadata(filename, result, wifffile, i);
        }
    }
    catch (std::exception& e)
    {
        throw std::runtime_error(e.what());
    }
    catch (...)
    {
        throw runtime_error("[Reader_ABI::read()] unhandled exception");
    }
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_ABI

//
// non-MSVC implementation
//

#include "Reader_ABI.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

PWIZ_API_DECL void Reader_ABI::read(const string& filename, const string& head, MSData& result) const
{
    throw ReaderFail("[Reader_ABI::read()] ABSciex WIFF reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access ABSciex DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires ABSciex DLLs which only work on Windows"
#endif
		);
}

PWIZ_API_DECL void Reader_ABI::read(const string& filename, const string& head, vector<MSDataPtr>& results) const
{
    throw ReaderFail("[Reader_ABI::read()] ABSciex WIFF reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access ABSciex DLLs - try building with MSVC instead of GCC"
#else // wrong platform
        "requires ABSciex DLLs which only work on Windows"
#endif
		);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_ABI
