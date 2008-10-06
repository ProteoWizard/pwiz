//
// Reader_Bruker.hpp
//
// 
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under Creative Commons 3.0 United States License, which requires:
//  - Attribution
//  - Noncommercial
//  - No Derivative Works
//
// http://creativecommons.org/licenses/by-nc-nd/3.0/us/
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _READER_BRUKER_HPP_ 
#define _READER_BRUKER_HPP_ 


#include "utility/misc/Export.hpp"
#include "data/msdata/Reader.hpp"
#include <memory>


// EDAL usage is msvc only - mingw doesn't provide COM support
#if (!defined(_MSC_VER) && !defined(PWIZ_NO_READER_BRUKER))
#define PWIZ_NO_READER_BRUKER
#endif


namespace pwiz {
namespace msdata {


class PWIZ_API_DECL Reader_Bruker : public Reader
{
    public:
    Reader_Bruker();
    ~Reader_Bruker();

	virtual std::string identify(const std::string& filename, 
                        const std::string& head) const; 

    virtual void read(const std::string& filename, 
                      const std::string& head, 
                      MSData& result) const;

    virtual const char * getType() const {return "Bruker Analysis";}

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


} // namespace msdata
} // namespace pwiz


#endif // _READER_BRUKER_HPP_
