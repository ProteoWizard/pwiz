//
// obo.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _OBO_HPP_
#define _OBO_HPP_


#include <vector>
#include <string>
#include <limits>


namespace pwiz {
namespace msdata {


/// a single controlled vocabulary term
struct Term
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
struct OBO
{
    std::string filename;
    std::vector<std::string> header;
    std::vector<Term> terms;

    OBO(){}
    OBO(const std::string& filename);
};


std::ostream& operator<<(std::ostream& os, const Term& term);
std::ostream& operator<<(std::ostream& os, const OBO& obo);


} // namespace msdata
} // namespace pwiz


#endif // _OBO_HPP_ 


