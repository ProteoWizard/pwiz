/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GERROR_H__
#define __GERROR_H__


#ifdef WINDOWS
#	include <BaseTsd.h>
#endif
#include <string>
#include <sstream>
#include <iostream>

#ifdef WINDOWS
// Throw out the min and max macros supplied by Microsoft that collide with std::min and std::max
#	define NOMINMAX
#	undef min
#	undef max
#endif

namespace GClasses {


#define INVALID_INDEX ((size_t)-1)



void GAssertFailed();
#ifdef _DEBUG
#define GAssert(x)\
	{\
		if(!(x))\
			GAssertFailed();\
	}
#else // _DEBUG
#define GAssert(x)	((void)0)
#endif // else _DEBUG






// Convert another type to a string
template<typename T>
std::string to_str(const T& n)
{
	std::ostringstream os;
	os.precision(14);
	os << n;
	return os.str();
}

void ThrowError(std::string s1);
void ThrowError(std::string s1, std::string s2);
void ThrowError(std::string s1, std::string s2, std::string s3);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5, std::string s6);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5, std::string s6, std::string s7);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5, std::string s6, std::string s7, std::string s8);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5, std::string s6, std::string s7, std::string s8, std::string s9);
void ThrowError(std::string s1, std::string s2, std::string s3, std::string s4, std::string s5, std::string s6, std::string s7, std::string s8, std::string s9, std::string s10);



#define COMPILER_ASSERT(expr)  enum { CompilerAssertAtLine##__LINE__ = sizeof( char[(expr) ? +1 : -1] ) }


// ----------------------------
// Platform Compatability Stuff
// ----------------------------

#ifdef WINDOWS
typedef UINT_PTR uintptr_t;
// typedef INT_PTR ptrdiff_t;
#else
int _stricmp(const char* szA, const char* szB);
int _strnicmp(const char* szA, const char* szB, int len);
long filelength(int filedes);
#endif

///\brief Verify that \a expected and \a got are equal for test code. Unlike Assert, this check does not disappear in optimized builds.
///
///If expected==got then does nothing.  Otherwise prints to stderr:
///
///<pre>
///Test for equality failed: ---------test_descr goes here ---------------
///
///Expected: ------------expected goes here---------------
///Got     : ------------got goes here     ---------------
///</pre>
///
///Then it throws an exception using ThrowError
///
///Calls operator==(const T1&,const T2&) to determine equality.
///
///Calls GClasses::to_str to form the string representation of \a expected
///and \a got
///

///\param expected The value expected from specifications
///
///\param got      The value actually produced by the code
///
///\param test_descr A short test description to allow a human to
///                  easily find the failing test in the code and
///                  understand why it was written and have some help
///                  in diagnosing the bug.
template<class T1, class T2>
void TestEqual(const T1& expected, const T2& got, std::string test_descr){
	using std::endl;
	if(!(expected == got)){
		std::cerr
			<< endl
			<< "Test for equality failed: " << test_descr << endl
			<< endl
			<< "Expected: " << GClasses::to_str(expected) << endl
			<< "Got     : " << GClasses::to_str(got) << endl
			;
		ThrowError("Test for equality failed: ", test_descr);
	}
}

///"Specialization" of TestEqual for c-strings done using overloading
void TestEqual(char const* expected, char const* got, std::string desc);

///"Specialization" of TestEqual for c-strings done using overloading
void TestEqual(char const* expected, char* got, std::string desc);

///"Specialization" of TestEqual for c-strings done using overloading
void TestEqual(char* expected, char* got, std::string desc);

} // namespace GClasses

#endif // __GERROR_H__
