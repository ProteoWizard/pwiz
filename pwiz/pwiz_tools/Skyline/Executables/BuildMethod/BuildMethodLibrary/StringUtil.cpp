/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/////////////////////////////////////////////////////////////////////////////
//  StringUtil.cpp
//    A few useful string utilities borrowed from the web.

#define _WIN32_WINNT 0x0501     // Windows XP

#include <comdef.h>

#include "StringUtil.h"

/**
 * str_to_wstr - convert from std::string to std::wstring for use with _bstr_t
 *               http://www.codeguru.com/forum/showthread.php?t=275978
 */
wstring str_to_wstr(const string& str)
{
  wstring wstr(str.length()+1, 0);  
  MultiByteToWideChar(CP_ACP, 0, str.c_str(), str.length(), &wstr[0], str.length());
  return wstr;
}

/**
 * wstr_to_str - convert from std::wstring to std::string for use _bstr_t
 *               output parameters from COM
 *               http://www.codeguru.com/forum/showthread.php?t=275978
 */
string wstr_to_str(const wstring& wstr)
{
  size_t size = wstr.length();
  string str(size + 1, 0);  
  WideCharToMultiByte(CP_ACP, 0, wstr.c_str(), size, &str[0], size, NULL, NULL);
  return str;
}

void trim(string& str)
{
    const char* whitespace = " \t\r\n";
    str.erase(0, str.find_first_not_of(whitespace))
        .erase(str.find_last_not_of(whitespace)+1);
}
