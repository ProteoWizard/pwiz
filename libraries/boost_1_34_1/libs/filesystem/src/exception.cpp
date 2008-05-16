//  Exception implementation file  -------------------------------------------//

//  Copyright 2002 Beman Dawes
//  Copyright 2001 Dietmar Kuehl 
//  Use, modification, and distribution is subject to the Boost Software
//  License, Version 1.0. (See accompanying file LICENSE_1_0.txt or copy
//  at http://www.boost.org/LICENSE_1_0.txt)

//  See library home page at http://www.boost.org/libs/filesystem

//----------------------------------------------------------------------------//

// define BOOST_FILESYSTEM_SOURCE so that <boost/filesystem/config.hpp> knows
// the library is being built (possibly exporting rather than importing code)
#define BOOST_FILESYSTEM_SOURCE 

#include <boost/filesystem/config.hpp>
#include <boost/filesystem/path.hpp>
#include <boost/filesystem/cerrno.hpp>

namespace fs = boost::filesystem;

#include <cstring> // SGI MIPSpro compilers need this

# ifdef BOOST_NO_STDC_NAMESPACE
    namespace std { using ::strerror; }
# endif


# if defined( BOOST_WINDOWS_API )
#   include "windows.h"
# endif

//----------------------------------------------------------------------------//

namespace
{
#ifdef BOOST_WINDOWS_API
  struct ec_xlate { fs::system_error_type sys_ec; fs::errno_type ec; };
  const ec_xlate ec_table[] =
  {
    // see WinError.h comments for descriptions of errors
    
    // most common errors first to speed sequential search
    { ERROR_FILE_NOT_FOUND, ENOENT },
    { ERROR_PATH_NOT_FOUND, ENOENT },

    // alphabetical for easy maintenance
    { 0, 0 }, // no error 
    { ERROR_ACCESS_DENIED, EACCES },
    { ERROR_ALREADY_EXISTS, EEXIST },
    { ERROR_BAD_UNIT, ENODEV },
    { ERROR_BUFFER_OVERFLOW, ENAMETOOLONG },
    { ERROR_BUSY, EBUSY },
    { ERROR_BUSY_DRIVE, EBUSY },
    { ERROR_CANNOT_MAKE, EACCES },
    { ERROR_CANTOPEN, EIO },
    { ERROR_CANTREAD, EIO },
    { ERROR_CANTWRITE, EIO },
    { ERROR_CURRENT_DIRECTORY, EACCES },
    { ERROR_DEV_NOT_EXIST, ENODEV },
    { ERROR_DEVICE_IN_USE, EBUSY },
    { ERROR_DIR_NOT_EMPTY, ENOTEMPTY },
    { ERROR_DIRECTORY, EINVAL }, // WinError.h: "The directory name is invalid"
    { ERROR_DISK_FULL, ENOSPC },
    { ERROR_FILE_EXISTS, EEXIST },
    { ERROR_HANDLE_DISK_FULL, ENOSPC },
    { ERROR_INVALID_ACCESS, EACCES },
    { ERROR_INVALID_DRIVE, ENODEV },
    { ERROR_INVALID_FUNCTION, ENOSYS },
    { ERROR_INVALID_HANDLE, EBADHANDLE },
    { ERROR_INVALID_NAME, EINVAL },
    { ERROR_LOCK_VIOLATION, EACCES },
    { ERROR_LOCKED, EACCES },
    { ERROR_NOACCESS, EACCES },
    { ERROR_NOT_ENOUGH_MEMORY, ENOMEM },
    { ERROR_NOT_READY, EAGAIN },
    { ERROR_NOT_SAME_DEVICE, EXDEV },
    { ERROR_OPEN_FAILED, EIO },
    { ERROR_OPEN_FILES, EBUSY },
    { ERROR_OUTOFMEMORY, ENOMEM },
    { ERROR_READ_FAULT, EIO },
    { ERROR_SEEK, EIO },
    { ERROR_SHARING_VIOLATION, EACCES },
    { ERROR_TOO_MANY_OPEN_FILES, ENFILE },
    { ERROR_WRITE_FAULT, EIO },
    { ERROR_WRITE_PROTECT, EROFS },
    { 0,EOTHER }
  };
#endif

} // unnamed namespace

namespace boost
{
  namespace filesystem
  {
# ifdef BOOST_WINDOWS_API

    BOOST_FILESYSTEM_DECL
    errno_type lookup_errno( system_error_type sys_err_code )
    {
      for ( const ec_xlate * cur = &ec_table[0];
        cur != ec_table
          + sizeof(ec_table)/sizeof(ec_xlate); ++cur )
      {
        if ( sys_err_code == cur->sys_ec ) return cur->ec;
      }
      return EOTHER;
    }

    BOOST_FILESYSTEM_DECL void
    system_message( system_error_type sys_err_code, std::string & target )
    {
      LPVOID lpMsgBuf;
      ::FormatMessageA( 
          FORMAT_MESSAGE_ALLOCATE_BUFFER | 
          FORMAT_MESSAGE_FROM_SYSTEM | 
          FORMAT_MESSAGE_IGNORE_INSERTS,
          NULL,
          sys_err_code,
          MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), // Default language
          (LPSTR) &lpMsgBuf,
          0,
          NULL 
      );
      target += static_cast<LPCSTR>(lpMsgBuf);
      ::LocalFree( lpMsgBuf ); // free the buffer
      while ( target.size()
        && (target[target.size()-1] == '\n' || target[target.size()-1] == '\r') )
          target.erase( target.size()-1 );
    }

#  ifndef BOOST_FILESYSTEM_NARROW_ONLY
    BOOST_FILESYSTEM_DECL void
    system_message( system_error_type sys_err_code, std::wstring & target )
    {
      LPVOID lpMsgBuf;
      ::FormatMessageW( 
          FORMAT_MESSAGE_ALLOCATE_BUFFER | 
          FORMAT_MESSAGE_FROM_SYSTEM | 
          FORMAT_MESSAGE_IGNORE_INSERTS,
          NULL,
          sys_err_code,
          MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), // Default language
          (LPWSTR) &lpMsgBuf,
          0,
          NULL 
      );
      target += static_cast<LPCWSTR>(lpMsgBuf);
      ::LocalFree( lpMsgBuf ); // free the buffer
      while ( target.size()
        && (target[target.size()-1] == L'\n' || target[target.size()-1] == L'\r') )
          target.erase( target.size()-1 );
    }
#  endif
# else
    void
    system_message( system_error_type sys_err_code, std::string & target )
    {
      target += std::strerror( sys_err_code );
    }
# endif

  } // namespace filesystem
} // namespace boost
