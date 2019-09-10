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

#include "Reader_ABI.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include <boost/foreach_field.hpp>


PWIZ_API_DECL std::string pwiz::msdata::Reader_ABI::identify(const std::string& filename, const std::string& head) const
{
	std::string result;
    // TODO: check header signature?
    if (bal::iends_with(filename, ".wiff") || bal::iends_with(filename, ".wiff2"))
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


using namespace pwiz::util;
using namespace pwiz::msdata::detail;
using namespace pwiz::msdata::detail::ABI;


//
// Reader_ABI
//

namespace {

void fillInMetadata(const string& wiffpath, MSData& msd, WiffFilePtr wifffile,
                    const ExperimentsMap& experimentsMap, int sample, const Reader::Config& config)
{
    msd.cvs = defaultCVList();

    string sampleName = wifffile->getSampleNames()[sample-1];

    BOOST_FOREACH_FIELD((boost::fusion::ignore)(const ExperimentPtr& msExperiment), experimentsMap)
    {
        if (msExperiment->getExperimentType() != MRM)
            msd.fileDescription.fileContent.set(translateAsSpectrumType(msExperiment->getExperimentType()));
        else
            msd.fileDescription.fileContent.set(MS_SRM_chromatogram);
    }

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(wiffpath);
    sourceFile->id = "WIFF";
    sourceFile->name = BFS_STRING(p.leaf());
    string location = BFS_COMPLETE(p.branch_path()).string();
    if (location.empty()) location = ".";
    sourceFile->location = "file://" + location;
    sourceFile->set(MS_WIFF_nativeID_format);
    sourceFile->set(MS_ABI_WIFF_format);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    // add a SourceFile for the .scan file if it exists
    bfs::path wiffscan = wiffpath + ".scan";
    if (bfs::exists(wiffscan))
    {
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = "WIFFSCAN";
        sourceFile->name = BFS_STRING(wiffscan.leaf());
        string location = BFS_COMPLETE(wiffscan.branch_path()).string();
        if (location.empty()) location = ".";
        sourceFile->location = "file://" + location;
        sourceFile->set(MS_WIFF_nativeID_format);
        sourceFile->set(MS_ABI_WIFF_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }

    msd.id = bfs::basename(p);
    if (!sampleName.empty())
    {
        // if the basename is in the sample name, just use the sample name;
        // otherwise add the sample name as a suffix
        if(sampleName.find(msd.id) != string::npos)
            msd.id = sampleName;
        else
            msd.id += "-" + sampleName;
    }

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
    dpPwiz->processingMethods.back().set(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_ABI* sl = dynamic_cast<SpectrumList_ABI*>(msd.run.spectrumListPtr.get());
    ChromatogramList_ABI* cl = dynamic_cast<ChromatogramList_ABI*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    auto instrumentModel = InstrumentModel_Unknown;
    try
    {
        instrumentModel = wifffile->getInstrumentModel();
    }
    catch (runtime_error&)
    {
        if (config.unknownInstrumentIsError)
            throw;
    }

    InstrumentConfigurationPtr ic = translateAsInstrumentConfiguration(instrumentModel, IonSpray);
    ic->softwarePtr = acquisitionSoftware;

    auto serialNumber = wifffile->getInstrumentSerialNumber();
    if (!serialNumber.empty())
        ic->set(MS_instrument_serial_number, serialNumber);

    msd.instrumentConfigurationPtrs.push_back(ic);
    msd.run.defaultInstrumentConfigurationPtr = ic;

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(wifffile->getSampleAcquisitionTime(sample, config.adjustUnknownTimeZonesToHostTimeZone));
}

void cacheExperiments(WiffFilePtr wifffile, ExperimentsMap& experimentsMap, int sample)
{
    int periodCount = wifffile->getPeriodCount(sample);
    for (int ii=1; ii <= periodCount; ++ii)
    {
        int experimentCount = wifffile->getExperimentCount(sample, ii);
        for (int iii=1; iii <= experimentCount; ++iii)
            experimentsMap[make_pair(ii, iii)] = wifffile->getExperiment(sample, ii, iii);
    }
}

} // namespace

PWIZ_API_DECL
void Reader_ABI::read(const string& filename,
                      const string& head,
                      MSData& result,
                      int runIndex,
                      const Config& config) const
{
    try
    {
        runIndex++; // one-based index
        WiffFilePtr wifffile = WiffFile::create(filename);

        // Loading the experiments is an expensive operation, so cache them.
        ExperimentsMap experimentsMap;
        cacheExperiments(wifffile, experimentsMap, runIndex);

        SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, experimentsMap, runIndex, config);
        ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, experimentsMap, runIndex);
        result.run.spectrumListPtr = SpectrumListPtr(sl);
        result.run.chromatogramListPtr = ChromatogramListPtr(cl);

        fillInMetadata(filename, result, wifffile, experimentsMap, runIndex, config);
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
                      vector<MSDataPtr>& results,
                      const Config& config) const
{
    try
    {
        WiffFilePtr wifffile = WiffFile::create(filename);

        int sampleCount = wifffile->getSampleCount();
        for (int i=1; i <= sampleCount; ++i)
        {
            try
            {
                MSDataPtr msDataPtr = MSDataPtr(new MSData);
                MSData& result = *msDataPtr;

                // Loading the experiments is an expensive operation, so cache them.
                ExperimentsMap experimentsMap;
                cacheExperiments(wifffile, experimentsMap, i);

                SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, experimentsMap, i, config);
                ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, experimentsMap, i);
                result.run.spectrumListPtr = SpectrumListPtr(sl);
                result.run.chromatogramListPtr = ChromatogramListPtr(cl);

                fillInMetadata(filename, result, wifffile, experimentsMap, i, config);

                results.push_back(msDataPtr);
            }
            catch (exception& e)
            {
                // TODO: make this a critical logged warning
                cerr << "[Reader_ABI::read] Error opening run " << i << " in " << bfs::path(filename).leaf() << ":\n" << e.what() << endl;
            }
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

PWIZ_API_DECL
void Reader_ABI::readIds(const string& filename,
                      const string& head,
                      vector<string>& results,
                      const Config& config) const
{
    try
    {
        WiffFilePtr wifffile = WiffFile::create(filename);
        vector<string> sampleNames = wifffile->getSampleNames();
        for (vector<string>::iterator it = sampleNames.begin(); it != sampleNames.end(); it++)
            results.push_back(*it);
    }
    catch (std::exception& e)
    {
        throw std::runtime_error(e.what());
    }
    catch (...)
    {
        throw runtime_error("[Reader_ABI::readIds()] unhandled exception");
    }
}

} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_ABI

//
// non-MSVC implementation
//

#include "Reader_ABI.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {

PWIZ_API_DECL void Reader_ABI::read(const string& filename, const string& head, MSData& result, int runIndex, const Config& config) const
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

PWIZ_API_DECL void Reader_ABI::read(const string& filename, const string& head, vector<MSDataPtr>& results, const Config& config) const
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

PWIZ_API_DECL void Reader_ABI::readIds(const std::string& filename, const std::string& head, std::vector<std::string>& results, const Config& config) const
{
    throw ReaderFail("[Reader_ABI::readIds()] ABSciex WIFF reader not implemented: "
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
