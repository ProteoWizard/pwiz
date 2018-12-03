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

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/String.hpp"
#include <cstring>
#include <cstdlib>

// Run the given BiblioSpec tool with the given arguments
int main(int argc, char** argv)
{
    string usage = "ExecuteBlib <blib tool> [<inputs>+] ";

    // require a Blib executable and then any number of inputs for it
    if( argc < 2 ){
        cerr << usage << endl;
        exit(1);
    }

    // this absurd bjam run rule requires that input files be
    // listed alphabetically and then doesn't pass them in a stable order
    // hunt down the components separately: executable, options, inputs, lib
    string command; // must contain BlibBuild, BlibFilter, BlibSearch, BlibToMs2
    string options; // must start with '-'
    vector<string> libNames; // collect so we can sort them
    const char* libExt = ".blib";
	const char* pdbExt = ".pdb";
    string inputs;  // all else
    string expectedErrorMsg = "";

    for(int i = 1; i < argc; i++)
    {
        string token = argv[i];
        if (token == "--teamcity-test-decoration")
            continue;

        if (token[0] == '-')
        {
            // replace any _ with ' ' so options can have args
            bal::replace_all(token, "@", " ");
            bal::replace_all(token, "~", "\"");
            options += token;
            options += " ";
            // Is this a negative test?
            if (token[1] == 'e')
                expectedErrorMsg = token.substr(3);
        }
        else if (bal::contains(token, "BlibBuild") ||
                 bal::contains(token, "BlibFilter") ||
                 bal::contains(token, "BlibToMs2") ||
                 bal::contains(token, "BlibSearch"))
        {
            // Ignore the .pdb if it ends up in the command line.
		    if ( bal::iends_with(token, pdbExt) ) {
                continue;
			}
            command = token; // check that only one token matches?
            command += " ";
        }
        else if (bal::iends_with(token, libExt))
        {
            libNames.push_back(token);
        }
        else
        {
            inputs += token;
            inputs += " ";
        }
    }

    // for multiple libs, put them in alphabetical order since we have no
    // guarantee how they will be passed to this
    if (libNames.size() > 0)
    {
        sort(libNames.begin(), libNames.end());

        // create an empty file for the output database
        if (!bfs::exists(libNames.back()))
        {
            bfs::create_directories(bfs::path(libNames.back()).parent_path());
            ofstream(libNames.back().c_str());
        }
    }

    string libName; 
    for(size_t i = 0; i < libNames.size(); i++){
        libName += libNames[i];
        libName += " ";
    }

    string fullCommand = command + options + inputs + libName;
    cerr << "Running " << fullCommand << endl;

    int returnValue = system(fullCommand.c_str());
    cerr << "System returned " << returnValue << endl;

    if (expectedErrorMsg != "")
    {
        return !returnValue; // Expecting a failure
    }
    // why does 'return returnValue' exit with 0 when returnValue != 0?

    if( returnValue != 0 ){
        return 1;
    }
}





/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
