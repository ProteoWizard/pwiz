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

#include "Compare.h"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <stdlib.h>


// Take as input two text files and report if the differ.  Optionally
// take a list of lines to be skipped and/or fields that don't require
// an exact text match
int test (const vector<string>& args)
{
    string usage = "CompareTextFiles <expected> <observed> [<skip lines>]";

    if( args.size() < 3 )
        throw runtime_error(usage);

    // since we can't rely on the order, sort inputs alphabetically
    vector<string> tokens;
    tokens.push_back(args[1]);
    tokens.push_back(args[2]);
    if( args.size() > 3 )
        tokens.push_back(args[3]);

    sort(tokens.begin(), tokens.end());

    string observedName = tokens[0];
    string expectedName = tokens[1];

    cerr << "Comparing expected, " << expectedName 
         << ", to observed, " << observedName << endl;

    // get the text from any lines that should not be compared
    CompareDetails compareDetails;
    if( args.size() > 3 )
        getSkipLines(tokens[2].c_str(), compareDetails);

    ifstream expectedFile(expectedName.c_str());
    if( !expectedFile.good() )
        throw runtime_error("Could not open file of expected results, '" + expectedName + "'");

    ifstream observedFile(observedName.c_str());
    if( !observedFile.good() )
        throw runtime_error("Could not open file of observed results, '" + observedName + "'");

    int lineNum = 1;
    vector<string>::iterator skipLinesIter = compareDetails.skipLines_.begin();
    vector<string>::iterator lastLine = compareDetails.skipLines_.end();
    string expected;
    string observed;
    getline(expectedFile, expected);
    expected = trim(expected);

    while( !expectedFile.eof() )
    {
        if( observedFile.eof() )
            throw runtime_error("The expected file has more lines than observed (" + lexical_cast<string>(lineNum) + ")");
        getline(observedFile, observed);
        observed = trim(observed);

        // should we compare this line or skip it?
        if( skipLinesIter < lastLine 
            && expected.find(*skipLinesIter) != string::npos )
        {
            lineNum++;
            getline(expectedFile, expected);
            expected = trim(expected);
            skipLinesIter++;
            continue;
        }

        if( ! linesMatch(expected, observed, compareDetails) )
        {
            cerr << "Line " << lineNum << " differs." << endl;
            cerr << "expected: " << expected << endl;
            cerr << "observed: " << observed << endl;
            throw runtime_error("Line " + lexical_cast<string>(lineNum) + " differs.");
        }
        lineNum++;

        getline(expectedFile, expected);
        expected = trim(expected);
    }

    // check the observed file for extra lines
    getline(observedFile, observed);
    if (!observedFile.eof())
        throw runtime_error("The observed file has more lines than the expected (" + lexical_cast<string>(lineNum) + ")");

    cerr << "All " << lineNum << " lines match" << endl;

    return 0;
}


int main(int argc, char* argv[])
{
    bnw::args utf8ArgWrapper(argc, argv); // modifies argv in-place with UTF-8 version on Windows
    pwiz::util::enable_utf8_path_operations();

    try
    {
        return test(vector<string>(argv, argv + argc));
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (const char* msg)
    {
        cerr << msg << endl;
        return 1;
    }
    catch (string msg)
    {
        cerr << msg << endl;
        return 1;
    }
    catch (...)
    {
        cerr << "Caught unknown exception." << endl;
        return 1;
    }
}



/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
