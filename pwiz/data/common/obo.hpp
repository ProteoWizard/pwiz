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


#ifndef _OBO_HPP_
#define _OBO_HPP_


#include "pwiz/utility/misc/Export.hpp"
#include <vector>
#include <set>
#include <map>
#include <string>
#include <limits>


namespace pwiz {
namespace data {


/// a single controlled vocabulary term
struct PWIZ_API_DECL Term
{
    typedef unsigned int id_type;
    typedef std::vector<id_type> id_list;
    typedef std::multimap<std::string, std::pair<std::string, id_type> > relation_map;
    const static id_type MAX_ID;

    std::string prefix;
    id_type id;
    std::string name;
    std::string def;
    id_list parentsIsA;
    id_list parentsPartOf;
    relation_map relations; // other than is_a and part_of
    std::multimap<std::string, std::string> propertyValues;
    std::vector<std::string> exactSynonyms;
    bool isObsolete;

    Term(id_type id = MAX_ID)
    :   id(id), isObsolete(false)
    {}

    bool operator< (const Term& rhs) const {return id < rhs.id;}
};


///
/// Represents a selectively parsed OBO file.
///
/// Note that the following are currently ignored during parsing:
/// - comments
/// - dbxrefs
/// - synonym tags other than exact_synonym
/// - non-Term stanzas
///
struct PWIZ_API_DECL OBO
{
    std::string filename;
    std::vector<std::string> header;
    std::set<std::string> prefixes; // e.g. "MS", "UO"
    std::set<Term> terms;

    OBO(){}
    OBO(const std::string& filename);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Term& term);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const OBO& obo);


} // namespace data
} // namespace pwiz


#endif // _OBO_HPP_ 


