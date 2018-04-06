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


#ifndef _SHA1_OSTREAM_HPP_
#define _SHA1_OSTREAM_HPP_


#include "SHA1Calculator.hpp"
#include "boost/iostreams/filtering_stream.hpp"
#include "boost/iostreams/filter/symmetric.hpp"
#include <string>


namespace pwiz {
namespace util {


/// model of boost::iostreams::SymmetricFilter
class SHA1SymmetricFilter 
{
    public:

    typedef char char_type;

    bool filter(const char*& src_begin, const char* src_end,
                char*& dest_begin, char* dest_end, bool flush)
    {
        const char* dest_begin_orig = dest_begin;

        for (; src_begin!=src_end && dest_begin!=dest_end; ++src_begin, ++dest_begin)
            *dest_begin = *src_begin;

        sha1_.update(reinterpret_cast<const unsigned char*>(dest_begin_orig), 
                     dest_begin - dest_begin_orig);

        return false;
    }

    void close() {}

    std::string hash() 
    {
        return sha1_.hashProjected();
    };

    private:
    SHA1Calculator sha1_;
};


/// model of boost::iostreams::Filter
class SHA1Filter : public boost::iostreams::symmetric_filter<SHA1SymmetricFilter>
{
    public:

    typedef boost::iostreams::symmetric_filter<SHA1SymmetricFilter> base_type;

    SHA1Filter(int bufferSize)
    :   base_type(bufferSize)
    {}

    std::string hash() {return this->filter().hash();}
};


/// ostream filter for calculating a SHA-1 hash of data on the fly 
class SHA1_ostream : public boost::iostreams::filtering_ostream
{
    public:

    SHA1_ostream(std::ostream& os, int bufferSize = 4096)
    :   os_(os), filter_(bufferSize)
    {
        push(filter_);
        push(os);
    }

    std::string hash() {return filter_.hash();}

    void explicitFlush()
    {
        // hack: not flushing properly with the filter in the pipeline
        pop(); // this flushes os_ explicitly
        push(os_);
    }

    private:
    std::ostream& os_;
    SHA1Filter filter_;
};


} // namespace util
} // namespace pwiz


#endif // _SHA1_OSTREAM_HPP_


