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
#include "boost/xpressive/xpressive_dynamic.hpp"
#include "pwiz/data/common/cv.hpp"

namespace bxp = boost::xpressive;

namespace pwiz{
namespace identdata{

struct PWIZ_API_DECL CVMap
{
    CVMap();
    CVMap(const std::string& keyword, cv::CVID cvid,
          const std::string& path);
    CVMap(const std::string& keyword, cv::CVID cvid,
          const std::string& path, const std::string& dependant);
    virtual ~CVMap() {}
    
    std::string keyword;
    cv::CVID cvid;
    std::string path;
    std::string dependant;

    static CVMap* createMap(const std::vector<std::string>& quad);

    virtual const char* getTag() const;
    
    virtual bool operator()(const std::string& text) const;
    virtual bool operator==(const CVMap& right) const;
};

typedef boost::shared_ptr<CVMap> CVMapPtr;

struct PWIZ_API_DECL RegexCVMap : public CVMap
{
    RegexCVMap();
    RegexCVMap(const std::string& pattern, cv::CVID cvid,
               const std::string& path);
    RegexCVMap(const std::string& pattern, cv::CVID cvid,
               const std::string& path, const std::string& dependant);
    virtual ~RegexCVMap();
    
    void setPattern(const std::string& pattern);
    
    virtual bxp::smatch match(std::string& text);
    
    virtual const char* getTag() const;

    virtual bool operator()(const std::string& text) const;

protected:
    bxp::sregex pattern;
};

typedef boost::shared_ptr<RegexCVMap> RegexCVMapPtr;

//
// Part matching classes.
//
struct PWIZ_API_DECL StringMatchCVMap : public CVMap
{
    StringMatchCVMap(const std::string& keyword);

    virtual bool operator()(const CVMap& right) const;
    virtual bool operator()(const CVMapPtr& right) const;
    virtual bool operator==(const CVMap& right) const;
    virtual bool operator==(const CVMapPtr& right) const;
};

struct PWIZ_API_DECL CVIDMatchCVMap : public CVMap
{
    CVIDMatchCVMap(cv::CVID cvid);

    virtual bool operator()(const CVMap& right) const;
    virtual bool operator()(const CVMapPtr& right) const;
    virtual bool operator==(const CVMap& right) const;
    virtual bool operator==(const CVMapPtr& right) const;
};


//
// Useful operators
//
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const CVMap& cm);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const CVMapPtr cmp);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const CVMap* cmp);

PWIZ_API_DECL std::istream& operator>>(std::istream& is, CVMapPtr& cm);

PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const std::vector<CVMapPtr>& cmVec);
PWIZ_API_DECL std::istream& operator>>(std::istream& is, std::vector<CVMapPtr>& cmVec);

} // namespace identdata
} // namespace pwiz

#endif // _KWCVMAP_HPP_

