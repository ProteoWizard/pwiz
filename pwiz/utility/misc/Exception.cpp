//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

#define PWIZ_SOURCE

#include "Exception.hpp"
#include <sstream>
#include <iostream>


#ifdef _DEBUG
#include <crtdbg.h>

namespace {

int CrtReportHook(int reportType, char *message, int *returnValue)
{
    std::cerr << message;
    if (returnValue) *returnValue = 0;
    return 1;
}

int CrtReportHookW(int reportType, wchar_t *message, int *returnValue)
{
    std::wcerr << message;
    if (returnValue) *returnValue = 0;
    return 1;
}

} // namespace

PWIZ_API_DECL ReportHooker::ReportHooker()
{
    _CrtSetReportHook2(_CRT_RPTHOOK_INSTALL, &CrtReportHook);
    _CrtSetReportHookW2(_CRT_RPTHOOK_INSTALL, &CrtReportHookW);
}

PWIZ_API_DECL ReportHooker::~ReportHooker()
{
    _CrtSetReportHook2(_CRT_RPTHOOK_REMOVE, &CrtReportHook);
    _CrtSetReportHookW2(_CRT_RPTHOOK_REMOVE, &CrtReportHookW);
}
#endif
