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

#include "Reader_FASTA.hpp"
#include "Serializer_FASTA.hpp"
#include "pwiz/data/common/BinaryIndexStream.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/iostreams/copy.hpp>


using namespace pwiz::util;


namespace pwiz {
namespace proteome {


//
// Reader_FASTA
//

PWIZ_API_DECL Reader_FASTA::Reader_FASTA(const Config& config)
: config_(config)
{}


PWIZ_API_DECL string Reader_FASTA::identify(const string& uri, shared_ptr<istream> uriStreamPtr) const
{
    // formerly this checked to see if uri contained .fasta, which meant .fa, .tfa etc failed
    boost::ignore_unused_variable_warning(uri);

    if (!uriStreamPtr.get())
        throw runtime_error("[Reader_FASTA::identify] Must have a valid stream to identify");

    uriStreamPtr->clear();
    uriStreamPtr->seekg(0);

    // first non-blank line in the stream should begin with '>'
#define TESTBUFSIZE 16
    char buf[TESTBUFSIZE+1];
    string result("");
    while (uriStreamPtr->good())
    {
        uriStreamPtr->getline(buf,TESTBUFSIZE);
        if (buf[0] && buf[0] != '\r') // skip blank lines
        {
            if (buf[0] == '>')
            {
                result = getType();
            }
            break;
        }
    }
    uriStreamPtr->clear();
    uriStreamPtr->seekg(0);
    return result;
}


PWIZ_API_DECL void Reader_FASTA::read(const std::string& uri, shared_ptr<istream> uriStreamPtr, ProteomeData& result) const
{
    result.id = uri;

    Serializer_FASTA::Config config;
    if (config_.indexed) // override default MemoryIndex with a BinaryIndexStream
    {
        {ofstream((uri + ".index").c_str(), ios::app);} // make sure the file exists
        shared_ptr<iostream> isPtr(new fstream((uri + ".index").c_str(), ios::in | ios::out | ios::binary));

        // indexes smaller than 200mb are loaded entirely into memory
        boost::uintmax_t indexSize = bfs::file_size(uri + ".index");
        if (indexSize > 0 && indexSize < 200000000)
        {
            stringstream* indexFileStream = new stringstream();
            bio::copy(*isPtr, *indexFileStream);
            isPtr.reset(indexFileStream);
        }

        if (!*isPtr) // stream is unavailable or read only
        {
            isPtr.reset(new fstream((uri + ".index").c_str(), ios::in | ios::binary));
            bool canOpenReadOnly = !!*isPtr;
            if (canOpenReadOnly)
            {
                // check that the index is up to date;
                // if it isn't, a read only index is worthless
                config.indexPtr.reset(new data::BinaryIndexStream(isPtr));
                Serializer_FASTA serializer(config);
                serializer.read(uriStreamPtr, result);
                if (result.proteinListPtr->size() > 0)
                    try
                    {
                        result.proteinListPtr->protein(0);
                        return;
                    }
                    catch (exception&)
                    {
                        // TODO: log warning about stale read only index
                        canOpenReadOnly = false;
                    }
            }

            // TODO: try opening an index in other locations, e.g.:
            // * current working directory (may be read only)
            // * executing directory (may be read only)
            // * temp directory (pretty much guaranteed to be writable)
            if (!canOpenReadOnly)
            {
                // fall back to in-memory index
                config.indexPtr.reset(new data::MemoryIndex);
            }
        }
        else // stream is ready and writable
            config.indexPtr.reset(new data::BinaryIndexStream(isPtr));
    }

    Serializer_FASTA serializer(config);
    serializer.read(uriStreamPtr, result);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Reader_FASTA::Config& config)
{
    return os;
}


} // namespace proteome
} // namespace pwiz
