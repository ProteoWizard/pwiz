/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GTHREAD_H__
#define __GTHREAD_H__

#include "GError.h"
#ifndef WINDOWS
#	include <unistd.h>
#	include <sched.h>
#endif

namespace GClasses {

#ifdef WINDOWS
#	define BAD_HANDLE (void*)1
	typedef void* THREAD_HANDLE;
#else
#	ifdef DARWIN
#		define BAD_HANDLE (_opaque_pthread_t*)1
		typedef _opaque_pthread_t* THREAD_HANDLE;
#	else
#   ifdef __FreeBSD__
#		  define BAD_HANDLE (pthread_t)-1
		  typedef pthread_t THREAD_HANDLE;
#   else
		  typedef unsigned long int THREAD_HANDLE;
#		  define BAD_HANDLE (unsigned long int)-2
#   endif
#	endif
#endif

/// A wrapper for PThreads on Linux and for some corresponding WIN32 api on Windows
class GThread
{
public:
	static THREAD_HANDLE spawnThread(unsigned int (*pFunc)(void*), void* pData);

	/// it may be an error to sleep more than 976ms (1,000,000 / 1024) on Unix
	static void sleep(unsigned int nMiliseconds);
};

} // namespace GClasses

#endif // __GTHREAD_H__
