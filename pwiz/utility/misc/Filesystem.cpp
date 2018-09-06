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
#include <boost/utility/singleton.hpp>
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>
#include <boost/locale/conversion.hpp>
//#include <boost/xpressive/xpressive.hpp>


using std::string;
using std::vector;
using std::runtime_error;


#ifdef WIN32
    #define _WIN32_WINNT 0x0400
    #include <windows.h>
    #include <direct.h>
    #include <boost/nowide/convert.hpp>
    #include <boost/noncopyable.hpp>
#else
    #include <sys/types.h>
    #include <sys/stat.h>
    #include <sys/ioctl.h>
    #include <glob.h>
    #include <dirent.h>
    #include <unistd.h>
    #include <errno.h>
    #ifndef MAX_PATH
        #define MAX_PATH 255
    #endif
#endif


namespace {

class UTF8_BoostFilesystemPathImbuer : public boost::singleton<UTF8_BoostFilesystemPathImbuer>
{
    public:
    UTF8_BoostFilesystemPathImbuer(boost::restricted)
    {
        std::locale global_loc = std::locale();
        std::locale loc(global_loc, new boost::filesystem::detail::utf8_codecvt_facet);
        bfs::path::imbue(loc);
    }

    void imbue() const {};
};

} // namespace

namespace pwiz {
namespace util {

PWIZ_API_DECL int expand_pathmask(const bfs::path& pathmask,
                                  vector<bfs::path>& matchingPaths)
{
    UTF8_BoostFilesystemPathImbuer::instance->imbue();

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


namespace
{
    void copy_recursive(const bfs::path& from, const bfs::path& to)
    {
        bfs::copy_directory(from, to);

        for(bfs::directory_entry& entry : bfs::directory_iterator(from))
        {
            bfs::file_status status = entry.status();
            if (status.type() == bfs::directory_file)
                copy_recursive(entry.path(), to / entry.path().filename());
            else if (status.type() == bfs::regular_file)
                bfs::copy_file(entry.path(), to / entry.path().filename());
            else
                throw bfs::filesystem_error("[copy_directory] invalid path type", entry.path(), boost::system::error_code(boost::system::errc::no_such_file_or_directory, boost::system::system_category()));
        }
    }

    void copy_recursive(const bfs::path& from, const bfs::path& to, boost::system::error_code& ec)
    {
        bfs::copy_directory(from, to, ec);
        if (ec.value() != 0)
            return;

        for(bfs::directory_entry& entry : bfs::directory_iterator(from))
        {
            bfs::file_status status = entry.status(ec);
            if (status.type() == bfs::directory_file)
                copy_recursive(entry.path(), to / entry.path().filename(), ec);
            else if (status.type() == bfs::regular_file)
                bfs::copy_file(entry.path(), to / entry.path().filename(), ec);
            else if (ec.value() != 0)
                ec.assign(boost::system::errc::no_such_file_or_directory, boost::system::system_category());
        }
    }
}

PWIZ_API_DECL void copy_directory(const bfs::path& from, const bfs::path& to, bool recursive, boost::system::error_code* ec)
{
    if (!bfs::is_directory(from))
        throw bfs::filesystem_error("[copy_directory] source path is not a directory", from, boost::system::error_code(boost::system::errc::not_a_directory, boost::system::system_category()));

    if (bfs::exists(to))
    {
        if (ec != NULL)
            ec->assign(boost::system::errc::file_exists, boost::system::system_category());
        else
            throw bfs::filesystem_error("[copy_directory] target path exists", to, boost::system::error_code(boost::system::errc::file_exists, boost::system::system_category()));
    }

    if (recursive)
    {
        if (ec != NULL)
            copy_recursive(from, to, *ec);
        else
            copy_recursive(from, to);
    }
    else
    {
        if (ec != NULL)
            bfs::copy_directory(from, to, *ec);
        else
            bfs::copy_directory(from, to);
    }
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


PWIZ_API_DECL bool isHTTP(const string& s)
{
    //using namespace boost::xpressive;

    // from URI RFC via http://stackoverflow.com/a/26766402/638445
    //sregex uriRegex = sregex::compile("^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))?");
    //return regex_match(s, uriRegex);

    return bal::istarts_with(s, "http://") || bal::istarts_with(s, "https://");
}


#ifdef WIN32
namespace {
    struct FileWrapper : boost::noncopyable
    {
        FileWrapper(HANDLE h) : h(h) {}
        ~FileWrapper() { CloseHandle(h); }
        bool operator== (const HANDLE& rhs) { return h==rhs; }
        private:
        HANDLE h;
    };
}
#endif


PWIZ_API_DECL string read_file_header(const string& filepath, size_t length)
{
    UTF8_BoostFilesystemPathImbuer::instance->imbue();

    string head;
    if (!bfs::is_directory(filepath) && !isHTTP(filepath))
    {
        if (!bfs::exists(filepath))
            throw runtime_error("[read_file_header()] Unable to open file " + filepath + " (file does not exist)");

#ifdef WIN32 // check for locked files which can be opened by ifstream but only produce garbage when read (at least in VC12)
        {
            std::wstring wide_filepath = boost::locale::conv::utf_to_utf<wchar_t>(filepath);
            FileWrapper handle(::CreateFileW(wide_filepath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL));
            if (handle == INVALID_HANDLE_VALUE)
                throw runtime_error("[read_file_header()] Unable to open file " + filepath + " (invalid permission or file locked)");
        }
#endif

        random_access_compressed_ifstream is(filepath.c_str());
        if (!is)
            throw runtime_error("[read_file_header()] Unable to open file " + filepath + " (" + strerror(errno) + ")");

        head.resize(length, '\0');
        if (!is.read(&head[0], (std::streamsize)head.size()) && !is.eof())
            throw runtime_error("[read_file_header()] Unable to read file " + filepath + " (" + strerror(errno) + ")");
    }
    return head;
}


PWIZ_API_DECL std::pair<int, int> get_console_bounds(const std::pair<int, int>& defaultBounds)
{
#ifdef WIN32
    CONSOLE_SCREEN_BUFFER_INFO csbi;
    BOOL ret;
    ret = GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), &csbi);
    if (ret)
        return make_pair(csbi.dwSize.X, csbi.dwSize.Y);
    else
        return defaultBounds;
#else
    winsize max;
    if (ioctl(0, TIOCGWINSZ, &max) == 0)
        return make_pair(max.ws_col, max.ws_row);
    else
        return defaultBounds;
#endif
}


} // util
} // pwiz
