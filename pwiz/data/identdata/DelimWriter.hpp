//
// $Id$
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
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

#ifndef _DELIMWRITER_HPP_
#define _DELIMWRITER_HPP_

#include "IdentData.hpp"
#include "pwiz/utility/misc/Export.hpp"

#include <string>
#include <vector>

namespace pwiz {
namespace identdata {

class PWIZ_API_DECL DelimWriter
{
public:
    typedef std::vector<std::string> line_type;
    //typedef std::vector<line_type>   file_type;

    DelimWriter(std::ostream* os = 0, char delim = '\t', bool headers = false)
        : os_(os), delim_(delim), headers_(headers)
    {}

    template<typename T>
    std::ostream* operator()(const T& t)
    {
        return this->write(t);
    }
    
    template<typename object_type>
    std::ostream* write(const std::vector<object_type>& v)
    {
        std::for_each(v.begin(), v.end(), (*this));
        return os_;
    }

    template<typename object_type>
    std::ostream* write(const boost::shared_ptr<object_type>& pob)
    {
        if (pob.get())
            return (*this)(*pob);
        
        return os_;
    }

    std::ostream* writeHeaders();
    
    std::ostream* write(const IdentData& mzid);

    std::ostream* write(const SpectrumIdentificationList& sir);

    std::ostream* write(const SpectrumIdentificationResult& sir);

    std::ostream* write(const SpectrumIdentificationItem& sii);

    std::ostream* write(const PeptideEvidence& pe);

    std::ostream* write(const line_type& line);

    operator bool() const;
    
private:
    std::ostream*  os_;
    char           delim_;
    bool           headers_;

    line_type      current_line;
};

} // namespace identdata
} // namespace pwiz 

#endif // _DELIMWRITER_HPP_

