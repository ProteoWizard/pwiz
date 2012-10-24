//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#include <iostream>
#include <sstream>
#include <algorithm>
#include <fstream>
#include <string>
#include <stdlib.h>
#include <vector>
#include "Compare.h"

using namespace std;

// Take as input two text files and report if the differ.  Optionally
// take a list of lines to be skipped and/or fields that don't require
// an exact text match
int main (int argc, char** argv){

    string usage = "CompareTextFiles <expected> <observed> [<skip lines>]";

    if( argc < 3 ){
        cerr << usage << endl;
        exit(1);
    }

    // since we can't rely on the order, sort inputs alphabetically
    vector<string> tokens;
    tokens.push_back(argv[1]);
    tokens.push_back(argv[2]);
    if( argc > 3 ){
        tokens.push_back(argv[3]);
    }
    sort(tokens.begin(), tokens.end());

    string observedName = tokens[0];
    string expectedName = tokens[1];

    cerr << "Comparing expected, " << expectedName 
         << ", to observed, " << observedName << endl;

    // get the text from any lines that should not be compared
    vector<string> skipLines;
    CompareDetails compareDetails;
    if( argc > 3 ){
        getSkipLines(tokens[2].c_str(), skipLines, compareDetails);
    }

    ifstream expectedFile(expectedName.c_str());
    if( !expectedFile.good() ){
        cerr << "Could not open file of expected results, '" 
             << expectedName << "'" << endl;
        exit(1);
    }

    ifstream observedFile(observedName.c_str());
    if( !observedFile.good() ){
        cerr << "Could not open file of observed results, '" 
             << observedName << "'" << endl;
        exit(1);
    }

    int lineNum = 1;
    vector<string>::iterator skipLinesIter = skipLines.begin();
    vector<string>::iterator lastLine = skipLines.end();
    string expected;
    string observed;
    getline(expectedFile, expected);

    while( !expectedFile.eof() ){
        if( observedFile.eof() ){
            cerr << "The expected file has more lines than observed ("
                 << lineNum << ")" << endl;
            exit(1);
        }
        getline(observedFile, observed);

        // should we compare this line or skip it?
        if( skipLinesIter < lastLine 
            && expected.find(*skipLinesIter) != string::npos ){
            lineNum++;
            getline(expectedFile, expected);
            skipLinesIter++;
            continue;
        }

        if( ! linesMatch(expected, observed, compareDetails) ){
            cerr << "Line " << lineNum << " differs." << endl;
            cerr << "expected: " << expected << endl;
            cerr << "observed: " << observed << endl;
            exit(1);
        }
        lineNum++;

        getline(expectedFile, expected);
    }

    // check the observed file for extra lines
    getline(observedFile, observed);
    if( !observedFile.eof() ){
        cerr << "The observed file has more lines than the expected ("
             << lineNum << ")" << endl;
        exit(1);
    }

    cerr << "All " << lineNum << " lines match" << endl;

    expectedFile.close();
    observedFile.close();

}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
