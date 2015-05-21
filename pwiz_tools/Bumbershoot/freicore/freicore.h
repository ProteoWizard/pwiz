//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

#ifndef _FREICORE_H
#define _FREICORE_H

#include "Profiler.h"
#include "ResidueMap.h"
#include "lnFactorialTable.h"
#include "shared_types.h"
#include "shared_defs.h"
#include "shared_funcs.h"
#include "SearchSpectrum.h"
#include "proteinStore.h"
#include "BaseSpectrum.h"

//#define BOOST_LIB_DIAGNOSTIC

#ifdef USE_MPI
	#undef SEEK_SET
	#undef SEEK_CUR
	#undef SEEK_END
	#include "mpi.h"
#endif

namespace freicore
{
    struct Version
    {
        static int Major();
        static int Minor();
        static int Revision();
        static std::string str();
        static std::string LastModified();
    };

} // namespace freicore

#endif
