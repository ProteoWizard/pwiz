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

#include "constants.h"

///Define the elemental composition of the C,H,N,O,S, and P
const double CARBON_ISOTOPES[NUM_ISOTOPES]      = { 0.9893,     0.0107,     0,          0,          0 };
const double HYDROGEN_ISOTOPES[NUM_ISOTOPES]    = { 0.999885,	0.000115,	0,          0,          0 };
const double OXYGEN_ISOTOPES[NUM_ISOTOPES]      = { 0.99757,	0.00038,	0.00205,	0,          0 };
const double NITROGEN_ISOTOPES[NUM_ISOTOPES]    = { 0.99632,	0.00368,	0.,         0,          0 };
const double SULPHUR_ISOTOPES[NUM_ISOTOPES]     = { 0.9493,     0.0076,     0.429,	    0.0002,     0 };
const double PHOSPHORUS_ISOTOPES[NUM_ISOTOPES]  = { 1.0,        0.0,        0.0,        0.0,        0 };

///Initialize the elemental masses
const double CARBON_MONO        = 12.00000;
const double CARBON_AVG         = 12.01078;
const double HYDROGEN_MONO      = 01.00783;
const double HYDROGEN_AVG       = 01.00794;
const double OXYGEN_MONO        = 15.99491;
const double OXYGEN_AVG         = 15.99943;
const double NITROGEN_MONO      = 14.00304;
const double NITROGEN_AVG       = 14.00672;
const double SULFUR_MONO        = 31.97207;
const double SULFUR_AVG         = 32.06550;
const double PHOSPHORUS_MONO    = 30.97376;
const double PHOSPHORUS_AVG     = 30.97376;


///Initialize the water and ammonia masses
const double WATER_MONO     = 2*HYDROGEN_MONO + OXYGEN_MONO;
const double WATER_AVG      = 2*HYDROGEN_AVG + OXYGEN_AVG;

const double AMMONIA_MONO   = 3*HYDROGEN_MONO + NITROGEN_MONO;
const double AMMONIA_AVG    = 3*HYDROGEN_AVG + NITROGEN_AVG;

///Initialize the peptide and protein termini characters
const char PEPTIDE_N_TERMINUS_STRING[2] = { PEPTIDE_N_TERMINUS_SYMBOL, '\0' };
const char PEPTIDE_C_TERMINUS_STRING[2] = { PEPTIDE_C_TERMINUS_SYMBOL, '\0' };

const char PROTEIN_N_TERMINUS_STRING[2] = { PROTEIN_N_TERMINUS_SYMBOL, '\0' };
const char PROTEIN_C_TERMINUS_STRING[2] = { PROTEIN_C_TERMINUS_SYMBOL, '\0' };
