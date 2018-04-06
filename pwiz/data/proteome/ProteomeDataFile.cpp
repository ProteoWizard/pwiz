//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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


#define PWIZ_SOURCE

#include "ProteomeDataFile.hpp"
#include "TextWriter.hpp"
#include "Reader_FASTA.hpp"
#include "Serializer_FASTA.hpp"
#include "DefaultReaderList.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/common/BinaryIndexStream.hpp"
#include "boost/iostreams/device/file.hpp"
#include "boost/iostreams/filtering_stream.hpp" 
#include "boost/iostreams/filter/gzip.hpp" 


namespace pwiz {
namespace proteome {


using namespace pwiz::util;
using namespace pwiz::data;
using boost::shared_ptr;


namespace {


void readFile(const string& uri, ProteomeData& pd, const Reader& reader)
{
    shared_ptr<istream> uriStreamPtr(new random_access_compressed_ifstream(uri.c_str()));
    if (!reader.accept(uri, uriStreamPtr))
        throw runtime_error("[ProteomeDataFile::readFile()] Unsupported file format.");

    reader.read(uri, uriStreamPtr, pd);
}


} // namespace


PWIZ_API_DECL ProteomeDataFile::ProteomeDataFile(const string& uri, bool indexed)
{
    readFile(uri, *this, DefaultReaderList(indexed));
}


PWIZ_API_DECL ProteomeDataFile::ProteomeDataFile(const string& uri, const Reader& reader)
{
    readFile(uri, *this, reader);
}


PWIZ_API_DECL
void ProteomeDataFile::write(const string& uri,
                             const WriteConfig& config,
                             const IterationListenerRegistry* iterationListenerRegistry)
{
    write(*this, uri, config, iterationListenerRegistry); 
}


namespace {


shared_ptr<ostream> openFile(const string& uri, bool gzipped)
{
    if (gzipped)
    {   // use boost's filter stack to count outgoing bytes, and gzip them
        shared_ptr<bio::filtering_ostream> filt(new bio::filtering_ostream);
        shared_ptr<ostream> result(filt);
        if (filt)
        {
            //filt->push(pwiz::minimxml::charcounter()); // for counting bytes before compression
            filt->push(bio::gzip_compressor(9)); // max compression
            filt->push(bio::file_sink(uri, ios::binary));
        }
        if (!result.get() || !*result || !filt->good())
            throw runtime_error(("[ProteomeDataFile::openFile()] Unable to open " + uri).c_str());
        return result; 
    } else 
    {
        shared_ptr<ostream> result(new ofstream(uri.c_str(), ios::binary));

        if (!result.get() || !*result)
            throw runtime_error(("[ProteomeDataFile::openFile()] Unable to open " + uri).c_str());

        return result;
    }
}


} // namespace


PWIZ_API_DECL
void ProteomeDataFile::write(const ProteomeData& pd,
                             const string& uri,
                             const WriteConfig& config,
                             const IterationListenerRegistry* iterationListenerRegistry)
{
    shared_ptr<ostream> os = openFile(uri, config.gzipped);
    
    switch (config.format)
    {
        case ProteomeDataFile::Format_Text:
        {
            TextWriter(*os,0)(pd);
            break;
        }

        case ProteomeDataFile::Format_FASTA:
        {
            Serializer_FASTA serializer;
            serializer.write(*os, pd, iterationListenerRegistry);
            break;
        }

        default:
            throw runtime_error("[ProteomeDataFile::write()] Format not implemented.");
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, ProteomeDataFile::Format format)
{
    switch (format)
    {
        case ProteomeDataFile::Format_Text:
            os << "Text";
            return os;
        case ProteomeDataFile::Format_FASTA:
            os << "FASTA";
            return os;
        default:
            os << "Unknown";
            return os;
    }
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const ProteomeDataFile::WriteConfig& config)
{
    os << config.format;
    if (config.format == ProteomeDataFile::Format_FASTA)
        os << " indexed=\"" << boolalpha << config.indexed << "\"";
    return os;
}


} // namespace proteome
} // namespace pwiz


