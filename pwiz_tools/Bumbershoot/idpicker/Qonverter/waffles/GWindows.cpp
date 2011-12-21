/*
	Copyright (C) 2006, Mike Gashler

	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.

	see http://www.gnu.org/copyleft/lesser.html
*/

#ifdef WINDOWS

#include "GWindows.h"
#include "GString.h"
#include "GError.h"
#include <windows.h>
#include <direct.h>
#include <stdio.h>
#include <errno.h>
#include <io.h>
#include <time.h>
#include <process.h>

namespace GClasses {

void GWindows::yield()
{
	 MSG   aMsg;

	 while(PeekMessage(&aMsg, NULL, WM_NULL, WM_NULL, PM_REMOVE))
	 {
			TranslateMessage(&aMsg);
			DispatchMessage(&aMsg);
	 }
}


} // namespace GClasses

#endif // WINDOWS
