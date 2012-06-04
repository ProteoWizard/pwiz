/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifndef __GSPINLOCK_H__
#define __GSPINLOCK_H__

#ifndef WINDOWS
#	include <pthread.h>
#endif

namespace GClasses {

/// A spin-lock for synchronization purposes
class GSpinLock
{
protected:
#ifdef _DEBUG
	const char* m_szWhoHoldsTheLock;
#endif
	volatile long m_dwLocked; /// maintaned on all platform as posix mutexes don't have a way to get current state.
                                 /// when not Win32 be aware that this value is shadowing the real mutex, and cannot be
                                 /// depended on especially in a MP enviroment.
#ifndef WINDOWS
	pthread_mutex_t m_mutex;
#endif

public:
	GSpinLock();
	virtual ~GSpinLock();

#ifndef NO_TEST_CODE
	/// Performs unit tests for this class. Throws an exception if there is a failure.
	static void test();
#endif // !NO_TEST_CODE

	void lock(const char* szWhoHoldsTheLock);
	void unlock();
	bool isLocked() { return m_dwLocked != 0; } /// see note above about m_dwLocked.
};


class GSpinLockHolder
{
protected:
	GSpinLock* m_pLock;

public:
	GSpinLockHolder(GSpinLock* pLock, const char* szWhoHoldsTheLock)
	{
		m_pLock = pLock;
		pLock->lock(szWhoHoldsTheLock);
	}

	~GSpinLockHolder()
	{
		m_pLock->unlock();
	}
};

} // namespace GClasses

#endif // __GSPINLOCK_H__
