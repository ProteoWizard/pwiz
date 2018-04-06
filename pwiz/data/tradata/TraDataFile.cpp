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

#include "TraDataFile.hpp"
#include "TextWriter.hpp"
#include "Serializer_traML.hpp"
#include "DefaultReaderList.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp" // for charcounter defn
#include "boost/iostreams/device/file.hpp"
#include "boost/iostreams/filtering_stream.hpp" 
#include "boost/iostreams/filter/gzip.hpp" 


namespace pwiz {
namespace tradata {


using namespace pwiz::util;
using boost::shared_ptr;


namespace {


void readFile(const string& filename, TraData& td, const Reader& reader, const string& head)
{
    if (!reader.accept(filename, head))
        throw runtime_error("[TraDataFile::readFile()] Unsupported file format.");

    reader.read(filename, head, td);
}


shared_ptr<DefaultReaderList> defaultReaderList_;


} // namespace


PWIZ_API_DECL TraDataFile::TraDataFile(const string& filename, const Reader* reader)
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
void TraDataFile::write(const string& filename, const WriteConfig& config)
{
    write(*this, filename, config); 
}


namespace {


shared_ptr<ostream> openFile(const string& filename, bool gzipped)
{
	if (gzipped) 
	{   // use boost's filter stack to count outgoing bytes, and gzip them
		boost::iostreams::filtering_ostream *filt = new boost::iostreams::filtering_ostream();
		shared_ptr<ostream> result(filt);
		if (filt)
		{
		filt->push(pwiz::minimxml::charcounter()); // for counting bytes before compression
		filt->push(boost::iostreams::gzip_compressor(9)); // max compression
		filt->push(boost::iostreams::file_sink(filename.c_str(), ios::binary));
		}
		if (!result.get() || !*result || !filt->good())
			throw runtime_error(("[TraDataFile::openFile()] Unable to open file " + filename).c_str());
	    return result; 
	} else 
	{
		shared_ptr<ostream> result(new ofstream(filename.c_str(), ios::binary));

		if (!result.get() || !*result)
			throw runtime_error(("[TraDataFile::openFile()] Unable to open file " + filename).c_str());

		return result; 		
	}
}


void writeStream(ostream& os, const TraData& td, const TraDataFile::WriteConfig& config)
{
    switch (config.format)
    {
        case TraDataFile::Format_Text:
        {
            TextWriter(os,0)(td);
            break;
        }

        case TraDataFile::Format_traML:
        {
            Serializer_traML serializer;
            serializer.write(os, td);
            break;
        }

        default:
            throw runtime_error("[TraDataFile::write()] Format not implemented.");
    }
}


} // namespace


PWIZ_API_DECL
void TraDataFile::write(const TraData& td,
                        const string& filename,
                        const WriteConfig& config)
{
    shared_ptr<ostream> os = openFile(filename,config.gzipped);
    writeStream(*os, td, config);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, TraDataFile::Format format)
{
    switch (format)
    {
        case TraDataFile::Format_Text:
            os << "Text";
            return os;
        case TraDataFile::Format_traML:
            os << "traML";
            return os;
        default:
            os << "Unknown";
            return os;
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const TraDataFile::WriteConfig& config)
{
    os << config.format;
    return os;
}


} // namespace tradata
} // namespace pwiz

