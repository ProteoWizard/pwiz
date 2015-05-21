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
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

#ifndef EXPAT_XML_H
#define EXPAT_XML_H

#ifndef Expat_INCLUDED
	// MSVC magic to get Expat to link statically
	#if defined(WIN32) || defined(WIN64)
		#ifndef _DEBUG // Expat doesn't work in Debug mode... something about duplicate symbols
			#ifndef XML_STATIC
				#define XML_STATIC
			#endif
		#endif
	#endif

	#include <expat.h>
#endif

#endif
