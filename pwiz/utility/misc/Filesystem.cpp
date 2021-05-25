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

#ifdef WIN32
    #define _WIN32_WINNT 0x0600
    #define WIN32_LEAN_AND_MEAN
    #define NOGDI
    #include <windows.h>
    #include <direct.h>
    #include <wincrypt.h>
    #include <winternl.h>
    #include <Psapi.h>
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

#include <boost/utility/singleton.hpp>
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>
#include <boost/locale/conversion.hpp>
#include <boost/spirit/include/karma.hpp>
//#include <boost/xpressive/xpressive.hpp>
#include <iostream>
#include <thread>

using std::string;
using std::vector;
using std::runtime_error;


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

#ifdef WIN32
#define STATUS_INFO_LENGTH_MISMATCH 0xc0000004

#define SystemHandleInformation 16

extern "C"
{
    typedef NTSTATUS(NTAPI *_NtQuerySystemInformation)(
        ULONG SystemInformationClass,
        PVOID SystemInformation,
        ULONG SystemInformationLength,
        PULONG ReturnLength
        );

    struct SYSTEM_HANDLE {
        ULONG ProcessID;
        BYTE HandleType;
        BYTE Flags;
        USHORT Handle;
        PVOID Object;
        ACCESS_MASK GrantedAccess;
    };

    struct SYSTEM_HANDLE_INFORMATION {
        ULONG HandleCount;
        SYSTEM_HANDLE Handles[1];
    };

    enum SECTION_INHERIT
    {
        ViewShare = 1,
        ViewUnmap = 2
    };

    struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
    {
        USHORT UniqueProcessId;
        USHORT CreatorBackTraceIndex;
        UCHAR ObjectTypeIndex;
        UCHAR HandleAttributes;
        USHORT HandleValue;
        PVOID Object;
        ULONG GrantedAccess;
    };


    typedef NTSTATUS(NTAPI *_NtUnmapViewOfSection)(
        HANDLE ProcessHandle,
        PVOID  BaseAddress
        );
    typedef NTSTATUS(NTAPI *_NtMapViewOfSection)(
        HANDLE          SectionHandle,
        HANDLE          ProcessHandle,
        PVOID           *BaseAddress,
        ULONG_PTR       ZeroBits,
        SIZE_T          CommitSize,
        PLARGE_INTEGER  SectionOffset,
        PSIZE_T         ViewSize,
        SECTION_INHERIT InheritDisposition,
        ULONG           AllocationType,
        ULONG           Win32Protect
        );

    typedef NTSTATUS(NTAPI *_NtQueryObject)(
        HANDLE Handle,
        OBJECT_INFORMATION_CLASS ObjectInformationClass,
        PVOID ObjectInformation,
        ULONG ObjectInformationLength,
        PULONG ReturnLength
        );

    PVOID GetLibraryProcAddress(PSTR LibraryName, PSTR ProcName) {
        return GetProcAddress(GetModuleHandleA(LibraryName), ProcName);
    }
}

    int GetFileHandleTypeNumber(SYSTEM_HANDLE_INFORMATION* handleInfos)
    {
        DWORD currentProcessId = GetCurrentProcessId();
        wstring fileType = L"File";
        std::vector<BYTE> typeInfoBytes(sizeof(PUBLIC_OBJECT_TYPE_INFORMATION));

        _NtQueryObject NtQueryObject = (_NtQueryObject)GetLibraryProcAddress("ntdll.dll", "NtQueryObject");
        if (NtQueryObject == nullptr)
        {
            fprintf(stderr, "[force_close_handles_to_filepath()] Error getting NtQueryObject function.\n");
            return 0;
        }

        map<int, int> handlesPerType;
        for (size_t i = 0; i < handleInfos->HandleCount; ++i)
        {
            if (handleInfos->Handles[i].ProcessID != currentProcessId)
                continue;

            if (handleInfos->Handles[i].HandleType < 20) // this is not the File string you're looking for
                continue;

            const auto handle = reinterpret_cast<HANDLE>(handleInfos->Handles[i].Handle);
            ULONG size;
            auto queryResult = NtQueryObject(handle, ObjectTypeInformation, typeInfoBytes.data(), typeInfoBytes.size(), &size);
            if (queryResult == STATUS_INFO_LENGTH_MISMATCH)
            {
                typeInfoBytes.resize(size);
                queryResult = NtQueryObject(handle, ObjectTypeInformation, typeInfoBytes.data(), size, nullptr);
            }

            if (NT_SUCCESS(queryResult))
            {
                const auto typeInfo = reinterpret_cast<PUBLIC_OBJECT_TYPE_INFORMATION*>(typeInfoBytes.data());
                const auto type = std::wstring(typeInfo->TypeName.Buffer, typeInfo->TypeName.Length / sizeof(WCHAR));
                if (type == fileType)
                    //return handleInfos->Handles[i].HandleType;
                    ++handlesPerType[handleInfos->Handles[i].HandleType];
            }
        }

        if (handlesPerType.empty())
            return 0;

        auto typeMode = std::max_element(handlesPerType.begin(), handlesPerType.end(), [](const auto& a, const auto& b) { return a.second < b.second; });
        return typeMode->first;
    }

    bool GetFileNameFromHandle(HANDLE hFile, wchar_t* filepathBuffer, size_t bufferLength)
    {
        // Get the file size.
        DWORD dwFileSizeHi = 0;
        DWORD dwFileSizeLo = GetFileSize(hFile, &dwFileSizeHi);

        // Cannot map 0-byte files
        if (dwFileSizeLo == 0 && dwFileSizeHi == 0)
            return false;

        // Create a file mapping object.
        HANDLE hFileMap = CreateFileMapping(hFile, NULL, PAGE_READONLY, 0, 1, NULL);

        if (!hFileMap)
            return false;

        void* pMem = MapViewOfFile(hFileMap, FILE_MAP_READ, 0, 0, 1);

        if (!pMem)
            return false;

        if (!GetMappedFileNameW(GetCurrentProcess(), pMem, filepathBuffer, bufferLength))
            return false;

        UnmapViewOfFile(pMem);
        CloseHandle(hFileMap);

        return true;
    }
#endif
} // namespace

namespace pwiz {
namespace util {


PWIZ_API_DECL bool running_on_wine()
{
#ifdef WIN32
    return GetLibraryProcAddress("ntdll.dll", "wine_get_version") != NULL;
#else
    return false;
#endif
}


PWIZ_API_DECL void force_close_handles_to_filepath(const std::string& filepath, bool closeMemoryMappedSections) noexcept(true)
{
#ifdef WIN32
    if (running_on_wine())
        return;

    _NtQuerySystemInformation NtQuerySystemInformation = (_NtQuerySystemInformation)GetLibraryProcAddress("ntdll.dll", "NtQuerySystemInformation");
    if (NtQuerySystemInformation == nullptr)
    {
        fprintf(stderr, "[force_close_handles_to_filepath()] Error getting NtQuerySystemInformation function.\n");
        return;
    }

    _NtUnmapViewOfSection NtUnmapViewOfSection = nullptr;
    _NtMapViewOfSection NtMapViewOfSection = nullptr;
    if (closeMemoryMappedSections)
    {
        NtUnmapViewOfSection = (_NtUnmapViewOfSection)GetLibraryProcAddress("ntdll.dll", "NtUnmapViewOfSection");
        if (NtUnmapViewOfSection == nullptr)
        {
            fprintf(stderr, "[force_close_handles_to_filepath()] Error getting NtUnmapViewOfSection function.\n");
            return;
        }

        NtMapViewOfSection = (_NtMapViewOfSection)GetLibraryProcAddress("ntdll.dll", "NtMapViewOfSection");
        if (NtMapViewOfSection == nullptr)
        {
            fprintf(stderr, "[force_close_handles_to_filepath()] Error getting NtMapViewOfSection function.\n");
            return;
        }
    }

    NTSTATUS status = 0;
    DWORD dwSize = sizeof(SYSTEM_HANDLE_INFORMATION);
    vector<BYTE> pInfoBytes(dwSize);

    do
    {
        // keep reallocing until buffer is big enough
        DWORD newSize = 0;
        status = NtQuerySystemInformation(SystemHandleInformation, pInfoBytes.data(), dwSize, &newSize);
        if (status == STATUS_INFO_LENGTH_MISMATCH)
        {
            if (newSize > 0)
                dwSize = newSize;
            else
                dwSize *= 2;
            pInfoBytes.resize(dwSize);
        }
    } while (status == STATUS_INFO_LENGTH_MISMATCH);

    if (status != 0)
    {
        char messageBuffer[256];
        memset(messageBuffer, 0, 256);
        FormatMessageA(FORMAT_MESSAGE_FROM_SYSTEM, NULL, status, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 255, NULL);

        fprintf(stderr, "[force_close_handles_to_filepath()] Error calling NtQuerySystemInformation function: %s\n", messageBuffer);
        return;
    }

    auto pInfo = reinterpret_cast<SYSTEM_HANDLE_INFORMATION*>(pInfoBytes.data());
    int fileHandleType = GetFileHandleTypeNumber(pInfo);
    if (fileHandleType == 0)
    {
        fprintf(stderr, "[force_close_handles_to_filepath()] Unable to determine file handle type number.\n");
        return;
    }

    auto currentProcessId = GetCurrentProcessId();
    HANDLE currentProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, currentProcessId);

    SYSTEM_INFO si;
    GetSystemInfo(&si);
    MEMORY_BASIC_INFORMATION mbi;
    PCHAR lpMem = 0;
    vector<wchar_t> mappedFilename(260);
    string narrowFilename = bfs::path(filepath).filename().string();
    vector<wchar_t> wideFilename(narrowFilename.length());
    std::use_facet<std::ctype<wchar_t>>(std::locale()).widen(narrowFilename.c_str(), narrowFilename.c_str() + narrowFilename.length(), &wideFilename[0]);
    wstring wideFilepathWithoutRoot = bfs::path(filepath).relative_path().wstring();

    if (closeMemoryMappedSections)
    {
        bool closedMappedSection = false;
        while (lpMem < si.lpMaximumApplicationAddress)
        {
            VirtualQueryEx(currentProcessHandle, lpMem, &mbi, sizeof(MEMORY_BASIC_INFORMATION));
            lpMem += mbi.RegionSize;

            if (mbi.Type != MEM_MAPPED || mbi.State != MEM_COMMIT || mbi.Protect == PAGE_NOACCESS || mbi.RegionSize <= 4096)
                continue;

            if (GetMappedFileNameW(currentProcessHandle, mbi.BaseAddress, &mappedFilename[0], 260) == 0)
                continue;

            if (!bal::iends_with(wstring(mappedFilename.data()), wideFilename))
                continue;

            if (!UnmapViewOfFile(mbi.BaseAddress))
            {
                fprintf(stderr, "[force_close_handles_to_filepath()] Error calling UnmapViewOfFile.\n");
                return;
            }
            else
            {
                //fprintf(stderr, "[force_close_handles_to_filepath()] Closed memory mapped section.\n");
                closedMappedSection = true;
            }
        }

        if (!closedMappedSection)
            fprintf(stderr, "[force_close_handles_to_filepath()] Failed to find memory mapped section.\n");
    }

    wchar_t szPath[260];

    // iterate over every handle and close file handles that match the filepath
    for (DWORD i = 0; i < pInfo->HandleCount; i++)
    {
        auto& handleInfo = pInfo->Handles[i];
        if (handleInfo.ProcessID != currentProcessId)
            continue;

        if (handleInfo.HandleType == fileHandleType)
        {
            szPath[0] = '\0';
            if (!GetFileNameFromHandle((HANDLE)handleInfo.Handle, szPath, 260))
            {
                /*char messageBuffer[256];
                memset(messageBuffer, 0, 256);
                FormatMessageA(FORMAT_MESSAGE_FROM_SYSTEM, NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 255, NULL);

                fprintf(stderr, "[force_close_handles_to_filepath()] Error calling GetFileNameFromHandle: %s\n", messageBuffer);*/
                continue;
            }

            wchar_t* handlePath = szPath;
            if (bal::iends_with(handlePath, wideFilepathWithoutRoot.c_str()))
            {
                if (!CloseHandle((HANDLE)handleInfo.Handle))
                    fprintf(stderr, "[force_close_handles_to_filepath()] Error closing file handle.\n");
                //else
                //    fprintf(stderr, "[force_close_handles_to_filepath()] Closed file handle: " + gcnew String(handlePath));
            }
        }
        else if (closeMemoryMappedSections)
        {
            // close handle to memory mapped section
            SIZE_T viewSize = 1;
            PVOID viewBase = NULL;

            NTSTATUS status = NtMapViewOfSection((HANDLE)handleInfo.Handle, currentProcessHandle, &viewBase, 0, 0, NULL, &viewSize, ViewShare, 0, PAGE_READONLY);

            if (!NT_SUCCESS(status))
                continue;

            vector<wchar_t> mappedFilename(260);
            auto result = GetMappedFileNameW(currentProcessHandle, viewBase, &mappedFilename[0], 260);

            NtUnmapViewOfSection(currentProcessHandle, viewBase);

            if (result == 0)
                continue;

            if (!bal::iends_with(wstring(mappedFilename.data()), wideFilename))
                continue;

            if (!CloseHandle((HANDLE)handleInfo.Handle))
                fprintf(stderr, "[force_close_handles_to_filepath()] Error closing section handle.\n");
            //else
            //    fprintf(stderr, "[force_close_handles_to_filepath()] Closed section handle.\n");
        }
    }
#endif
}


PWIZ_API_DECL void enable_utf8_path_operations()
{
    UTF8_BoostFilesystemPathImbuer::instance->imbue();
}


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

template <typename T>
struct double3_policy : boost::spirit::karma::real_policies<T>
{
    // up to 3 digits total, but no unnecessary precision
    static unsigned int precision(T n)
    {
        double fracPart, intPart;
        fracPart = modf(n, &intPart);
        return fracPart < 0.005 ? 0 : n < 10 ? 2 : n < 100 ? 1 : 0;
    }
    static bool trailing_zeros(T) { return false; }

    template <typename OutputIterator>
    static bool dot(OutputIterator& sink, T n, unsigned int precision)
    {
        if (precision == 0)
            return false;
        return boost::spirit::karma::real_policies<T>::dot(sink, n, precision);
    }
};

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
    double byteSizeDbl;

    if( byteSize >= G )
    {
        byteSizeDbl = (double) byteSize / G;
        //byteSizeDbl = round(byteSizeDbl * 100) / 100;
        suffix = GS;
    } else if( byteSize >= M )
    {
        byteSizeDbl = (double) byteSize / M;
        suffix = MS;
    } else if( byteSize >= K )
    {
        byteSizeDbl = (double) byteSize / K;
        suffix = KS;
    } else
    {
        byteSizeDbl = (double) byteSize;
        suffix = " B";
        return lexical_cast<string>(byteSize) + suffix;
    }

    using namespace boost::spirit::karma;
    typedef real_generator<double, double3_policy<double> > double3_type;
    static const double3_type double3 = double3_type();
    char buffer[256];
    char* p = buffer;
    generate(p, double3, byteSizeDbl);
    return std::string(&buffer[0], p) + suffix;
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
    if (bfs::is_directory(filepath) || isHTTP(filepath))
        return head;

    if (!bfs::exists(filepath))
        throw runtime_error("[read_file_header()] Unable to open file " + filepath + " (file does not exist)");

    const int RETRY_COUNT = 10;
    for (int retry=1; retry <= RETRY_COUNT; ++retry)
    {
        try
        {
#ifdef WIN32 // check for locked files which can be opened by ifstream but only produce garbage when read (at least in VC12)
            {
                std::wstring wide_filepath = boost::locale::conv::utf_to_utf<wchar_t>(filepath);
                FileWrapper handle(::CreateFileW(wide_filepath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL));
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

            break;
        }
        catch (runtime_error& e)
        {
            if (retry == RETRY_COUNT)
                throw e;
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
        }
    }
    return head;
}


PWIZ_API_DECL TemporaryFile::TemporaryFile(const string& extension)
{
    filepath = bfs::temp_directory_path() / bfs::unique_path("%%%%%%%%%%%%%%%%" + extension);
}

PWIZ_API_DECL TemporaryFile::~TemporaryFile()
{
    if (bfs::exists(filepath))
        bfs::remove(filepath);
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
