//
// obo.cpp
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


#define PWIZ_SOURCE

#include "obo.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/regex.hpp"
#include <iostream>
#include <fstream>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::lexical_cast;


namespace {


// [Term]
// id: MS:1000001
// name: sample number
// def: "A reference number relevant to the sample under study." [PSI:MS]
// relationship: part_of MS:1000548 ! sample attribute


// The functions parseValue_* could be replaced by a general function that parses the whole
// line into [tag, value, dbxrefs, comment] if the OBO grammar were more regular.


// boost::regex notes:
//  parentheses are used for submatch captures
//  \\d matches digit
//  \\s matches whitespace 
//  \\w matches [a-zA-Z_]


void parse_id(const string& line, Term& term)
{
    static const boost::regex e("id: (\\w+):(\\d+)");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term id on line: " + line);

    term.prefix = what[1];
    term.id = lexical_cast<Term::id_type>(what[2]);
}


void parse_name(const string& line, Term& term)
{
    static const boost::regex e("name: (.*)");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term name.");    

    term.name = what[1];
}


void parse_def(const string& line, Term& term)
{
    static const boost::regex e("def: \"(OBSOLETE )?(.*)\"\\s*\\[.*\\].*");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term def.");    

    term.def = what[2];
    term.isObsolete = what[1].matched;
}


void parse_relationship(const string& line, Term& term)
{
    static const boost::regex e("relationship: (\\w+) (\\w+):(\\d+).*");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term relationship.");

    if (what[2] != term.prefix)
    {
        //cerr << "[obo] Ignoring relationship with different prefix:\n  " << line << endl;
        return;
    }

    if (what[1] == "part_of")
    {
        term.parentsPartOf.push_back(lexical_cast<Term::id_type>(what[3]));
        return;
    }

    cerr << "[obo] Ignoring unknown relationship type " << what[1] << ":\n  " << line << endl;
}


void parse_is_obsolete(const string& line, Term& term)
{
    static const boost::regex e("is_obsolete: (\\w+)");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term is_obsolete.");    

    term.isObsolete = (what[1] == "true");
}


void parse_is_a(const string& line, Term& term)
{
    static const boost::regex e("is_a: (\\w+):(\\d+).*");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term is_a.");    

    if (what[1] != term.prefix)
    {
        cerr << "[obo] Ignoring is_a with different prefix:\n  " << line << endl;
        return;
    }

    term.parentsIsA.push_back(lexical_cast<Term::id_type>(what[2]));
}


void parse_exact_synonym(const string& line, Term& term)
{
    static const boost::regex e("exact_synonym: \"(.*)\".*");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term exact_synonym.");    

    term.exactSynonyms.push_back(what[1]);
}


void parse_synonym(const string& line, Term& term)
{
    static const boost::regex e("synonym: \"(.*)\"\\s*(\\w+).*");

    boost::smatch what; 
    if (!regex_match(line, what, e))
        throw runtime_error("Error matching term synonym.");    

    if (what[2] == "EXACT")
        term.exactSynonyms.push_back(what[1]);
}


void parseTagValuePair(const string& line, Term& term)
{
    string::size_type tagSize = line.find(':'); 
    if (tagSize==0 || tagSize==string::npos)
        throw runtime_error("[parseTagValuePair()]: No tag found.");
    
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
    else if (tag == "related_synonym" ||
             tag == "narrow_synonym" ||
             tag == "comment" ||
             tag == "alt_id" ||
             tag == "namespace" ||
			 tag == "xref")
        ; // ignore these tags
    else
        cerr << "[obo] Unknown tag \"" << tag << "\":\n  " << line << endl;
}


Term parseTerm(istream& is)
{
    Term result;

    for (string line; getline(is,line) && !line.empty();)
        parseTagValuePair(line, result);

    return result;
}


void parseStanza(istream& is, OBO& obo)
{
    string stanzaType;
    while (is && stanzaType.empty())
        getline(is, stanzaType);

    if (stanzaType == "[Term]")
    {
        Term term = parseTerm(is);

        // validate prefix
        if (obo.prefix.empty())
        {
            obo.prefix = term.prefix;
        }
        else
        {
            if (term.prefix != obo.prefix)
                throw runtime_error("[obo] Prefix mismatch: " +
                                    obo.prefix + ", " + 
                                    term.prefix + ":" + lexical_cast<string>(term.id));
        }

        // add all terms, even obsolete ones
        obo.terms.push_back(term);
    }
    else
    {
        // ignore stanza 
        for (string buffer; getline(is,buffer) && !buffer.empty(););
    }
}


void parse(const string& filename, OBO& obo)
{
    ifstream is(filename.c_str());
    if (!is)
        throw runtime_error(("[obo] Unable to open file " + filename).c_str());

    // read header lines until blank line
    for (string buffer; getline(is,buffer) && !buffer.empty();)
        obo.header.push_back(buffer);      

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

    return os;
}


PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const OBO& obo)
{
    copy(obo.header.begin(), obo.header.end(), ostream_iterator<string>(os,"\n"));
    os << endl;
    copy(obo.terms.begin(), obo.terms.end(), ostream_iterator<Term>(os,"\n"));
    return os;
}


} // namespace msdata
} // namespace pwiz


