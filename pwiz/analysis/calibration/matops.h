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

#ifndef MATOPS
#define MATOPS

#include <complex>
#include <vector>
#include <fstream>
#include <iostream>

using namespace std;

vector<int> d2i(vector<double>);
vector<double> i2d(vector<int>);
vector<vector<int> > d2i(vector<vector<double> >);
vector<vector<double> > i2d(vector<vector<int> >);

bool operator == (vector<double> &,vector<double> &);
bool operator == (vector<vector<double> > &, vector<vector<double> > &);
vector<vector<double> > eig(vector<vector<double> >);
vector<vector<double> > eig2(vector<vector<double> >);
vector<double> cm(vector<vector<double> > positions, vector<double> weights = vector<double>(0));
vector<vector<double> > cov(vector<vector<double> >, vector<double> = vector<double>(0));
double mean_angle(vector<vector<double> > pts, vector<double> c);
vector<double> operator + (vector<double>,vector<double>);
void operator += (vector<int>&, vector<int>);
void operator += (vector<double>&, vector<double>);
void operator += (vector<complex<double> >&, vector<complex<double> >);
void operator *= (vector<double>&, double);
vector<int> operator + (vector<int>,vector<int>);
vector<int> operator - (vector<int>,vector<int>);
vector<double> operator - (vector<double>,vector<double>);
vector<vector<double> >operator +(vector<vector<double> >,vector<vector<double> >);
vector<vector<complex<double> > > operator +
   (vector<vector<complex<double> > >&,vector<vector<complex<double> > >&);
void operator += (vector<vector<double> >&, vector<vector<double> >);
void operator += (vector<vector<complex<double> > >&,
        vector<vector<complex<double> > >&);
vector<vector<double> >operator -(vector<vector<double> >,vector<vector<double> > );
double operator * (vector<double>,vector<double>); // inner product
double operator * (vector<complex<double> >,vector<complex<double> >); // inner product
int operator * (vector<int>,vector<int>); // inner product
double operator * (vector<int>,vector<double>); // inner product
vector<vector<double> > operator % (vector<double>,vector<double>); //outer prod
vector<vector<int> > operator % (vector<int>,vector<int>); //outer prod
vector<double> operator * (double,vector<double>); // scalar mult
vector<complex<double> > operator * (double,vector<complex<double> > );
vector<int> operator * (int,vector<int>); // scalar mult
vector<double> operator * (double,vector<int>); // scalar mult
vector<vector<double> > operator * (double,vector<vector<double> >); //scal mult
vector<vector<double> > operator * (double,vector<vector<int> >); //scal mult
vector<double> operator * (vector<vector<double> >,vector<double>); // mat * vect
vector<double> operator * (vector<vector<double> >,vector<int>); // mat * vect
vector<int> operator * (vector<vector<int> >,vector<int>); // mat * vect
vector<vector<double> > operator *
      (vector<vector<double> >,vector<vector<double> >); // mat * mat
vector<vector<complex<double> > > operator *
      (vector<vector<complex<double> > >,vector<vector<complex<double> > >); // mat * mat
vector<vector<double> > transpose(vector<vector<double> >);
vector<vector<int> > transpose(vector<vector<int> >);
vector<vector<complex<double> > > transpose(vector<vector<complex<double> > >);
double norm(vector<double>);
double norm(vector<int>);
vector<double> operator ^ (vector<double>,vector<double>); // cross product
vector<double> e(int,int);
vector<vector<double> > inverse(vector<vector<double> >);
ostream& operator<<(ostream& out,vector<vector<complex<double> > > v);
ostream& operator<<(ostream& out,vector<complex<double> > v);
ostream& operator<<(ostream& out,vector<vector<double> > v);
ostream& operator<<(ostream& out,vector<vector<int> > v);
ostream& operator<<(ostream& out,vector<double> v);
ostream& operator<<(ostream& out,vector<int> v);
ostream& operator<<(ostream& out,vector<bool> v);
vector<vector<double> > ident(int);
vector<double> zero(int);
vector<vector<double> > zero_mat(int);
vector<vector<double> > rotmat(vector<double>,double);
vector<double> rotate(vector<double>,vector<double>,double);
vector<vector<double> > rot_align(vector<double> v, vector<double> target);
double trace(vector<vector<double> >);
void rep_rot(vector<vector<double> >,vector<double> *, double *);
double quad_form(vector<double>,vector<vector<double> >,vector<double>);
double quad_form(vector<int>,vector<vector<double> >,vector<int>);
vector<vector<double> > diag(vector<double>);
vector<double> euler(vector<double>); // theta, phi
int epsilon(int,int,int);
vector<vector<double> > kill_row(vector<vector<double> >, int);
vector<vector<double> > kill_col(vector<vector<double> >, int);
double determinant(vector<vector<double> >);

vector<double> getNormal(const vector<double> &, const vector<double> &);
double getAngle(const vector<double> &, const vector<double> &);
double getAngle(const vector<double> &, const vector<double> &, const vector<double> &normal);

vector<complex<double> > operator + (vector<complex<double> >&,
                                     vector<complex<double> >&);
vector<double> projection(vector<double> v, vector< vector<double> > space);
vector<vector<double> > matrix_fracture(vector<vector<double> >, int);
vector<vector<double> > rank_reduce(vector<bool> dof, vector<vector<double> >);
vector<double> rank_reduce(vector<bool> dof, vector<double>);
vector<int> rank_reduce(vector<bool> dof, vector<int>);
vector<vector<double> > matrix_fracture(vector<vector<double> >,int);
vector<double> expand(vector<bool> dof, vector<double>);
double ran01();
double rnd_gaussian(double sd=1);
vector<double> mean_and_sigma(vector<double> v);
vector<double> reject_outliers(vector<double>);
vector<int> histogram(vector<double>,double bin_size=1.0);
vector<int> sort(vector<int>);
vector<double> sort(vector<double>);
vector<int> ranks(vector<double>);
double max(vector<double> v);
double min(vector<double> v);
int max(vector<int> v);
int min(vector<int> v);
vector<int> reorder(vector<int>);
vector<vector<double> > permute(vector<vector<double> >, vector<int>);
vector<double> permute(vector<double>, vector<int>);

class LU {
   vector<vector<double> > mat_;
   vector<int> indx_;
   int d_;
public:
   LU(vector<vector<double> >);
   vector<vector<double> > mat();
   vector<int> indx();
   int d();
};


vector<double> lubksb(LU,vector<double>);
vector<double> linefit(vector<double>,vector<double>);
vector<double> linefit_or(vector<double>,vector<double>); // outlier rejection
vector< vector<double> > axisfit( vector< vector<double> > p );
double angle(double,double);
double angle(vector<double>);

vector<complex<double> > fourier(vector<double> &v, int n, vector<double> &s,
                                 vector<double> &c);
vector<complex<double> > fourier2(vector<double> *v, int n, vector<double> *s,
                                 vector<double> *c,vector<int> q);
double fourier_sum(vector<complex<double> > f, double x);
vector<double> parabola_fit (vector<double> x,vector<double> y);
vector<double> gaussian_fit (vector<double> x,vector<double> y);
vector<double> gaussian_fit (vector<vector<double> > v);
int argmax (vector<double>);

vector<double> splined(vector<double> &);
double spline(vector<double> &f, vector<double> &d2f, double x);
vector<vector<double> > spline_dy2(vector<vector<double> > &);
double spline2 (vector<vector<double> > &f, vector<vector<double> > &d2f_dx2,
        double x, double y);
vector<double> dspline(vector<double> &, vector<double> &, double);
vector<double> dspline2(vector<vector<double> > &f, vector<vector<double> > &ft,
        vector<vector<double> > &dx2, vector<vector<double> > &dy2,
        double, double);
vector<double> circle_fit(vector<vector<double> > &, vector<double>);
vector<double> circle_fit_or(vector<vector<double> > &, vector<double>);
vector<vector<double> > orthogonal_basis(vector<vector<double> >);
double binomial (int n, int k, double p);
void concatenate(vector<vector<int> > &a, vector<vector<int> > &b);
void concatenate(vector<int> &a, vector<int>  &b);
void concatenate_all(int a, vector<vector<int> > &b);
vector<vector<int> > partition(int balls, int urns, int min, int max);
vector<vector<int> > partition2(int balls, int urns, int min, int max);
#endif
