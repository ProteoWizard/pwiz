/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#include <stdio.h>
#include "GTime.h"
#include "GString.h"
#include <time.h>
#ifdef WINDOWS
#	include <windows.h>
#else
#	include <sys/time.h>
#endif
#include <sstream>

namespace GClasses {

/*static*/ double GTime::seconds()
{
#ifdef WINDOWS
	return (double)GetTickCount() * 1e-3;
#else
	struct timeval tp;
	gettimeofday(&tp, NULL);
	return ((double)tp.tv_sec + (double)tp.tv_usec * 1e-6);
#endif
}

/*static*/ const char* GTime::asciiTime(char* szBuf, int nSize, bool bGreenwichMeanTime)
{
	time_t t = time((time_t*)0);
#ifdef WINDOWS
	struct tm thetime;
	if(bGreenwichMeanTime)
		gmtime_s(&thetime, &t);
	else
		localtime_s(&thetime, &t);
	asctime_s(szBuf, nSize, &thetime);
	int len = (int)strlen(szBuf);
#else
	struct tm* pTime = bGreenwichMeanTime ? gmtime(&t) : localtime(&t);
	int len = safe_strcpy(szBuf, asctime(pTime), nSize);
#endif

	// Remove trailing whitespace (cuz asctime appends a '\n' at the end)
	while(len > 0 && szBuf[len - 1] <= ' ')
		szBuf[--len] = '\0';
	return szBuf;
}

/*static*/ void GTime::appendTimeStampValue(std::string* pS, bool bGreenwichMeanTime)
{
	time_t t = time((time_t*)0);
#ifdef WINDOWS
	struct tm thetime;
	if(bGreenwichMeanTime)
		gmtime_s(&thetime, &t);
	else
		localtime_s(&thetime, &t);
	struct tm* pTime = &thetime;
#else
	struct tm* pTime = bGreenwichMeanTime ? gmtime(&t) : localtime(&t);
#endif
	std::ostringstream os;
	os << pTime->tm_year + 1900;
	os.width(2);
	os.fill('0');
	os << pTime->tm_mon + 1;
	os << pTime->tm_mday;
	os << pTime->tm_hour;
	os << pTime->tm_min;
	os << pTime->tm_sec;
	(*pS).append(os.str());
}

/*
#include <tcl.h>

class GStopWatch
{
protected:
	struct Tcl_Time m_begin;
	struct Tcl_Time m_end;

public:
	void start()
	{
		Tcl_GetTime(&m_begin);
	}

	double stop()
	{
		Tcl_GetTime(&m_end);
		double secs = m_end.secs - m_begin.secs;
		double usecs = m_end.usecs - m_begin.usecs;
		return secs + 1e-6 * usecs;
	}
};
*/

} // namespace GClasses

