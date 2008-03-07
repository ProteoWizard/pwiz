//
// MSDataFile.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
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


#include "MSDataFile.hpp"
#include "TextWriter.hpp"
#include "Serializer_mzML.hpp"
#include "Serializer_mzXML.hpp"
#include "SpectrumList_mzXML.hpp"
#include "Reader_RAW.hpp"
#include <iostream> 
#include <fstream> 
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;


namespace {


class Reader_mzML : public MSDataFile::Reader
{
    public:

    virtual bool accept(const std::string& filename, const std::string& head) const
    {
         istringstream iss(head); 
         return type(iss) != Type_Unknown; 
    }

    virtual void read(const std::string& filename, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
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
    }

    private:

    enum Type { Type_mzML, Type_mzML_Indexed, Type_Unknown }; 

    Type type(istream& is) const
    {
        is.seekg(0);

        string buffer;
        is >> buffer;

        if (buffer != "<?xml")
            return Type_Unknown;
            
        getline(is, buffer);
        is >> buffer; 

        if (buffer == "<indexedmzML")
            return Type_mzML_Indexed;
        else if (buffer == "<mzML")
            return Type_mzML;
        else
            return Type_Unknown;
    }
};


class Reader_mzXML : public MSDataFile::Reader
{
    virtual bool accept(const std::string& filename, const std::string& head) const
    {
        istringstream iss(head); 

        string buffer;
        iss >> buffer;

        if (buffer != "<?xml") return false;

        getline(iss, buffer);
        iss >> buffer; 

        return (buffer=="<mzXML" || buffer=="<msRun");
    }

    virtual void read(const std::string& filename, MSData& result) const
    {
        shared_ptr<istream> is(new ifstream(filename.c_str(), ios::binary));
        if (!is.get() || !*is)
            throw runtime_error(("[MSDataFile::Reader_mzXML] Unable to open file " + filename).c_str());

        try
        {
            // assume there is a scan index
            Serializer_mzXML serializer;
            serializer.read(is, result);
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
        return;
    }
};


// static instances of default readers
Reader_mzML reader_mzML_;
Reader_mzXML reader_mzXML_;
Reader_RAW reader_RAW_;


vector<const MSDataFile::Reader*> readers_;


void initializeDefaultReaders()
{
    // initialize default Readers if we don't have anything registered 

    if (readers_.empty())
    {
        readers_.push_back(&reader_mzML_);
        readers_.push_back(&reader_mzXML_);
        readers_.push_back(&reader_RAW_);
    }
}


} // namespace


void MSDataFile::registerReader(const Reader& reader)
{
    readers_.clear();
    readers_.push_back(&reader);
}


void MSDataFile::clearReader()
{
    readers_.clear();
}


namespace {


void readFile(const string& filename, MSData& msd)
{
    // peek at head of file 

    ifstream is(filename.c_str(), ios::binary);
    if (!is)
        throw runtime_error(("[MSDataFile::readFile()] Unable to open file " + filename).c_str());

    string head(512, '\0');
    is.read(&head[0], (std::streamsize)head.size());
    is.close();

    // delegate to Readers

    initializeDefaultReaders();

    for (vector<const MSDataFile::Reader*>::const_iterator it=readers_.begin();
         it!=readers_.end(); ++it)
    {
        if ((*it)->accept(filename, head))
        {
            (*it)->read(filename, msd);
            return;
        }
    }

    throw runtime_error("Unsupported file format.");
}


} // namespace


MSDataFile::MSDataFile(const string& filename)
{
    readFile(filename, *this); 
}


void MSDataFile::write(const string& filename,
                       const WriteConfig& config)
{
    write(*this, filename, config); 
}


namespace {


shared_ptr<ostream> openFile(const string& filename)
{
    shared_ptr<ostream> result(new ofstream(filename.c_str(), ios::binary));

    if (!result.get() || !*result)
        throw runtime_error(("[MSDataFile::openFile()] Unable to open file " + filename).c_str());

    return result; 
}


void writeStream(ostream& os, const MSData& msd, const MSDataFile::WriteConfig& config)
{
    switch (config.format)
    {
        case MSDataFile::Format_Text:
        {
            TextWriter(os,0)(msd);
            break;
        }
        case MSDataFile::Format_mzML:
        {
            Serializer_mzML::Config serializerConfig;
            serializerConfig.binaryDataEncoderConfig = config.binaryDataEncoderConfig;
            serializerConfig.indexed = config.indexed;
            Serializer_mzML serializer(serializerConfig);
            serializer.write(os, msd);
            break;
        }
        case MSDataFile::Format_mzXML:
        {
            Serializer_mzXML::Config serializerConfig;
            serializerConfig.binaryDataEncoderConfig = config.binaryDataEncoderConfig;
            serializerConfig.indexed = config.indexed;
            Serializer_mzXML serializer(serializerConfig);
            serializer.write(os, msd);
            break;            
        }
        default:
        {
            throw runtime_error("[MSDataFile::write()] Format not implemented.");
        }
    }
}


} // namespace


void MSDataFile::write(const MSData& msd,
                       const string& filename,
                       const WriteConfig& config)
{
    shared_ptr<ostream> os = openFile(filename);
    writeStream(*os, msd, config);
}


ostream& operator<<(ostream& os, MSDataFile::Format format)
{
    switch (format)
    {
        case MSDataFile::Format_Text:
            os << "Text";
            return os;
        case MSDataFile::Format_mzML:
            os << "mzML";
            return os;
        case MSDataFile::Format_mzXML:
            os << "mzXML";
            return os;
        default:
            os << "Unknown";
            return os;
    }
}


ostream& operator<<(ostream& os, const MSDataFile::WriteConfig& config)
{
    os << config.format << " " << config.binaryDataEncoderConfig 
       << " indexed=\"" << boolalpha << config.indexed << "\"";
    return os;
}


} // namespace msdata
} // namespace pwiz


