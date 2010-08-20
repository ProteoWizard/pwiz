//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

#include "MzIdentMLFile.hpp"
#include "TextWriter.hpp"
#include "Serializer_mzid.hpp"
#include "DefaultReaderList.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp" // for charcounter defn
#include "boost/iostreams/device/file.hpp"
#include "boost/iostreams/filtering_stream.hpp" 
#include "boost/iostreams/filter/gzip.hpp" 


namespace pwiz {
namespace mziddata {


using namespace pwiz::util;


namespace {


void readFile(const string& filename, MzIdentML& mzid, const Reader& reader, const string& head)
{
    if (!reader.accept(filename, head))
        throw runtime_error("[MzIdentMLFile::readFile()] Unsupported file format.");

    reader.read(filename, head, mzid);
}


shared_ptr<DefaultReaderList> defaultReaderList_;


} // namespace


PWIZ_API_DECL MzIdentMLFile::MzIdentMLFile(const string& filename, const Reader* reader)
{
    // peek at head of file 
    string head = read_file_header(filename, 512);

    if (reader)
    {
        readFile(filename, *this, *reader, head); 
    }
    else
    {
        if (!defaultReaderList_.get())
            defaultReaderList_ = shared_ptr<DefaultReaderList>(new DefaultReaderList);
        readFile(filename, *this, *defaultReaderList_, head);
    }
}


PWIZ_API_DECL
void MzIdentMLFile::write(const string& filename, const WriteConfig& config)
{
    write(*this, filename, config); 
}


namespace {


shared_ptr<ostream> openFile(const string& filename)
{
    shared_ptr<ostream> result(new ofstream(filename.c_str(), ios::binary));
    
    if (!result.get() || !*result)
        throw runtime_error(("[MzIdentMLFile::openFile()] Unable to open file " + filename).c_str());
    
    return result; 		
}


void writeStream(ostream& os, const MzIdentML& td, const MzIdentMLFile::WriteConfig& config)
{
    switch (config.format)
    {
        case MzIdentMLFile::Format_Text:
        {
            TextWriter(os,0)(td);
            break;
        }

        case MzIdentMLFile::Format_MzIdentML:
        {
            Serializer_mzIdentML serializer;
            serializer.write(os, td);
            break;
        }

        default:
            throw runtime_error("[MzIdentMLFile::write()] Format not implemented.");
    }
}


} // namespace


PWIZ_API_DECL
void MzIdentMLFile::write(const MzIdentML& td,
                        const string& filename,
                        const WriteConfig& config)
{
    shared_ptr<ostream> os = openFile(filename);
    writeStream(*os, td, config);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, MzIdentMLFile::Format format)
{
    switch (format)
    {
        case MzIdentMLFile::Format_Text:
            os << "Text";
            return os;
        case MzIdentMLFile::Format_MzIdentML:
            os << "traML";
            return os;
        default:
            os << "Unknown";
            return os;
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const MzIdentMLFile::WriteConfig& config)
{
    os << config.format;
    return os;
}


} // namespace mziddata
} // namespace pwiz


