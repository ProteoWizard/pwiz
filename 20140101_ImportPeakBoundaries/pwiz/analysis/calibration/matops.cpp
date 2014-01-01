// $Id$
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

//#include <sys/time.h>
#define _USE_MATH_DEFINES
#include "matops.h"
#include <ctime>




vector<int> d2i(vector<double> v)
{
   int n = v.size();
   vector<int> r(n);
   for (int i=0;i<n;i++) {
      r[i] = (int) (v[i]+0.5);
   }
   return r;
}

vector<vector<int> > d2i(vector<vector<double> > v)
{
   int n = v.size();
   vector<vector<int> > r(n);
   for (int i=0;i<n;i++) {
      r[i] = d2i(v[i]);
   }
   return r;
}

vector<double> i2d(vector<int> v)
{
   int n = v.size();
   vector<double> r(n);
   for (int i=0;i<n;i++) {
      r[i] = (double) v[i];
   }
   return r;
}

vector<vector<double> > d2i(vector<vector<int> > v)
{
   int n = v.size();
   vector<vector<double> > r(n);
   for (int i=0;i<n;i++) {
      r[i] = i2d(v[i]);
   }
   return r;
}

bool operator == (vector<int> &a, vector<int> &b)
{
   int n = a.size();
   if (n!=b.size()) return false;
   for (int i=0; i<n; i++) {
      if (a[i]!=b[i]) return false;
   }
   return true;
}

bool operator == (vector<vector<int> > &a, vector<vector<int> > &b)
{
   int n = a.size();
   if (n!=b.size()) return false;
   for (int i=0; i<n; i++) {
      if (a[i]!=b[i]) return false;
   }
   return true;
}

bool operator == (vector<double> &a, vector<double> &b)
{
   int n = a.size();
   if (n!=b.size()) return false;
   for (int i=0; i<n; i++) {
      if (a[i]!=b[i]) return false;
   }
   return true;
}

bool operator == (vector<vector<double> > &a, vector<vector<double> > &b)
{
   int n = a.size();
   if (n!=b.size()) return false;
   for (int i=0; i<n; i++) {
      if (a[i]!=b[i]) return false;
   }
   return true;
}



vector<vector<double> > eig(vector<vector<double> > k)
{
   int dim = k.size();
   if (k[0].size()!=dim) return(vector<vector<double> >(0));
   if (dim==2) return eig2(k);
   vector<vector<double> > result = vector<vector<double> >(0);
   vector<double> lambda(dim);
   double precis = 1e-07;
   bool non_zero_evs = true;
   for (int i=0;non_zero_evs&&i<dim;i++) {
//      cout << "searching for eigenvector " << i+1 << endl;
      vector<double> v;
      bool in_span = true;
      for (int j=0;in_span;j++) {
         v = vector<double>(dim);
         v[j] = 1;
         for (int q=0;q<result.size();q++) { v = v - (v*result[q])*result[q]; }
         double n = norm(v);
         if (n>0.1) in_span = false;
         v = (1/n)*v;
      }
      bool not_aligned = true;
      double d;
      while (not_aligned) {
         vector<double> lastv = v;
         for (int q=0;q<result.size();q++) { v = v - (v*result[q])*result[q]; }
         double n = norm(v);
         v = (1/n)*v;
         vector<double> u = k*v;
         d = norm(u);
//         cout << v << ' ' << k*v << ' ' << d << endl;
         if (d>precis) {
            v = (1/d) * u;
            double angle = acos(v*lastv);
            if (angle<precis) not_aligned = false;
//            cout << angle << endl;
         }
         else { d = 0; not_aligned = false; }
      }
//      cout << i+1 << ' ' << v << ' ' << d << endl;
      result.push_back(v);
      lambda[i] = d;
   }
//   cout << "filling null space\n";
   result = orthogonal_basis(result);
   for (int i=0;i<dim;i++) { result[i].push_back(lambda[i]); }

// sort eigenvectors by abs(eigenvalue)
   for (int i=0;i<dim;i++) {
      for (int j=0;j<dim-1;j++) {
         if (abs(result[j][dim])<abs(result[j+1][dim])) swap(result[j],result[j+1]);
      }
   }
   return result;
}

vector<double> cm(vector<vector<double> > x, vector<double> weight)
{
   int n = x.size();
   if (weight.size() == 0) {
      weight = vector<double>(n, 1.0);
   }
   int dim = x[0].size();
   vector<double> m = vector<double>(dim);
   double tot_weight = 0;
   for (int i=0;i<n;i++) {
      m = m + (weight[i] * x[i]);
      tot_weight += weight[i];
   }
   m = (1/tot_weight) * m;
   return m;
}

vector<vector<double> > cov(vector<vector<double> > x, vector<double> weight)
{
   vector<double> m = cm(x, weight);

   int n = x.size();
   int dim = x[0].size();
   vector< vector<double> > k = vector< vector<double> >(dim);
   for (int i=0; i < dim; i++) {
      k[i] = vector<double>(dim);
      for (int j=0; j < dim; j++) {
         for (int p=0; p < n; p++) {
            k[i][j] += weight[p] * (x[p][i]-m[i]) * (x[p][j]-m[j]);
         }
      }
   }

   return k;
}

vector<int> operator +(vector<int> v1, vector<int> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (vector<int>(0));
   vector<int> v = vector<int>(n);
   for (int i=0;i<n;i++) v[i] = v1[i]+v2[i];
   return(v);
}

vector<double> operator +(vector<double> v1, vector<double> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (vector<double>(0));
   vector<double> v = vector<double>(n);
   for (int i=0;i<n;i++) v[i] = v1[i]+v2[i];
   return(v);
}

void operator += (vector<int> &V, vector<int> va)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] += va[i];
}

void operator += (vector<double> &V, vector<double> va)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] += va[i];
}

void operator += (vector<complex<double> > &V, vector<complex<double> > va)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] += va[i];
}

void operator += (vector<vector<double> > &V, vector<vector<double> > va)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] += va[i];
}

void operator += (vector<vector<complex<double> > > &V,
            vector<vector<complex<double> > > &va)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] += va[i];
}

void operator *= (vector<double> &V, double scale)
{
   int n = V.size();
   for (int i=0;i<n;i++) V[i] *= scale;
}

vector<vector<double> > operator + (vector<vector<double> > m1, vector<vector<double> > m2)
{
   int n = m1.size();
   if (m2.size()!=n) return(vector<vector<double> >(0));
   vector<vector<double> > r = vector<vector<double> >(n);
   for (int i=0;i<n;i++) r[i] = m1[i] + m2[i];
   return(r);
}

vector<vector<complex<double> > > operator +
  (vector<vector<complex<double> > > &m1, vector<vector<complex<double> > > &m2)
{
   int n = m1.size();
   if (m2.size()!=n) return(vector<vector<complex<double> > >(0));
   vector<vector<complex<double> > > r = vector<vector<complex<double> > >(n);
   for (int i=0;i<n;i++) r[i] = m1[i] + m2[i];
   return(r);
}

vector<int> operator -(vector<int> v1, vector<int> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (vector<int>(0));
   vector<int> v = vector<int>(n);
   for (int i=0;i<n;i++) v[i] = v1[i]-v2[i];
   return(v);
}

vector<double> operator -(vector<double> v1, vector<double> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (vector<double>(0));
   vector<double> v = vector<double>(n);
   for (int i=0;i<n;i++) v[i] = v1[i]-v2[i];
   return(v);
}

vector<vector<double> > operator - (vector<vector<double> > m1, vector<vector<double> > m2)
{
   int n = m1.size();
   if (m2.size()!=n) return(vector<vector<double> >(0));
   vector<vector<double> > r = vector<vector<double> >(n);
   for (int i=0;i<n;i++) r[i] = m1[i] - m2[i];
   return(r);
}

double operator *(vector<double> v1, vector<double> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (0);
   double sum = 0;
   for (int i=0;i<n;i++) sum += v1[i]*v2[i];
   return(sum);
}

int operator *(vector<int> v1, vector<int> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (0);
   int sum = 0;
   for (int i=0;i<n;i++) sum += v1[i]*v2[i];
   return(sum);
}

double operator *(vector<int> v1, vector<double> v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (0);
   double sum = 0;
   for (int i=0;i<n;i++) sum += v1[i]*v2[i];
   return(sum);
}

// v1, v2 interpreted as Fourier coeffs 0..N-1 of f,g
// v1*v2 should return f*g
double operator *(vector<complex<double> > v1, vector<complex<double> > v2)
{
   int n = v1.size();
   if (v2.size()!=n) return (0);
   double sum = real(v1[0]*v2[0]);
   for (int i=1;i<n;i++) sum += 2*real(v1[i]*conj(v2[i]));
   return(sum);
}

vector<double> operator *(double c, vector<int> v)
{
   int n = v.size();
   vector<double> cv = vector<double>(n);
   for (int i=0;i<n;i++) cv[i] = c*v[i];
   return(cv);
}

vector<double> operator *(double c, vector<double> v)
{
   int n = v.size();
   vector<double> cv = vector<double>(n);
   for (int i=0;i<n;i++) cv[i] = c*v[i];
   return(cv);
}

vector<complex<double> > operator *(double c, vector<complex<double> > v)
{
   int n = v.size();
   vector<complex<double> > cv(n);
   for (int i=0;i<n;i++) cv[i] = c*v[i];
   return(cv);
}

vector<int> operator *(int c, vector<int> v)
{
   int n = v.size();
   vector<int> cv = vector<int>(n);
   for (int i=0;i<n;i++) cv[i] = c*v[i];
   return(cv);
}

vector<vector<double> > operator *(double c, vector<vector<int> > m)
{
   int n = m.size();
   vector<vector<double> > mv = vector<vector<double> >(n);
   for (int i=0;i<n;i++) mv[i] = c*m[i];
   return(mv);
}

vector<vector<double> > operator *(double c, vector<vector<double> > m)
{
   int n = m.size();
   vector<vector<double> > mv = vector<vector<double> >(n);
   for (int i=0;i<n;i++) mv[i] = c*m[i];
   return(mv);
}

vector<double> operator *(vector<vector<double> >m, vector<double> v)
{
   int c = m[0].size();
   if (c!=v.size()) return(vector<double>(0));
   int r = m.size();
   vector<double> mv(r);
   for (int i=0;i<r;i++) mv[i] = m[i]*v;
   return(mv);
}

vector<int> operator *(vector<vector<int> >m, vector<int> v)
{
   int c = m[0].size();
   if (c!=v.size()) return(vector<int>(0));
   int r = m.size();
   vector<int> mv(r);
   for (int i=0;i<r;i++) mv[i] = m[i]*v;
   return(mv);
}

vector<double> operator *(vector<vector<double> >m, vector<int> v)
{
   int r = v.size();
   vector<double> vd = vector<double>(r);
   for (int i=0;i<r;i++) vd[i] = v[i];
   return(m*vd);
}

vector<vector<double> > operator *
      (vector<vector<double> > a ,vector<vector<double> > b)
{
   int r1 = a.size();
   int c1 = a[0].size();
   int r2 = b.size();
   int c2 = b[0].size();
   if (c1!=r2) return(vector<vector<double> >(0));
   vector<vector<double> > m = vector<vector<double> >(r1);
   for (int i=0;i<r1;i++) m[i] = vector<double>(c2);
   vector<vector<double> > bt = transpose(b);
   for (int i=0;i<r1;i++)
      for (int j=0;j<c2;j++) m[i][j] = a[i]*bt[j];
   return(m);
}

vector<vector<complex<double> > > operator *
      (vector<vector<complex<double> > > a ,vector<vector<complex<double> > > b)
{
   int r1 = a.size();
   int c1 = a[0].size();
   int r2 = b.size();
   int c2 = b[0].size();
   if (c1!=r2) return(vector<vector<complex<double> > >(0));
   vector<vector<complex<double> > > m = vector<vector<complex<double> > >(r1);
   for (int i=0;i<r1;i++) m[i] = vector<complex<double> >(c2);
   vector<vector<complex<double> > > bt = transpose(b);
   for (int i=0;i<r1;i++)
      for (int j=0;j<c2;j++) m[i][j] = a[i]*bt[j];
   return(m);
}

vector<vector<int> > transpose(vector<vector<int> >v)
{
   int r = v.size();
   int c = v[0].size();
   vector<vector<int> > vt = vector<vector<int> >(c);
   for (int i=0;i<c;i++) {
      vt[i] = vector<int>(r);
      for (int j=0;j<r;j++)
         vt[i][j] = v[j][i];
   }
   return(vt);
}

vector<vector<double> > transpose(vector<vector<double> >v)
{
   int r = v.size();
   int c = v[0].size();
   vector<vector<double> > vt = vector<vector<double> >(c);
   for (int i=0;i<c;i++) {
      vt[i] = vector<double>(r);
      for (int j=0;j<r;j++)
         vt[i][j] = v[j][i];
   }
   return(vt);
}

vector<vector<complex<double> > > transpose(vector<vector<complex<double> > >v)
{
   int r = v.size();
   int c = v[0].size();
   vector<vector<complex<double> > > vt = vector<vector<complex<double> > >(c);
   for (int i=0;i<c;i++) {
      vt[i] = vector<complex<double> >(r);
      for (int j=0;j<r;j++)
         vt[i][j] = v[j][i];
   }
   return(vt);
}

double norm(vector<int> v)
{
   double sum = 0;
   int n = v.size();
   for (int i=0;i<n;i++)
      sum += v[i]*v[i];
   return(sqrt(sum));
}

double norm(vector<double> v)
{
   double sum = 0;
   int n = v.size();
   for (int i=0;i<n;i++)
      sum += v[i]*v[i];
   return(sqrt(sum));
}

vector<double> operator ^(vector<double>a,vector<double>b)
{
   if (a.size()!=3||b.size()!=3) return(vector<double>(0));
   vector<double> axb = vector<double>(3);
   axb[0] = a[1]*b[2]-a[2]*b[1];
   axb[1] = a[2]*b[0]-a[0]*b[2];
   axb[2] = a[0]*b[1]-a[1]*b[0];
   return(axb);
}

vector<double> zero(int dim)
{
   vector<double> z = vector<double>(dim);
   for (int i=0;i<dim;i++) z[i] = 0;
   return(z);
}

vector<vector<double> > zero_mat(int dim)
{
   vector<vector<double> > res(dim);
   for (int i=0;i<dim;i++) {
      res[i] = vector<double>(dim);
   }
   return res;
}

vector<vector<double> > ident(int dim)
{
   vector<vector<double> > id = vector<vector<double> >(dim);
   for (int i=0;i<dim;i++) id[i] = e(i,dim);
   return(id);
}

vector<double> e(int i,int dim)
{
   vector<double> v = zero(dim);
   v[i] = 1.0;
   return(v);
}

vector<vector<double> > inverse(vector<vector<double> > a)
{
   int n = a.size();
   double tolerance = 1e-20;
//   if (abs(determinant(a))<tolerance) {
//      cout << "singular matrix!\n";
//      return zero_mat(n);
//   }
   LU l = LU(a);
   for (int j=0;j<n;j++) {
      vector<double> col = e(j,n);
      vector<double> v = lubksb(l,col);
      a[j] = v;
   }
   return(transpose(a));
}

#define TINY 1e-20;
LU::LU(vector<vector<double> > a)
{
   int n = a.size();
   vector<double> vv = vector<double>(n); // implicit scaling of each row
   indx_ = vector<int>(n);
   d_ = 1; // parity of row interchanges +/- 1
   for (int i=0;i<n;i++) { // loop to get implicit scaling info
      double big = 0.0;
      for (int j=0;j<n;j++) {
         double temp = fabs(a[i][j]);
         if (temp>big) big = temp;
      }
      if (big==0.0) cout << "Singular matrix in LU\n";
      vv[i] = 1.0/big;
   }
   for (int j=0;j<n;j++) { // loop over columns of Crout's method
      for (int i=0;i<j;i++) { // eq 2.3.12 except for i=j
         double sum = a[i][j];
         for (int k=0;k<i;k++) sum -= a[i][k]*a[k][j];
         a[i][j] = sum;
      }
      double big = 0.0; // search for largest pivot element
      int imax;
      for (int i=j;i<n;i++) {
         double sum = a[i][j];
         for (int k=0;k<j;k++) sum -= a[i][k]*a[k][j];
         a[i][j] = sum;
         double dum = vv[i]*fabs(sum);
         if (dum>=big) { // is figure of merit for pivot best so far
            big = dum;
            imax = i;
         }
      }
      if (j!=imax) { // do we need to interchange rows?
         for (int k=0;k<n;k++) {
            double dum = a[imax][k];
            a[imax][k] = a[j][k];
            a[j][k] = dum;
         }
         d_ *= -1; // change parity
         vv[imax] = vv[j]; // interchange scale factor
      }
      indx_[j] = imax;
      if (a[j][j]== 0.0) a[j][j] = TINY;
      if (j!=n) {
         double dum = 1.0/a[j][j];
         for (int i=j+1;i<n;i++) a[i][j] *= dum;
      }
   }
   mat_ = a;
}

vector<vector<double> > LU::mat() { return(mat_); }
vector<int> LU::indx() { return(indx_); }
int LU::d() { return(d_); }

vector<double> lubksb(LU l,vector<double> b)
{
   vector<vector<double> > a = l.mat();
   vector<int> indx = l.indx();
   int ii = -1;
   int n = a.size();
   for (int i=0;i<n;i++) {
      int ip = indx[i];
      double sum = b[ip];
      b[ip] = b[i];
      if (ii!=-1) for (int j=ii;j<i;j++) sum -= a[i][j]*b[j];
      else if (sum) ii = i;
      b[i] = sum;
   }
   for (int i=n-1;i>=0;i--) {
      double sum = b[i];
      for (int j=i+1;j<n;j++) sum -= a[i][j]*b[j];
      b[i] = sum/a[i][i];
   }
   return(b);
}

ostream& operator<<(ostream& out,vector<vector<int> > v){
   int n = v.size();
   for (int i=0;i<n;i++){
      out << v[i] << endl;
   }
   return out;
}
ostream& operator<<(ostream& out,vector<bool> v){
   int n = v.size();
   vector<int> val(n);
   for (int i=0;i<n;i++){
      if (v[i]) val[i] = 1;
      else val[i] = 0;
   }
   out << val;
   return out;
}

ostream& operator<<(ostream& out,vector<vector<complex<double> > > v){
   int n = v.size();
   for (int i=0;i<n;i++){
      out << v[i] << endl;
   }
   return out;
}

ostream& operator<<(ostream& out,vector<vector<double> > v){
   int n = v.size();
   for (int i=0;i<n;i++){
      out << v[i] << endl;
   }
   return out;
}

ostream& operator<<(ostream& out,vector<complex<double> > v){
   int n = v.size();
   out << "( ";
   for (int i=0;i<n;i++){
      out << v[i] << " ";
   }
   out << ")";
   return out;
}

ostream& operator<<(ostream& out,vector<double> v){
   int n = v.size();
   out << "( ";
   for (int i=0;i<n;i++){
      out << v[i] << " ";
   }
   out << ")";
   return out;
}

ostream& operator<<(ostream& out,vector<int> v){
   int n = v.size();
   out << "( ";
   for (int i=0;i<n;i++){
      out << v[i] << " ";
   }
   out << ")";
   return out;
}

vector<vector<int> > operator % (vector<int> a, vector<int> b)
{
   int n = a.size();
   int m = b.size();
   vector<vector<int> > c = vector<vector<int> >(n);

   for (int i=0;i<n;i++) {
      c[i] = vector<int>(m);
      for (int j=0;j<m;j++)
         c[i][j] = a[i]*b[j];
   }
   return(c);
}

vector<vector<double> > operator % (vector<double> a, vector<double> b)
{
   int n = a.size();
   int m = b.size();
   vector<vector<double> > c = vector<vector<double> >(n);

   for (int i=0;i<n;i++) {
      c[i] = vector<double>(m);
      for (int j=0;j<m;j++)
         c[i][j] = a[i]*b[j];
   }
   return(c);
}

vector<vector<double> > rotmat(vector<double> axis, double theta)
{
   axis = (1/norm(axis))*axis;
   double ct = cos(theta);
   vector<vector<double> > k = vector<vector<double> >(3);
   for (int j=0;j<3;j++) k[j] = vector<double>(3);
   k[0][0] = 0;        k[0][1] = -axis[2]; k[0][2] = axis[1];
   k[1][0] = axis[2];  k[1][1] = 0;        k[1][2] = -axis[0];
   k[2][0] = -axis[1]; k[2][1] = axis[0];  k[2][2] = 0;
   vector<vector<double> > r = ct*ident(3) + sin(theta)*k + (1-ct)*(axis%axis);
   return(r);
}

vector<double> rotate(vector<double> v, vector<double> axis, double theta)
{
   return(rotmat(axis,theta)*v);
}

double trace (vector<vector<double> > r)
{
   int n = r.size();
   double sum = 0;
   for (int i = 0;i<n;i++) sum += r[i][i];
   return(sum);
}

void rep_rot (vector<vector<double> > r, vector<double> *axis, double *theta)
{
   *theta = acos(0.5*(trace(r)-1));
   if (*theta==0.0) return;
   vector<vector<double> > m = (0.5/sin(*theta))*(r - transpose(r));
   (*axis)[0] = m[2][1];
   (*axis)[1] = m[0][2];
   (*axis)[2] = m[1][0];
}

double quad_form(vector<double> a,vector<vector<double> > M,vector<double> b)
{
   double sum = 0;
   int n = a.size();
   for (int i=0;i<n;i++)
      for (int j=0;j<n;j++)
         sum += a[i]* M[i][j] * b[j];
   return(sum);
}

double quad_form(vector<int> a,vector<vector<double> > M,vector<int> b)
{
   double sum = 0;
   int n = a.size();
   for (int i=0;i<n;i++)
      for (int j=0;j<n;j++)
         sum += a[i]* M[i][j] * b[j];
   return(sum);
}

vector<complex<double> > operator + (vector<complex<double> > &a,
                                     vector<complex<double> > &b)
{
   int n = a.size();
   vector<complex<double> > r = vector<complex<double> >(n);
   for (int i=0;i<n;i++) r[i] = a[i] + b[i];
   return(r);
}

vector<vector<double> > diag(vector<double> v)
{
   int n = v.size();
   vector<vector<double> > m = vector<vector<double> >(n);
   for (int i = 0; i<n; i++) {
      m[i] = vector<double>(n);
      m[i][i] = v[i];
   }
   return(m);
}

// returns [b, a] where y = b + ax is line of best fit through points (xi,yi)
vector<double> linefit(vector<double> x, vector<double> y)
{
   int n = x.size();
   double sum_x = 0;
   double sum_y = 0;
   double sum_xy = 0;
   double sum_x2 = 0;
   for (int i=0;i<n;i++) {
      double xi = x[i];
      double yi = y[i];
      sum_x += xi;
      sum_y += yi;
      sum_xy += xi*yi;
      sum_x2 += xi*xi;
   }
   double a = (sum_xy - sum_x*sum_y/(double)n) /
                  (sum_x2 - sum_x*sum_x/(double)n);
   double b = (sum_y - a*sum_x)/(double) n;
   vector<double> v = vector<double>(2);
   v[0] = b; v[1] = a; //intercept, slope
   return(v);
}

double angle (vector<double> v) { return angle(v[0],v[1]); }

double angle (double x, double y)
{
   if (x==0) {
      if (y>0) return 0.5*M_PI; else if (y<0) return 1.5*M_PI; else return 0;
   }
   else if (x>0) {
      if (y>=0) return atan(y/x); else return 2*M_PI + atan(y/x);
   }
   else return M_PI + atan(y/x);
}

// written by RAN :)
// function to calculate unit vector normal to two input vectors
vector<double> getNormal(const vector<double> &axis, const vector<double> &target)
{
   vector<double> cross = axis ^ target;    // calculates cross product of two vectors

   return ((1/norm(cross)) * cross);        // returns a unit vector
}

// written by RAN :)
// function to calculate angle between two input vectors
double getAngle(const vector<double> &axis, const vector<double> &target)
{
   vector<double> normal = getNormal(axis, target);
   return getAngle( axis, target, normal );
}

// written by RAN :)
// overloaded version of above function to calculate angle between two input vectors;
// also takes a user-defined normal vector
double getAngle(const vector<double> &axis, const vector<double> &target, const vector<double> &normal)
{
   // recall dot product of two vectors = length(1) * length(2) * cos(angle between vectors)
   double angle = acos( (axis * target) / (norm(axis) * norm(target)) );

   double sign = ( axis ^ target ) * normal;
   if ( sign > 0 ) {
      return angle;
   }
   else {
      return ( -1.0 * angle );
   }
}

vector<double> euler(vector<double> v)
{
   vector<double> a(2);
   a[0] = angle(v[0],-v[1]);
   a[1] = acos(v[2]/norm(v));
   return a;
}

int epsilon(int i,int j, int k)
{
   if (i==0) {
      if (j==1&&k==2) return 1;
      if (j==2&&k==1) return -1;
      return 0;
   }
   if (i==1) {
      if (j==2&&k==0) return 1;
      if (j==0&&k==2) return -1;
      return 0;
   }
   if (i==2) {
      if (j==0&&k==1) return 1;
      if (j==1&&k==0) return -1;
      return 0;
   }
}

vector<complex<double> > fourier(vector<double> & v,int n,vector<double> &s,
    vector<double> &c)
{
   vector<complex<double> > f(n);
   int np = v.size();
   for (int i=0;i<np;i++) {
      double value = v[i];
      for (int j=0;j<n;j++) {
         int index = (i*j)%np;
         f[j] += value*complex<double>(c[index],-1.*s[index]);
      }
   }

   for (int j=0;j<n;j++) {
      f[j] *= 1./(double)np;
   }

   return f;
}
vector<complex<double> > fourier2(vector<double> *v,int nf,vector<double> *s,
    vector<double> *c,vector<int> q)
{
   cout << "fourier2\n";
   int T = v->size();
   cout << " T = " << T << endl;
   vector<vector<double> > m(2*nf-1);
   for (int i=0;i<2*nf-1;i++) {
      m[i] = vector<double>(2*nf-1);
   }

// first equation: c0
   double Tp = q[0] - q[1]; if (Tp<0) Tp += T;
   m[0][0] = Tp;
   cout << "Tp = " << Tp << endl;
   double pi = 3.14159265;
   double factor = T/(2*pi);
   for (int n=1;n<nf;n++) {
      double factorn = factor/n; // T/(2pi*n)
      double x = Tp/factorn; // (2pi*n*Tp)/T
      m[0][2*n-1] = 2*factorn*sin(x);
      m[0][2*n] = -2*factorn*(1-cos(x));
// remaining equations: Re(c1), Im(c1) ... Re(cn), Im(cn)
      m[2*n-1][0] = factorn*sin(x);
      m[2*n][0] = factorn*(1-cos(x));
      for (int k=1;k<nf;k++) {
         double factor1 = factor/(n+k); // T/(2*pi*(n+k))
         double factor2 = factor/(n-k); // T/(2*pi*(n-k))
         double x1 = Tp/factor1; // 2*pi*(n+k)*Tp/T
         double x2 = Tp/factor2; // 2*pi*(n-k)*Tp/T
         double s1 = sin(x1);
         double s2 = sin(x2);
         double c1 = 1-cos(x1);
         double c2 = 1-cos(x2);
         if (n==k) {
            m[2*k-1][2*k-1] = factor1*s1 + Tp;
            m[2*k-1][2*k] = -factor1*c1;
            m[2*k][2*k-1] = -m[2*k-1][2*k];
            m[2*k][2*k] = factor1*s1 - Tp;
         }
         else {
            m[2*k-1][2*n-1] = factor1*s1+factor2*s2;
            m[2*k-1][2*n] = -(factor1*c1+factor2*c2);
            m[2*k][2*n-1] = factor1*c1-factor2*c2;
            m[2*k][2*n] =  factor1*s1-factor2*s2;
         }
      }
   }

   cout << " m = \n";
   cout << m << endl;
   vector<double> g(2*nf-1);
   for (int i=0;i<Tp;i++) {
      double value = (*v)[(i+q[1])%T];
      cout << i << ' ' << (i+q[1])%T << ' ' << value << endl;
      g[0] += value;
      for (int k=1;k<nf;k++) {
         g[2*k-1] += value * (*c)[(i*k)%T];
         g[2*k] += value * (*s)[(i*k)%T];
      }
   }

   cout << "g = " << g << endl;
   vector<double> p = inverse(m)*g;
   cout << "p = " << p << endl;

   vector<complex<double> > f(nf);
   f[0] = p[0];
   for (int k=1;k<nf;k++) {
      f[k] = complex<double>(p[2*k-1],p[2*k])*polar(1.,2*pi*k*q[0]/T);
   }
   return f;
}

// returns components of v *
vector<double> projection( vector<double> v, vector< vector<double> > space )
{
   return transpose(space)*(space*v);
}

int argmax(vector <double> v)
{
   int index = 0;
   double max = v[0];
   int n = v.size();
   for (int i=1;i<n;i++) {
      double value = v[i];
      if (value>max) {
     max = value;
     index = i;
      }
   }
   return index;
}

double fourier_sum(vector<complex<double> > f,double theta)
{
   int N = f.size();
   double fsum = abs(f[0]);
   for (int n=1;n<N;n++) {
      double cosine = cos(n*theta);
      double sine = sin(n*theta);
      fsum += 2*(f[n].real()*cosine - f[n].imag()*sine);
   }
   return fsum;
}

vector<vector<double> > rot_align(vector<double> v, vector<double> target)
{
   vector<double> axis = v ^ target;
   axis = (1/norm(axis))*axis;
   double theta = acos(1/(norm(v)*norm(target))*(v*target));
   return rotmat(axis,theta);
}

vector<vector<double> > eig2(vector<vector<double> > m)
{
   vector<vector<double> > e(2);

   double a = m[0][0];
   double b = m[0][1];
   double d = m[1][1];

   if (b==0) {
     e = ident(2); e[0].push_back(a); e[1].push_back(d);
     if (abs(d)>abs(a))
        { vector<double> temp = e[0]; e[0] = e[1]; e[1] = temp; }
     return e;
   }

   double q = 0.5*sqrt((a-d)*(a-d)+4*b*b);
   double r = 0.5*(a+d);
   vector<double> lambda(2);
   lambda[0] = r+q;
   lambda[1] = r-q;

   if (lambda[0]==lambda[1]) {
     e = ident(2);
     for (int i=0;i<2;i++) {
    e[i].push_back(lambda[i]);
     }
     return e;
   }

   for (int i=0;i<2;i++) {
      e[i] = vector<double>(2);
      e[i][0] = -b; e[i][1] = a-lambda[i];
      double n = norm(e[i]);
      if (n) e[i] = (1/n)*e[i];
      e[i].push_back(lambda[i]);
   }
  if (abs(lambda[0])<abs(lambda[1]))
     { vector<double> temp = e[0]; e[0] = e[1]; e[1] = temp; }

   return(e);
}

double determinant(vector<vector<double> > m)
{
   int n = m.size();
   if (n==1) return m[0][0];
   double sum = 0;
   vector<vector<double> > mp = kill_row(m,0);
   int p = 1;
   for (int c=0;c<n;c++,p*=-1) {
      sum += p*m[0][c]*determinant(kill_col(mp,c));
   }
   return sum;
}

vector<vector<double> > kill_row(vector<vector<double> > m, int r)
{
   int n = m.size();
   vector<vector<double> > result(n-1);
   for (int i=0;i<r;i++) {
      result[i] = m[i];
   }
   for (int i=r+1;i<n;i++) {
      result[i-1] = m[i];
   }
   return result;
}

vector<vector<double> > kill_col(vector<vector<double> > m, int c)
{
   return transpose(kill_row(transpose(m),c));
}


vector<double> linefit_or(vector<double> x, vector<double> y)
{
// linefit, plus iterative 3-sigma outlier rejection

   double nsigma_cut = 3;
   vector<double> result = linefit(x,y);
   int k = 0;
   bool done = false;
   while (!done) {
      done = true;
      int n = x.size();
      double b = result[0];
      double a = result[1];
      vector<double> e(n);
      double e2_sum = 0.;
      for (int i=0;i<n;i++) {
         double y_fit = a*x[i]+b;
         e[i] = abs(y_fit - y[i]);
//   cout << x[i] << ' ' << y[i] << ' ' << y_fit << ' ' << e[i] << endl;
         e2_sum += e[i]*e[i];
      }
      double sigma = sqrt(e2_sum/(double)n);
      cout << "iteration " << k++ << result << ' ' << n << ' ' << sigma << endl;
      double cut = nsigma_cut*sigma;
      vector<double> xp(0);
      vector<double> yp(0);
      for (int i=0;i<n;i++) {
         if (e[i]<cut) {
            xp.push_back(x[i]);
        yp.push_back(y[i]);
     }
     else {
            cout << "rejecting " << i << ' ' << x[i] << ' ' << y[i] << ' ' << e[i] << endl;
        done = false;
     }
      }
      x = xp;
      y = yp;
//      cout << n << endl;
      result = linefit(xp,yp);
      cout << result << endl;
   }

   return result;
}

vector<double> reject_outliers(vector<double> v)
{
   double nsigma_cut = 2.5;
   int k = 0;
   bool done = false;
   while (!done) {
      done = true;
      int n = v.size();
      vector<double> p = mean_and_sigma(v);
      double m = p[0];
      double s = p[1];
      vector<double> e(n);
      for (int i=0;i<n;i++) {
         e[i] = abs(m - v[i]);
      }
      cout << "iteration " << k++ << ' ' << p << ' ' << n << ' ' << endl;
      double cut = nsigma_cut*s;
//      cout << cut << endl;
      vector<double> vp(0);
      for (int i=0;i<n;i++) {
//   cout << i << ' ' << e[i] << endl;
         if (e[i]<cut) {
            vp.push_back(v[i]);
     }
     else {
            cout << "rejecting " << i << ' ' << v[i] << ' ' << e[i] << endl;
        done = false;
     }
      }
      v = vp;
//      cout << n << endl;
   }
   return v;
}

vector<double> mean_and_sigma(vector<double> v)
{
   int n = v.size();
   double sum=0;
   double sum2 = 0;
   for (int i=0;i<n;i++) {
      double value = v[i];
      sum += value;
      sum2 += value*value;
   }
   vector<double> result(2);
   double mean = sum/(double)n;
   result[0] = mean;
   result[1] = sqrt(sum2/(double)n - mean*mean);
   return result;
}

double max(vector<double> v)
{
   int n = v.size();
   double result = v[0];
   for (int i=1;i<n;i++) {
      if (v[i]>result) result = v[i];
   }
   return result;
}

double min(vector<double> v)
{
   int n = v.size();
   double result = v[0];
   for (int i=1;i<n;i++) {
      if (v[i]<result) result = v[i];
   }
   return result;
}
int max(vector<int> v)
{
   int n = v.size();
   int result = v[0];
   for (int i=1;i<n;i++) {
      if (v[i]>result) result = v[i];
   }
   return result;
}

int min(vector<int> v)
{
   int n = v.size();
   int result = v[0];
   for (int i=1;i<n;i++) {
      if (v[i]<result) result = v[i];
   }
   return result;
}

vector<double> sort(vector<double> v)
{
   int n = v.size();
   if (n<2) return v;
   for (int i=0;i<n;i++) {
      for (int j=0;j<n-1;j++) {
     if (v[j]>v[j+1]) swap(v[j],v[j+1]);
      }
   }
   return v;
}

vector<int> sort(vector<int> v)
{
   int n = v.size();
   if (n<2) return v;
   for (int i=0;i<n;i++) {
      for (int j=0;j<n-1;j++) {
     if (v[j]>v[j+1]) swap(v[j],v[j+1]);
      }
   }
   return v;
}

vector<int> ranks(vector<double> v)
{
   int n = v.size();
   vector<int> result(n);
   for (int i=0;i<n;i++) {
      result[i] = i;
   }
   if (n<2) return result;
   for (int i=0;i<n;i++) {
      for (int j=0;j<n-1;j++) {
     if (v[j]>v[j+1]) { swap(v[j],v[j+1]); swap(result[j],result[j+1]); }
      }
   }
   return result;
}

vector<int> histogram(vector<double> v,double bin_size)
{
   double m1 = min(v);
   double m2 = max(v);
   int n = v.size();
   int nbins = (int) ((m2-m1)/bin_size) + 1;
   vector<int> h(nbins);
   for (int i=0;i<n;i++) {
      int bin = (int) ((v[i]-m1)/bin_size);
      (h[bin])++;
   }

   return h;
}


vector<vector<double> > rank_reduce(vector<bool> dof,vector<vector<double> > m)
{
   int n = dof.size();
   int nm = m.size();
   for (int i=0;i<nm-n;i++) { dof.push_back(false); }
   int q = 0;
   for (int i=0;i<n;i++) if (dof[i]) q++;
   vector<int> map(q);
   int j = 0;
   for (int i=0;i<n;i++) {
      if (dof[i]) { map[j] = i; j++; }
   }
   vector<vector<double> > result(q);
   for (int j=0;j<q;j++) {
      result[j] = vector<double>(q);
      for (int k=0;k<q;k++) {
         result[j][k] = m[map[j]][map[k]];
      }
   }
   return result;
}

vector<double> rank_reduce(vector<bool> dof,vector<double> v)
{
   int n = dof.size();
   int nv = v.size();
   for (int i=0;i<nv-n;i++) { dof.push_back(false); }
   int q = 0;
   for (int i=0;i<n;i++) if (dof[i]) q++;
   vector<int> map(q);
   int j = 0;
   for (int i=0;i<n;i++) {
      if (dof[i]) { map[j] = i; j++; }
   }
   vector<double> result(q);
   for (int j=0;j<q;j++) {
      result[j] = v[map[j]];
   }
   return result;
}

vector<int> rank_reduce(vector<bool> dof,vector<int> v)
{
   int n = dof.size();
   int nv = v.size();
   for (int i=0;i<nv-n;i++) { dof.push_back(false); }
   int q = 0;
   for (int i=0;i<n;i++) if (dof[i]) q++;
   vector<int> map(q);
   int j = 0;
   for (int i=0;i<n;i++) {
      if (dof[i]) { map[j] = i; j++; }
   }
   vector<int> result(q);
   for (int j=0;j<q;j++) {
      result[j] = v[map[j]];
   }
   return result;
}

vector<double> expand(vector<bool> dof, vector<double> v_r)
{
   int n = dof.size();
   vector<double> result(n);
   int j = 0;
   for (int i=0;i<n;i++) {
      if (dof[i]) {
     result[i] = v_r[j];
     j++;
      }
   }
   return result;
}

double mean_angle(vector<vector<double> > pts, vector<double> c)
{
   vector<double> v = cm(pts) - c;

   return angle(v[0],v[1]);
}

vector<vector<double> > orthogonal_basis(vector<vector<double> > v)
{
   int m = v.size();
   int n = v[0].size();

   vector<vector<double> > u(0);
   for (int i=0;i<m;i++) {
      vector<double> w = v[i];
      if (i>0) {
         w = w - projection(w,u);
      }
      w = (1/norm(w))*w;
      u.push_back(w);
   }

   vector<vector<double> > e = ident(n);
   int j = 0;
   for (int i=m;i<n;i++) {
      bool independent = false;
      vector<double> w;
      vector<double> p;
      while (!independent) {
         w = e[j++];
         p = projection(w,u);
         if (norm(p)>0.01) independent = true;
      }
      w = w - p;
      w = (1/norm(w))*w;
      u.push_back(w);
   }
   return u;
}

double rnd_gaussian(double sd)
{
   double x1 = ran01();
   double x2 = ran01();
   double q1 = sd*sqrt(-2*log(x1));
   double q2 = 2*M_PI*x2;
   double y = q1*cos(q2);

   return y;
}




double ran01()
{
/*
   struct timeval tp;
   struct timezone tzp;
   double range = 2147483648.;
   double x;
   gettimeofday(&tp,&tzp);
   for (int i=0;i<=tp.tv_usec%1000;i++) x = random();
   return(x/range);
*/
    static bool initialized = false;
    if (!initialized)
    {
        srand(time(0));
        initialized = true;
    }

    return double(rand())/RAND_MAX;
}


double binomial (int n, int k, double p)
{
   double result = 1;
   double p1 = 1-p;
   for (int i=1;i<=k;i++) { result *= (n+1-i)*p/(double)i; }
   for (int i=0;i<n-k;i++) { result *= p1; }
   return result;
}


vector<vector<double> > matrix_fracture(vector<vector<double> > m, int index)
{
   vector<double> v = m[index];
   double q = v[index];
   int n = m.size();
   vector<bool> dof(n,true);
   dof[index] = false;
   m = rank_reduce(dof,m);
   v = rank_reduce(dof,v);
   v.push_back(q);
   m.push_back(v);

   return m;
}

vector<double> gaussian_fit(vector<vector<double> > v)
{
   int n = v.size();
   vector<double> x(n);
   vector<double> y(n);
   vector<double> p;
   for (int i=0;i<n;i++) {
      p = v[i];
      x[i] = p[0];
      y[i] = p[1];
   }
   return gaussian_fit(x,y);
}

vector<double> gaussian_fit(vector<double> x,vector<double> y)

// input: vectors of x and y values
// output: (A,m,s) to minimize squared difference between logy and logA - (x-m)^2/(2s^2)
//        i.e. y = A*exp(-(x-m)^2/(2s^2))
//        for small differences, (log y - log y')^2 ~ [(y-y')/y]^2
//        Note that differences in the tails are weighted heavily, so
//        algorithm is best used for region near peak.
//        Also note that algorithm does not account for background intensity (baseline)

{
   int n = x.size();

   double ymax = y[0];
   int imax = 0;
   for (int i=1;i<n;i++) {
      double val = y[i];
      if (val>ymax) { ymax = val; imax = i; }
   }

   int start = imax - 2;
   if (start<0) start = 0;
   int end = start + 4;
   if (end>=n) { end = n-1; }

   int np = end - start + 1;

   vector<double> xp(np);
   vector<double> logy(np);
   for (int i=start;i<=end;i++) {
      xp[i-start] = x[i];
      logy[i-start] = log(y[i]);
   }

   double xsum = 0;
   for (int i=0;i<np;i++) {
      xsum += xp[i];
   }
   xsum = xsum/(double)np;
   vector<double> shift(np,xsum);

   vector<double> abc = parabola_fit(xp-shift,logy);
   double a = abc[0];
   double b = abc[1];
   double c = abc[2];
   double sigma;
   if (a>=0) sigma = 0;
   else sigma = sqrt(-1/(2*a));
   double m = xsum - b/(2*a);
   double A = exp(c - b*b/(4*a));
   vector<double> Ams(4);
   Ams[0] = A;
   Ams[1] = m;
   Ams[2] = sigma;

   vector<double> ye(n);
   for (int i=0;i<n;i++) {
      double d = x[i] - m;
//      cout << x[i] << ' ' << y[i] << ' ' << A*exp(-d*d/(2*sigma*sigma)) << endl;
      if (sigma==0) ye[i] = A;
      else ye[i] = A*exp(-d*d/(2*sigma*sigma));
   }
   Ams[3] = (y*ye)/sqrt((y*y)*(ye*ye));
//   cout << endl;

   return Ams;
}

// input: vectors of x and y values
// output: (a,b,c) to minimize squared difference between y and ax^2 + b^x + c

vector<double> parabola_fit(vector<double> xv, vector<double> yv)
{
   int n = xv.size();
   double xsum = 0;
   double x2sum = 0;
   double x3sum = 0;
   double x4sum = 0;
   double ysum = 0;
   double yxsum = 0;
   double yx2sum = 0;
   for (int i=0;i<n;i++) {
      double x = xv[i];
      double x2 = x*x;
      double x3 = x2*x;
      double x4 = x3*x;
      double y = yv[i];
      double yx = y*x;
      double yx2 = y*x2;
      xsum += x;
      x2sum += x2;
      x3sum += x3;
      x4sum += x4;
      ysum += y;
      yxsum += yx;
      yx2sum += yx2;
   }
   vector<vector<double> > m = zero_mat(3);
   vector<double> v(3);
   m[0][0] = x4sum;
   m[0][1] = m[1][0] = x3sum;
   m[0][2] = m[1][1] = m[2][0] = x2sum;
   m[1][2] = m[2][1] = xsum;
   m[2][2] = n;
   v[0] = yx2sum;
   v[1] = yxsum;
   v[2] = ysum;
   vector<double> abc = inverse(m)*v;

   for (int i=0;i<n;i++) {
      double x = xv[i];
//      cout << xv[i] << ' ' << yv[i] << ' ' << abc[0]*x*x+abc[1]*x+abc[2] << endl;
   }
//   cout << endl;
   return abc;
}

vector<int> reorder(vector<int> v)
{
   int n = v.size();
   int index = v[0];
   for (int i=0;i<n;i++) {
      if (v[i]>index) v[i]--;
   }

   vector<bool> dof(n,true);
   dof[0] = false;
   return rank_reduce(dof,v);
}

void concatenate(vector<vector<int> > &v1, vector<vector<int> > &v2)
{
   int n = v2.size();
   for (int i=0;i<n;i++) {
      v1.push_back(v2[i]);
   }
}

void concatenate(vector<int> &v1, vector<int> &v2)
{
   int n = v2.size();
   for (int i=0;i<n;i++) {
      v1.push_back(v2[i]);
   }
}

void concatenate_all(int q, vector<vector<int> > &v2)
{
   int nv = v2.size();
   for (int i=0;i<nv;i++) {
      v2[i].push_back(q);
   }
}

vector<vector<double> > permute (vector<vector<double> > K, vector<int> order)
{
   int n = K.size();
   vector<vector<double> > result(n);
   for (int i=0;i<n;i++) {
      result[i] = vector<double>(n);
      for (int j=0;j<n;j++) {
         result[i][j] = K[order[i]][order[j]];
      }
   }
   return result;
}

vector<double> permute (vector<double> v, vector<int> order)
{
   int n = v.size();
   vector<double> result(n);
   for (int i=0;i<n;i++) {
      result[i] = v[order[i]];
   }
   return result;
}
