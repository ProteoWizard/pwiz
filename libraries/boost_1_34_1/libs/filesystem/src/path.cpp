//  path.cpp  ----------------------------------------------------------------//

//  Copyright 2005 Beman Dawes
//  Use, modification, and distribution is subject to the Boost Software
//  License, Version 1.0. (See accompanying file LICENSE_1_0.txt or copy
//  at http://www.boost.org/LICENSE_1_0.txt)

//  See library home page at http://www.boost.org/libs/filesystem

//----------------------------------------------------------------------------// 

// define BOOST_FILESYSTEM_SOURCE so that <boost/filesystem/config.hpp> knows
// the library is being built (possibly exporting rather than importing code)
#define BOOST_FILESYSTEM_SOURCE 

#include <boost/filesystem/config.hpp>

#ifndef BOOST_FILESYSTEM_NARROW_ONLY

#include <boost/filesystem/path.hpp>
#include <boost/scoped_array.hpp>

#include <locale>
#include <cerrno>

namespace
{
  // std::locale construction can throw (if LC_MESSAGES is wrong, for example),
  // so a static at function scope is used to ensure that exceptions can be
  // caught. (A previous version was at namespace scope, so initialization
  // occurred before main(), preventing exceptions from being caught.)

  std::locale & loc()
  {
    // ISO C calls this "the locale-specific native environment":
    static std::locale lc("");
    return lc;
  }

  const std::codecvt<wchar_t, char, std::mbstate_t> *&
  converter()
  {
   static const std::codecvt<wchar_t, char, std::mbstate_t> *
     cvtr(
       &std::use_facet<std::codecvt<wchar_t, char, std::mbstate_t> >
        ( loc() ) );
   return cvtr;
  }

  bool locked(false);
} // unnamed namespace

namespace boost
{
  namespace filesystem
  {
    bool wpath_traits::imbue( const std::locale & new_loc, const std::nothrow_t & )
    {
      if ( locked ) return false;
      locked = true;
      loc() = new_loc;
      converter() = &std::use_facet
        <std::codecvt<wchar_t, char, std::mbstate_t> >( loc() );
      return true;
    }

    void wpath_traits::imbue( const std::locale & new_loc )
    {
      if ( locked ) boost::throw_exception( filesystem_wpath_error(
        "boost::filesystem::wpath_traits::imbue() after lockdown", 0 ) );
      imbue( new_loc, std::nothrow );
    }
    
# ifdef BOOST_POSIX_API

//  Because this is POSIX only code, we don't have to worry about ABI issues
//  described in http://www.boost.org/more/separate_compilation.html

    wpath_traits::external_string_type
    wpath_traits::to_external( const wpath & ph, 
      const internal_string_type & src )
    {
      locked = true;
      std::size_t work_size( converter()->max_length() * (src.size()+1) );
      boost::scoped_array<char> work( new char[ work_size ] );
      std::mbstate_t state;
      const internal_string_type::value_type * from_next;
      external_string_type::value_type * to_next;
      if ( converter()->out( 
        state, src.c_str(), src.c_str()+src.size(), from_next, work.get(),
        work.get()+work_size, to_next ) != std::codecvt_base::ok )
        boost::throw_exception( boost::filesystem::filesystem_wpath_error(
          "boost::filesystem::wpath::to_external conversion error",
          ph, EINVAL ) );
      *to_next = '\0';
      return external_string_type( work.get() );
    }

    wpath_traits::internal_string_type
    wpath_traits::to_internal( const external_string_type & src )
    {
      locked = true;
      std::size_t work_size( src.size()+1 );
      boost::scoped_array<wchar_t> work( new wchar_t[ work_size ] );
      std::mbstate_t state;
      const external_string_type::value_type * from_next;
      internal_string_type::value_type * to_next;
      if ( converter()->in( 
        state, src.c_str(), src.c_str()+src.size(), from_next, work.get(),
        work.get()+work_size, to_next ) != std::codecvt_base::ok )
        boost::throw_exception( boost::filesystem::filesystem_wpath_error(
          "boost::filesystem::wpath::to_internal conversion error", EINVAL ) );
      *to_next = L'\0';
      return internal_string_type( work.get() );
    }
# endif // BOOST_POSIX_API

  } // namespace filesystem
} // namespace boost

#endif // ifndef BOOST_FILESYSTEM_NARROW_ONLY
