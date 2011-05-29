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


PWIZ_API_DECL std::string pwiz::msdata::Reader_ABI::identify(const std::string& filename, const std::string& head) const
{
	std::string result;
    // TODO: check header signature?
    if (bal::iends_with(filename, ".wiff"))
		result = getType();
    return result;
}


#ifdef PWIZ_READER_ABI
#include "pwiz_aux/msrc/utility/vendor_api/ABI/WiffFile.hpp"
#include "SpectrumList_ABI.hpp"
#include "ChromatogramList_ABI.hpp"
#include "Reader_ABI_Detail.hpp"
#include <windows.h> // GetModuleFileName


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_ABI
//

namespace {

void fillInMetadata(const string& wiffpath, MSData& msd, WiffFilePtr wifffile,
                    ExperimentsMap& experimentsMap, int sample)
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
            experimentsMap[pair<int, int>(ii, iii)] = msExperiment;
            if (msExperiment->getExperimentType() != MRM)
                msd.fileDescription.fileContent.set(translateAsSpectrumType(msExperiment->getExperimentType()));
            else
                msd.fileDescription.fileContent.set(MS_SRM_chromatogram);
        }
    }

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(wiffpath);
    sourceFile->id = "WIFF";
    sourceFile->name = p.leaf();
    string location = bfs::complete(p.branch_path()).string();
    if (location.empty()) location = ".";
    sourceFile->location = "file://" + location;
    sourceFile->set(MS_WIFF_nativeID_format);
    sourceFile->set(MS_ABI_WIFF_file);
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.run.defaultSourceFilePtr = sourceFile;

    // add a SourceFile for the .scan file if it exists
    bfs::path wiffscan = wiffpath + ".scan";
    if (bfs::exists(wiffscan))
    {
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = "WIFFSCAN";
        sourceFile->name = wiffscan.leaf();
        string location = bfs::complete(wiffscan.branch_path()).string();
        if (location.empty()) location = ".";
        sourceFile->location = "file://" + location;
        sourceFile->set(MS_WIFF_nativeID_format);
        sourceFile->set(MS_ABI_WIFF_file);
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
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumList_ABI* sl = dynamic_cast<SpectrumList_ABI*>(msd.run.spectrumListPtr.get());
    ChromatogramList_ABI* cl = dynamic_cast<ChromatogramList_ABI*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    InstrumentConfigurationPtr ic = translateAsInstrumentConfiguration(wifffile);
    ic->softwarePtr = acquisitionSoftware;
    msd.instrumentConfigurationPtrs.push_back(ic);
    msd.run.defaultInstrumentConfigurationPtr = ic;

    msd.run.id = msd.id;
    msd.run.startTimeStamp = encode_xml_datetime(wifffile->getSampleAcquisitionTime());
}

void search_path(bfs::path basepath, string filename, vector<bfs::path>& matchingPaths)
{
    const static bfs::directory_iterator endItr;
    bfs::directory_iterator itr(basepath);
    for (; itr != endItr; ++itr)
    {
        if (bfs::is_directory(itr->status()))
        {
            try
            {
                search_path(itr->path(), filename, matchingPaths);
            }
            catch (bfs::filesystem_error& e)
            {
                // ignore permission errors
                if (e.code() != boost::system::errc::permission_denied)
                    throw e;
            }
        }
        else if (bfs::is_regular_file(itr->status()) && itr->path().filename() == filename)
            matchingPaths.push_back(itr->path());
    }
}

void copyProteinPilotDLLs()
{
    // get the filepath of the calling .exe using WinAPI
    TCHAR tmpFilepath[1024];
    // check for pwiz_bindings_cli.dll first, so that unit tests run from
    // vstesthost.exe run correctly.  if pwiz_bindings_cli.dll is not running,
    // GetModuleHandle will return NULL, and the exe path will be returned.
    DWORD tmpFilepathLength = ::GetModuleFileName(::GetModuleHandle("pwiz_bindings_cli.dll"), (LPCH) tmpFilepath, 1024);
    bfs::path callingExecutablePath = bfs::path(string(tmpFilepath, tmpFilepath + tmpFilepathLength)).parent_path();

    // make sure the necessary DLLs are available side-by-side or copy them if ProteinPilot is installed
    if (!bfs::exists(callingExecutablePath / "ABSciex.DataAccess.WiffFileDataReader.dll"))
    {
        // copy the ProteinPilot DLLs if it is installed, else throw an exception informing the user to download it
        char* programFilesPath = ::getenv("ProgramFiles");
        bfs::path proteinPilotPath;
        if (!programFilesPath)
        {
            if (bfs::exists("C:/Program Files(x86)"))
                proteinPilotPath = "C:/Program Files(x86)/Applied Biosystems MDS Analytical Technologies/ProteinPilot";
            else if (bfs::exists("C:/Program Files"))
                proteinPilotPath = "C:/Program Files/Applied Biosystems MDS Analytical Technologies/ProteinPilot";
            else
                throw runtime_error("[Reader_ABI::ctor] When trying to find Protein Pilot, the Program Files directory could not be found!");
        }
        else
            proteinPilotPath = bfs::path(programFilesPath) / "Applied Biosystems MDS Analytical Technologies/ProteinPilot";

        if (bfs::exists(proteinPilotPath / "ABSciex.DataAccess.WiffFileDataReader.dll"))
        {
            bfs::copy_file(proteinPilotPath / "ABSciex.DataAccess.WiffFileDataReader.dll", callingExecutablePath / "ABSciex.DataAccess.WiffFileDataReader.dll");
            if (!bfs::exists(callingExecutablePath / "Clearcore.dll"))
                bfs::copy_file(proteinPilotPath / "Clearcore.dll", callingExecutablePath / "Clearcore.dll");
            if (!bfs::exists(callingExecutablePath / "ClearCore.Storage.dll"))
                bfs::copy_file(proteinPilotPath / "ClearCore.Storage.dll", callingExecutablePath / "ClearCore.Storage.dll");
            if (!bfs::exists(callingExecutablePath / "rscoree.dll"))
                bfs::copy_file(proteinPilotPath / "rscoree.dll", callingExecutablePath / "rscoree.dll");
        }
        else // couldn't find Protein Pilot 3; try finding a Skyline installation
        {
            // Windows Vista/7 have %LOCALAPPDATA%
            string localAppDataPath = ::getenv("LOCALAPPDATA");

            // Windows 2000/XP have %APPDATA%\Local Settings
            if (localAppDataPath.empty()) localAppDataPath = string(::getenv("USERPROFILE")) + "\\Local Settings";

            if (localAppDataPath.empty())
                throw runtime_error("[Reader_ABI::ctor] When trying to find Skyline, the Local Settings directory could not be found!");

            vector<bfs::path> matchingPaths;
            search_path(localAppDataPath, "rscoree.dll", matchingPaths);

            if (matchingPaths.empty())
                throw std::runtime_error("[Reader_ABI::ctor] Reading ABI WIFF files requires Protein Pilot 3.0 or Skyline to be installed. Skyline is freely available at:\nhttps://brendanx-uw1.gs.washington.edu/labkey/project/home/software/Skyline/begin.view");

            // there could be multiple copies of Skyline installed, so we pick the first one
            bfs::path skylinePath = matchingPaths[0].parent_path();
            
            if (bfs::exists(skylinePath / "ABSciex.DataAccess.WiffFileDataReader.dll"))
            {
                bfs::copy_file(skylinePath / "ABSciex.DataAccess.WiffFileDataReader.dll", callingExecutablePath / "ABSciex.DataAccess.WiffFileDataReader.dll");
                if (!bfs::exists(callingExecutablePath / "Clearcore.dll"))
                    bfs::copy_file(skylinePath / "Clearcore.dll", callingExecutablePath / "Clearcore.dll");
                if (!bfs::exists(callingExecutablePath / "ClearCore.Storage.dll"))
                    bfs::copy_file(skylinePath / "ClearCore.Storage.dll", callingExecutablePath / "ClearCore.Storage.dll");
                if (!bfs::exists(callingExecutablePath / "rscoree.dll"))
                    bfs::copy_file(skylinePath / "rscoree.dll", callingExecutablePath / "rscoree.dll");
            }
            else
                throw runtime_error("[Reader_ABI::ctor] Reading ABI WIFF files requires Protein Pilot 3.0 or Skyline to be installed. Skyline is freely available at:\nhttps://brendanx-uw1.gs.washington.edu/labkey/project/home/software/Skyline/begin.view");
        }
    }
}

} // namespace


PWIZ_API_DECL
void Reader_ABI::read(const string& filename,
                      const string& head,
                      MSData& result,
                      int runIndex) const
{
//    copyProteinPilotDLLs();

    try
    {
        runIndex++; // one-based index
        WiffFilePtr wifffile = WiffFile::create(filename);
        // Loading the experiments are one of the more expensive operations,
        // so make sure it only happens once.
        ExperimentsMap experimentsMap;

        fillInMetadata(filename, result, wifffile, experimentsMap, runIndex);

        SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, experimentsMap, runIndex);
        ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, experimentsMap, runIndex);
        result.run.spectrumListPtr = SpectrumListPtr(sl);
        result.run.chromatogramListPtr = ChromatogramListPtr(cl);
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
//    copyProteinPilotDLLs();

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
                // Loading the experiments are one of the more expensive operations,
                // so make sure it only happens once.
                ExperimentsMap experimentsMap;

                fillInMetadata(filename, result, wifffile, experimentsMap, i);

                SpectrumList_ABI* sl = new SpectrumList_ABI(result, wifffile, experimentsMap, i);
                ChromatogramList_ABI* cl = new ChromatogramList_ABI(result, wifffile, experimentsMap, i);
                result.run.spectrumListPtr = SpectrumListPtr(sl);
                result.run.chromatogramListPtr = ChromatogramListPtr(cl);

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
                      vector<string>& results) const
{
//    copyProteinPilotDLLs();

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

PWIZ_API_DECL void Reader_ABI::read(const string& filename, const string& head, MSData& result, int runIndex) const
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

PWIZ_API_DECL void Reader_ABI::readIds(const std::string& filename, const std::string& head, std::vector<std::string>& results) const
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
