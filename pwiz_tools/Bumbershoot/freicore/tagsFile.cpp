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
#include "shared_defs.h"
#include "shared_funcs.h"
#include "tagsFile.h"

using namespace freicore;

namespace std
{
    ostream& operator<< ( ostream& o, const TagInfo& rhs )
    {
        o    << "( "
            << rhs.tag << ' '
            << rhs.lowPeakMz << ' '
            << rhs.nTerminusMass << ' '
            << rhs.cTerminusMass << ' '
            << rhs.valid << ' '
            << rhs.totalScore;

        for( map< string, float >::const_iterator itr = rhs.scores.begin(); itr != rhs.scores.end(); ++itr )
            if( itr->first != "total" )
                o << ' ' << itr->second;
        return o << " )";
    }
}
