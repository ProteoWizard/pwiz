//
// Filesystem.cpp
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "Filesystem.hpp"
using std::string;
using std::vector;
using std::runtime_error;

#ifdef WIN32
    #define _WIN32_WINNT 0x0400
    #include <windows.h>
    #include <direct.h>
#else
    #include <sys/types.h>
    #include <sys/stat.h>
    #include <glob.h>
    #include <dirent.h>
    #include <unistd.h>
    #include <errno.h>
    #ifndef MAX_PATH
        #define MAX_PATH 255
    #endif
#endif


namespace pwiz {
namespace util {

void FindFilesByMask(const string& mask, vector<string>& matchingFilepaths)
{
#ifdef WIN32
    string maskPathname = bfs::path(mask).branch_path().string();
	WIN32_FIND_DATA fdata;
	HANDLE srcFile = FindFirstFileEx(mask.c_str(), FindExInfoStandard, &fdata, FindExSearchNameMatch, NULL, 0);
	if (srcFile == INVALID_HANDLE_VALUE)
		return; // no matches

    do {
	    matchingFilepaths.push_back( maskPathname + fdata.cFileName );
    } while (FindNextFile(srcFile, &fdata));

	FindClose(srcFile);

#else

	glob_t globbuf;
	int rv = glob(mask.c_str(), 0, NULL, &globbuf);
	if(rv > 0 && rv != GLOB_NOMATCH)
		throw runtime_error("FindFilesByMask(): glob() error");

	DIR* curDir = opendir(".");
	struct stat curEntryData;

	for (size_t i=0; i < globbuf.gl_pathc; ++i)
	{
		stat(globbuf.gl_pathv[i], &curEntryData);
		if (S_ISREG( curEntryData.st_mode))
			matchingFilepaths.push_back(globbuf.gl_pathv[i]);
	}
	closedir(curDir);

	globfree(&globbuf);

#endif
}


PWIZ_API_DECL vector<string> FindFilesByMask(const std::string& mask)
{
    vector<string> matchingFilepaths;
    FindFilesByMask(mask, matchingFilepaths);
    return matchingFilepaths;
}

} // util
} // pwiz
