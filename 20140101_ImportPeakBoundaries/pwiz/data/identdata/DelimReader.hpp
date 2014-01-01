//
// $Id$
//
//
// Origional author: Robert Burke <robert.burke@proteowizard.org>
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


#ifndef _MZID_DELIMREADER_HPP_
#define _MZID_DELIMREADER_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "Reader.hpp"
#include "IdentData.hpp"
#include <string>
#include <stdexcept>

namespace pwiz {
namespace identdata {

class PWIZ_API_DECL DelimReader : public Reader
{
public:
    DelimReader();
    virtual ~DelimReader();

    enum Param
    {
        mzType,
        ScoreType
    };

    DelimReader& set(Param param, const std::string& value);
    const std::string get(Param param) const;
    
    virtual std::string identify(const std::string& filename,
                                 const std::string& head) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentData& result) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      IdentDataPtr& result) const;

    virtual void read(const std::string& filename,
                      const std::string& head,
                      std::vector<IdentDataPtr>& results) const;

    virtual const char *getType() const;

private:
    class Impl;

    Impl* pimpl;
};

} // namespace pwiz 
} // namespace identdata 

#endif // _MZID_DELIMREADER_HPP_
