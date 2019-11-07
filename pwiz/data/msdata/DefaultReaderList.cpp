//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/minimxml/SAXParser.hpp"
#include "DefaultReaderList.hpp"
#include "SpectrumList_mzXML.hpp"
#include "SpectrumList_MGF.hpp"
#include "SpectrumList_MSn.hpp"
#include "SpectrumList_BTDX.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "Serializer_MGF.hpp"
#include "Serializer_MSn.hpp"
#ifndef WITHOUT_MZ5
#include "Serializer_mz5.hpp"
#endif
#include "References.hpp"
#include "ChromatogramListBase.hpp"
#include "pwiz/data/msdata/Version.hpp"
#ifndef WITHOUT_MZ5
#include "mz5/Connection_mz5.hpp"
#endif

namespace pwiz {
namespace msdata {


using namespace pwiz::minimxml;


namespace {

void appendSourceFile(const string& filename, MSData& msd)
{
    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = sourceFile->name = BFS_STRING(p.leaf());
    sourceFile->location = "file:///" + BFS_COMPLETE(p.branch_path()).string();
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
}

SoftwarePtr getSoftwarePwiz(vector<SoftwarePtr>& softwarePtrs)
{
    string version = pwiz::msdata::Version::str();

    for (vector<SoftwarePtr>::const_iterator it=softwarePtrs.begin(); it!=softwarePtrs.end(); ++it)
        if ((*it)->hasCVParam(MS_pwiz) && (*it)->version==version)
            return *it;

    SoftwarePtr sp(new Software);
    sp->id = "pwiz_" + version;
    sp->set(MS_pwiz);
    sp->version = pwiz::msdata::Version::str();
    softwarePtrs.push_back(sp);
    return sp;
}

void fillInCommonMetadata(const string& filename, MSData& msd)
{
    appendSourceFile(filename, msd);
    msd.cvs = defaultCVList();

    SoftwarePtr softwarePwiz = getSoftwarePwiz(msd.softwarePtrs);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_conversion";
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);

    // give ownership of dpPwiz to the SpectrumList (and ChromatogramList)
    SpectrumListBase* sl = dynamic_cast<SpectrumListBase*>(msd.run.spectrumListPtr.get());
    ChromatogramListBase* cl = dynamic_cast<ChromatogramListBase*>(msd.run.chromatogramListPtr.get());
    if (sl) sl->setDataProcessingPtr(dpPwiz);
    if (cl) cl->setDataProcessingPtr(dpPwiz);

    // the file-level ids can't be empty
    if (msd.id.empty() || msd.run.id.empty())
        msd.id = msd.run.id = bfs::basename(filename);
}

// return true if filename has form xxxx.ext or xxxx.ext.gz
static bool has_extension(std::string const &test_filename,const char *ext)
{
    std::string filename(test_filename);
    if (bal::iends_with(filename, ".gz"))
        filename.erase(filename.length()-3);
    return bal::iends_with(filename, ext);
}

} // namespace


//
// Reader_mzML
//

PWIZ_API_DECL std::string Reader_mzML::identify(const std::string& filename, const std::string& head) const
{
     istringstream iss(head);
     return std::string((type(iss) != Type_Unknown)?getType():"");
}

PWIZ_API_DECL void Reader_mzML::read(const std::string& filename,
                                     const std::string& head,
                                     MSData& result,
                                     int runIndex,
                                     const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_mzML::read] multiple runs not supported");

    shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
    if (!is.get() || !*is)
        throw runtime_error(("[Reader_mzML::read] Unable to open file " + filename).c_str());

    switch (type(*is))
    {
        case Type_mzML:
        {
            Serializer_mzML::Config config;
            config.indexed = false;
            Serializer_mzML serializer(config);
            serializer.read(is, result);
            break;
        }
        case Type_mzML_Indexed:
        {
            Serializer_mzML serializer;
            serializer.read(is, result);
            break;
        }
        case Type_Unknown:
        default:
        {
            throw runtime_error("[MSDataFile::Reader_mzML] This isn't happening.");
        }
    }

    fillInCommonMetadata(filename, result);
}

PWIZ_API_DECL void Reader_mzML::read(const std::string& filename,
                                     const std::string& head,
                                     std::vector<MSDataPtr>& results,
                                     const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back(), 0, config);
}

Reader_mzML::Type Reader_mzML::type(istream& is) const
{
    try
    {
        string rootElement = xml_root_element(is);
        if (rootElement == "indexedmzML")
            return Type_mzML_Indexed;
        if (rootElement == "mzML")
            return Type_mzML;
    }
    catch (runtime_error&)
    {
    }
    return Type_Unknown;
}


//
// Reader_mzXML
//

PWIZ_API_DECL std::string Reader_mzXML::identify(const std::string& filename, const std::string& head) const
{
    std::string result;
    try
    {
        string rootElement = xml_root_element(head);
        result = (rootElement == "mzXML" || rootElement == "msRun")?getType():"";
    }
    catch (runtime_error&)
    {
    }
    return result;
}

PWIZ_API_DECL void Reader_mzXML::read(const std::string& filename,
                                      const std::string& head,
                                      MSData& result,
                                      int runsIndex,
                                      const Config& config) const
{
    if (runsIndex != 0)
        throw ReaderFail("[Reader_mzXML::read] multiple runs not supported");

    shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
    if (!is.get() || !*is)
        throw runtime_error(("[Reader_mzXML::read] Unable to open file " + filename).c_str());

    try
    {
        // assume there is a scan index
        Serializer_mzXML serializer;
        serializer.read(is, result);
        fillInCommonMetadata(filename, result);
        result.fileDescription.sourceFilePtrs.back()->set(MS_scan_number_only_nativeID_format);
        result.fileDescription.sourceFilePtrs.back()->set(MS_ISB_mzXML_format);
        return;
    }
    catch (SpectrumList_mzXML::index_not_found&)
    {}

    // error looking for index -- try again, but generate index
    is->seekg(0);
    Serializer_mzXML::Config serializerConfig;
    serializerConfig.indexed = false;
    Serializer_mzXML serializer(serializerConfig);
    serializer.read(is, result);
    fillInCommonMetadata(filename, result);
    result.fileDescription.sourceFilePtrs.back()->set(MS_scan_number_only_nativeID_format);
    result.fileDescription.sourceFilePtrs.back()->set(MS_ISB_mzXML_format);
    return;
}

PWIZ_API_DECL void Reader_mzXML::read(const std::string& filename,
                                      const std::string& head,
                                      std::vector<MSDataPtr>& results,
                                      const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back(), 0, config);
}


//
// Reader_MGF
//

PWIZ_API_DECL std::string Reader_MGF::identify(const string& filename, const string& head) const
{
    return std::string(((bal::to_lower_copy(bfs::extension(filename)) == ".mgf"))?getType():"");
}

PWIZ_API_DECL void Reader_MGF::read(const string& filename,
                                    const string& head,
                                    MSData& result,
                                    int runIndex,
                                    const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_MGF::read] multiple runs not supported");

    shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
    if (!is.get() || !*is)
        throw runtime_error(("[Reader_MGF::read] Unable to open file " + filename));

    Serializer_MGF serializer;
    serializer.read(is, result);
    fillInCommonMetadata(filename, result);
    result.fileDescription.sourceFilePtrs.back()->set(MS_multiple_peak_list_nativeID_format);
    result.fileDescription.sourceFilePtrs.back()->set(MS_Mascot_MGF_format);
    return;
}

PWIZ_API_DECL void Reader_MGF::read(const std::string& filename,
                                    const std::string& head,
                                    std::vector<MSDataPtr>& results,
                                    const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back());
}


//
// Reader_MSn
//

PWIZ_API_DECL std::string Reader_MSn::identify(const string& filename, const string& head) const
{
    if (has_extension(filename, ".ms1") || has_extension(filename, ".cms1") || has_extension(filename, ".bms1"))
        return "MS1";
    if (has_extension(filename, ".ms2") || has_extension(filename, ".cms2") || has_extension(filename, ".bms2"))
        return "MS2";
    return "";
}

PWIZ_API_DECL void Reader_MSn::read(const string& filename,
                                    const string& head,
                                    MSData& result,
                                    int runIndex,
                                    const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_MSn::read] multiple runs not supported");

    MSn_Type filetype = MSn_Type_UNKNOWN;
    if (has_extension(filename, ".ms1"))
        filetype = MSn_Type_MS1;
    else if (has_extension(filename, ".cms1"))
        filetype = MSn_Type_CMS1;
    else if (has_extension(filename, ".bms1"))
        filetype = MSn_Type_BMS1;
    else if (has_extension(filename, ".ms2"))
        filetype = MSn_Type_MS2;
    else if (has_extension(filename, ".cms2"))
        filetype = MSn_Type_CMS2;
    else if (has_extension(filename, ".bms2"))
        filetype = MSn_Type_BMS2;

    shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
    if (!is.get() || !*is)
        throw runtime_error(("[Reader_MSn::read] Unable to open file " + filename));

    Serializer_MSn serializer(filetype);
    serializer.read(is, result);
    fillInCommonMetadata(filename, result);
    result.fileDescription.sourceFilePtrs.back()->set(MS_scan_number_only_nativeID_format);
    result.fileDescription.sourceFilePtrs.back()->set(MS_MS2_format);
}

PWIZ_API_DECL void Reader_MSn::read(const std::string& filename,
                                    const std::string& head,
                                    std::vector<MSDataPtr>& results,
                                    const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back());
}


//
// Reader_BTDX
//

PWIZ_API_DECL std::string Reader_BTDX::identify(const string& filename, const string& head) const
{
    std::string result;
    try
    {
        // TODO: congratulate Bruker for their unique root element name
        string rootElement = xml_root_element(head);
        result = (rootElement == "root")?getType():"";
    }
    catch (runtime_error&)
    {
    }
    return result;
}

PWIZ_API_DECL void Reader_BTDX::read(const string& filename,
                                     const string& head,
                                     MSData& result,
                                     int runIndex,
                                     const Config& config) const
{
    if (runIndex != 0)
        throw ReaderFail("[Reader_BTDX::read] multiple runs not supported");

    shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
    if (!is.get() || !*is)
        throw runtime_error(("[Reader_BTDX::read] Unable to open file " + filename));

    result.fileDescription.fileContent.set(MS_MSn_spectrum);
    result.fileDescription.fileContent.set(MS_centroid_spectrum);
    SourceFilePtr sourceFile(new SourceFile);
    sourceFile->id = "BTDX1";
    bfs::path p(filename);
    sourceFile->name = BFS_STRING(p.leaf());
    sourceFile->location = "file:///" + BFS_COMPLETE(p.branch_path()).string();
    result.fileDescription.sourceFilePtrs.push_back(sourceFile);

    result.id = result.run.id = bfs::basename(filename);
    result.run.spectrumListPtr = SpectrumListPtr(SpectrumList_BTDX::create(is, result));
    result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramListSimple);
    return;
}

PWIZ_API_DECL void Reader_BTDX::read(const std::string& filename,
                                     const std::string& head,
                                     std::vector<MSDataPtr>& results,
                                     const Config& config) const
{
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back());
}


//
// Reader_mz5
//

// TODO: add mz5 specific header and check this. This version only checks whether the file is a HDF5 file.
namespace {

const char mz5Header[] = {'\x89', '\x48', '\x44', '\x46', '\x0d', '\x0a', '\x1a', '\x0a'};
const size_t mz5HeaderSize = sizeof(mz5Header) / sizeof(char);

} // namespace

PWIZ_API_DECL std::string Reader_mz5::identify(const string& filename, const string& head) const
{
    if (head.length() < mz5HeaderSize)
        return "";

    for (size_t i=0; i < mz5HeaderSize; ++i)
        if (head[i] != mz5Header[i])
            return "";

    try
    {
#ifndef WITHOUT_MZ5
        mz5::Connection_mz5 c(filename, mz5::Connection_mz5::ReadOnly);
#endif
        return getType();
    }
    catch (ReaderFail& e)
    {
        if (bal::contains(e.what(), "MZ5 does not support Unicode"))
            throw e;
    }
    catch (std::runtime_error&)
    {
        return "";
    }

    return "";
}

PWIZ_API_DECL void Reader_mz5::read(const string& filename,
                                    const string& head,
                                    MSData& result,
                                    int runIndex,
                                    const Config& config) const
{
#ifdef WITHOUT_MZ5
    throw ReaderFail("[Reader_mz5::read] library was not built with mz5 support.");
#else
    if (runIndex != 0)
        throw ReaderFail("[Reader_mz5::read] multiple runs not supported, yet...");

    Serializer_mz5 serializer;
    serializer.read(filename, result);

    // TODO: add "conversion to mz5 tag", sourceFile history and pwiz

    // the file-level ids can't be empty
    if (result.id.empty() || result.run.id.empty())
        result.id = result.run.id = bfs::basename(filename);
#endif
}

PWIZ_API_DECL void Reader_mz5::read(const std::string& filename,
                                    const std::string& head,
                                    std::vector<MSDataPtr>& results,
                                    const Config& config) const
{
    // TODO multiple read mz5
    results.push_back(MSDataPtr(new MSData));
    read(filename, head, *results.back());
}


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList()
{
    emplace_back(new Reader_mzML);
    emplace_back(new Reader_mzXML);
    emplace_back(new Reader_MGF);
    emplace_back(new Reader_MS1);
    emplace_back(new Reader_MS2);
    emplace_back(new Reader_BTDX);
    emplace_back(new Reader_mz5);
}


} // namespace msdata
} // namespace pwiz


