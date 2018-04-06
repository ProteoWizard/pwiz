//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2010 Vanderbilt University - Nashville, TN 37232
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

#include "Reader_ABI_T2D.hpp"
#include "pwiz/utility/misc/SHA1Calculator.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/Version.hpp"


PWIZ_API_DECL std::string pwiz::msdata::Reader_ABI_T2D::identify(const std::string& datapath, const std::string& head) const
{
	std::string result;

    if (!bfs::is_directory(datapath))
    {
        if (bal::iends_with(datapath, ".t2d"))
            result = getType();
    }
    else
    {
        vector<bfs::path> t2d_filepaths;
        pwiz::util::expand_pathmask(bfs::path(datapath) / "*.t2d", t2d_filepaths);
        pwiz::util::expand_pathmask(bfs::path(datapath) / "MS/*.t2d", t2d_filepaths);
        pwiz::util::expand_pathmask(bfs::path(datapath) / "MSMS/*.t2d", t2d_filepaths);
        if (!t2d_filepaths.empty())
            result = getType();
    }

    return result;
}


#ifdef PWIZ_READER_ABI_T2D
#include "Reader_ABI_T2D_Detail.hpp"
#include "SpectrumList_ABI_T2D.hpp"


namespace pwiz {
namespace msdata {


using namespace pwiz::util;
using namespace pwiz::msdata::detail;


//
// Reader_ABI
//

namespace {

void fillInSources(const string& datapath, MSData& msd, DataPtr t2d_data)
{
    bfs::path rootpath(datapath);

    BOOST_FOREACH(const bfs::path& t2d_filepath, t2d_data->getSpectrumFilenames())
    {
        // in "/foo/bar/SomeT2Ds/MS/A1.t2d", replace "/foo/bar/SomeT2Ds/" with "" so relativePath is "MS/A1.t2d"
        bfs::path relativePath = t2d_filepath;
        if (rootpath.has_branch_path())
            relativePath = bal::replace_first_copy(relativePath.string(), rootpath.branch_path().string() + "/", "");

        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = relativePath.string();
        sourceFile->name = BFS_STRING(relativePath.leaf());

        // relativePath: <source>\MS\A1.t2d
        // rootpath: c:\path\to\<source>\A1.t2d
        bfs::path location = rootpath.has_branch_path() ?
                             BFS_COMPLETE(rootpath.branch_path() / relativePath) :
                             BFS_COMPLETE(relativePath); // uses initial path
        sourceFile->location = "file://" + location.branch_path().string();

        sourceFile->set(MS_SCIEX_TOF_TOF_T2D_nativeID_format);
        sourceFile->set(MS_SCIEX_TOF_TOF_T2D_format);
        msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
    }
}

void fillInMetadata(const string& datapath, MSData& msd, DataPtr t2d_data)
{
    msd.cvs = defaultCVList();

    msd.id = bfs::basename(datapath);

    SoftwarePtr acquisitionSoftware(new Software);
    acquisitionSoftware->id = "Data Explorer";
    acquisitionSoftware->set(MS_Data_Explorer);
    acquisitionSoftware->version = "unknown";
    msd.softwarePtrs.push_back(acquisitionSoftware);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_ABI_T2D";
    softwarePwiz->set(MS_pwiz);
    softwarePwiz->version = pwiz::msdata::Version::str();
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_ABI_T2D_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    // give ownership of dpPwiz to the SpectrumList
    SpectrumList_ABI_T2D* sl = dynamic_cast<SpectrumList_ABI_T2D*>(msd.run.spectrumListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);

    InstrumentConfigurationPtr ic = translateAsInstrumentConfiguration(*t2d_data);
    ic->softwarePtr = acquisitionSoftware;
    msd.instrumentConfigurationPtrs.push_back(ic);
    msd.run.defaultInstrumentConfigurationPtr = ic;

    msd.run.id = msd.id;
    if (t2d_data->getSampleAcquisitionTime() != blt::local_date_time(bdt::not_a_date_time))
        msd.run.startTimeStamp = encode_xml_datetime(t2d_data->getSampleAcquisitionTime());
}

} // namespace


PWIZ_API_DECL
void Reader_ABI_T2D::read(const string& filename,
                          const string& head,
                          MSData& result,
                          int runIndex,
                          const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_ABI_T2D::read] multiple runs not supported");

    DataPtr t2d_data = Data::create(filename);

    fillInSources(filename, result, t2d_data);

    SpectrumList_ABI_T2D* sl = new SpectrumList_ABI_T2D(result, t2d_data);
    result.run.spectrumListPtr = SpectrumListPtr(sl);

    fillInMetadata(filename, result, t2d_data);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_READER_ABI_T2D

//
// non-MSVC implementation
//

#include "Reader_ABI_T2D.hpp"
#include "pwiz/utility/misc/String.hpp"

namespace pwiz {
namespace msdata {

PWIZ_API_DECL void Reader_ABI_T2D::read(const string& filename, const string& head, MSData& result, int runIndex, const Config& config) const
{
    throw ReaderFail("[Reader_ABI_T2D::read()] ABSciex T2D reader not implemented: "
#ifdef _MSC_VER // should be possible, apparently somebody decided to skip it
        "support was explicitly disabled when program was built"
#elif defined(WIN32) // wrong compiler
        "program was built without COM support and cannot access Data Explorer - try building with MSVC instead of GCC"
#else // wrong platform
        "requires Data Explorer installation which only work on Windows"
#endif
    );
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_READER_ABI_T2D
