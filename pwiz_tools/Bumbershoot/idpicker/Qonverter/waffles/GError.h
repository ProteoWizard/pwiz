/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GMACROS_H__
#define __GMACROS_H__


#ifdef WINDOWS
#	include <BaseTsd.h>
#endif
#include <string>
#include <sstream>

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



} // namespace GClasses

#endif // __GMACROS_H__
