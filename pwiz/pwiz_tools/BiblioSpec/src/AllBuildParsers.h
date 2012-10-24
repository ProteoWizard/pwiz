//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#pragma once

#include "BuildParser.h"
#include "IdpXMLreader.h"
#include "SQTreader.h"
#include "PepXMLreader.h"
#ifdef USE_MASCOT_PARSER
#include "MascotResultsReader.h"
#else
#include "MascotResultsReader_dummy.h"
#endif
#include "SslReader.h"
#include "TandemNativeParser.h"
#include "PwizReader.h"
#include "ProteinPilotReader.h"
#include "MzIdentMLReader.h"
#include "PercolatorXmlReader.h"
#include "WatersMseReader.h"
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
