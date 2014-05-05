//
// $Id$
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
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"


using std::string;
using std::vector;
using std::runtime_error;


#ifdef WIN32
    #define _WIN32_WINNT 0x0400
    #include <windows.h>
    #include <direct.h>
    #include <boost/nowide/convert.hpp>
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

PWIZ_API_DECL int expand_pathmask(const bfs::path& pathmask,
                                  vector<bfs::path>& matchingPaths)
{
    using bfs::path;
    int matchingPathCount = 0;

#ifdef WIN32
    path maskParentPath = pathmask.branch_path();
	WIN32_FIND_DATAW fdata;
	HANDLE srcFile = FindFirstFileExW(boost::nowide::widen(pathmask.string()).c_str(), FindExInfoStandard, &fdata, FindExSearchNameMatch, NULL, 0);
	if (srcFile == INVALID_HANDLE_VALUE)
		return 0; // no matches

    do
    {
        if (!bal::equals(fdata.cFileName, L".") &&
            !bal::equals(fdata.cFileName, L"..") != 0)
        {
	        matchingPaths.push_back( maskParentPath / fdata.cFileName );
            ++matchingPathCount;
        }
    }
    while (FindNextFileW(srcFile, &fdata));

	FindClose(srcFile);

#else

	glob_t globbuf;
	int rv = glob(pathmask.string().c_str(), 0, NULL, &globbuf);
	if(rv > 0 && rv != GLOB_NOMATCH)
		throw runtime_error("FindFilesByMask(): glob() error");

	DIR* curDir = opendir(".");
	struct stat curEntryData;

	for (size_t i=0; i < globbuf.gl_pathc; ++i)
	{
		stat(globbuf.gl_pathv[i], &curEntryData);
		if (S_ISDIR(curEntryData.st_mode) ||
            S_ISREG(curEntryData.st_mode) ||
            S_ISLNK(curEntryData.st_mode))
        {
			matchingPaths.push_back(globbuf.gl_pathv[i]);
            ++matchingPathCount;
        }
	}
	closedir(curDir);

	globfree(&globbuf);

#endif

    return matchingPathCount;
}


using boost::uintmax_t;

PWIZ_API_DECL
string abbreviate_byte_size(uintmax_t byteSize, ByteSizeAbbreviation abbreviationType)
{
    uintmax_t G, M, K;
    string GS, MS, KS;

    switch (abbreviationType)
    {
        default:
        case ByteSizeAbbreviation_IEC:
            G = (M = (K = 1024) << 10) << 10;
            GS = " GiB"; MS = " MiB"; KS = " KiB";
            break;

        case ByteSizeAbbreviation_JEDEC:
            G = (M = (K = 1024) << 10) << 10;
            GS = " GB"; MS = " MB"; KS = " KB";
            break;

        case ByteSizeAbbreviation_SI:
            G = (M = (K = 1000) * 1000) * 1000;
            GS = " GB"; MS = " MB"; KS = " KB";
            break;
    }

    string suffix;

    if( byteSize >= G )
    {
        byteSize /= G;
        suffix = GS;
    } else if( byteSize >= M )
    {
        byteSize /= M;
        suffix = MS;
    } else if( byteSize >= K )
    {
        byteSize /= K;
        suffix = KS;
    } else
    {
        suffix = " B";
    }

    return lexical_cast<string>(byteSize) + suffix;
}


PWIZ_API_DECL string read_file_header(const string& filepath, size_t length)
{
    string head;
    if (!bfs::is_directory(filepath))
    {
        random_access_compressed_ifstream is(filepath.c_str());
        if (!is)
            throw runtime_error(("[read_file_header()] Unable to open file " + filepath).c_str());

        head.resize(length, '\0');
        is.read(&head[0], (std::streamsize)head.size());
    }
    return head;
}


} // util
} // pwiz
