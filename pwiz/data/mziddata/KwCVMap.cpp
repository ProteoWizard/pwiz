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

#define PWIZ_SOURCE

#include <typeinfo>

#include "KwCVMap.hpp"

namespace pwiz{
namespace mziddata{

using namespace std;
using namespace boost;
using namespace pwiz::cv;

CVMap::CVMap()
    : keyword(), cvid(cv::CVID_Unknown)
{
}

CVMap::CVMap(const string& keyword, CVID cvid)
    : keyword(keyword), cvid(cvid)
{
}

bool CVMap::operator()(const string& text) const
{
    return keyword == text;
}

RegexCVMap::RegexCVMap()
    : CVMap(".*", CVID_Unknown)
{
}

RegexCVMap::RegexCVMap(const string& pattern, CVID cvid)
    : pattern(pattern)
{
    this->cvid = cvid;
}

RegexCVMap::~RegexCVMap()
{
}

bool RegexCVMap::operator()(const string& text) const
{
    cmatch what;
    if (regex_match(text.c_str(), what, pattern))
    {
        return true;
    }
    
    return false;
}

ostream& operator<<(ostream& os, const CVMap& cm)
{
    string id = typeid(cm).name();
    os << id << ":\t";

    if (id == "CVMap")
    {
        os << cm.keyword << "\t" << cm.cvid << "\n";
    }
    else if (id == "RegexCVMap")
    {
        os << ((RegexCVMap*)&cm)->pattern << "\t" << cm.cvid << "\n";
    }
    else
        throw runtime_error(("CVMap output: Unknown class "+id).c_str());

    return os;
}

ostream& operator<<(ostream& os, CVMapPtr cmp)
{
    if (!cmp.get())
        return os << (*cmp);

    return os;
}

ostream& operator<<(ostream& os, const CVMap* cmp)
{
    if (cmp)
        return os << (*cmp);

    return os;
}

} // namespace mziddata
} // namespace pwiz
