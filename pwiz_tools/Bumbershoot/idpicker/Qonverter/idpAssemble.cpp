//
// $Id: idpQuery.cpp 554 2014-04-08 17:29:44Z chambm $
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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2013 Vanderbilt University
//
// Contributor(s):
//


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "boost/foreach_field.hpp"
#include "boost/range/algorithm/remove_if.hpp"
#include "boost/range/adaptor/map.hpp"
#include "boost/crc.hpp"
#include "boost/variant.hpp"

#include "SchemaUpdater.hpp"
#include "Parser.hpp"
#include "Merger.hpp"
#include "sqlite3pp.h"
#include <iomanip>
//#include "svnrev.hpp"

using namespace IDPicker;
namespace sqlite = sqlite3pp;
using std::setw;
using std::setfill;
using boost::format;


BEGIN_IDPICKER_NAMESPACE

struct Version
{
    static int Major();
    static int Minor();
    static int Revision();
    static std::string str();
    static std::string LastModified();
};

int Version::Major()                { return 3; }
int Version::Minor()                { return 0; }
int Version::Revision()             { return 0; }//SVN_REV;}
string Version::LastModified()      { return ""; }//SVN_REVDATE;}
string Version::str()
{
    std::ostringstream v;
    v << Major() << "." << Minor() << "." << Revision();
    return v.str();
}

END_IDPICKER_NAMESPACE



int main(int argc, const char* argv[])
{
    cout << "IDPickerAssemble " << IDPicker::Version::str() << " (" << IDPicker::Version::LastModified() << ")\n" <<
        "" << endl;

    string usage = "IDPAssemble is a command-line tool for merging idpDB files and/or assigning source group hierarchies.\n"
                   "\n"
                   "Usage: idpAssemble <idpDB filepath> [another idpDB filepath ...] [-MergeTargetFilepath <filepath to merge to>]\n"
                   "       [-AssignSourceHierarchy <assemble.tsv>] [-cpus <max thread count>]\n"
                   "       [-b <file containing a long list of newline-separated idpDB filemasks>\n"
                   "\n"
                   "Example: idpAssemble fraction1.idpDB fraction2.idpDB fraction3.idpDB -MergeTargetFilepath mudpit.idpDB\n"
                   "\n"
                   "The assemble.txt file is a tab-delimited file with two columns. The first column is the source group path,\n"
                   "the second column is a source group name to assign to that group.";

    string mergeTargetFilepath;
    string assembleTextFilepath;
    vector<string> mergeSourceFilepaths;
    int maxThreads = 8;
    string batchFile;

    vector<string> args(argv + 1, argv + argc);

    for (size_t i = 0; i < args.size(); ++i)
    {
        if (args[i][0] == '-' && !bfs::exists(args[i]) && i + 1 == args.size())
        {
            cerr << args[i] << " must be followed by a value." << endl;
            return 1;
        }

        if (args[i] == "-MergeTargetFilepath")
            mergeTargetFilepath = args[++i];
        else if (args[i] == "-AssignSourceHierarchy")
            assembleTextFilepath = args[++i];
        else if (args[i] == "-cpus")
            maxThreads = lexical_cast<int>(args[++i]);
        else if (args[i] == "-b")
            batchFile = args[++i];
        else
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(args[i], matchingFiles);
            if (matchingFiles.empty())
                cerr << "Warning: no matching files for filemask: " << args[i] << endl;
            else
                BOOST_FOREACH(const bfs::path& p, matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    if (!batchFile.empty())
    {
        ifstream batchStream(batchFile.c_str());
        string line;
        while (batchStream >> line)
        {
            vector<bfs::path> matchingFiles;
            pwiz::util::expand_pathmask(line, matchingFiles);
            if (matchingFiles.empty())
                cerr << "Warning: no matching files for filemask: " << line << endl;
            else
                BOOST_FOREACH(const bfs::path& p, matchingFiles)
                    mergeSourceFilepaths.push_back(p.string());
        }
    }

    if (mergeSourceFilepaths.empty())
    {
        cerr << "Error: not enough arguments. No idpDB files given as input.\n" << endl;
        cerr << usage << endl;
        return 1;
    }

    if (mergeSourceFilepaths.size() > 1 && mergeTargetFilepath.empty())
    {
        cerr << "Error: more than one idpDB file was given as input but no merge target filepath was given.\n" << endl;
        cerr << usage << endl;
        return 1;
    }

    if (!assembleTextFilepath.empty() && !bfs::exists(assembleTextFilepath))
    {
        cerr << "Error: the assembly file specified by AssignSourceHierarchy does not exist." << endl;
        return 1;
    }

    if (mergeSourceFilepaths.size() == 1)
    {
        if (assembleTextFilepath.empty())
        {
            cerr << "Error: a single idpDB was given but no assembly file was specified by AssignSourceHierarchy.\n" << endl;
            cerr << usage << endl;
            return 1;
        }

        mergeTargetFilepath = mergeSourceFilepaths[0];
    }

    try
    {
        if (mergeSourceFilepaths.size() > 1)
        {
            cout << "Merging " << mergeSourceFilepaths.size() << " files to: " << mergeTargetFilepath << endl;
            Merger merger;
            merger.merge(mergeTargetFilepath, mergeSourceFilepaths, maxThreads);
        }

        if (!assembleTextFilepath.empty())
        {
            //assignSourceGroupHierarchy(mergeTargetFilepath, assembleTextFilepath);
        }

        return 0;
    }
    catch (exception& e)
    {
        cerr << "Unhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unknown exception." << endl;
    }
    return 1;
}
