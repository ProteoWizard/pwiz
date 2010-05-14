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

#ifndef _KWCVMAP_HPP_
#define _KWCVMAP_HPP_

#include <string>
#include <vector>
#include "boost/shared_ptr.hpp"
#include "boost/regex.hpp"
#include "pwiz/data/common/cv.hpp"

namespace pwiz{
namespace mziddata{

struct PWIZ_API_DECL CVMap
{
    CVMap();
    CVMap(const std::string& keyword, cv::CVID cvid);
    virtual ~CVMap() {}
    
    std::string keyword;
    cv::CVID cvid;

    static CVMap* createMap(const std::vector<std::string>& triplet);

    virtual const char* getTag() const;
    
    virtual bool operator()(const std::string& text) const;
};

typedef boost::shared_ptr<CVMap> CVMapPtr;

struct PWIZ_API_DECL RegexCVMap : public CVMap
{
    RegexCVMap();
    RegexCVMap(const std::string& pattern, cv::CVID cvid);
    virtual ~RegexCVMap();
    
    boost::regex pattern;

    void setPattern(const std::string& pattern);
    
    virtual boost::cmatch match(std::string& text);
    
    virtual const char* getTag() const;

    virtual bool operator()(const std::string& text) const;
};

typedef boost::shared_ptr<RegexCVMap> RegexCVMapPtr;

std::ostream& operator<<(std::ostream& os, const CVMap& cm);
std::ostream& operator<<(std::ostream& os, const CVMapPtr cmp);
std::ostream& operator<<(std::ostream& os, const CVMap* cmp);

std::istream& operator>>(std::istream& is, CVMapPtr& cm);

std::ostream& operator<<(std::ostream& os, const std::vector<CVMapPtr>& cmVec);
std::istream& operator>>(std::istream& is, std::vector<CVMapPtr>& cmVec);

} // namespace mziddata
} // namespace pwiz

#endif // _KWCVMAP_HPP_

