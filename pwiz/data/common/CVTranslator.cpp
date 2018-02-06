//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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
//


#define PWIZ_SOURCE

#include "CVTranslator.hpp"
#include "boost/algorithm/string/predicate.hpp"
#include "pwiz/utility/misc/Std.hpp"


namespace pwiz {
namespace data {


using namespace pwiz::cv;


//
// default extra translations
//


namespace {

struct ExtraEntry
{
    const char* text;
    CVID cvid;
};

ExtraEntry defaultExtraEntries_[] =
{
    {"ITMS", MS_ion_trap},
    {"FTMS", MS_FT_ICR},
};

size_t defaultExtraEntriesSize_ = sizeof(defaultExtraEntries_)/sizeof(ExtraEntry);

} // namespace


//
// CVTranslator::Impl
//


class CVTranslator::Impl
{
    public:

    Impl();
    void insert(const string& text, CVID cvid);
    CVID translate(const string& text) const;

    private:

    typedef map<string,CVID> Map;
    Map map_;

    void insertCVTerms();
    void insertDefaultExtraEntries();
};


CVTranslator::Impl::Impl()
{
    insertCVTerms();
    insertDefaultExtraEntries();
}


namespace {


inline char alnum_lower(char c)
{
    // c -> lower-case, whitespace, or +
    return isalnum(c) ? static_cast<char>(tolower(c)) : c == '+' ? c : ' ';
}


string preprocess(const string& s)
{
    string result = s;
    transform(result.begin(), result.end(), result.begin(), alnum_lower);
    return result;
}


string canonicalize(const string& s)
{
    // remove non-alnum characters
    istringstream iss(preprocess(s));

    // remove whitespace around tokens
    vector<string> tokens;
    copy(istream_iterator<string>(iss), istream_iterator<string>(), back_inserter(tokens));

    // concatenate with underscores
    ostringstream oss; 
    copy(tokens.begin(), tokens.end(), ostream_iterator<string>(oss, "_"));

    return oss.str();
}


} // namespace


bool shouldIgnore(const string& key, CVID value, CVID cvid)
{
    return (key=="unit_" && value==MS_unit_OBSOLETE && cvid==UO_unit ||
            key=="pi_" && value==MS_PI && cvid==UO_pi || // MS_PI==photoionization, UO_pi==3.14
            key=="pi_" && value==MS_PI && cvid==MS_pI || // MS_pI==isoelectric point
            key=="de_" && value==MS_DE && cvid==1001274 || // conflict between 1000246 and 1001274
            cvid == UO_volt_second_per_square_centimeter); // conflict between 1002814 200010007 (volt-second per square centimeter) 
}


bool shouldReplace(const string& key, CVID value, CVID cvid)
{
    return false;
}


void CVTranslator::Impl::insert(const string& text, CVID cvid)
{
    string key = canonicalize(text);

    if (map_.count(key))
    {
        if (shouldIgnore(key, map_[key], cvid))
            return;

        if (!shouldReplace(key, map_[key], cvid))
        {
            throw runtime_error("[CVTranslator::insert()] Collision: " + 
                                lexical_cast<string>(map_[key]) + " " +
                                lexical_cast<string>(cvid));
        }
    }

    map_[key] = cvid;
}


CVID CVTranslator::Impl::translate(const string& text) const
{
    Map::const_iterator it = map_.find(canonicalize(text));
    if (it != map_.end())
        return it->second; 
    return CVID_Unknown;
}


void CVTranslator::Impl::insertCVTerms()
{
    for (vector<CVID>::const_iterator cvid=cvids().begin(); cvid!=cvids().end(); ++cvid)
    {
        const CVTermInfo& info = cvTermInfo(*cvid);

        if (info.isObsolete) continue;

        if (!(bal::starts_with(info.id, "MS") ||
              bal::starts_with(info.id, "UO"))) continue;
        
        // insert name
        insert(info.name, *cvid);

        // insert synonyms
        if (*cvid < 100000000) // prefix == "MS"
        {
            for (vector<string>::const_iterator syn=info.exactSynonyms.begin(); 
                 syn!=info.exactSynonyms.end(); ++syn)
                insert(*syn, *cvid);
        }
    }
}


void CVTranslator::Impl::insertDefaultExtraEntries()
{
    for (const ExtraEntry* it=defaultExtraEntries_; 
         it!=defaultExtraEntries_+defaultExtraEntriesSize_; ++it)
        insert(it->text, it->cvid);
}


//
// CVTranslator
//


PWIZ_API_DECL CVTranslator::CVTranslator() : impl_(new Impl) {}
PWIZ_API_DECL void CVTranslator::insert(const string& text, CVID cvid) {impl_->insert(text, cvid);}
PWIZ_API_DECL CVID CVTranslator::translate(const string& text) const {return impl_->translate(text);}


} // namespace data
} // namespace pwiz

