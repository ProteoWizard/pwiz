#include "stdafx.h"
/*=====================================================================*/
/* Program MERCURY2.C                                                  */
/*                                                                     */
/* MERCURY5 is a version of MERCURY2 using (mostly) double percision.  */
/* instead of floating point arithmatic. It gives more accurate        */
/* intensity values than MERCURY2.                                     */
/* MERCURY2 is an integer based version of MERCURY, although most of   */
/* the arithmetic is floating point. Using integer (changed to float)  */
/* values for isotopic masses, the calculation can be performed with   */
/* a much smaller data set and is extremely fast.  The ASCII output    */
/* file is a stick representation of the mass spectrum. There is no    */
/* ultrahigh resolution mode in this program.                          */
/*                                                                     */
/* Algorithm by       Alan L. Rockwood                                 */
/* Programming by     Steve Van Orden                                  */
/*=====================================================================*/

/*=====================================================================*/
/* C++ implementation (CMercury5) by Michael Hoopmann, 2004            */
/*                                                                     */
/* To use:                                                             */
/*   1. Create CMercury5 object.                                       */
/*   2. Call GoMercury(formula, [optional] charge, [optional] filename)*/
/*   3. Optionally call Echo(true) to display output to screen.        */
/*                                                                     */
/* Example:                                                            */
/*   #include "CMercury5.h"                                            */
/*   using namespace std;                                              */
/*   int main(){                                                       */
/*     CMercury5 dist;                                                 */
/*     dist.Echo(true);                                                */
/*     dist.GoMercury("C6H12O6",1);                                    */
/*     return 0;                                                       */
/*   };                                                                */
/*                                                                     */
/*  Including the optional filename to GoMercury outputs the           */
/*  distribution to file. The data can be manipulated in code in the   */
/*  FixedData vector. See the header files CMercury5.h and mercury.h   */
/*  for struct type.                                                   */
/*=====================================================================*/
 
#include "CMercury8.h"
#include <cmath>
#include <iostream>
using namespace std;

CMercury8::CMercury8(){
  showOutput = false;
  bAccMass = false;
  bRelAbun = true;
	zeroMass=0;
};

CMercury8::~CMercury8(){
};

void CMercury8::Echo(bool b){
  showOutput = b;
};
  

/*************************************************/
/* FUNCTION Intro - called by main()             */
/*************************************************/
void CMercury8::Intro()
{

   printf("*********************************************************************\n");
   printf("*                       M E R C U R Y  V I I I                      *\n");
   printf("*                                                                   *\n");
   printf("*  An Integer based Fourier transform isotopic distibution program  *\n");
   printf("*          Now capable of calculating accurate masses!              *\n");
   printf("*********************************************************************\n");
   printf("\n");
   printf("      Algorithm by : Alan L. Rockwood\n\n");
   printf("      Program by   : Steven L. Van Orden - 1\n");
   printf("                     Michael R. Hoopmann - 2\n\n");
   printf("      Developed at : 1 - Pacific Northwest Laboratories / \n");
   printf("                         Battelle Northwest\n");
   printf("                         in the laboratory of Richard D. Smith\n");
   printf("                     2 - University of Washington\n");
   printf("                         Department of Genome Science\n");
   printf("                         Michael J. MacCoss laboratory\n\n\n");
 
}  /* End of Intro() */

/*************************************************/
/* FUNCTION CalcVariances - called by main()     */
/*************************************************/
void CMercury8::CalcVariances(double *MolVar, double *IntMolVar){
  int i, j, Z;
  double Var, IntVar;
  double avemass, intavemass;
  
  *MolVar = *IntMolVar = 0;
  zeroMass=0;
  for (map<string, int, string_less>::iterator it = AtomicNum.begin(); it != AtomicNum.end(); it++) {
	Atomic5 atomic5 = Element[it->first];
	avemass = intavemass = 0;
    for (j=0; j<atomic5.NumIsotopes; j++){
      avemass += atomic5.IsoMass[j] * atomic5.IsoProb[j];
      intavemass += atomic5.IntMass[j] * atomic5.IsoProb[j];
    };
    Var = IntVar = 0;
    for (j=0; j<atomic5.NumIsotopes; j++){
      Var += (atomic5.IsoMass[j] - avemass) * (atomic5.IsoMass[j] - avemass) * atomic5.IsoProb[j];
      IntVar += (atomic5.IntMass[j] - intavemass) * (atomic5.IntMass[j] - intavemass) * atomic5.IsoProb[j];
    };
    *MolVar += it->second * Var;
    *IntMolVar += it->second * IntVar;
	zeroMass+=(atomic5.IsoMass[0] * it->second);
  };
	monoMass=zeroMass;  
};  /* End of CalcVariances() */

/*************************************************/
/* FUNCTION CalcMassRange - called by main()     */
/*************************************************/
void CMercury8::CalcMassRange(int *MassRange, double MolVar, int charge, int type) {
   int i;
   double dPoints;
 
   //This is insufficient without adding the one to the end
   if ((type == 1) || (charge == 0)) dPoints = (sqrt(1+MolVar)*10);
   else  dPoints = (sqrt(1+MolVar)*10/charge);  /* +/- 5 sd's : Multiply charged */

   /* Set to nearest (upper) power of 2 */
   for (i=1024; i>0; i/=2) {
     if (i < dPoints) {
       *MassRange = i * 2 * 2;   //MRH: Added extra power of 2 since this rule is often insufficient
       i = 0;
     };
   };
   
}  /* End of CalcMassRange() */
 
/*************************************************/
/* FUNCTION AddElement - called by ParseMF()     */
/*************************************************/

//Atom is the atomic abbreviation
//Acount is the number of atoms of the element in the formula
void CMercury8::AddElement(std::string Atom, long Acount) {
	map<string,int, string_less>::iterator it = AtomicNum.find(Atom);
	if (it != AtomicNum.end()) {
		AtomicNum[Atom] = it->second + Acount;
	} else {
		AtomicNum[Atom] = Acount;
	}
}
 
/*************************************************/
/* FUNCTION ParseMF - called by main()           */
/*************************************************/
//Return codes
//	0: Success
//	-1: Invalid character
int CMercury8::ParseMF(const char MF[])
{
   int  i, COND, ERRFLAG;
   long atomcount;
   string Atom;
   char ch, errorch;

   atomcount=0; COND=0; ERRFLAG=0;
   for (i=0; i<=strlen(MF); i++) {
     
     ch = MF[i];
     if (isspace(ch)) ch = 0;
     switch (COND) {
       
     case 0: 
       if (isupper(ch)) {
	 Atom += ch;
	 COND = 1;
       } else {
	 COND = -1;
	 errorch = ch;
       };
       break;  /* out of case 0 */
       
     case 1: 
       if (isupper(ch)) {
	 AddElement(Atom,1);
	 Atom = "";
	 COND = 0;
	 i--;
       } else {
	 if (islower(ch)) {
	   Atom+= ch;
	 } else {
	   if (isdigit(ch)) {
	     atomcount = atoi(&ch);
	     COND = 3;
	   } else {
	     if (ch == 0) { 
	       AddElement(Atom,1);
	     } else {
	       COND = -1;
	       errorch = ch;
	     };
	   };
	 };
       };
       break;  /* out of case 1 */
       
     case 2: 
       if (isupper(ch)) {
	 AddElement(Atom,1);
	 Atom[0] = ch; Atom[1] = 0;
	 COND = 1;
       } else {
	 if (isdigit(ch)) {
	   atomcount = atoi(&ch);
	   COND = 3;
	 } else {
	   if (ch == 0) {
	     AddElement(Atom,1);
	   } else {
	     COND = -1;
	     errorch = ch;
	   };
	 };
       };
       break;  /* out of case 2 */
       
     case 3: 
       if (isupper(ch)) {
	 AddElement(Atom,atomcount);
	 atomcount = 0;
	 Atom = "";
	 COND = 0;
	 i--;
       } else {
	 if (isdigit(ch)) {
	   atomcount = atomcount*10+atoi(&ch);
	 } else {
	   if (ch == 0) {
	     AddElement(Atom,atomcount);
	     COND = 0;
	   } else {
	     COND = -1;
	     errorch = ch;
	   };
	 };
       };
       break;  /* out of case 3 */
       
     case -1: 
       ERRFLAG = -1;
       i = strlen(MF) + 1;  /* skip out of loop */
       break;  /* out ot case -1 */
       
     };  /* end switch */
   };  /* end for i ... */

   if (ERRFLAG) {
     printf("\nError in format of input...  The character '%c' is invalid\n",errorch);
     return(-1);
   } else {
	   return(0);
   };

};
 
/*************************************************/
/* FUNCTION CalcFreq - called by main()          */
/*    Could be done with less code, but this     */
/*    saves a few operations.                    */
/*************************************************/
void CMercury8::CalcFreq(complex* FreqData, long NumPoints, int MassRange, long MassShift) {
  
  int    i, j, k, Z;
  double real, imag, freq, X, theta, r, tempr;
  double a, b, c, d;
 
  vector<Atomic5> atomTypes;
  vector<int> atomQuantities;
  for (map<string, int, string_less>::iterator it = AtomicNum.begin(); it != AtomicNum.end(); it++) {
	  atomTypes.push_back(Element[it->first]);
	  atomQuantities.push_back(it->second);
  }

  /* Calculate first half of Frequency Domain (+)masses */
  for (i=0; i<NumPoints/2; i++) {
    
    freq = (double)i/MassRange;
    r = 1;
    theta = 0;
	for (int iAtom = 0; iAtom < atomTypes.size(); iAtom++) {
	  Atomic5 atomic5 = atomTypes[iAtom];
      real = imag = 0;
      for (k=0; k<atomic5.NumIsotopes; k++) {
				X = TWOPI * atomic5.IntMass[k] * freq;
				real += atomic5.IsoProb[k] * cos(X);
				imag += atomic5.IsoProb[k] * sin(X);
      };
      
      /* Convert to polar coordinates, r then theta */
      tempr = sqrt(real*real+imag*imag);
      r *= pow(tempr,(double)atomQuantities[iAtom]);
      if (real > 0) theta += atomQuantities[iAtom] * atan(imag/real);
      else if (real < 0) theta += atomQuantities[iAtom] * (atan(imag/real) + PI);
      else if (imag > 0) theta += atomQuantities[iAtom] * HALFPI;
      else theta += atomQuantities[iAtom] * -HALFPI;
      
    };  /* end for(j) */
    
    /* Convert back to real:imag coordinates and store */
    a = r * cos(theta);
    b = r * sin(theta);
    c = cos(TWOPI*MassShift*freq);
    d = sin(TWOPI*MassShift*freq);
    FreqData[i].real = a*c - b*d;
    FreqData[i].imag = b*c + a*d;
    
  };  /* end for(i) */
  
  /* Calculate second half of Frequency Domain (-)masses */
  for (i=NumPoints/2; i<NumPoints; i++) {
    
    freq = (double)(i-NumPoints)/MassRange;
    r = 1;
    theta = 0;
    for (int iAtom = 0; iAtom < atomTypes.size(); iAtom++) {
      Atomic5 atomic5 = atomTypes[iAtom];
      real = imag = 0;
      for (k=0; k < atomic5.NumIsotopes; k++) {
				X = TWOPI * atomic5.IntMass[k] * freq;
				real += atomic5.IsoProb[k] * cos(X);
				imag += atomic5.IsoProb[k] * sin(X);
      };
      
      /* Convert to polar coordinates, r then theta */
      tempr = sqrt(real*real+imag*imag);
      r *= pow(tempr,(double)atomQuantities[iAtom]);
      if (real > 0) theta += atomQuantities[iAtom] * atan(imag/real);
      else if (real < 0) theta += atomQuantities[iAtom] * (atan(imag/real) + PI);
      else if (imag > 0) theta += atomQuantities[iAtom] * HALFPI;
      else theta += atomQuantities[iAtom] * -HALFPI;
      
    };  /* end for(j) */
    
    /* Convert back to real:imag coordinates and store */
    a = r * cos(theta);
    b = r * sin(theta);
    c = cos(TWOPI*MassShift*freq);
    d = sin(TWOPI*MassShift*freq);
    FreqData[i].real = a*c - b*d;
    FreqData[i].imag = b*c + a*d;
    
  };  /* end of for(i) */
  
}  /* End of CalcFreq() */
  
//This function resets all atomic states to those at initialization.
//This is so the same object can be reused after performing a calculation
//or after user intervention, such as Enrich().
void CMercury8::Reset(){
  
	AtomicNum.clear();  
};

//This function calculates the accurate mass. It's a bit slower, and it performs poorly
//on the fringes of large proteins because of the estimation of MassRange algorithm, and the
//fact that abundances would be so small as to be nearly indistinguishable from zero.
void CMercury8::AccurateMass(int Charge){
  int 	 i,j, k;
  int	 MassRange;
  int   PtsPerAmu;
  long  NumPoints;			/* Working # of datapoints (real:imag) */
  complex *FreqData;              /* Array of real:imaginary frequency values for FFT */
  float MW;
  double MIMW, tempMW, MolVar, IntMolVar;
  long intMW, MIintMW;
  long dummyLong;
  int dummyInt;


  complex *AltData;
  complex *AltData2;
  long MaxIntMW;
  long PMIintMW;
  int IsoShift;

  Result r;
  vector<Result> vParent;
  vector<Result> vProduct;
  
  //If we made it this far, we have valid input, so calculate molecular weight
  CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, MaxIntMW, IsoShift);

  //If the user specified an Echo, output the data now.
  if (showOutput){
    if (Charge != 0) {
      printf("Average Molecular Weight: %.3lf, at m/z: %.3f\n",MW,MW/fabs((double)Charge));
      printf("Average Integer MW: %ld, at m/z: %.3f\n\n",intMW,(float)intMW/fabs((double)Charge));
    } else {
      printf("Average Molecular Weight: %.3lf\n",MW);
      printf("Average Integer MW: %ld\n\n",intMW);
    };
  };

  //Set our parental molecular weight. This will be used as the lower bounds.
  //PMIintMW = (int)round(MIMW);
  PMIintMW = (int)(MIMW+0.5);
  
  //Calculate mass range to use based on molecular variance 
  CalcVariances(&MolVar,&IntMolVar);
  CalcMassRange(&MassRange,MolVar,1,1);
  PtsPerAmu = 1;
  
  //Allocate memory for Axis arrays
  NumPoints = MassRange * PtsPerAmu;
  FreqData = new complex[NumPoints];
  
  //Start isotope distribution calculation
  //MH notes: How is this different from using -MW instead of -intMW?
  CalcFreq(FreqData,NumPoints,MassRange,-intMW);
  FFT(FreqData,NumPoints,false);

  //Converts complex numbers back to masses
  ConvertMass(FreqData,NumPoints,PtsPerAmu,MW,tempMW,intMW,MIintMW,1,MolVar,IntMolVar);

  //Put our data in the global array
  FixedData.clear();
  for(j=NumPoints/2; j<NumPoints; j++){
    r.data=FreqData[j].real;
    r.mass=FreqData[j].imag;
    FixedData.push_back(r);
  };

  for(j=0; j<NumPoints/2; j++){
    r.data=FreqData[j].real;
    r.mass=FreqData[j].imag;
    FixedData.push_back(r);
  };

  //Convert the data to integers
  MassToInt(FreqData,NumPoints);

  //Shift the lower bound to reflect the range of points that will be in common
  //with all product variants. This is necessary for proteins in which the monoisotopic
  //mass is not visible, thus we must adjust for the first data point in each product
  //if(FixedData.at(0).mass > (PMIintMW+IsoShift)) PMIintMW=(int)round(FixedData.at(0).mass)+IsoShift;
  if(FixedData[0].mass > (PMIintMW+IsoShift)) PMIintMW=(int)(FixedData[0].mass+0.5)+IsoShift;

  //Reduce the parent set to this new boundary
  GetPeaks(FreqData,NumPoints,vParent,PMIintMW,MaxIntMW);

  //Set the upper bound to the max determined in the parent distribution
  //MaxIntMW = (int)round(vParent.at(vParent.size()-1).mass);
  MaxIntMW = (int)(vParent[vParent.size()-1].mass+0.5);

  //set parental masses to 0
  for(i=0;i<vParent.size();i++) vParent[i].mass=0;

  //We have now completed the analysis on the actual protein.
  //Next we compute compositions for each element and each isotope
  for(map<string, int, string_less>::iterator it = AtomicNum.begin(); it != AtomicNum.end(); it++){
    Atomic5 atomic5 = Element[it->first];
    //Subtract one atom
    it->second--;

    //Calculate the product masses and variances
    //However, use the same mass range as the parent (calculated above)
    CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, dummyLong, dummyInt);
    CalcVariances(&MolVar,&IntMolVar);

    //Allocate memory for Axis arrays
    AltData = new complex[NumPoints];
    AltData2 = new complex[NumPoints];
    
    //Start isotope distribution calculation
    CalcFreq(AltData,NumPoints,MassRange,-intMW);
    FFT(AltData,NumPoints,false);

    ConvertMass(AltData,NumPoints,PtsPerAmu,MW,tempMW,intMW,MIintMW,1,MolVar,IntMolVar);
    MassToInt(AltData,NumPoints);


    //Add the integer isotope mass
    for(j=0;j<atomic5.NumIsotopes;j++){

      //add mass to each point
      for(k=0;k<NumPoints;k++) {
				AltData2[k].imag=AltData[k].imag+atomic5.IntMass[j];
				AltData2[k].real=AltData[k].real;
      };

      GetPeaks(AltData2,NumPoints,vProduct,PMIintMW,MaxIntMW);

      //find ratio of abundances, multiply by abundance & number of atoms
      for(k=0;k<vProduct.size();k++){
				vProduct[k].data/=vParent[k].data;
				vProduct[k].data*=atomic5.IsoProb[j];
				vProduct[k].data*=(it->second+1);

				//Add to the real mass for the parent
				vParent[k].mass += vProduct[k].data * atomic5.IsoMass[j];
			};
      
    };

		delete [] AltData;
		delete [] AltData2;
    
    //Add back the atom
    it->second++;
    
  };

  //output accurate masses:
  FixedData.clear();
  for(i=0;i<vParent.size();i++){
    if(vParent[i].data < 0.000001) continue;
    vParent[i].mass=(vParent[i].mass+ProtonMass*Charge)/Charge;
    FixedData.push_back(vParent[i]);
  };

	delete [] FreqData;

};




void CMercury8::ConvertMass(complex* Data, int NumPoints, int PtsPerAmu,
		float MW, float tempMW, long intMW, long MIintMW, int charge,
		double MolVar, double IntMolVar) {

  int i;
  double mass, ratio, CorrIntMW;

  if (IntMolVar == 0) ratio = 1;
  else ratio = sqrt(MolVar) / sqrt(IntMolVar);
  
  CorrIntMW = tempMW * ratio;
  for (i=NumPoints/2; i<NumPoints; i++) {
    mass = (double)(i-NumPoints)/PtsPerAmu + intMW;
    mass *= ratio;
    mass += MW - CorrIntMW;
    //mass /= charge;
    Data[i].imag = mass;
  };
  
  for (i=0; i<NumPoints/2; i++) {
    mass = (double)i/PtsPerAmu + intMW;
    mass *= ratio;
    mass += MW - CorrIntMW;
    //mass /= charge;
    Data[i].imag = mass;
  };

};

void CMercury8::MassToInt(complex* Data, int NumPoints) {

  int i, mass;

  for (i=NumPoints/2; i<NumPoints; i++) {

    //Since rounding poses problems, adjust when deviant values occur
    //This assumes that the average width >1
	mass = (int)(Data[i].imag+0.5);
    if(i>NumPoints/2){
      if(mass == Data[i-1].imag) mass++;

    };

    Data[i].imag = mass;
  };
  
  for (i=0; i<NumPoints/2; i++) {

    //Since rounding poses problems, adjust when deviant values occur
    //This assumes that the average width >1
	mass = (int)(Data[i].imag+0.5);	
    if(i>0){
      if(mass == Data[i-1].imag) mass++;
    };

    Data[i].imag = mass;
  };
  
};


void CMercury8::GetPeaks(complex* Data, int NumPoints, vector<Result>& v, 
			 int lower, int upper){
  int i;
  Result r;
  v.clear();

  for(i=NumPoints/2; i<NumPoints; i++){
    if(Data[i].imag>=lower && Data[i].imag<=upper){
      r.data=Data[i].real;
      r.mass=Data[i].imag;
      v.push_back(r);
    };
  };

  for(i=0; i<NumPoints/2; i++){
    if(Data[i].imag>=lower && Data[i].imag<=upper){
      r.data=Data[i].real;
      r.mass=Data[i].imag;
      v.push_back(r);
    };
  };

};

void CMercury8::RelativeAbundance(vector<Result>& v){

  int i;
  double max=0;
	double sum=0;

	FracAbunData.clear();
  
  /* Normalize intensity to 0%-100% scale */
  for (i=0; i<v.size(); i++) {
		if (v[i].data > max) max = v[i].data;
		//also, store fractional abundances anyway (useful for Hardklor, which uses both);
		FracAbunData.push_back(v[i]);
		sum+=v[i].data;
	};
  for (i=0; i<v.size(); i++) {
		v[i].data = 100 * v[i].data/max;
		FracAbunData[i].data/=sum;
	};

};


void CMercury8::CalcWeights(float& MW, double& MIMW, double& tempMW, long& intMW, 
			    long& MIintMW, long& MaxIntMW, int& IsoShift){

  int j, k, Z;

  MW = MIMW = tempMW = 0; intMW = MIintMW = MaxIntMW = 0;
  IsoShift=0;

  for (map<string, int, string_less>::iterator it = AtomicNum.begin(); it != AtomicNum.end(); it++) {
    Atomic5 atomic5 = Element[it->first];
    for (k=0; k<atomic5.NumIsotopes; k++) {
      MW += it->second * atomic5.IsoMass[k] * atomic5.IsoProb[k];
      tempMW += it->second * atomic5.IntMass[k] * atomic5.IsoProb[k];
      if (k==0) {
	MIMW += it->second * atomic5.IsoMass[k];
	MIintMW += it->second * atomic5.IntMass[k];
      };
      if(k==atomic5.NumIsotopes-1){

	//IsoShift is only used for accurate masses as an indication of how
	//much each product distribution will differ in mass range from the
	//parent distribution.
	if((atomic5.IntMass[k]-atomic5.IntMass[0]) > IsoShift){
	  IsoShift = atomic5.IntMass[k]-atomic5.IntMass[0];
	};

	MaxIntMW += it->second * atomic5.IntMass[k];
      };
    };
  };
  
  MW -= (float)ElectronMass;
  tempMW -= ElectronMass;
  MIMW -= ElectronMass;
  intMW = (long)(tempMW+0.5);

};

//Calculates Mercury as done originally
void CMercury8::Mercury(int Charge) {
  
  int 	  j;
  int	  MassRange;
  int     PtsPerAmu;
  long    NumPoints;		       // Working # of datapoints (real:imag) 
  complex *FreqData;              // Array of real:imaginary frequency values for FFT 
  float   MW;
  double  MIMW, tempMW, MolVar, IntMolVar;
  long    intMW, MIintMW, MaxIntMW;
  clock_t start, end;
  float   timex, seconds;
  int	  minutes, dummyInt;
  Result r;

	//Calculate molecular weight
  CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, MaxIntMW, dummyInt);
   
  //If the user specified an Echo, output the data now.
  if (showOutput){
    if (Charge != 0) {
      printf("Average Molecular Weight: %.3lf, at m/z: %.3f\n",MW,MW/fabs((double)Charge));
      printf("Average Integer MW: %ld, at m/z: %.3f\n\n",intMW,(float)intMW/fabs((double)Charge));
    } else {
      printf("Average Molecular Weight: %.3lf\n",MW);
      printf("Average Integer MW: %ld\n\n",intMW);
    };
  };
 
  //Calculate mass range to use based on molecular variance 
  CalcVariances(&MolVar,&IntMolVar);
  CalcMassRange(&MassRange,MolVar,Charge,1);
  PtsPerAmu = 1;

	monoMass+=(Charge*(ProtonMass));
	monoMass/=Charge;
	if(showOutput) printf("True MonoMass: %.8lf\n",monoMass);

  //Allocate memory for Axis arrays
  NumPoints = MassRange * PtsPerAmu;
  FreqData = new complex[NumPoints];
  
  //Start isotope distribution calculation
  //MH notes: How is this different from using -MW instead of -intMW?
  start = clock();
  CalcFreq(FreqData,NumPoints,MassRange,-intMW);
  FFT(FreqData,NumPoints,false);
  end = clock();

  //Output the results if the user requested an Echo.
  if(showOutput){
    timex = (end - start) / CLOCKS_PER_SEC;
    minutes = (int)(timex/60);
    seconds = timex - (60*minutes);
    printf("Calculation performed in %d min %.3f sec\n",minutes,seconds);
  };
	
  
  //Not sure why we do this...
  if (Charge == 0) Charge = 1;

  //Convert complex numbers to masses
  ConvertMass(FreqData,NumPoints,PtsPerAmu,MW,tempMW,intMW,MIintMW,(int)fabs((double)Charge),MolVar,IntMolVar);

  //Put our data in the global array
  //This performs a bit of computation to eliminate meaningless data.
  FixedData.clear();
  for(j=NumPoints/2; j<NumPoints; j++){
		if((int)(FreqData[j].imag+0.5)<MIintMW) continue;
    r.data=FreqData[j].real;
		r.mass=(FreqData[j].imag+(ProtonMass)*Charge)/Charge;
		if( (monoMass-r.mass)*Charge > 0.5 ) continue;
    r.data=FreqData[j].real;
		r.mass=(FreqData[j].imag+ProtonMass*Charge)/Charge;
    FixedData.push_back(r);
  };

  for(j=0; j<NumPoints/2; j++){
	if((int)(FreqData[j].imag+0.5)>MaxIntMW) continue;
    if(FreqData[j].real<0) break;
    r.data=FreqData[j].real;
		r.mass=(FreqData[j].imag+ProtonMass*Charge)/Charge;
    FixedData.push_back(r);
  };
  
  //Clean up the memory
  delete [] FreqData;
 
};


void CMercury8::AccMass(bool b) {
  bAccMass = b;
};

void CMercury8::RelAbun(bool b) {
  bRelAbun = b;
};

double CMercury8::getZeroMass(){
	return zeroMass;
};

double CMercury8::getMonoMass(){
	return monoMass;
};