// $Id$
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

#ifdef WINDOWS_NATIVE // MSVC or MinGW
#include <windows.h>
#include <stdio.h>
#include <iostream>
#include <vector>
#include "wglob.h"


/*							//for testing
int main()	
	{
	char* pattern = "*.txt";
	glob_t globbo;

	glob(pattern,0,NULL,&globbo);

	globfree(&globbo);
	return 1;
	}
*/

int glob(const char* pattern,int flags,int* unused, glob_t* pglob)
	{
	WIN32_FIND_DATA fileData;
	HANDLE hFind;
	char** fileList= NULL;


	if(!pattern || !pglob || flags || unused)	//errors , unsupported features
		{
		std::cout<<"Error: invalid argument or unsupported mode in wglob.cxx (int glob(...)) \n";
		std::cout<<"Suggestion: Second and third arguments must be NULL (unsupported features) \n";
		exit(1);
		}
	pglob->gl_pathc = 0;
	pglob->gl_pathv = NULL;
    const char *slash = strrchr(pattern,'/');
	if (!slash) {
		slash = strrchr(pattern,'\\');
	}
	char *path = strdup(slash?pattern:"");
	if (slash) {
		*(path+(slash-pattern)+1) = 0;
	}

	hFind = FindFirstFile(pattern,&fileData);	//first file

	if(!hFind || hFind == INVALID_HANDLE_VALUE)
		return 1;

   std::vector<char*> v;
	
	do {
		char *fpath = (char *)malloc(strlen(path)+strlen(fileData.cFileName)+1);
		strcpy(fpath, path);
		strcat(fpath, fileData.cFileName);
		v.push_back(fpath);		
		} while (FindNextFile(hFind,&fileData));

	int vsize=(int)v.size();
	pglob->gl_pathv = new char*[vsize];
	for(int i=vsize;i--;)
		{
		pglob->gl_pathv[i] = v[i];		
		}
	pglob->gl_pathc=vsize;						
    FindClose(hFind);
	free(path);
	return 0;
	}	

void globfree(glob_t* globbo)					//free memory
	{
	if(!globbo)
		return;

	if(globbo->gl_pathv)
		{
		for(int i=0;i<globbo->gl_pathc;i++)
			{
			free(globbo->gl_pathv[i]);			//we use free not delete because of strdup
			}
		delete[] globbo->gl_pathv;
		}
	}
#endif
