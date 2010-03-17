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

#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include "pwiz/utility/misc/Stream.hpp"
#include "DefaultReaderList.hpp"
#include "SpectrumList_mzXML.hpp"
#include "SpectrumList_MGF.hpp"
#include "SpectrumList_MSn.hpp"
#include "SpectrumList_BTDX.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "Serializer_MGF.hpp"
#include "Serializer_MSn.hpp"
#include "References.hpp"
#include "ChromatogramListBase.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "boost/regex.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"

namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;


namespace {

string GetXMLRootElement(const string& fileheader)
{
    const static boost::regex e("<\\?xml.*?>.*?<([^?!]\\S+?)[\\s>]");

    // convert Unicode to ASCII
    string asciiheader;
    asciiheader.reserve(fileheader.size());
    BOOST_FOREACH(char c, fileheader)
    {
        if(c > 0)
            asciiheader.push_back(c);
    }

    boost::smatch m;
    if (boost::regex_search(asciiheader, m, e))
        return m[1];
    throw runtime_error("[GetXMLRootElement] Root element not found (header is not well-formed XML)");
}

string GetXMLRootElement(istream& is)
{
    char buf[513];
    is.read(buf, 512);
    return GetXMLRootElement(buf);
}

string GetXMLRootElementFromFile(const string& filepath)
{
    pwiz::util::random_access_compressed_ifstream file(filepath.c_str());
    if (!file)
        throw runtime_error("[GetXMLRootElementFromFile] Error opening file");
    return GetXMLRootElement(file);
}

void appendSourceFile(const string& filename, MSData& msd)
{
    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = sourceFile->name = p.leaf();
    string location = bfs::complete(p.branch_path()).string();
    if (location.empty()) location = ".";
    sourceFile->location = string("file://") + location;
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

class Reader_mzML : public Reader
{
    public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head);
         return std::string((type(iss) != Type_Unknown)?getType():"");
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runIndex = 0) const
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

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "mzML";}

    private:

    enum Type { Type_mzML, Type_mzML_Indexed, Type_Unknown };

    Type type(istream& is) const
    {
        try
        {
            string rootElement = GetXMLRootElement(is);
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
};


class Reader_mzXML : public Reader
{
    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
        std::string result;
        try
        {
            string rootElement = GetXMLRootElement(head);
            result = (rootElement == "mzXML" || rootElement == "msRun")?getType():"";
        }
        catch (runtime_error&)
        {
        }
        return result;
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result, int runsIndex = 0) const
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
            result.fileDescription.sourceFilePtrs.back()->set(MS_ISB_mzXML_file);
            return;
        }
        catch (SpectrumList_mzXML::index_not_found&)
        {}

        // error looking for index -- try again, but generate index
        is->seekg(0);
        Serializer_mzXML::Config config;
        config.indexed = false;
        Serializer_mzXML serializer(config);
        serializer.read(is, result);
        fillInCommonMetadata(filename, result);
        result.fileDescription.sourceFilePtrs.back()->set(MS_scan_number_only_nativeID_format);
        result.fileDescription.sourceFilePtrs.back()->set(MS_ISB_mzXML_file);
        return;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "mzXML";}
};


class Reader_MGF : public Reader
{
    virtual std::string identify(const string& filename, const string& head) const
    {
        return std::string(((bal::to_lower_copy(bfs::extension(filename)) == ".mgf"))?getType():"");
    }

    virtual void read(const string& filename, const string& head, MSData& result, int runIndex = 0) const
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
        result.fileDescription.sourceFilePtrs.back()->set(MS_Mascot_MGF_file);
        return;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "Mascot Generic";}
};

class Reader_MSn : public Reader
{
  virtual std::string identify(const string& filename, const string& head) const
  {
    bool isOK = (bal::to_lower_copy(bfs::extension(filename)) == ".ms2") ||
                (bal::to_lower_copy(bfs::extension(filename)) == ".cms2") ||
                (bal::to_lower_copy(bfs::extension(filename)) == ".bms2");
 
    return std::string( isOK ? getType() : "" );
  }

  virtual void read(const string& filename, const string& head, MSData& result, int runIndex = 0) const
  {
      if (runIndex != 0)
          throw ReaderFail("[Reader_MSn::read] multiple runs not supported");
      
      MSn_Type filetype = MSn_Type_UNKNOWN;
      if( (bal::to_lower_copy(bfs::extension(filename)) == ".ms2" )){
        filetype = MSn_Type_MS2;
      }else if( (bal::to_lower_copy(bfs::extension(filename)) == ".cms2" )){
        filetype = MSn_Type_CMS2;
      }else if( (bal::to_lower_copy(bfs::extension(filename)) == ".bms2" )){
        filetype = MSn_Type_BMS2;
      }

     shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
     if (!is.get() || !*is)
       throw runtime_error(("[Reader_MSn::read] Unable to open file " + filename));

     Serializer_MSn serializer(filetype);
     serializer.read(is, result);
     fillInCommonMetadata(filename, result);
     result.fileDescription.sourceFilePtrs.back()->set(MS_scan_number_only_nativeID_format);
     result.fileDescription.sourceFilePtrs.back()->set(MS_MS2_file);
     return;
  }

  virtual void read(const std::string& filename,
                    const std::string& head,
                    std::vector<MSDataPtr>& results) const
  {
      results.push_back(MSDataPtr(new MSData));
      read(filename, head, *results.back());
  }

  virtual const char *getType() const {return "MSn";}
};

class Reader_BTDX : public Reader
{
    virtual std::string identify(const string& filename, const string& head) const
    {
        std::string result;
        try
        {
            // TODO: congratulate Bruker for their unique root element name
            string rootElement = GetXMLRootElement(head);
            result = (rootElement == "root")?getType():"";
        }
        catch (runtime_error&)
        {
        }
        return result;
    }

    virtual void read(const string& filename, const string& head, MSData& result, int runIndex = 0) const
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
        sourceFile->name = p.leaf();
        string location = bfs::complete(p.branch_path()).string();
        if (location.empty()) location = ".";
        sourceFile->location = string("file:///") + location;
        result.fileDescription.sourceFilePtrs.push_back(sourceFile);

        result.id = result.run.id = bfs::basename(filename);
        result.run.spectrumListPtr = SpectrumListPtr(SpectrumList_BTDX::create(is, result));
        result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramListSimple);
        return;
    }

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<MSDataPtr>& results) const
    {
        results.push_back(MSDataPtr(new MSData));
        read(filename, head, *results.back());
    }

    virtual const char *getType() const {return "Bruker Data Exchange";}
};


} // namespace


/// default Reader list
PWIZ_API_DECL DefaultReaderList::DefaultReaderList()
{
    push_back(ReaderPtr(new Reader_mzML));
    push_back(ReaderPtr(new Reader_mzXML));
    push_back(ReaderPtr(new Reader_MGF));
    push_back(ReaderPtr(new Reader_MSn));
    push_back(ReaderPtr(new Reader_BTDX));
}


} // namespace msdata
} // namespace pwiz


