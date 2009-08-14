//
// $Id$
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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
// NOTE: this files uses an hpp suffix because I don't know how to
// tell emacs that a .h should be able to type a "c" without going to
// the minibuffer.
#ifndef _UTILS_HPP_
#define _UTILS_HPP_

#ifndef _WIN32
#include <execinfo.h>
#endif

#include <iostream>
#include <fstream>
#include <iomanip>
#include <vector>
#include <string>

#include "pepxml/Peptide.hpp"

using namespace std;
using namespace pwiz::pepxml;

// Tokenizes a string with a given set of delimiters. This should be
// moved into an independant class.
std::vector<string> tokenize(const string& str,const string& delimiters)
{
    std::vector<string> tokens;
    	
	// skip delimiters at beginning.
    	string::size_type lastPos = str.find_first_not_of(delimiters, 0);
    	
	// find first "non-delimiter".
    	string::size_type pos = str.find_first_of(delimiters, lastPos);

    	while (string::npos != pos || string::npos != lastPos)
    	{
        	// found a token, add it to the vector.
        	tokens.push_back(str.substr(lastPos, pos - lastPos));
		
        	// skip delimiters.  Note the "not_of"
        	lastPos = str.find_first_not_of(delimiters, pos);
		
        	// find next "non-delimiter"
        	pos = str.find_first_of(delimiters, lastPos);
    	}

	return tokens;
}

// Reads a tab delimited file. This format is based on fake data files
// provided by Parag where the fields were:
//    ^(unknown)\t(peptide)\t(retention time)$
//
// The rest of the line is ignored after the third word or line break.
void readTabFileStream(istream* fin, vector<Peptide>& peptides, double tol,
                       bool hasJunk, bool hasPValue)
{
    //const char* delim = "\t";
    string line;

    while(getline(*fin, line))
    {
        istringstream ss(line);
        string junk;
        string peptide;
        string rt;
        string pvalue;

        if (hasJunk)
            ss >> junk;
        ss >> peptide;
        ss >> rt;
        ss >> pvalue;
        //cout << "line:\n";
        //cout << "\t" << peptide << "\n\t";
        //cout << rt << "\n\t";
        //cout << pvalue << "\n";

        //Peptide p(peptide, rt, pvalue);
        Peptide p;
        p.setPeptide(peptide);
        p.setRetentionTime(rt);
        p.setPValue(pvalue);

        if (atof(pvalue.c_str()) >= tol)
            peptides.push_back(p);
    }
}

void readTabFile(const string& in, vector<Peptide>& peptides, double tol,
    bool hasJunk, bool hasPValue)
{
    ifstream* fin = new ifstream(in.c_str());

    readTabFileStream(fin, peptides, tol, hasJunk, hasPValue);

    fin->close();
    
    delete fin;
}

// Prints the stack trace symbols.
#ifndef _WIN32
inline void printStackTrace()
{
    void * array[25];
    int nSize = backtrace(array, 25);
    char ** symbols = backtrace_symbols(array, nSize);

    for (int i = 0; i < nSize; i++)
    {
        cout << symbols[i] << endl;
    }

    free(symbols);

}
#else
inline void printStackTrace()
{}
#endif

#endif // _UTILS_HPP_
