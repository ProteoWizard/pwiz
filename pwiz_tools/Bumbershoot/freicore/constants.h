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

#ifndef _CONSTANTS_H
#define _CONSTANTS_H

/**
   This file defines elemental masses for organic elements such as
   carbon (C), Oxygen (O), Nitrogen (N), Sulphur (S), and Phosphorus (P).
   These elements make up the basic composition of all naturally occuring 
   amino acids. All constants are derived from:
   http://www.lbl.gov/abc/wallchart/chapters/appendix/appendixc.html
   */

///Defines proton, neutron and electron masses
#define PROTON                1.007276
#define NEUTRON                1.008665
#define ELECTRON            0.000549

///Defines number of isotopes for each element
#define NUM_ISOTOPES        5
extern const double CARBON_ISOTOPES[NUM_ISOTOPES];
extern const double HYDROGEN_ISOTOPES[NUM_ISOTOPES];
extern const double OXYGEN_ISOTOPES[NUM_ISOTOPES];
extern const double NITROGEN_ISOTOPES[NUM_ISOTOPES];
extern const double SULPHUR_ISOTOPES[NUM_ISOTOPES];
extern const double PHOSPHORUS_ISOTOPES[NUM_ISOTOPES];

//extern const float C12_MASS;
//extern const float SCALE_FACTOR;

///Defines the elemental masses
extern const double CARBON_MONO;
extern const double CARBON_AVG;
extern const double HYDROGEN_MONO;
extern const double HYDROGEN_AVG;
extern const double OXYGEN_MONO;
extern const double OXYGEN_AVG;
extern const double NITROGEN_MONO;
extern const double NITROGEN_AVG;
extern const double SULFUR_MONO;
extern const double SULFUR_AVG;
extern const double PHOSPHORUS_AVG;
extern const double PHOSPHORUS_MONO;

///Define mass of water and ammonia
extern const double WATER_MONO;
extern const double WATER_AVG;
#define WATER(useAvg) (useAvg?WATER_AVG:WATER_MONO)

extern const double AMMONIA_MONO;
extern const double AMMONIA_AVG;
#define AMMONIA(useAvg) (useAvg?AMMONIA_AVG:AMMONIA_MONO)

#define PEPTIDE_N_TERMINUS_SYMBOL    '('
#define PEPTIDE_C_TERMINUS_SYMBOL    ')'
extern const char PEPTIDE_N_TERMINUS_STRING[2];
extern const char PEPTIDE_C_TERMINUS_STRING[2];

#define PROTEIN_N_TERMINUS_SYMBOL    '['
#define PROTEIN_C_TERMINUS_SYMBOL    ']'
extern const char PROTEIN_N_TERMINUS_STRING[2];
extern const char PROTEIN_C_TERMINUS_STRING[2];

#endif
