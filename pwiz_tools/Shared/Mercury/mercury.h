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
