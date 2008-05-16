//  libs/filesystem/test/convenience_test.cpp  -------------------------------//

//  Copyright Beman Dawes, 2002
//  Copyright Vladimir Prus, 2002
//  Use, modification, and distribution is subject to the Boost Software
//  License, Version 1.0. (See accompanying file LICENSE_1_0.txt or copy at
//  http://www.boost.org/LICENSE_1_0.txt)

//  See library home page at http://www.boost.org/libs/filesystem

//  VC++ 8.0 warns on various less-than-safe practices.
//  See http://msdn.microsoft.com/msdnmag/issues/05/05/SafeCandC/default.aspx
//  But at least in VC++ 8.0 betas, their own libraries use the problem
//  practices. So turn off the warnings.
#define _CRT_SECURE_NO_DEPRECATE
#define _SCL_SECURE_NO_DEPRECATE

#include <boost/filesystem/convenience.hpp>
namespace fs = boost::filesystem;
using fs::path;

#include <boost/test/minimal.hpp>
#include <boost/bind.hpp>
#include <fstream>
#include <iostream>

#ifndef BOOST_FILESYSTEM_NARROW_ONLY
# define BOOST_FS_IS_EMPTY fs::is_empty
# define BOOST_BND(BOOST_FUNC_TO_DO) BOOST_FUNC_TO_DO<fs::path>
#else
# define BOOST_FS_IS_EMPTY fs::_is_empty
# define BOOST_BND(BOOST_FUNC_TO_DO) BOOST_FUNC_TO_DO
#endif

namespace
{
  template< typename F >
    bool throws_fs_error( F func, fs::errno_type ec = 0 )
  {
    try { func(); }

    catch ( const fs::filesystem_error & ex )
    {
      if ( ec == 0
        || ec == fs::lookup_error_code(ex.system_error()) ) return true;
      std::cout
        << "exception reports " << fs::lookup_error_code(ex.system_error())
        << ", should be " << ec
        << "\n system_error() is " << ex.system_error()
        << std::endl;
      return false;
    }
    return false;
  }
}
int test_main( int, char*[] )
{
  path::default_name_check( fs::no_check ); // names below not valid on all O/S's
                                            // but they must be tested anyhow

//  create_directories() tests  ----------------------------------------------//

  BOOST_CHECK( !fs::create_directories( "" ) );  // should be harmless
  BOOST_CHECK( !fs::create_directories( "/" ) ); // ditto

  fs::remove_all( "xx" );  // make sure slate is blank
  BOOST_CHECK( !fs::exists( "xx" ) ); // reality check

  BOOST_CHECK( fs::create_directories( "xx" ) );
  BOOST_CHECK( fs::exists( "xx" ) );
  BOOST_CHECK( fs::is_directory( "xx" ) );

  BOOST_CHECK( fs::create_directories( "xx/ww/zz" ) );
  BOOST_CHECK( fs::exists( "xx" ) );
  BOOST_CHECK( fs::exists( "xx/ww" ) );
  BOOST_CHECK( fs::exists( "xx/ww/zz" ) );
  BOOST_CHECK( fs::is_directory( "xx" ) );
  BOOST_CHECK( fs::is_directory( "xx/ww" ) );
  BOOST_CHECK( fs::is_directory( "xx/ww/zz" ) );

  path is_a_file( "xx/uu" );
  {
    std::ofstream f( is_a_file.native_file_string().c_str() );
    BOOST_CHECK( !!f );
  }
  BOOST_CHECK( throws_fs_error(
    boost::bind( BOOST_BND(fs::create_directories), is_a_file ) ) );
  BOOST_CHECK( throws_fs_error(
    boost::bind( BOOST_BND(fs::create_directories), is_a_file / "aa" ) ) );
  
// extension() tests ---------------------------------------------------------//

  BOOST_CHECK( fs::extension("a/b") == "" );
  BOOST_CHECK( fs::extension("a/b.txt") == ".txt" );
  BOOST_CHECK( fs::extension("a/b.") == "." );
  BOOST_CHECK( fs::extension("a.b.c") == ".c" );
  BOOST_CHECK( fs::extension("a.b.c.") == "." );
  BOOST_CHECK( fs::extension("") == "" );
  BOOST_CHECK( fs::extension("a/") == "." );
  
// basename() tests ----------------------------------------------------------//

  BOOST_CHECK( fs::basename("b") == "b" );
  BOOST_CHECK( fs::basename("a/b.txt") == "b" );
  BOOST_CHECK( fs::basename("a/b.") == "b" ); 
  BOOST_CHECK( fs::basename("a.b.c") == "a.b" );
  BOOST_CHECK( fs::basename("a.b.c.") == "a.b.c" );
  BOOST_CHECK( fs::basename("") == "" );
  
// change_extension tests ---------------------------------------------------//

  BOOST_CHECK( fs::change_extension("a.txt", ".tex").string() == "a.tex" );
  BOOST_CHECK( fs::change_extension("a.", ".tex").string() == "a.tex" );
  BOOST_CHECK( fs::change_extension("a", ".txt").string() == "a.txt" );
  BOOST_CHECK( fs::change_extension("a.b.txt", ".tex").string() == "a.b.tex" );  
  // see the rationale in html docs for explanation why this works
  BOOST_CHECK( fs::change_extension("", ".png").string() == ".png" );

// what() tests --------------------------------------------------------------//

    try { throw fs::filesystem_path_error( "abc", "p1", "p2", 0 ); }
    catch ( const fs::filesystem_path_error & ex )
    {
#   if !defined( BOOST_MSVC ) || BOOST_MSVC >= 1300 // > VC++ 7.0
      BOOST_CHECK( fs::what(ex) == std::string( "abc: \"p1\", \"p2\"" ) );
#   endif
    }


  return 0;
}
