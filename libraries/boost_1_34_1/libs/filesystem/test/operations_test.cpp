//  Boost operations_test.cpp  -----------------------------------------------//

//  Copyright Beman Dawes 2002.
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

#include <boost/filesystem/operations.hpp>
#include <boost/filesystem/convenience.hpp>
#include <boost/filesystem/cerrno.hpp>
namespace fs = boost::filesystem;

#include <boost/config.hpp>
#include <boost/test/minimal.hpp>
#include <boost/concept_check.hpp>
#include <boost/bind.hpp>
using boost::bind;

#include <fstream>
#include <iostream>
#include <string>
#include <ctime>
#include <cstdlib> // for system()

#ifndef BOOST_FILESYSTEM_NARROW_ONLY
# define BOOST_BND(BOOST_FUNC_TO_DO) BOOST_FUNC_TO_DO<fs::path>
#else
# define BOOST_BND(BOOST_FUNC_TO_DO) BOOST_FUNC_TO_DO
#endif

// VC++ 7.0 and earlier has a serious namespace bug that causes a clash
// between boost::filesystem::is_empty and the unrelated type trait
// boost::is_empty.
#if !defined( BOOST_MSVC ) || BOOST_MSVC > 1300
# define BOOST_FS_IS_EMPTY fs::is_empty
#else
# define BOOST_FS_IS_EMPTY fs::_is_empty
#endif

# ifdef BOOST_NO_STDC_NAMESPACE
    namespace std { using ::asctime; using ::gmtime; using ::localtime;
    using ::difftime; using ::time; using ::tm; using ::mktime; using ::system; }
# endif

#define CHECK_EXCEPTION(b,e) throws_fs_error(b,e,__LINE__)

namespace
{
  bool report_throws;
  fs::directory_iterator end_itr;

  const char * temp_dir_name = "temp_fs_test_dir";

  void create_file( const fs::path & ph, const std::string & contents )
  {
    std::ofstream f( ph.file_string().c_str() );
    if ( !f )
      throw fs::filesystem_path_error( "operations_test create_file",
        ph, errno );
    if ( !contents.empty() ) f << contents;
  }

  void verify_file( const fs::path & ph, const std::string & expected )
  {
    std::ifstream f( ph.file_string().c_str() );
    if ( !f )
      throw fs::filesystem_path_error( "operations_test verify_file",
        ph, errno );
    std::string contents;
    f >> contents;
    if ( contents != expected )
      throw fs::filesystem_path_error( "operations_test verify_file contents \""
        + contents  + "\" != \"" + expected + "\"", ph, 0 );
  }

  template< typename F >
    bool throws_fs_error( F func, fs::errno_type ec, int line )
  {
    try { func(); }

    catch ( const fs::filesystem_path_error & ex )
    {
      if ( report_throws )
      {
        // use the what() convenience function to display exceptions
        std::cout << fs::what(ex) << "\n";
      }
      if ( ec == 0
        || ec == fs::lookup_error_code(ex.system_error()) ) return true;
      std::cout
        << "\nWarning: line " << line
        << " exception reports " << fs::lookup_error_code(ex.system_error())
        << ", should be " << ec
        << "\n system_error() is " << ex.system_error()
        << std::endl;
      return true;
    }
    return false;
  }

  // compile-only two argument "do-the-right-thing" tests
  //   verifies that all overload combinations compile without error
  void do_not_call()
  {
    fs::path p;
    std::string s;
    const char * a = 0;
    fs::copy_file( p, p );
    fs::copy_file( s, p );
    fs::copy_file( a, p );
    fs::copy_file( p, s );
    fs::copy_file( p, a );
    fs::copy_file( s, s );
    fs::copy_file( a, s );
    fs::copy_file( s, a );
    fs::copy_file( a, a );
  }

} // unnamed namespace

//  test_main  ---------------------------------------------------------------//

int test_main( int argc, char * argv[] )
{
  if ( argc > 1 && *argv[1]=='-' && *(argv[1]+1)=='t' ) report_throws = true;

  std::string platform( BOOST_PLATFORM );

  // The choice of platform is make at runtime rather than compile-time
  // so that compile errors for all platforms will be detected even though
  // only the current platform is runtime tested.
# if defined( BOOST_POSIX_API )
    platform = "POSIX";
# elif defined( BOOST_WINDOWS_API )
    platform = "Windows";
# else
    platform = ( platform == "Win32" || platform == "Win64" || platform == "Cygwin" )
               ? "Windows"
               : "POSIX";
# endif
  std::cout << "API is " << platform << '\n';

  std::cout << "\ninitial_path<path>().string() is\n  \""
    << fs::initial_path<fs::path>().string()
            << "\"\n";
  std::cout << "\ninitial_path<fs::path>().file_string() is\n  \""
            << fs::initial_path<fs::path>().file_string()
            << "\"\n\n";
  BOOST_CHECK( fs::initial_path<fs::path>().is_complete() );
  BOOST_CHECK( fs::current_path<fs::path>().is_complete() );
  BOOST_CHECK( fs::initial_path<fs::path>().string()
    == fs::current_path<fs::path>().string() );

  BOOST_CHECK( fs::complete( "" ).empty() );
  BOOST_CHECK( fs::complete( "/" ).string() == fs::initial_path<fs::path>().root_path().string() );
  BOOST_CHECK( fs::complete( "foo" ).string() == fs::initial_path<fs::path>().string()+"/foo" );
  BOOST_CHECK( fs::complete( "/foo" ).string() == fs::initial_path<fs::path>().root_path().string()+"foo" );
  BOOST_CHECK( fs::complete( "foo", fs::path( "//net/bar" ) ).string()
      ==  "//net/bar/foo" );

  // predicate and status tests
  fs::path ng( " no-way, Jose" );
  BOOST_CHECK( !fs::exists( ng ) );
  BOOST_CHECK( !fs::is_directory( ng ) );
  BOOST_CHECK( !fs::is_regular( ng ) );
  BOOST_CHECK( !fs::is_symlink( ng ) );
  fs::file_status stat( fs::status( ng ) );
  BOOST_CHECK( fs::status_known( stat ) );
  BOOST_CHECK( !fs::exists( stat ) );
  BOOST_CHECK( !fs::is_directory( stat ) );
  BOOST_CHECK( !fs::is_regular( stat ) );
  BOOST_CHECK( !fs::is_other( stat ) );
  BOOST_CHECK( !fs::is_symlink( stat ) );
  stat = fs::status( "" );
  BOOST_CHECK( fs::status_known( stat ) );
  BOOST_CHECK( !fs::exists( stat ) );
  BOOST_CHECK( !fs::is_directory( stat ) );
  BOOST_CHECK( !fs::is_regular( stat ) );
  BOOST_CHECK( !fs::is_other( stat ) );
  BOOST_CHECK( !fs::is_symlink( stat ) );

  fs::path dir(  fs::initial_path<fs::path>() / temp_dir_name );
  
  // Windows only tests
  if ( platform == "Windows" )
  {
    BOOST_CHECK( !fs::exists( fs::path( "//share-not" ) ) );
    BOOST_CHECK( !fs::exists( fs::path( "//share-not/" ) ) );
    BOOST_CHECK( !fs::exists( fs::path( "//share-not/foo" ) ) );
    BOOST_CHECK( !fs::exists( "tools/jam/src/:sys:stat.h" ) ); // !exists() if ERROR_INVALID_NAME
    BOOST_CHECK( !fs::exists( ":sys:stat.h" ) ); // !exists() if ERROR_INVALID_PARAMETER
    BOOST_CHECK( dir.string().size() > 1
      && dir.string()[1] == ':' ); // verify path includes drive

    BOOST_CHECK( fs::system_complete( "" ).empty() );
    BOOST_CHECK( fs::system_complete( "/" ).string()
      == fs::initial_path<fs::path>().root_path().string() );
    BOOST_CHECK( fs::system_complete( "foo" ).string()
      == fs::initial_path<fs::path>().string()+"/foo" );
    BOOST_CHECK( fs::system_complete( "/foo" ).string()
      == fs::initial_path<fs::path>().root_path().string()+"foo" );
    BOOST_CHECK( fs::complete( fs::path( "c:/" ) ).string()
      == "c:/" );
    BOOST_CHECK( fs::complete( fs::path( "c:/foo" ) ).string()
      ==  "c:/foo" );

    BOOST_CHECK( fs::system_complete( fs::path( fs::initial_path<fs::path>().root_name() ) ).string() == fs::initial_path<fs::path>().string() );
    BOOST_CHECK( fs::system_complete( fs::path( fs::initial_path<fs::path>().root_name()
      + "foo" ) ).string() == fs::initial_path<fs::path>().string()+"/foo" );
    BOOST_CHECK( fs::system_complete( fs::path( "c:/" ) ).string()
      == "c:/" );
    BOOST_CHECK( fs::system_complete( fs::path( "c:/foo" ) ).string()
      ==  "c:/foo" );
    BOOST_CHECK( fs::system_complete( fs::path( "//share" ) ).string()
      ==  "//share" );
  } // Windows

  else if ( platform == "POSIX" )
  {
    BOOST_CHECK( fs::system_complete( "" ).empty() );
    BOOST_CHECK( fs::initial_path<fs::path>().root_path().string() == "/" );
    BOOST_CHECK( fs::system_complete( "/" ).string() == "/" );
    BOOST_CHECK( fs::system_complete( "foo" ).string()
      == fs::initial_path<fs::path>().string()+"/foo" );
    BOOST_CHECK( fs::system_complete( "/foo" ).string()
      == fs::initial_path<fs::path>().root_path().string()+"foo" );
  } // POSIX

  fs::remove_all( dir );  // in case residue from prior failed tests
  BOOST_CHECK( !fs::exists( dir ) );

  // the bound functions should throw, so CHECK_EXCEPTION() should return true
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::file_size), ng ), ENOENT ) );

  // test path::exception members
  try { fs::file_size( ng ); } // will throw

  catch ( const fs::filesystem_path_error & ex )
  {
    BOOST_CHECK( ex.path1().string() == " no-way, Jose" );
  }

  BOOST_CHECK( fs::create_directory( dir ) );

  // several functions give unreasonable results if uintmax_t isn't 64-bits
  std::cout << "sizeof(boost::uintmax_t) = " << sizeof(boost::uintmax_t) << '\n';
  BOOST_CHECK( sizeof( boost::uintmax_t ) >= 8 );

  // make some reasonable assuptions for testing purposes
  fs::space_info spi( fs::space( dir ) );
  BOOST_CHECK( spi.capacity > 1000000 );
  BOOST_CHECK( spi.free > 1000 );
  BOOST_CHECK( spi.capacity > spi.free );
  BOOST_CHECK( spi.free >= spi.available );

  // it is convenient to display space, but older VC++ versions choke 
# if !defined(BOOST_MSVC) || _MSC_VER >= 1300  // 1300 == VC++ 7.0
    std::cout << " capacity = " << spi.capacity << '\n';
    std::cout << "     free = " << spi.free << '\n';
    std::cout << "available = " << spi.available << '\n';
# endif

  BOOST_CHECK( fs::exists( dir ) );
  BOOST_CHECK( BOOST_FS_IS_EMPTY( dir ) );
  BOOST_CHECK( fs::is_directory( dir ) );
  BOOST_CHECK( !fs::is_regular( dir ) );
  BOOST_CHECK( !fs::is_other( dir ) );
  BOOST_CHECK( !fs::is_symlink( dir ) );
  stat = fs::status( dir );
  BOOST_CHECK( fs::exists( stat ) );
  BOOST_CHECK( fs::is_directory( stat ) );
  BOOST_CHECK( !fs::is_regular( stat ) );
  BOOST_CHECK( !fs::is_other( stat ) );
  BOOST_CHECK( !fs::is_symlink( stat ) );

  if ( platform == "Windows" )
    BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::file_size), dir ), 
      ENOENT ) );
  else
    BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::file_size), dir ), 0 ) );
  BOOST_CHECK( !fs::create_directory( dir ) );

  BOOST_CHECK( !fs::is_symlink( dir ) );
  BOOST_CHECK( !fs::is_symlink( "nosuchfileordirectory" ) );

  BOOST_CHECK( !fs::symbolic_link_exists( dir ) );
  BOOST_CHECK( !fs::symbolic_link_exists( "nosuchfileordirectory" ) );

  fs::path d1( dir / "d1" );
  BOOST_CHECK( fs::create_directory( d1 ) );
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( fs::is_directory( d1 ) );
  BOOST_CHECK( BOOST_FS_IS_EMPTY( d1 ) );

  boost::function_requires< boost::InputIteratorConcept< fs::directory_iterator > >();

  bool dir_itr_exception(false);
  try { fs::directory_iterator it( "" ); }
  catch ( const fs::filesystem_error & ) { dir_itr_exception = true; }
  BOOST_CHECK( dir_itr_exception );

  dir_itr_exception = false;
  try { fs::directory_iterator it( "nosuchdirectory" ); }
  catch ( const fs::filesystem_error & ) { dir_itr_exception = true; }
  BOOST_CHECK( dir_itr_exception );

  dir_itr_exception = false;
  try
  {
    fs::system_error_type ec(0);
    fs::directory_iterator it( "nosuchdirectory", ec );
    BOOST_CHECK( ec != 0 );
    BOOST_CHECK( ec == fs::detail::not_found_error );
  }
  catch ( const fs::filesystem_error & ) { dir_itr_exception = true; }
  BOOST_CHECK( !dir_itr_exception );
  
  {
    // probe query function overloads
    fs::directory_iterator dir_itr( dir );
    BOOST_CHECK( fs::is_directory( *dir_itr ) );
    BOOST_CHECK( fs::is_directory( dir_itr->status() ) );
    BOOST_CHECK( fs::is_directory( fs::symlink_status(*dir_itr) ) );
    BOOST_CHECK( fs::is_directory( dir_itr->symlink_status() ) );
    BOOST_CHECK( dir_itr->leaf() == "d1" );
  }

  // create a second directory named d2
  fs::path d2( dir / "d2" );
  fs::create_directory(d2 );
  BOOST_CHECK( fs::exists( d2 ) );
  BOOST_CHECK( fs::is_directory( d2 ) );

  // test the basic operation of directory_iterators, and test that
  // stepping one iterator doesn't affect a different iterator.
  {
    fs::directory_iterator dir_itr( dir );
    BOOST_CHECK( fs::exists(dir_itr->status()) );
    BOOST_CHECK( fs::is_directory(dir_itr->status()) );
    BOOST_CHECK( !fs::is_regular(dir_itr->status()) );
    BOOST_CHECK( !fs::is_other(dir_itr->status()) );
    BOOST_CHECK( !fs::is_symlink(dir_itr->status()) );

    fs::directory_iterator dir_itr2( dir );
    BOOST_CHECK( dir_itr->leaf() == "d1"
      || dir_itr->leaf() == "d2" );
    BOOST_CHECK( dir_itr2->leaf() == "d1" || dir_itr2->leaf() == "d2" );
    if ( dir_itr->leaf() == "d1" )
    {
      BOOST_CHECK( (++dir_itr)->leaf() == "d2" );
      BOOST_CHECK( dir_itr2->leaf() == "d1" );
      BOOST_CHECK( (++dir_itr2)->leaf() == "d2" );
    }
    else
    {
      BOOST_CHECK( dir_itr->leaf() == "d2" );
      BOOST_CHECK( (++dir_itr)->leaf() == "d1" );
      BOOST_CHECK( (dir_itr2)->leaf() == "d2" );
      BOOST_CHECK( (++dir_itr2)->leaf() == "d1" );
    }
    BOOST_CHECK( ++dir_itr == fs::directory_iterator() );
    BOOST_CHECK( dir_itr2 != fs::directory_iterator() );
    BOOST_CHECK( ++dir_itr2 == fs::directory_iterator() );
  }

  { // *i++ must work to meet the standard's InputIterator requirements
    fs::directory_iterator dir_itr( dir );
    BOOST_CHECK( dir_itr->leaf() == "d1"
      || dir_itr->leaf() == "d2" );
    if ( dir_itr->leaf() == "d1" )
    {
      BOOST_CHECK( (*dir_itr++).leaf() == "d1" );
      BOOST_CHECK( dir_itr->leaf() == "d2" );
    }
    else
    {
      // Check C++98 input iterator requirements
      BOOST_CHECK( (*dir_itr++).leaf() == "d2" );
      // input iterator requirements in the current WP would require this check:
      // BOOST_CHECK( implicit_cast<std::string const&>(*dir_itr++).leaf() == "d1" );

      BOOST_CHECK( dir_itr->leaf() == "d1" );
    }

    // test case reported in comment to SourceForge bug tracker [937606]
    fs::directory_iterator it( dir );
    const fs::path p1 = *it++;
    BOOST_CHECK( it != fs::directory_iterator() );
    const fs::path p2 = *it++;
    BOOST_CHECK( p1 != p2 );
    BOOST_CHECK( it == fs::directory_iterator() );
  }

  //  Windows has a tricky special case when just the root-name is given,
  //  causing the rest of the path to default to the current directory.
  //  Reported as S/F bug [ 1259176 ]
  if ( platform == "Windows" )
  {
    fs::path root_name_path( fs::current_path<fs::path>().root_name() );
    fs::directory_iterator it( root_name_path );
    BOOST_CHECK( it != fs::directory_iterator() );
    BOOST_CHECK( fs::exists( *it ) );
    BOOST_CHECK( it->path().branch_path() == root_name_path );
    bool found(false);
    do
    {
      if ( it->leaf() == temp_dir_name ) found = true;
    } while ( ++it != fs::directory_iterator() );
    BOOST_CHECK( found );
  }

  // create an empty file named "f0"
  fs::path file_ph( dir / "f0");
  create_file( file_ph, "" );
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::is_directory( file_ph ) );
  BOOST_CHECK( fs::is_regular( file_ph ) );
  BOOST_CHECK( BOOST_FS_IS_EMPTY( file_ph ) );
  BOOST_CHECK( fs::file_size( file_ph ) == 0 );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::create_directory),
    file_ph ), EEXIST ) );
  stat = fs::status( file_ph );
  BOOST_CHECK( fs::status_known( stat ) );
  BOOST_CHECK( fs::exists( stat ) );
  BOOST_CHECK( !fs::is_directory( stat ) );
  BOOST_CHECK( fs::is_regular( stat ) );
  BOOST_CHECK( !fs::is_other( stat ) );
  BOOST_CHECK( !fs::is_symlink( stat ) );

  // create a file named "f1"
  file_ph = dir / "f1";
  create_file( file_ph, "foobar1" );

  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::is_directory( file_ph ) );
  BOOST_CHECK( fs::is_regular( file_ph ) );
  BOOST_CHECK( fs::file_size( file_ph ) == 7 );
  verify_file( file_ph, "foobar1" );

  // equivalence tests
  fs::path ng2("does_not_exist2");
  BOOST_CHECK( CHECK_EXCEPTION(
    bind( BOOST_BND(fs::equivalent), ng, ng2 ), ENOENT ) );
  BOOST_CHECK( fs::equivalent( file_ph, dir / "f1" ) );
  BOOST_CHECK( fs::equivalent( dir, d1 / ".." ) );
  BOOST_CHECK( !fs::equivalent( file_ph, dir ) );
  BOOST_CHECK( !fs::equivalent( dir, file_ph ) );
  BOOST_CHECK( !fs::equivalent( d1, d2 ) );
  BOOST_CHECK( !fs::equivalent( dir, ng ) );
  BOOST_CHECK( !fs::equivalent( ng, dir ) );
  BOOST_CHECK( !fs::equivalent( file_ph, ng ) );
  BOOST_CHECK( !fs::equivalent( ng, file_ph ) );
  
  // hard link tests
  fs::path from_ph( dir / "f3" );
  BOOST_CHECK( !fs::exists( from_ph ) );
  BOOST_CHECK( fs::exists( file_ph ) );
  bool create_hard_link_ok(true);
  try { fs::create_hard_link( file_ph, from_ph ); }
  catch ( const fs::filesystem_error & ex )
  {
    create_hard_link_ok = false;
    std::cout
      << "create_hard_link() attempt failed\n"
      << "filesystem_error.what() reports: " << ex.what() << '\n'
      << "create_hard_link() may not be supported on this file system\n";
  }

  if ( create_hard_link_ok )
  {
    std::cout << "create_hard_link() succeeded\n";
    BOOST_CHECK( fs::exists( from_ph ) );
    BOOST_CHECK( fs::exists( file_ph ) );
    BOOST_CHECK( fs::equivalent( from_ph, file_ph ) );
  }

  fs::system_error_type ec(0);
  BOOST_CHECK( fs::create_hard_link( fs::path("doesnotexist"),
    fs::path("shouldnotwork"), ec ) != 0 );
  BOOST_CHECK( ec != 0 );

  // symbolic link tests
  from_ph = dir / "f4";
  BOOST_CHECK( !fs::exists( from_ph ) );
  BOOST_CHECK( fs::exists( file_ph ) );
  bool create_symlink_ok(true);
  try { fs::create_symlink( file_ph, from_ph ); }
  catch ( const fs::filesystem_error & ex )
  {
    create_symlink_ok = false;
    std::cout
      << "create_symlink() attempt failed\n"
      << "filesystem_error.what() reports: " << ex.what() << '\n'
      << "create_symlink() may not be supported on this file system\n";
  }

  if ( create_symlink_ok )
  {
    std::cout << "create_symlink() succeeded\n";
    BOOST_CHECK( fs::exists( from_ph ) );
    BOOST_CHECK( fs::is_symlink( from_ph ) );
    BOOST_CHECK( fs::exists( file_ph ) );
    BOOST_CHECK( fs::equivalent( from_ph, file_ph ) );
    stat = fs::symlink_status( from_ph );
    BOOST_CHECK( fs::exists( stat ) );
    BOOST_CHECK( !fs::is_directory( stat ) );
    BOOST_CHECK( !fs::is_regular( stat ) );
    BOOST_CHECK( !fs::is_other( stat ) );
    BOOST_CHECK( fs::is_symlink( stat ) );
  }

  ec = 0;
  BOOST_CHECK( fs::create_symlink( "doesnotexist", "", ec ) != 0 );
  BOOST_CHECK( ec != 0 );

  // there was an inital bug in directory_iterator that caused premature
  // close of an OS handle. This block will detect regression.
  {
    fs::directory_iterator di;
    { di = fs::directory_iterator( dir ); }
    BOOST_CHECK( ++di != fs::directory_iterator() );
  }

  // copy_file() tests
  std::cout << "begin copy_file test..." << std::endl;
  fs::copy_file( file_ph, d1 / "f2" );
  std::cout << "copying complete" << std::endl;
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::is_directory( d1 / "f2" ) );
  verify_file( d1 / "f2", "foobar1" );
  std::cout << "copy_file test complete" << std::endl;

  // rename() test case numbers refer to operations.htm#rename table

  // [case 1] make sure can't rename() a non-existent file
  BOOST_CHECK( !fs::exists( d1 / "f99" ) );
  BOOST_CHECK( !fs::exists( d1 / "f98" ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), d1 / "f99", d1 / "f98" ),
    ENOENT ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), fs::path(""), d1 / "f98" ),
    ENOENT ) );

  // [case 2] rename() target.empty()
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), file_ph, "" ),
    ENOENT ) );

  // [case 3] make sure can't rename() to an existent file or directory
  BOOST_CHECK( fs::exists( dir / "f1" ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename),
    dir / "f1", d1 / "f2" ), EEXIST ) );
  // several POSIX implementations (cygwin, openBSD) report ENOENT instead of EEXIST,
  // so we don't verify error type on the above test.
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), dir, d1 ), 0 ) );

  // [case 4A] can't rename() file to a nonexistent parent directory
  BOOST_CHECK( !fs::is_directory( dir / "f1" ) );
  BOOST_CHECK( !fs::exists( dir / "d3/f3" ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), dir / "f1", dir / "d3/f3" ),
    ENOENT ) );

  // [case 4B] rename() file in same directory
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::exists( d1 / "f50" ) );
  fs::rename( d1 / "f2", d1 / "f50" );
  BOOST_CHECK( !fs::exists( d1 / "f2" ) );
  BOOST_CHECK( fs::exists( d1 / "f50" ) );
  fs::rename( d1 / "f50", d1 / "f2" );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::exists( d1 / "f50" ) );

  // [case 4C] rename() file d1/f2 to d2/f3
  fs::rename( d1 / "f2", d2 / "f3" );
  BOOST_CHECK( !fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::exists( d2 / "f2" ) );
  BOOST_CHECK( fs::exists( d2 / "f3" ) );
  BOOST_CHECK( !fs::is_directory( d2 / "f3" ) );
  verify_file( d2 / "f3", "foobar1" );
  fs::rename( d2 / "f3", d1 / "f2" );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );

  // [case 5A] rename() directory to nonexistent parent directory
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( !fs::exists( dir / "d3/d5" ) );
  BOOST_CHECK( !fs::exists( dir / "d3" ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::rename), d1, dir / "d3/d5" ),
    ENOENT ) );

  // [case 5B] rename() on directory
  fs::path d3( dir / "d3" );
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::exists( d3 ) );
  fs::rename( d1, d3 );
  BOOST_CHECK( !fs::exists( d1 ) );
  BOOST_CHECK( fs::exists( d3 ) );
  BOOST_CHECK( fs::is_directory( d3 ) );
  BOOST_CHECK( !fs::exists( d1 / "f2" ) );
  BOOST_CHECK( fs::exists( d3 / "f2" ) );
  fs::rename( d3, d1 );
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  BOOST_CHECK( !fs::exists( d3 ) );

  // [case 5C] rename() rename and move d1 to d2 / "d20"
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( !fs::exists( d2 / "d20" ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );
  fs::rename( d1, d2 / "d20" );
  BOOST_CHECK( !fs::exists( d1 ) );
  BOOST_CHECK( fs::exists( d2 / "d20" ) );
  BOOST_CHECK( fs::exists( d2 / "d20" / "f2" ) );
  fs::rename( d2 / "d20", d1 );
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( !fs::exists( d2 / "d20" ) );
  BOOST_CHECK( fs::exists( d1 / "f2" ) );

  // remove() tests on file
  file_ph = dir / "shortlife";
  BOOST_CHECK( !fs::exists( file_ph ) );
  create_file( file_ph, "" );
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::is_directory( file_ph ) );
  BOOST_CHECK( fs::remove( file_ph ) );
  BOOST_CHECK( !fs::exists( file_ph ) );
  BOOST_CHECK( !fs::remove( "no-such-file" ) );
  BOOST_CHECK( !fs::remove( "no-such-directory/no-such-file" ) );

  // remove() test on directory
  d1 = dir / "shortlife_dir";
  BOOST_CHECK( !fs::exists( d1 ) );
  fs::create_directory( d1 );
  BOOST_CHECK( fs::exists( d1 ) );
  BOOST_CHECK( fs::is_directory( d1 ) );
  BOOST_CHECK( BOOST_FS_IS_EMPTY( d1 ) );
  BOOST_CHECK( CHECK_EXCEPTION( bind( BOOST_BND(fs::remove), dir ), ENOTEMPTY ) );
  BOOST_CHECK( fs::remove( d1 ) );
  BOOST_CHECK( !fs::exists( d1 ) );

// STLport is allergic to std::system, so don't use runtime platform test
# ifdef BOOST_POSIX

  // remove() test on dangling symbolic link
  fs::path link( "dangling_link" );
  fs::remove( link );
  BOOST_CHECK( !fs::is_symlink( link ) );
  BOOST_CHECK( !fs::exists( link ) );
  std::system("ln -s nowhere dangling_link");
  BOOST_CHECK( !fs::exists( link ) );
  BOOST_CHECK( fs::is_symlink( link ) );
  BOOST_CHECK( fs::remove( link ) );
  BOOST_CHECK( !fs::is_symlink( link ) );

  // remove() test on symbolic link to a file
  file_ph = "link_target";
  fs::remove( file_ph );
  BOOST_CHECK( !fs::exists( file_ph ) );
  create_file( file_ph, "" );
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::is_directory( file_ph ) );
  BOOST_CHECK( fs::is_regular( file_ph ) );
  std::system("ln -s link_target non_dangling_link");
  link = "non_dangling_link";
  BOOST_CHECK( fs::exists( link ) );
  BOOST_CHECK( !fs::is_directory( link ) );
  BOOST_CHECK( fs::is_regular( link ) );
  BOOST_CHECK( fs::is_symlink( link ) );
  BOOST_CHECK( fs::remove( link ) );
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::exists( link ) );
  BOOST_CHECK( !fs::is_symlink( link ) );
  BOOST_CHECK( fs::remove( file_ph ) );
  BOOST_CHECK( !fs::exists( file_ph ) );
# endif

  // write time tests

  file_ph = dir / "foobar2";
  create_file( file_ph, "foobar2" );
  BOOST_CHECK( fs::exists( file_ph ) );
  BOOST_CHECK( !fs::is_directory( file_ph ) );
  BOOST_CHECK( fs::is_regular( file_ph ) );
  BOOST_CHECK( fs::file_size( file_ph ) == 7 );
  verify_file( file_ph, "foobar2" );

  // Some file system report last write time as local (FAT), while
  // others (NTFS) report it as UTC. The C standard does not specify
  // if time_t is local or UTC. 

  std::time_t ft = fs::last_write_time( file_ph );
  std::cout << "\nUTC last_write_time() for a file just created is "
    << std::asctime(std::gmtime(&ft)) << std::endl;

  std::tm * tmp = std::localtime( &ft );
  std::cout << "\nYear is " << tmp->tm_year << std::endl;
  --tmp->tm_year;
  std::cout << "Change year to " << tmp->tm_year << std::endl;
  fs::last_write_time( file_ph, std::mktime( tmp ) );
  std::time_t ft2 = fs::last_write_time( file_ph );
  std::cout << "last_write_time() for the file is now "
    << std::asctime(std::gmtime(&ft2)) << std::endl;
  BOOST_CHECK( ft != fs::last_write_time( file_ph ) );


  std::cout << "\nReset to current time" << std::endl;
  fs::last_write_time( file_ph, ft );
  double time_diff = std::difftime( ft, fs::last_write_time( file_ph ) );
  std::cout 
    << "original last_write_time() - current last_write_time() is "
    << time_diff << " seconds" << std::endl;
  BOOST_CHECK( time_diff >= -60.0 && time_diff <= 60.0 );

  // post-test cleanup
  BOOST_CHECK( fs::remove_all( dir ) != 0 );
  // above was added just to simplify testing, but it ended up detecting
  // a bug (failure to close an internal search handle). 
  BOOST_CHECK( !fs::exists( dir ) );
  BOOST_CHECK( fs::remove_all( dir ) == 0 );
  return 0;
} // main

