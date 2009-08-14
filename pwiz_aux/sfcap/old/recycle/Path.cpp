//
// $Id$
//
//
// Darren Kessner <darren@proteowizard.org>
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


#include "Path.h"


namespace pwiz {
namespace msaux {

	
using namespace std;


string Path::directory(const string& fullpath)
{
    string::size_type indexSlash = fullpath.find_last_of("/\\");
    if (indexSlash != string::npos)
        return fullpath.substr(0, indexSlash) + '/';
    else
        return string("./");
}


string Path::filename(const string& fullpath)
{
    string::size_type indexSlash = fullpath.find_last_of("/\\");
    if (indexSlash != string::npos)
    {
        if (indexSlash+1 < fullpath.size())
            return fullpath.substr(indexSlash+1);
        else
            return string();
    }
    else
    {
        return fullpath;
    }
}


string Path::base(const string& fullpath)
{
    string filename = Path::filename(fullpath);
    string::size_type indexLastDot = filename.find_last_of(".");
    if (indexLastDot != string::npos)
        return filename.substr(0, indexLastDot);
    else
        return filename;
}


string Path::extension(const string& fullpath)
{
    string filename = Path::filename(fullpath);
    string::size_type indexLastDot = filename.find_last_of(".");
    if (indexLastDot != string::npos && indexLastDot+1 < filename.size())
        return filename.substr(indexLastDot+1);
    else
        return string();
}


bool Path::mkdir(const string& directoryName)
{
    string systemCommand = "mkdir " + directoryName + " 2> nul";
    int result = system(systemCommand.c_str());
    return (result == 0);
}


} // namespace msaux
} // namespace pwiz
