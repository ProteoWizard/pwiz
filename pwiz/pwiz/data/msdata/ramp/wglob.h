// $Id$
#ifndef WGLOB_H
#define WGLOB_H

/*

Program       : Glob for windows                                                  
Author        : Nathan Heinecke <nate.heinecke@insilicos.com>                                                     
Date          : 6.20.2007

Glob for Windows
	Emulates basic Glob (from unix) functionality (really very basic)
	The following features of unix glob are currently unsupported:
		-Passing any nonzero value as second or third argument (will force program exit)
		-Using the same glob_t struct for multiple calls to glob (will cause memory leaks)

Copyright (C) 2007 Nathan Heinecke

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

Nathan Heinecke
Insilicos
2722 Eastlake ave. E #300 
Seattle, WA  98102  USA
nate.heinecke@insilicos.com

*/

struct glob_t
	{
	int gl_pathc;
	char** gl_pathv;
	};

int glob(const char*,int,int*,glob_t*);

void globfree(glob_t*);

#endif
