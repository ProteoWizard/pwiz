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
#include "pwiz/utility/misc/unit.hpp"
#include <cstring>
#include <cstdlib>


class TempFileDeleter
{
    bfs::path filepath_;

    public:
    TempFileDeleter(const bfs::path& filepath) : filepath_(filepath)
    {
    }

    ~TempFileDeleter()
    {
        boost::system::error_code ec;
        bfs::remove(filepath_, ec);
    }

    const bfs::path& filepath() const { return filepath_; }
};


// Run the given BiblioSpec tool with the given arguments
int executeBlib(const vector<string>& argv)
{
    string usage = "ExecuteBlib <blib tool> [<inputs>+] ";

    // require a Blib executable and then any number of inputs for it
    if( argv.size() < 2 ){
        cerr << usage << endl;
        return 1;
    }

#ifdef _WIN32
#define SLASH "\\"
#else
#define SLASH "/"
#endif

    string pathToBlibExe = bfs::path(argv[0]).parent_path().string();
    bal::replace_all(pathToBlibExe, "BiblioSpec" SLASH "tests", "BiblioSpec" SLASH "src");

    // this absurd bjam run rule requires that input files be
    // listed alphabetically and then doesn't pass them in a stable order
    // hunt down the components separately: executable, options, inputs, lib
    string command; // must contain BlibBuild, BlibFilter, BlibSearch, BlibToMs2
    string options; // must start with '-'
    vector<string> libNames; // collect so we can sort them
    const char* libExt = ".blib";
	const char* pdbExt = ".pdb";
    string inputs;  // all else
    string references, skiplines;
    string outputs; // passed with -o
    string expectedErrorMsg = "";
    bool unicodeTest = false;
    vector<TempFileDeleter> tempFiles;

    string compareCmd; // CompareLibraryContents or CompareTextFiles

    for(int i = 1; i < argv.size(); i++)
    {
        string token = argv[i];

        if (token[0] == '-')
        {
            // replace any _ with ' ' so options can have args
            bal::replace_all(token, "@", " ");
            bal::replace_all(token, "~", "\"");
            // Is this a negative test?
            if (token[1] == 'e')
                expectedErrorMsg = token.substr(3);
            else if (bal::iequals(token, "--unicode"))
                unicodeTest = true;
            else if (bal::istarts_with(token, "--out="))
                outputs += token.substr(6) + " ";
            else
            {
                options += token + " ";
            }
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
            command = pathToBlibExe + SLASH + token; // check that only one token matches?
            command += " ";
        }
        else if (bal::contains(token, "CompareLibraryContents") ||
                 bal::contains(token, "CompareTextFiles"))
        {
            // Ignore the .pdb if it ends up in the command line.
		    if ( bal::iends_with(token, pdbExt) ) {
                continue;
			}
            compareCmd = token; // check that only one token matches?
            compareCmd += " ";
        }
        else if (bal::iends_with(token, ".check") ||
                 bal::iends_with(token, ".report") ||
                 bal::iends_with(token, ".lms2"))
        {
            references += token + " ";
        }
        else if (bal::iends_with(token, "skip-lines"))
        {
            skiplines += token + " ";
        }
        else if (bal::iends_with(token, libExt))
        {
            libNames.push_back(token);
        }
        else
        {
            if (unicodeTest)
            {
                bfs::path unicodePath(token);
                unicodePath = unicodePath.parent_path() / ("试验_" + unicodePath.filename().string());
                if (!bfs::exists(unicodePath))
                {
                    bfs::copy_file(token, unicodePath);
                    tempFiles.emplace_back(unicodePath);
                }
                token = unicodePath.string();
            }
            inputs += token + " ";
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

    string fullCommand;
    if (bal::contains(command, "BlibToMs2"))
        fullCommand = command + options + "-f " + outputs + libName;
    else if (bal::contains(command, "BlibSearch"))
    {
        string outputsTweaked = outputs;
        bal::replace_all(outputsTweaked, ".decoy.report", ".report"); // when testing decoy.report output, use original report name
        fullCommand = command + options + "-R " + outputsTweaked + inputs + libName;
    }
    else
        fullCommand = command + options + inputs + libName + outputs;
    cerr << "Running " << fullCommand << endl;

    int returnValue = bnw::system(fullCommand.c_str());

    if (expectedErrorMsg != "")
    {
        cerr << "Blib command returned " << returnValue << endl;
        return returnValue == 0; // Expecting a failure
    }
    else if (returnValue != 0)
        throw runtime_error("Blib command returned " + lexical_cast<string>(returnValue));

    string fullCompareCommand = compareCmd + outputs + references + skiplines;
    cerr << "Testing output with: " << fullCompareCommand << endl;
    
    returnValue = bnw::system(fullCompareCommand.c_str());

    if (returnValue != 0)
        throw runtime_error("Compare returned " + lexical_cast<string>(returnValue));

    return returnValue;
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)
    if (teamcityTestDecoration)
        testArgs.erase(find(testArgs.begin(), testArgs.end(), "--teamcity-test-decoration"));

    try
    {
        testExitStatus = executeBlib(testArgs);
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (const char* msg) {
        TEST_FAILED(msg);
    }
    catch (string msg) {
        TEST_FAILED(msg.c_str());
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}




/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
