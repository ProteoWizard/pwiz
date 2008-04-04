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
#include "DefaultReaderList.hpp"
#include <fstream>
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;


namespace {


void readFile(const string& filename, MSData& msd, const Reader& reader)
{
    // peek at head of file 

    ifstream is(filename.c_str(), ios::binary);
    if (!is)
        throw runtime_error(("[MSDataFile::readFile()] Unable to open file " + filename).c_str());

    string head(512, '\0');
    is.read(&head[0], (std::streamsize)head.size());
    is.close();

    if (!reader.accept(filename, head))
        throw runtime_error("[MSDataFile::readFile()] Unsupported file format.");

    reader.read(filename, head, msd);
}


shared_ptr<DefaultReaderList> defaultReaderList_;


} // namespace


MSDataFile::MSDataFile(const string& filename, const Reader* reader)
{
    if (reader)
    {
        readFile(filename, *this, *reader); 
    }
    else
    {
        if (!defaultReaderList_.get())
            defaultReaderList_ = shared_ptr<DefaultReaderList>(new DefaultReaderList);
        readFile(filename, *this, *defaultReaderList_);
    }
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


