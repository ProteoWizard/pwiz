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
*/
#ifndef _SMOOTH_H
#define _SMOOTH_H

#include "Spectrum.h"

double SG_GenFact(int, int);
double SG_GramPoly(int, int, int, int);
void SG_Smooth(MSToolkit::Spectrum&, int, int);
void SG_SmoothD(float*, int, int, int);
double SG_Weight(int, int, int, int, int);

#endif
