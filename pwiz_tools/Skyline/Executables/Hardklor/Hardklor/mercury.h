/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

Uses Mercury code, with permission, from Alan L. Rockwood and Steve van Orden
*/
#ifndef _MERCURY_H
#define _MERCURY_H

#include <vector>

#define PI      3.14159265358979323846
#define TWOPI   6.28318530717958647
#define HALFPI  1.57079632679489666
#define GAUSSIAN 1
#define LORENTZIAN 2
#define ProtonMass   1.00727649
#define ElectronMass 0.00054858
#define MAXAtomNo    108		/* allows for elements H - Lr */
#define MAXIsotopes  20			/* allows for 20 elements in molecular formula */
#define EZS Element[Z].Symbol
#define EZNI Element[Z].NumIsotopes
#define EZM Element[Z].IsoMass
#define EZP Element[Z].IsoProb
#define EZW Element[Z].WrapMass
#define EZNA Element[Z].NumAtoms

struct Result {
  double mass;
  double data;
};

#endif
