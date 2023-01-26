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
#include "FFT.h"

void BitReverse(complex* data, int size){

	complex swap;
	int i,j,k;
  j=0;
	k=0;

  for (i=0; i<size; i++) {

		if (j > i) {
			swap = data[i];
			data[i] = data[j];
			data[j] = swap;
		};

    k = size >> 1;
    while ( k>1 && j>k-1 ) {
			j -= k;
			k >>= 1;
    };
    j += k;

  };

};

void FFT(complex* data, int size, bool forward){

	int dir;
	int jump;
	int level=1;
	int i,j,k;
	double a,b,c,d,e,f;
	complex swap;

	if(forward) dir=1;
	else dir=-1;

	BitReverse(data,size);

  while (size > level) {

		jump = level << 1;
		a = dir*PI/level;			
		b = sin(a);			 
    c = sin(0.5*a);			
    d = -2.0*c*c;  
    e = 1.0;					
    f = 0.0;						

    for (i=0; i<level; i++){
			for (j=i; j<size; j+=jump) {
				k = j + level;
				swap.real = (e*data[k].real) - (f*data[k].imag);
				swap.imag = (e*data[k].imag) + (f*data[k].real);
				data[k].real = data[j].real - swap.real;
				data[k].imag = data[j].imag - swap.imag;
				data[j].real += swap.real;
				data[j].imag += swap.imag;
			};
			c = e;
			e += (c*d) - (f*b);
			f += (f*d) + (c*b);
    };
    level = jump;
  };

	/* Normalization - adapted from Mercury */
	/*
	if(forward) {
    for (i=0; i<size; i++) {
			data[i].real /= size;
			data[i].imag /= size;
    };
	};
	*/

};

void FFTreal(complex* data, int size){

	int i,n;
	double a,b,c,d,e,f;
	complex x,x2;

	FFT(data,size,true);
	n=size>>1;

	a = PI/size;	
	b = sin(a);			
  c = sin(0.5*a);	
  d = -2.0*c*c;   
  e = 1.0+d;		
  f = b;	  		

	for(i=1;i<n;i++) {

		x.real=0.5*(data[i].real+data[size-i].real);
		x.imag=0.5*(data[i].imag-data[size-i].imag);
		x2.real=0.5*(data[i].imag+data[size-i].imag);
		x2.imag=-0.5*(data[i].real-data[size-i].real);

		data[i].real = x.real + e * x2.real - f * x2.imag;
		data[i].imag = x.imag + e * x2.imag + f * x2.real;
		data[size-i].real = x.real - e * x2.real + f * x2.imag;
		data[size-i].imag = -x.imag + e * x2.imag + f * x2.real;

		c = e;
		e += (c*d) - (f*b);
		f += (f*d) + (c*b);

	};
	
	d=data[0].real;
	data[0].real=d+data[0].imag;
	data[0].imag=d-data[0].imag;
	

};
