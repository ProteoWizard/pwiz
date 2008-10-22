//
// DefaultReaderList.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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

#include "utility/misc/Filesystem.hpp"
#include "utility/misc/String.hpp"
#include "utility/misc/Stream.hpp"
#include "DefaultReaderList.hpp"
#include "SpectrumList_mzXML.hpp"
#include "SpectrumList_MGF.hpp"
#include "SpectrumList_BTDX.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "References.hpp"
#include "boost/regex.hpp"
#include "boost/foreach.hpp"
#include "utility/misc/random_access_compressed_ifstream.hpp"

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
    sourceFile->location = string("file://") + bfs::complete(p.branch_path()).string();
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);
}

class Reader_mzML : public Reader
{
    public:

    virtual std::string identify(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head); 
		 return std::string((type(iss) != Type_Unknown)?getType():""); 
    }

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
		shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzML] Unable to open file " + filename).c_str());

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

        appendSourceFile(filename, result);
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

    virtual void read(const std::string& filename, const std::string& head, MSData& result) const
    {
        shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzXML] Unable to open file " + filename).c_str());

        try
        {
            // assume there is a scan index
            Serializer_mzXML serializer;
            serializer.read(is, result);
            appendSourceFile(filename, result);
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
        appendSourceFile(filename, result);
        return;
    }
	virtual const char *getType() const {return "mzXML";}
};


class Reader_MGF : public Reader
{
    virtual std::string identify(const string& filename, const string& head) const
    {
		return std::string(((bal::to_lower_copy(bfs::extension(filename)) == ".mgf"))?getType():"");
    }

    virtual void read(const string& filename, const string& head, MSData& result) const
    {
        shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[Reader_MGF::read] Unable to open file " + filename));

        result.fileDescription.fileContent.set(MS_MSn_spectrum);
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = "MGF1";
        bfs::path p(filename);
        sourceFile->name = p.leaf();
        sourceFile->location = string("file://") + bfs::complete(p.branch_path()).string();
        result.fileDescription.sourceFilePtrs.push_back(sourceFile);
        result.run.id = "Run1";
        result.run.spectrumListPtr = SpectrumListPtr(SpectrumList_MGF::create(is, result));
        result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramListSimple);
        return;
    }
	virtual const char *getType() const {return "Mascot Generic";}
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

    virtual void read(const string& filename, const string& head, MSData& result) const
    {
        shared_ptr<istream> is(new pwiz::util::random_access_compressed_ifstream(filename.c_str()));
        if (!is.get() || !*is)
            throw runtime_error(("[Reader_BTDX::read] Unable to open file " + filename));

        result.fileDescription.fileContent.set(MS_MSn_spectrum);
        result.fileDescription.fileContent.set(MS_centroid_mass_spectrum);
        SourceFilePtr sourceFile(new SourceFile);
        sourceFile->id = "BTDX1";
        bfs::path p(filename);
        sourceFile->name = p.leaf();
        sourceFile->location = string("file://") + bfs::complete(p.branch_path()).string();
        result.fileDescription.sourceFilePtrs.push_back(sourceFile);
        result.run.id = "Run1";
        result.run.spectrumListPtr = SpectrumListPtr(SpectrumList_BTDX::create(is, result));
        result.run.chromatogramListPtr = ChromatogramListPtr(new ChromatogramListSimple);
        return;
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
    push_back(ReaderPtr(new Reader_BTDX));
}


} // namespace msdata
} // namespace pwiz


