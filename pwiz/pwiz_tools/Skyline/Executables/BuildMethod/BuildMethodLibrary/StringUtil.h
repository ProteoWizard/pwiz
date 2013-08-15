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
//  StringUtil.h
//    A few useful string utilities borrowed from the web.

#pragma once

#include <string>
#include <vector>

using namespace std;

wstring str_to_wstr(const string& str);
string wstr_to_str(const wstring& wstr);

void trim(string& str);

/**
 * split - split a std::string into a vector<string>
 *         http://www.codeproject.com/KB/string/stringsplit.aspx
 */
template<typename _Cont>
void split(const string& str, _Cont& container, const string& delim=",")
{
    string::size_type lpos = 0;
    string::size_type pos = str.find_first_of(delim, lpos);
    while(lpos != string::npos)
    {
        container.insert(container.end(), str.substr(lpos,pos - lpos));

        lpos = (pos == string::npos) ?   string::npos : pos + 1;
        pos = str.find_first_of(delim, lpos);
    }
}