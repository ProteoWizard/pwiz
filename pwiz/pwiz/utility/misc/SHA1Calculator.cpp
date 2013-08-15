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

#include "endian.hpp"
#ifdef PWIZ_BIG_ENDIAN
#define SHA1_BIG_ENDIAN // for SHA1.h
#endif // PWIZ_BIG_ENDIAN
#include "SHA1.h" // TODO: link errors if not first (?)


#include "SHA1Calculator.hpp"
#include "pwiz/utility/misc/Std.hpp"

namespace pwiz {
namespace util {


namespace {
string formatHash(const CSHA1& csha1)
{
    char buffer[100];
    memset(buffer, 0, sizeof(buffer));
    csha1.ReportHash(buffer);

    string result(40,'\0');
    const char* p = buffer;
    for (string::iterator it=result.begin(); it!=result.end();)
    {
        *it++ = static_cast<char>(tolower(*p++));
        *it++ = static_cast<char>(tolower(*p++));
        ++p;
    }
    
    return result;
}
} // namespace


class SHA1Calculator::Impl
{
    public:
    CSHA1 csha1;
    bool closed;

    Impl()
    :   closed(false)
    {}
};


PWIZ_API_DECL SHA1Calculator::SHA1Calculator() : impl_(new Impl) {}


PWIZ_API_DECL void SHA1Calculator::reset()
{
    impl_->csha1.Reset();
    impl_->closed = false;
}


PWIZ_API_DECL void SHA1Calculator::update(const unsigned char* buffer, size_t bufferSize)
{
    if (impl_->closed) 
        throw runtime_error("[SHA1Calculator::update()] Should not be called after close().");

    impl_->csha1.Update(const_cast<unsigned char*>(buffer), static_cast<UINT_32>(bufferSize));
}
    

PWIZ_API_DECL void SHA1Calculator::update(const string& buffer)
{
    if (!buffer.empty())
        update(reinterpret_cast<const unsigned char*>(&buffer[0]), buffer.size());
}


PWIZ_API_DECL void SHA1Calculator::close()
{
    impl_->csha1.Final();
    impl_->closed = true;
}


PWIZ_API_DECL string SHA1Calculator::hash() const
{
    return formatHash(impl_->csha1);
}


PWIZ_API_DECL string SHA1Calculator::hashProjected() const
{
    if (impl_->closed) 
        throw runtime_error("[SHA1Calculator::hashProjected()] Should not be called after close().");

    CSHA1 temp(impl_->csha1);
    temp.Final();
    return formatHash(temp);
}


PWIZ_API_DECL string SHA1Calculator::hash(const string& buffer)
{
    return hash((const unsigned char*)buffer.c_str(), buffer.size());
}


PWIZ_API_DECL string SHA1Calculator::hash(const unsigned char* buffer, size_t bufferSize)
{
    CSHA1 sha1;
    sha1.Update(buffer, static_cast<UINT_32>(bufferSize));
    sha1.Final();
    return formatHash(sha1);
}


PWIZ_API_DECL string SHA1Calculator::hash(istream& is)
{
    CSHA1 sha1;
    is.clear();
    is.seekg(0);
    unsigned char buffer[65535];
    while (is && is.read(reinterpret_cast<char*>(buffer), 65535))
        sha1.Update(buffer, 65535u);
    sha1.Update(buffer, is.gcount());
    sha1.Final();
    return formatHash(sha1);
}


PWIZ_API_DECL string SHA1Calculator::hashFile(const string& filename)
{
    CSHA1 sha1;

    if (!(sha1.HashFile(filename.c_str())))
        throw runtime_error(("[SHA1Calculator] Error hashing file " + filename).c_str());

    sha1.Final();
    return formatHash(sha1);
}


} // namespace util
} // namespace pwiz


