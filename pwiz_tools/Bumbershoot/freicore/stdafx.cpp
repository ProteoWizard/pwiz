//
// $Id$
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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#include "stdafx.h"

namespace freicore {
HostEndianType GetHostEndianType()
{
    int testInt = 127;
    char* testIntP = (char*) &testInt;

    if( testIntP[0] == 127 )
        return COMMON_LITTLE_ENDIAN;
    else if( testIntP[ sizeof(int)-1 ] == 127 )
        return COMMON_BIG_ENDIAN;
    else
        return COMMON_UNKNOWN_ENDIAN;
}

}
