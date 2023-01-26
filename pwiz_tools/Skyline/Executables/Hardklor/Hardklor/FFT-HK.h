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
#ifndef _FFT_HK_H
#define _FFT_HK_H

#include "MSReader.h"
#include "FFT.h"
#include <cmath>
#include <vector>

void FFTCharge(double *f, MSToolkit::Spectrum& s, unsigned int start, unsigned int stop,
							 unsigned int lowCharge, unsigned int highCharge, double interval, bool bSpline=false);
double GetIntensity(MSToolkit::Spectrum& s, unsigned int start, unsigned int stop, double mz);
void Patterson(double *f, MSToolkit::Spectrum& s, unsigned int start, unsigned int stop,
							 unsigned int lowCharge, unsigned int highCharge, double interval);
void SenkoCharge(std::vector<int> *charges, MSToolkit::Spectrum& s, unsigned int start, unsigned int stop,
								 unsigned int lowCharge, unsigned int highCharge, double interval, char method);

#endif
