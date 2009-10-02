#ifndef _CMercury8_H
#define _CMercury8_H

#include <cstdio>
#include <cstdlib>
#include <cmath>
#include <cstring>
#include "ctype.h"
#include <ctime>
#include <map>
#include "mercury.h"
#include "FFT.h"
using namespace std;
	struct string_less
		: public binary_function<string, string, bool>
	{	// functor for operator<
	bool operator()(const string& _Left, const string& _Right) const
		{	// apply operator< to operands
		return (_Left.compare(_Right)) < 0;
		}
	};

typedef struct
{
	string  Symbol;	/* Elemental symbol */
   int	 NumIsotopes;	/* Number of stable isotopes */
   vector<float> IsoMass;	/* Array of isotopic masses */
   vector<int>   IntMass;	/* Array of integer isotopic masses */
   vector<float> IsoProb;	/* Array of isotopic probabilities */ 
} Atomic5;

class CMercury8 {
 public:

  //Data Members:
  map<string, Atomic5, string_less> Element;
  map<string, int, string_less> AtomicNum;
  bool showOutput;
  bool bAccMass;
  bool bRelAbun;
  vector<int> EnrichAtoms;
	double monoMass;
	double zeroMass;

  //Functions:
  void AccurateMass(int);
  void AddElement(string,long);
  void CalcFreq(complex*, long, int, long);
  void CalcMassRange(int*, double, int, int);
  void CalcVariances(double*, double*);
  void CalcWeights(float&,double&,double&,long&,long&,long&,int&);
  void ConvertMass(complex*, int, int, float, float, long, long, int, double, double);
  void GetPeaks(complex*, int, vector<Result>&, int, int);
  void MassToInt(complex*, int);
  void Mercury(int);
  int ParseMF(const char[]);
  void RelativeAbundance(vector<Result>&);
 
 public:
  //Data Members:
  vector<Result> FixedData;
	vector<Result> FracAbunData;

  //Constructors & Destructors:
  CMercury8();
  ~CMercury8();

  //Functions:
  void AccMass(bool);
  void Echo(bool);
  void Enrich(int,int,double d=0.99);
	double getMonoMass();
	double getZeroMass();
  int GoMercury(char*, int=1);
  void Intro();
  void RelAbun(bool);
  void Reset();

};

#endif
