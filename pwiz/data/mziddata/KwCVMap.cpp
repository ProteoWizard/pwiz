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
#include <sstream>
#include <iostream>
#include <stdexcept>
#include <boost/tokenizer.hpp>
#include <boost/foreach.hpp>
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

CVMap* CVMap::createMap(const vector<string>& triplet) 
{
    CVMap* map;

    if (triplet.size() < 3)
        throw runtime_error("Too few elements in createMap triplet");

    CVID cvid = cvTermInfo(triplet[2]).cvid;

    if (cvid == CVID_Unknown)
        throw runtime_error(("Unknown CVID: "+triplet[2]).c_str());
    
    if (triplet[0] == "plain")
        map = new CVMap(triplet[1], cvid);
    else if (triplet[0] == "regex")
        map = new RegexCVMap(triplet[1], cvid);
    else
        throw runtime_error(("Unknown map: "+triplet[0]).c_str());

    return map;
}

const char* CVMap::getTag() const
{
    return "plain";
}

bool CVMap::operator()(const string& text) const
{
    return keyword == text;
}

RegexCVMap::RegexCVMap()
    : CVMap(".*", CVID_Unknown), pattern(".*")
{
}

RegexCVMap::RegexCVMap(const string& pattern, CVID cvid)
    : CVMap(pattern, cvid), pattern(pattern)
{
    //this->cvid = cvid;
}

RegexCVMap::~RegexCVMap()
{
}

cmatch RegexCVMap::match(std::string& text)
{
    cmatch what;

    regex_match(text.c_str(), what, pattern);

    return what;
}

const char* RegexCVMap::getTag() const
{
    return "regex";
}

void RegexCVMap::setPattern(const std::string& pattern)
{
    keyword = pattern;
    this->pattern = regex(pattern);
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
    
    os << cm.getTag() << "\t" << cm.keyword << "\t" <<
        cvTermInfo(cm.cvid).id << "\n";

    return os;
}

ostream& operator<<(ostream& os, const CVMapPtr cmp)
{
    if (cmp.get())
        return os << (*cmp);

    return os;
}

ostream& operator<<(ostream& os, const CVMap* cmp)
{
    if (cmp)
        return os << (*cmp);

    return os;
}

istream& operator>>(istream& is, CVMapPtr& cm)
{
    string line;
    getline(is, line);

    if (!line.size())
        throw length_error("empty line found where record expected.");
    
    vector<string> tokens;
    char_separator<char> delim("\t");
    typedef tokenizer< char_separator<char> > tab_tokenizer;

    // Step 1: explode the line.
    tab_tokenizer tokes(line, delim);
    for (tab_tokenizer::iterator t=tokes.begin(); t!=tokes.end(); t++)
    {
        tokens.push_back(*t);
    }

    // Step 2: verify the # of fields.
    if (tokens.size()<3)
    {
        ostringstream err;
        err << "Too few fields (" << tokens.size()
            << ") in line: " << line;
        throw runtime_error(err.str().c_str());
    }

    // Step 3: Call the factory method to get a *Ptr object
    CVMap* map = CVMap::createMap(tokens);

    if (map)
        cm = CVMapPtr(map);
    else
        // Might want to find a more descriptive error to throw.
        throw runtime_error("No CVMap available");

    return is;
}

ostream& operator<<(ostream& os, const vector<CVMapPtr>& cmVec)
{
    for (vector<CVMapPtr>::const_iterator i=cmVec.begin();
         i != cmVec.end(); i++)
    {
        os << (*i);
    }

    return os;
}

istream& operator>>(istream& is, vector<CVMapPtr>& cmVec)
{
    while(is)
    {
        try {
            CVMapPtr ptr;
            is >> ptr;
            cmVec.push_back(ptr);
        }
        catch(length_error le)
        {
            // This occurs after the last record has been read.
        }
    }

    return is;
}

} // namespace mziddata
} // namespace pwiz
