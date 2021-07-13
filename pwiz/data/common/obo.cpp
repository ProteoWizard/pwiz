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


#define PWIZ_SOURCE

#include "obo.hpp"
#include "boost/xpressive/xpressive_dynamic.hpp"
#include "pwiz/utility/misc/Std.hpp"
namespace bxp = boost::xpressive;

namespace pwiz {
namespace data {
    
const Term::id_type Term::MAX_ID = std::numeric_limits<id_type>::max();

namespace {


// [Term]
// id: MS:1000001
// name: sample number
// def: "A reference number relevant to the sample under study." [PSI:MS]
// relationship: part_of MS:1000548 ! sample attribute


// The functions parseValue_* could be replaced by a general function that parses the whole
// line into [tag, value, dbxrefs, comment] if the OBO grammar were more regular.


// regex notes:
//  parentheses are used for submatch captures
//  \\d matches digit
//  \\s matches whitespace 
//  \\w matches [a-zA-Z_]


// OBO format has some escape characters that C++ doesn't,
// so we unescape them when reading in a tag:value pair.
// http://www.geneontology.org/GO.format.obo-1_2.shtml#S.1.5
string& unescape(string& str)
{
    bal::replace_all(str, "\\!", "!");
    bal::replace_all(str, "\\:", ":");
    bal::replace_all(str, "\\,", ",");
    bal::replace_all(str, "\\(", "(");
    bal::replace_all(str, "\\)", ")");
    bal::replace_all(str, "\\[", "[");
    bal::replace_all(str, "\\]", "]");
    bal::replace_all(str, "\\{", "{");
    bal::replace_all(str, "\\}", "}");
    return str;
}


string unescape_copy(const string& str)
{
    string copy(str);
    unescape(copy);
    return copy;
}


istream& getcleanline(istream& is, string& buffer)
{
    if (getlinePortable(is, buffer))
        bal::trim(buffer);

    return is;
}


void parse_id(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("id:\\s*(\\w+):(\\d+)\\s*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term id on line: \"" + line + "\"");

    term.prefix = unescape_copy(what[1]);
    string id = what[2];
    bal::trim_left_if(id, bal::is_any_of("0")); // trim leading zeros
    if(id.empty())
        term.id = 0; // id was all zeros
    else
        term.id = lexical_cast<Term::id_type>(unescape(id));
}


void parse_name(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("name:\\s*(.*?)\\s*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term name on line: \"" + line + "\"");    

    term.name = unescape_copy(what[1]);
}


void parse_def(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("def:\\s*\"(OBSOLETE )?(.*)\"(\\s*\\[.*\\].*)?");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term def on line: \"" + line + "\"");

    term.def = what[2];
    term.isObsolete = what[1].matched;
}


void parse_relationship(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("relationship:\\s*(\\w+) (\\w+):(\\d+).*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term relationship on line: \"" + line + "\"");

    if (what[1] == "part_of")
    {
        if (what[2].str() != term.prefix)
            cerr << "[obo] Ignoring part_of relationship with different prefix:\n  " << line << endl;
        else
            term.parentsPartOf.push_back(lexical_cast<Term::id_type>(what[3]));
        return;
    }

    term.relations.insert(make_pair(unescape_copy(what[1]), make_pair(what[2], lexical_cast<Term::id_type>(what[3]))));
}


void parse_is_obsolete(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("is_obsolete:\\s*(\\w+)\\s*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term is_obsolete on line: \"" + line + "\"");    

    term.isObsolete = (what[1] == "true");
}


void parse_is_a(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("is_a:\\s*(\\w+):(\\d+).*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term is_a on line: \"" + line + "\"");    

    if (what[1].str() != term.prefix)
    {
        cerr << "[obo] Ignoring is_a with different prefix:\n  " << line << endl;
        return;
    }

    term.parentsIsA.push_back(lexical_cast<Term::id_type>(what[2]));
}


void parse_exact_synonym(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("exact_synonym:\\s*\"(.*)\".*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term exact_synonym on line: \"" + line + "\"");    

    term.exactSynonyms.push_back(unescape_copy(what[1]));
}


void parse_synonym(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("synonym:\\s*\"(.*)\"\\s*(\\w+)?.*");

    bxp::smatch what; 
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term synonym on line: \"" + line + "\"");    

    if (what[2] == "EXACT")
        term.exactSynonyms.push_back(unescape_copy(what[1]));
}


void parse_property_value(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("property_value:\\s*(\\S+?)\\s*[=:]?\\s*\"(.*)\".*");

    bxp::smatch what;
    if (!bxp::regex_match(line, what, e))
        throw runtime_error("Error matching term property_value on line: \"" + line + "\"");

    term.propertyValues.insert(make_pair(what[1], what[2]));
}


void parse_xref(const string& line, Term& term)
{
    static const bxp::sregex e = bxp::sregex::compile("xref:\\s*(\\S+?)\\s*\"(.*)\".*");

    bxp::smatch what;
    if (!bxp::regex_match(line, what, e))
    {
        static const bxp::sregex e2 = bxp::sregex::compile("xref:\\s*(\\S+?)\\s*");
        if (!bxp::regex_match(line, what, e2))
            throw runtime_error("Error matching term xref on line: \"" + line + "\"");
    }

    term.propertyValues.insert(make_pair(unescape_copy(what[1]), unescape_copy(what[2])));
}


void parseTagValuePair(const string& line, Term& term)
{
    string::size_type tagSize = line.find(':'); 
    if (tagSize==0 || tagSize==string::npos)
        throw runtime_error("[parseTagValuePair()]: No tag found on line: \"" + line + "\"");
    
    string tag = line.substr(0, tagSize);
    
    if (tag == "id")
        parse_id(line, term);
    else if (tag == "name")
        parse_name(line, term);
    else if (tag == "def")
        parse_def(line, term);
    else if (tag == "relationship")
        parse_relationship(line, term);
    else if (tag == "is_obsolete")
        parse_is_obsolete(line, term);
    else if (tag == "is_a")
        parse_is_a(line, term);
    else if (tag == "exact_synonym")
        parse_exact_synonym(line, term);
    else if (tag == "synonym")
        parse_synonym(line, term);
    else if (tag == "property_value")
        parse_property_value(line, term);
    else if (tag == "xref")
        parse_xref(line, term);
    else if (tag == "related_synonym" ||
             tag == "narrow_synonym" ||
             tag == "comment" ||
             tag == "alt_id" ||
             tag == "namespace" ||
             tag == "xref_analog" ||
             tag == "replaced_by" ||
             tag == "created_by" ||
             tag == "has_domain" || // first appeared in psi-ms.obo 3.52.0
             tag == "has_order"  || // first appeared in psi-ms.obo 3.52.0
             tag == "subset"  ||    // first appeared in unit.obo in rev 2755
             tag == "creation_date")
        ; // ignore these tags
    else
        cerr << "[obo] Unknown tag \"" << tag << "\":\n  " << line << endl;
}


Term parseTerm(istream& is)
{
    Term result;

    for (string line; getcleanline(is,line) && !line.empty();)
        parseTagValuePair(line, result);

    return result;
}


void parseStanza(istream& is, OBO& obo)
{
    string stanzaType;
    while (is && stanzaType.empty())
        getcleanline(is, stanzaType);

    if (stanzaType == "[Term]")
    {
        Term term = parseTerm(is);

        // validate prefix
        if (obo.prefixes.empty())
        {
            obo.prefixes.insert(term.prefix);
        }
        else
        {
            if (obo.prefixes.count(term.prefix) == 0)
                throw runtime_error("[obo] Prefix mismatch: " +
                                    lexical_cast<string>(obo.prefixes) + ", " + 
                                    term.prefix + ":" + lexical_cast<string>(term.id));
        }

        // add all terms, even obsolete ones
        obo.terms.insert(term);
    }
    else
    {
        // ignore stanza 
        for (string buffer; getcleanline(is,buffer) && !buffer.empty(););
    }
}


void parse(const string& filename, OBO& obo)
{
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error(("[obo] Unable to open file " + filename).c_str());

    auto namespaceRegex = bxp::sregex::compile("remark: namespace:\\s*(\\w+).*");
    bxp::smatch what;

    // read header lines until blank line
    for (string buffer; getcleanline(is, buffer) && !buffer.empty();)
    {
        if (bxp::regex_match(buffer, what, namespaceRegex))
            obo.prefixes.insert(what[1]);
        obo.header.push_back(buffer);
    }

    // parse stanzas to end of file
    while (is)
        parseStanza(is, obo);
}


} // namespace


PWIZ_API_DECL OBO::OBO(const string& _filename)
:   filename(_filename) 
{
    parse(filename, *this);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const Term& term)
{
    os << "[Term]\n"
       << "id: " << term.prefix << ":" << term.id << endl
       << "name: " << term.name << endl
       << "def: \"" << term.def << "\"\n"; 

    for (Term::id_list::const_iterator it=term.parentsIsA.begin();
         it!=term.parentsIsA.end(); ++it)
        os << "is_a: " << term.prefix << ":" << *it << endl;

    for (Term::id_list::const_iterator it=term.parentsPartOf.begin();
         it!=term.parentsPartOf.end(); ++it)
        os << "relationship: part_of " << term.prefix << ":" << *it << endl;

    for (Term::relation_map::const_iterator it=term.relations.begin();
         it!=term.relations.end(); ++it)
        os << "relationship: " << it->first << " " << it->second.first << ":" << it->second.second << endl;

    return os;
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const OBO& obo)
{
    copy(obo.header.begin(), obo.header.end(), ostream_iterator<string>(os,"\n"));
    os << endl;
    copy(obo.terms.begin(), obo.terms.end(), ostream_iterator<Term>(os,"\n"));
    return os;
}


} // namespace data
} // namespace pwiz
