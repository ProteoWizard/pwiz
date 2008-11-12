//
// obo.hpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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
#include <string>
#include <limits>


namespace pwiz {
namespace msdata {


/// a single controlled vocabulary term
struct PWIZ_API_DECL Term
{
    typedef unsigned int id_type;
    typedef std::vector<id_type> id_list; 

    std::string prefix;
    id_type id;
    std::string name;
    std::string def;
    id_list parentsIsA;
    id_list parentsPartOf;
    std::vector<std::string> exactSynonyms;
    bool isObsolete;

    Term()
    :   id(std::numeric_limits<int>::max()), isObsolete(false)
    {}
};


///
/// Represents a selectively parsed OBO file.
///
/// Note that the following are currently ignored during parsing:
/// - comments
/// - dbxrefs
/// - synonym tags other than exact_synonym
/// - non-Term stanzas
/// - obsolete Terms
///
struct PWIZ_API_DECL OBO
{
    std::string filename;
    std::vector<std::string> header;
    std::string prefix; // e.g. "MS", "UO"
    std::vector<Term> terms;

    OBO(){}
    OBO(const std::string& filename);
};


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Term& term);
PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const OBO& obo);


} // namespace msdata
} // namespace pwiz


#endif // _OBO_HPP_ 


