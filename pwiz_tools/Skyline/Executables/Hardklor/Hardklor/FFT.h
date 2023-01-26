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
#ifndef _FFT_H
#define _FFT_H

#include <cmath>

#ifndef PI
#define PI      3.14159265358979323846
#endif

typedef struct complex{
	double real;
	double imag;
} complex;


void BitReverse(complex* data, int size);
void FFT(complex* data, int size, bool forward);
void FFTreal(complex* data, int size);

#endif
