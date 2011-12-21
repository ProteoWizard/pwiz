/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include "GError.h"
#include <stdarg.h>
#include <wchar.h>
#include <exception>
#ifdef WINDOWS
#else
#	include <unistd.h>
#endif
#include <signal.h>
#include <sys/stat.h>
#include <string.h>
#include <string>
#include <stdlib.h>
#include <iostream>
#include <sstream>
#include "GString.h"

using std::exception;
using std::string;
using std::cerr;

namespace GClasses {

string g_errorMessage;
class GException : public exception
{
public:
	virtual const char* what() const throw()
	{ return g_errorMessage.c_str(); }
};
GException g_exception;

void ThrowError(string s)
{
	g_errorMessage = s;

	// Behold! The central location from which all exceptions in this library are thrown!
	// (This might be a good place to put a breakpoint.)
	throw g_exception;
}


void ThrowError(string s1, string s2)
{
	string s = s1;
	s += s2;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3)
{
	string s = s1;
	s += s2;
	s += s3;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5, string s6)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	s += s6;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5, string s6, string s7)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	s += s6;
	s += s7;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5, string s6, string s7, string s8)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	s += s6;
	s += s7;
	s += s8;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5, string s6, string s7, string s8, string s9)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	s += s6;
	s += s7;
	s += s8;
	s += s9;
	ThrowError(s);
}

void ThrowError(string s1, string s2, string s3, string s4, string s5, string s6, string s7, string s8, string s9, string s10)
{
	string s = s1;
	s += s2;
	s += s3;
	s += s4;
	s += s5;
	s += s6;
	s += s7;
	s += s8;
	s += s9;
	s += s10;
	ThrowError(s);
}


void TestEqual(char const*expected, char const*got, std::string desc){
  TestEqual(std::string(expected), std::string(got), desc);
}

void TestEqual(char const* expected, char* got, std::string desc){
  TestEqual(std::string(expected), std::string(got), desc);
}

void TestEqual(char* expected, char* got, std::string desc){
  TestEqual(std::string(expected), std::string(got), desc);
}



#ifdef WINDOWS
void GAssertFailed()
{
	cerr << "Debug Assert Failed!\n";
	cerr.flush();
	__debugbreak();
}
#else
void GAssertFailed()
{
	cerr << "Debug Assert Failed!\n";
	cerr.flush();
	raise(SIGINT);
}

int _stricmp(const char* szA, const char* szB)
{
	while(*szA)
	{
		if((*szA | 32) < (*szB | 32))
			return -1;
		if((*szA | 32) > (*szB | 32))
			return 1;
		szA++;
		szB++;
	}
	if(*szB)
		return -1;
	return 0;
}

int _strnicmp(const char* szA, const char* szB, int len)
{
	int n;
	for(n = 0; n < len; n++)
	{
		if((*szA | 32) < (*szB | 32))
			return -1;
		if((*szA | 32) > (*szB | 32))
			return 1;
		szA++;
		szB++;
	}
	return 0;
}

long filelength(int filedes)
{
	struct stat s;
	if(fstat(filedes, &s) == -1)
		return 0;
	return s.st_size;
}
#endif

} // namespace GClasses

