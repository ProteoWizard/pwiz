//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _STDAFX_H
#define _STDAFX_H

#ifdef _WIN32
#ifndef WIN32
#define WIN32
#endif
#endif

#ifdef _WIN64
#ifndef WIN64
#define WIN64
#endif
#endif

/* WINDOWS ONLY INCLUDES */
#ifdef WIN32
    // use std min/max instead of Win32 macros
    #define NOMINMAX

    #define _WIN32_WINNT    0x0400
    #ifndef _WINDOWS_
        #if defined(_AFXDLL) || defined(_ATL_STATIC_REGISTRY)
        #include <afx.h>
        #include <afxwin.h>         // MFC core and standard components
        #include <afxext.h>         // MFC extensions
        #ifndef _AFX_NO_OLE_SUPPORT
        #include <afxdtctl.h>        // MFC support for Internet Explorer 4 Common Controls
        #endif
        #ifndef _AFX_NO_AFXCMN_SUPPORT
        #include <afxcmn.h>            // MFC support for Windows Common Controls
        #endif

        #if defined(_ATL_DLL) || defined(_ATL_STATIC_REGISTRY)
        #define _ATL_CSTRING_EXPLICIT_CONSTRUCTORS    // some CString constructors will be explicit

        #include <atlbase.h>
        #include <atlstr.h>
        #endif

        #else // No AFX
        #include <windows.h>
        #pragma comment( lib, "ws2_32.lib" )
        #endif // AFXDLL or ATL_STATIC_REGISTRY
    #endif // _WINDOWS_

    #include <direct.h>
    #pragma warning( disable : 4996 4267 )

    #define SYS_PATH_DELIMITER "\\/"        // Windows pretty much works with both.
    #define SYS_PATH_SEPARATOR "\\"

/* POSIX ONLY INCLUDES */
#else

    #include <sys/types.h>
    #include <sys/stat.h>
    #include <sys/time.h>
#ifndef __CYGWIN__
#include <sys/sysinfo.h>
#endif
    #include <sys/wait.h>
    #include <glob.h>
    #include <dirent.h>
    #include <unistd.h>
    #include <errno.h>
    #ifndef MAX_PATH
        #define MAX_PATH 255
    #endif

    #define SYS_PATH_DELIMITER "/"
    #define SYS_PATH_SEPARATOR "/"

#endif

#include <iostream>
#include <iomanip>
#include <exception>
#include <stdexcept>
#include <typeinfo>
#include <fstream>
#include <string>
#include <sstream>
//#include <map>
//#include <vector>
//#include <list>
//#include <set>
#include <utility>
#include <deque>
#include <numeric>
#include <algorithm>
#include <cmath>
#include <ctime>
#include <bitset>

//#include <boost/lexical_cast.hpp>
#include "pwiz/utility/misc/optimized_lexical_cast.hpp"
#include <boost/random.hpp>
#include <boost/tokenizer.hpp>
#include <boost/archive/text_iarchive.hpp>
#include <boost/archive/text_oarchive.hpp>
#include <boost/archive/binary_iarchive.hpp>
#include <boost/archive/binary_oarchive.hpp>
#include <boost/archive/xml_iarchive.hpp>
#include <boost/archive/xml_oarchive.hpp>
#include <boost/serialization/utility.hpp>
#include <boost/serialization/base_object.hpp>
#include <boost/serialization/level.hpp>
#include <boost/serialization/tracking.hpp>
#include <boost/serialization/vector.hpp>
#include <boost/serialization/list.hpp>
#include <boost/serialization/map.hpp>
#include <boost/serialization/set.hpp>
#include <boost/serialization/string.hpp>
#include <boost/serialization/shared_ptr.hpp>
#include <boost/serialization/version.hpp>
#include <boost/serialization/split_member.hpp>
#include <boost/archive/iterators/base64_from_binary.hpp>
#include <boost/archive/iterators/binary_from_base64.hpp>
#include <boost/archive/iterators/transform_width.hpp>
#include <boost/preprocessor/library.hpp>
#include <boost/array.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/filesystem/operations.hpp>
#include <boost/filesystem/convenience.hpp>
#include <boost/foreach.hpp>
#include <boost/range.hpp>
#include <boost/format.hpp>
#include <boost/logic/tribool.hpp>


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/data/common/cv.hpp"
#include "pwiz/data/proteome/Peptide.hpp"
#include "pwiz/data/proteome/Digestion.hpp"
#include "pwiz/data/proteome/AminoAcid.hpp"
#include "pwiz/utility/chemistry/Ion.hpp"
#include "pwiz/utility/chemistry/MZTolerance.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/utility/math/erf.hpp"
#include "pwiz/utility/math/round.hpp"

using namespace pwiz::cv;
using namespace pwiz::proteome;
using namespace pwiz::chemistry;
using namespace pwiz::util;
using namespace pwiz::math;

using std::ios;
using std::iostream;
using std::ostream;
using std::istream;

using std::endl;
using std::flush;

using std::string;
using std::stringstream;
using std::ostringstream;
using std::streamsize;

using std::map;
using std::multimap;
using std::vector;
using std::deque;
using std::set;
using std::multiset;
using std::bitset;
using std::list;
using std::pair;
using std::make_pair;
using boost::sub_range;
using boost::array;

using std::remove;
using std::remove_if;
using std::copy;
using std::accumulate;

typedef std::ostream_iterator<char> ostream_inserter;
typedef std::istream_iterator<char> istream_inserter;
using std::back_inserter;
using std::front_inserter;
using std::advance;

using std::internal;
using std::right;
using std::left;
using std::boolalpha;
using std::noboolalpha;
using std::showpoint;
using std::noshowpoint;
using std::showpos;
using std::noshowpos;
using std::fixed;
using std::dec;
using std::scientific;
using std::min;
using std::max;

using std::exception;
using std::invalid_argument;
using std::out_of_range;
using std::overflow_error;
using std::runtime_error;

using boost::lexical_cast;
using boost::bad_lexical_cast;
typedef boost::tokenizer< boost::char_separator<char> > stokenizer;

namespace bal = boost::algorithm;
namespace bfs = boost::filesystem;

using boost::logic::tribool;
using boost::algorithm::to_lower;
using boost::algorithm::to_upper;
using boost::algorithm::to_lower_copy;
using boost::algorithm::to_upper_copy;
using boost::algorithm::replace_all;
using boost::algorithm::replace_all_copy;
using boost::algorithm::trim;
using boost::algorithm::trim_copy;
using boost::algorithm::trim_left;
using boost::algorithm::trim_left_copy;
using boost::algorithm::trim_right;
using boost::algorithm::trim_right_copy;
using boost::algorithm::split;
using boost::algorithm::join;
using boost::format;
using boost::is_any_of;

using boost::filesystem::path;
using boost::filesystem::file_size;
using boost::filesystem::last_write_time;
using boost::filesystem::exists;
using boost::filesystem::current_path;
using boost::filesystem::change_extension;
using boost::filesystem::basename;
using boost::filesystem::extension;

#ifdef WIN32
#define MAKE_PATH_FOR_BOOST(str) (boost::filesystem::path((str), boost::filesystem::native))
#else
#define MAKE_PATH_FOR_BOOST(str) (boost::filesystem::path((str), boost::filesystem::native))
#endif

using boost::archive::text_iarchive;
using boost::archive::text_oarchive;
using boost::archive::binary_iarchive;
using boost::archive::binary_oarchive;
using boost::archive::xml_iarchive;
using boost::archive::xml_oarchive;
//using boost::archive::iterators::transform_width;
//using boost::archive::iterators::base64_from_binary;
//using boost::archive::iterators::binary_from_base64;

using boost::shared_ptr;

#define SetInsertPair(type) pair<type::iterator, bool>
#define TemplateSetInsertPair(type) pair<typename type::iterator, bool>
#define TemplateSetEqualPair(type) pair<typename type::iterator, typename type::iterator>

#include "topset.h"
#include "CharIndexedVector.h"
#include "constants.h"

/**
    comparePWIZPeptides compares two peptide objects. The sequences are compared
    initially and in case of ties the modifcations are compared.
*/
inline bool comparePWIZPeptides(const Peptide& lhs, const Peptide& rhs) 
{
    return (lhs.sequence().length() == rhs.sequence().length() && lhs.sequence() == rhs.sequence() && lhs.modifications() == rhs.modifications());
}

inline bool isIsobaric(const Peptide& lhs, const Peptide& rhs, double massTolerance)
{
        if(lhs.sequence().length()==rhs.sequence().length() && std::abs(lhs.monoisotopicMass(0,false)-rhs.monoisotopicMass(0,false)) < massTolerance) {
                string lhsStr = string(lhs.sequence());
                string rhsStr = string(rhs.sequence());
                replace_all(lhsStr, "L", "I");
                replace_all(lhsStr, "Q", "K");
                replace_all(rhsStr, "L", "I");
                replace_all(rhsStr, "Q", "K");
                if(lhsStr.compare(rhsStr)==0) {
                        return true;
                }
        }
        return false;
}

namespace boost
{
    template<typename Pair, typename Iter>
    iterator_range<Iter> make_iterator_range(const Pair& p)
    {
        return make_iterator_range(p.first, p.second);
    }
}

namespace std
{
#ifndef _INCLUDED_COMMON_H_
    template< class KeyT, class ValueT, class PredT >
    vector< KeyT > keys( const map< KeyT, ValueT, PredT >& m )
    {
        vector< KeyT > keys;
        keys.reserve( m.size() );
        for( typename map< KeyT, ValueT, PredT >::const_iterator itr = m.begin(); itr != m.end(); ++itr )
            keys.push_back( itr->first );
        return keys;
    }

    template< class KeyT, class ValueT, class PredT >
    vector< ValueT > values( const map< KeyT, ValueT, PredT >& m )
    {
        vector< ValueT > values;
        values.reserve( m.size() );
        for( typename map< KeyT, ValueT, PredT >::const_iterator itr = m.begin(); itr != m.end(); ++itr )
            values.push_back( itr->second );
        return values;
    }

    template< class ValueT >
    typename map< string, ValueT >::const_iterator find_first_of( const map< string, ValueT >& m, const string& keyList, const string& keyListDelimiters = " " )
    {
        vector< string > keys;
        split( keys, keyList, boost::is_any_of( keyListDelimiters ) );
        typename map< string, ValueT >::const_iterator itr = m.end();
        for( size_t i=0; i < keys.size() && itr == m.end(); ++i )
            itr = m.find( keys[i] );
        return itr;
    }

    template< class ValueT >
    typename map< string, ValueT >::iterator find_first_of( map< string, ValueT >& m, const string& keyList, const string& keyListDelimiters = " " )
    {
        vector< string > keys;
        split( keys, keyList, boost::is_any_of( keyListDelimiters ) );
        typename map< string, ValueT >::iterator itr = m.end();
        for( size_t i=0; i < keys.size() && itr == m.end(); ++i )
            itr = m.find( keys[i] );
        return itr;
    }

    template< class KeyT, class ValueT, class PredT >
    typename map< KeyT, ValueT, PredT >::const_iterator find_first_of( const map< KeyT, ValueT, PredT >& m, const string& keyList, const string& keyListDelimiters = " " )
    {
        vector< string > keyStrings;
        split( keyStrings, keyList, boost::is_any_of( keyListDelimiters ) );
        vector< KeyT > keys;
        std::transform( keyStrings.begin(), keyStrings.end(), keys.begin(), lexical_cast< KeyT, string >() );
        typename map< KeyT, ValueT, PredT >::const_iterator itr = m.end();
        for( size_t i=0; i < keys.size() && itr == m.end(); ++i )
            itr = m.find( keys[i] );
        return itr;
    }

    template< class KeyT, class ValueT, class PredT >
    typename map< KeyT, ValueT, PredT >::iterator find_first_of( map< KeyT, ValueT, PredT >& m, const string& keyList, const string& keyListDelimiters = " " )
    {
        vector< string > keyStrings;
        split( keyStrings, keyList, boost::is_any_of( keyListDelimiters ) );
        vector< KeyT > keys;
        std::transform( keyStrings.begin(), keyStrings.end(), keys.begin(), lexical_cast< KeyT, string >() );
        typename map< KeyT, ValueT, PredT >::iterator itr = m.end();
        for( size_t i=0; i < keys.size() && itr == m.end(); ++i )
            itr = m.find( keys[i] );
        return itr;
    }

    template< class ValueT, class PredT >
    typename map< int, ValueT, PredT >::const_iterator find_nearest( const map< int, ValueT, PredT >& m, int query, int tolerance )
    {
        typename map< int, ValueT, PredT >::const_iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || abs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        int minDiff = abs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            int curDiff = abs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

    template< class ValueT, class PredT >
    typename map< int, ValueT, PredT >::iterator find_nearest( map< int, ValueT, PredT >& m, int query, int tolerance )
    {
        typename map< int, ValueT, PredT >::iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || abs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        int minDiff = abs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            int curDiff = abs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

    template< class ValueT, class PredT >
    typename map< float, ValueT, PredT >::const_iterator find_nearest( const map< float, ValueT, PredT >& m, float query, float tolerance )
    {
        typename map< float, ValueT, PredT >::const_iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || fabs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        float minDiff = fabs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            float curDiff = fabs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

    template< class ValueT, class PredT >
    typename map< float, ValueT, PredT >::iterator find_nearest( map< float, ValueT, PredT >& m, float query, float tolerance )
    {
        typename map< float, ValueT, PredT >::iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || fabs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        float minDiff = fabs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            float curDiff = fabs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

    template< class ValueT, class PredT >
    typename map< double, ValueT, PredT >::const_iterator find_nearest( const map< double, ValueT, PredT >& m, double query, double tolerance )
    {
        typename map< double, ValueT, PredT >::const_iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || fabs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        double minDiff = fabs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            double curDiff = fabs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

    template< class ValueT, class PredT >
    typename map< double, ValueT, PredT >::iterator find_nearest( map< double, ValueT, PredT >& m, double query, double tolerance )
    {
        typename map< double, ValueT, PredT >::iterator cur, min, max, best;

        min = m.lower_bound( query - tolerance );
        max = m.lower_bound( query + tolerance );

        if( min == m.end() || fabs( query - min->first ) > tolerance )
            return m.end();
        else if( min == max )
            return min;
        else
            best = min;

        double minDiff = fabs( query - best->first );
        for( cur = min; cur != max; ++cur )
        {
            double curDiff = fabs( query - cur->first );
            if( curDiff < minDiff )
            {
                minDiff = curDiff;
                best = cur;
            }
        }
        return best;
    }

#endif

    template< class T >
    ostream&        operator<< ( ostream& o, const ::freicore::CharIndexedVector< T >& v )
    {
        o << "(";
        for( size_t i=0; i < 129; ++i )
            o << " " << v[i];
        o << " )";

        return o;
    }

    template< class T >
    ostream&        operator<< ( ostream& o, const typename T::iterator& itr )
    {
        return o << "itr";//*itr;
    }

    template< class BI >
    void advance_to_bound( BI& itr, const BI& bound, int off )
    {
        for( ; itr != bound && 0 < off; --off )
            ++itr;
        for( ; itr != bound && off < 0; ++off )
            --itr;
    }
}


namespace freicore
{
    template< class OutputType, class InputType >
    OutputType arithmetic_mean( const vector< InputType >& v )
    {
        InputType sum = accumulate( v.begin(), v.end(), InputType() );
        return static_cast< OutputType >( sum / static_cast< InputType >( v.size() ) );
    }

    template< class T >
    void deallocate( T& container )
    {
        container.clear();
        T tmp;
        std::swap( tmp, container );
    }

    enum HostEndianType { COMMON_LITTLE_ENDIAN, COMMON_BIG_ENDIAN, COMMON_UNKNOWN_ENDIAN };
    HostEndianType  GetHostEndianType();

    string            GetFileType( const string& filepath );
    bool            TestFileType( const string& filepath, const string& type, bool printErrorMsg = true );
    string            GetFilenameFromFilepath( const string& filepath );
    string            GetPathnameFromFilepath( const string& filepath );
    string            GetTopLevelOfFilepath( const string& filepath );
    string            GetFilenameWithoutExtension( const string& filename );
    string            GetFilenameExtension( const string& filename );
    string            ChangeFilenameExtension( const string& filename, const string& extension );
    long long        GetFileSize( const string& filename );
    string            GetFileLastModified( const string& filename );

    float            round( float f, int precision );
    double            round( double f, int precision );
  int             round( float f );
    void            GetHostname( char* buf, int len );
    string            GetHostname();
    long long        GetAvailablePhysicalMemory();
    int                GetNumProcessors();
    string            GetDateString();
    string            GetTimeString();
    string            GetDateTime( bool useLocal = true );
    string            GetDateTime( const time_t& now, bool useLocal = true );
    string            MakeProcessFilename( string str, int processID = 0 );
    string            TrimWhitespace( const string& str );
    string            QuoteString( const string& str );
    string            UnquoteString( const string& str );

    #define TRY_ARCHIVE( a, var ) \
        try \
        { \
            a & var; \
        } catch( exception& e ) \
        { \
            cerr << "Error archiving: " << BOOST_PP_STRINGIZE(var) << " (" << var << ")" << endl; \
            throw e; \
        }
}

#endif
