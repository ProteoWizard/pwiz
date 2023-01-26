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
#include "Smooth.h"
#include <iostream>

using namespace std;
using namespace MSToolkit;

//Savitzky-Golay smoothing algorithm
void SG_Smooth(Spectrum& sp, int m, int p){

  if(2*m<p) {
    cout << "Invalid Smoothing Parameters == Smoothing Skipped!" << endl;
    return;
  }
 
  int t,i,s;
  int k;
  int c1,c2,c3;
  double weightSum;
  double *weight, *holder;
  weight = new double[2*m+1];
  holder = new double[sp.size()];
  
  s = 0;   // Derivative order s = 0 for smooth
 
  //copy intensities to holder array and set to 0
  for(i=0;i<sp.size();i++){
    holder[i] = sp.at(i).intensity;
    sp.at(i).intensity=0;
  }
 
  //Calculate the Savitzky-Golay Weights for t = 0
  c1 = 0;
  weightSum=0;
  for(i=-m; i<=m; i++){
    weight[c1] = SG_Weight(i, 0, m, p, s);
    weightSum+=weight[c1];
    c1++;
  }
  
  c1 = 0;
  t = -m;
  // Smoothing for the points 0 to m-1
  for(k=0; k<m; k++){
    c3 = 0;
    for(i=-m; i<=m; i++){
      sp.at(k).intensity += (float)(SG_Weight(i, t, m, p, s) * holder[k + c3]);
      c3++;
    }
    t++;
  }
    
  // Smoothing for the bulk of the chromatogram at t = 0
  for(k=m;k<sp.size()-m;k++){
    c2=-m;
    for(i=0; i<2*m+1; i++){
      sp.at(k).intensity += (float)(weight[i] * holder[k+c2]);
      c2++;
    }
  }
    
  // Smoothing for the points end-m+1 to end
  t = 1;
  for(k=sp.size()-m; k<sp.size(); k++){
    c3 = -(2*m+1);
    for(i=-m; i<=m; i++){
      sp.at(k).intensity += (float)(SG_Weight(i, t, m, p, s) * holder[k + c3]);
      c3++;
    }
    t++;
  }

  delete [] weight;
  delete [] holder;
}

//Savitzky-Golay smoothing with an array instead of spectrum object
void SG_SmoothD(float *d, int sz, int m, int p){
    
  int t,i,s;
  int k;
  int c1,c2,c3;
  float *weight,*holder;
  weight = new float[sz];
  holder = new float[sz];
  
  s = 0;   // Derivative order s = 0 for smooth
  
  for(i=0;i<sz;i++){
    holder[i] = d[i];
    d[i]=0;
  }

  //Calculate the Savitsky-Golay Weights for t = 0
  c1 = 0;
  for(i=-m; i<=m; i++){
    weight[c1] = (float)SG_Weight(i, 0, m , p, s);
    c1++;
  }
  
  c1 = 0;
  // Smoothing for the points 0 to m-1
  t = -m;
  for(k=0; k<m; k++){
    c3 = 0;
    for(i=-m; i<=m; i++){
      d[k] += (float)(SG_Weight(i, t, m, p, s) * holder[k + c3]);
      c3++;
    }
    t++;
  }
    
  // Smoothing for the bulk of the chromatogram at t = 0
  for(k=m;k<sz-m;k++){
    c2=-m;
    for(i=0; i<(2*m+1); i++){
      d[k] += weight[i] * holder[k+c2];
      c2++;
    }
  }
    
  // Smoothing for the points end-m+1 to end
  t = 1;
  for(k=sz-m; k<sz; k++){
    c3 = -(2*m+1);
    for(i=-m; i<=m; i++){
      d[k] += (float)(SG_Weight(i, t, m, p, s) * holder[k + c3]);
      c3++;
    }
    t++;
  }

  delete [] weight;
  delete [] holder;

}

// Calculates the Savitsky-Golay weight of the i'th data point
// for the t'th Least-Square point of the s'th derivative
// over 2m+1 points, order n.
double SG_Weight(int i, int t, int m, int n, int s){
  
  int k;
  double sum;

  sum = 0;
  for(k=0;k<=n;k++){
    sum += (double)(2 * k + 1) * 
      (SG_GenFact(2 * m, k) / SG_GenFact(2 * m + k + 1, k + 1)) * 
      SG_GramPoly(i, m, k, 0) * 
      SG_GramPoly(t, m, k, s);
  }

  return sum;
}

// Calculates the Savitsky-Golay Gram Polynomial (s = 0) or it's s'th
// derivative evaluated at i, order k, over 2m+1 points.
double SG_GramPoly(int i, int m, int k, int s){

  if(k>0){
    return (double)(4*k-2) / (double)(k*(2*m-k+1)) * 
      (i*SG_GramPoly(i,m,k-1,s) + s*SG_GramPoly(i,m,k-1,s-1)) - 
      (double)((k-1) * (2*m+k)) / (double)(k*(2*m-k+1)) * 
      SG_GramPoly(i,m,k-2,s);
  } else if(k==0 && s==0) {
    return 1;
  } else {
    return 0;
  }

}

double SG_GenFact(int a, int b) {
  int j;
  int c;
  double gf;

  gf = 1;
  c = a-b+1;
  for(j=c; j<=a; j++) gf *= j;
  return gf;
}
