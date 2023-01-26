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

Uses Mercury code, with permission, from Alan L. Rockwood and Steve van Orden
*/

#include "CMercury8.h"
#include <cmath>
#include <iostream>
using namespace std;

CMercury8::CMercury8(){
  InitializeData();
  showOutput = false;
  bAccMass = false;
  bRelAbun = true;
  zeroMass=0;
}

CMercury8::CMercury8(const char* fn){
  InitializeData(fn);
  showOutput = false;
  bAccMass = false;
  bRelAbun = true;
  zeroMass=0;
}

CMercury8::~CMercury8(){
  int Z;

  for (Z=0; Z<=MAXAtomNo; Z++) {

    delete [] Element[Z].IsoMass;
    delete [] Element[Z].IntMass;
    delete [] Element[Z].IsoProb;
    //if(Element[Z].WrapMass!=NULL) delete [] Element[Z].WrapMass;

    delete [] Orig[Z].IsoMass;
    delete [] Orig[Z].IntMass;
    delete [] Orig[Z].IsoProb;
    //if(Orig[Z].WrapMass!=NULL) delete [] Orig[Z].WrapMass;
  }
}

void CMercury8::Echo(bool b){
  showOutput = b;
}

//quick hack for N-enrichment
//This needs to be expanded to be universal for all elements
void CMercury8::Enrich(int c,int e,double d){
  int i=0;
  int j=0;
  double ab=0;
	float f=(float)d;

  //c++;
  if(showOutput) cout << "Enrich: " << Element[c].Symbol << " " << c << "\t" << d << endl;

  //Find highest probability
  for(i=0;i<Element[c].NumIsotopes;i++){
    if(Element[c].IsoProb[i]>ab){
      j=i;
      ab=Element[c].IsoProb[i];
    }
  }

  //Normalize all isotope abundances
  for(i=0;i<Element[c].NumIsotopes;i++){
    Element[c].IsoProb[i]/=Element[c].IsoProb[j];
  }

  //Calculate enrichment
  for(i=0;i<Element[c].NumIsotopes;i++){
    if(i==e) Element[c].IsoProb[i]=(1-f)*Element[c].IsoProb[i]+f;
    else Element[c].IsoProb[i]=(1-f)*Element[c].IsoProb[i];
  }

  EnrichAtoms.push_back(c);

}
  

/*************************************************/
/* FUNCTION Intro - called by main()             */
/*************************************************/
void CMercury8::Intro() {
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
 
/***************************************************/
/* FUNCTION InitializeData - called by constructor */
/***************************************************/
//This function reads the ISOTOPE.DAT file that must be in the same
//folder as the application.
void CMercury8::InitializeData(const char* fn) {

  FILE *ElementFile;
  int  i, Z,ret;

  //Use default values if an isotope file is not provided
  if(fn==NULL || strlen(fn)==0){
    DefaultValues();
    return;
  }
 
  if ((ElementFile = fopen(fn, "rt")) == NULL) {
    printf("\nError - Cannot open File: ISOTOPE.DAT\n");
    exit(-1);
  }
   
  for (Z=0; Z<=MAXAtomNo; Z++) {
    Element[Z].Symbol[0]=Element[Z].Symbol[1]=Element[Z].Symbol[2]=0;

    ret=fscanf(ElementFile,"%2s %d\n", &Element[Z].Symbol[0],&Element[Z].NumIsotopes);
    strcpy(Orig[Z].Symbol,Element[Z].Symbol);
    Orig[Z].NumIsotopes = Element[Z].NumIsotopes;

    Element[Z].IsoMass = new float[Element[Z].NumIsotopes+1];
    Element[Z].IntMass = new int[Element[Z].NumIsotopes+1];
    Element[Z].IsoProb = new float[Element[Z].NumIsotopes+1];
    //Element[Z].WrapMass = NULL;

    Orig[Z].IsoMass = new float[Orig[Z].NumIsotopes+1];
    Orig[Z].IntMass = new int[Orig[Z].NumIsotopes+1];
    Orig[Z].IsoProb = new float[Orig[Z].NumIsotopes+1];
    //Orig[Z].WrapMass = NULL;
    
    for (i=0; i<Element[Z].NumIsotopes; i++) {
      ret=fscanf(ElementFile, "%f \n", &Element[Z].IsoMass[i]);
      ret=fscanf(ElementFile, "%f \n", &Element[Z].IsoProb[i]);
      Element[Z].IntMass[i] = (int)(Element[Z].IsoMass[i]+0.5);
      Orig[Z].IsoMass[i]=Element[Z].IsoMass[i];
      Orig[Z].IsoProb[i]=Element[Z].IsoProb[i];
      Orig[Z].IntMass[i]=Element[Z].IntMass[i];
    }
      
    Element[Z].NumAtoms = 0;
    Element[Z].IsoMass[Element[Z].NumIsotopes] = 0;
    Element[Z].IsoProb[Element[Z].NumIsotopes] = 0;
    Orig[Z].NumAtoms = 0;
    Orig[Z].IsoMass[Orig[Z].NumIsotopes] = 0;
    Orig[Z].IsoProb[Orig[Z].NumIsotopes] = 0;

    ret=fscanf(ElementFile, " \n");
  }

  fclose(ElementFile);
  
}
 
/*************************************************/
/* FUNCTION CalcVariances - called by main()     */
/*************************************************/
void CMercury8::CalcVariances(double *MolVar, double *IntMolVar, int NumElements){
  int i, j, Z;
  double Var, IntVar;
  double avemass, intavemass;
  
  *MolVar = *IntMolVar = 0;
  for (i=0; i<NumElements; i++) {
    Z = AtomicNum[i];
    avemass = intavemass = 0;
    for (j=0; j<Element[Z].NumIsotopes; j++){
      avemass += Element[Z].IsoMass[j] * Element[Z].IsoProb[j];
      intavemass += Element[Z].IntMass[j] * Element[Z].IsoProb[j];
    };
    Var = IntVar = 0;
    for (j=0; j<Element[Z].NumIsotopes; j++){
      Var += (Element[Z].IsoMass[j] - avemass) * (Element[Z].IsoMass[j] - avemass) * Element[Z].IsoProb[j];
      IntVar += (Element[Z].IntMass[j] - intavemass) * (Element[Z].IntMass[j] - intavemass) * Element[Z].IsoProb[j];
    };
    *MolVar += Element[Z].NumAtoms * Var;
    *IntMolVar += Element[Z].NumAtoms * IntVar;
  };

	//monoisotopic mass (zero mass, EXACT)
	zeroMass=0;
	for (i=0; i<NumElements; i++) {
    Z = AtomicNum[i];
		zeroMass+=(Element[Z].IsoMass[0] * Element[Z].NumAtoms);
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

//Atom is the atomic abbreviation, Ecount is nth element in the formula
//Acount is the number of atoms of the element in the formula
void CMercury8::AddElement(char Atom[3], int Ecount, int Acount) {

  int Z, FOUND=0;
 
  for (Z=1; Z<=MAXAtomNo; Z++) {

    if (strcmp(Atom,Element[Z].Symbol) == 0) {

      if (Element[Z].NumAtoms != 0) { 

	printf("\nError - the element %s has been entered twice in molecular formula\n", Element[Z].Symbol);
	exit(-1);

      } else {

	AtomicNum[Ecount] = Z;
	Element[Z].NumAtoms = Acount;
	//Element[Z].WrapMass = new int[Element[Z].NumIsotopes+1];
	//Element[Z].WrapMass[Element[Z].NumIsotopes] = 0;
	FOUND=1;
	break;

      };

    };
  };

  if (!FOUND) {
    printf("\nError - Unknown element in Molecular Formula\n");
    exit(-1);
  };

}
 
/*************************************************/
/* FUNCTION ParseMF - called by main()           */
/*************************************************/
//Return codes
//	0: Success
//	-1: Invalid character
int CMercury8::ParseMF(char MF[], int *elementcount) {
  int COND, ERRFLAG;
  int atomcount;
  char Atom[3], errorch;

  atomcount=0; COND=0; ERRFLAG=0;
  Atom[0] = Atom[1] = Atom[2] = '\0';

  unsigned int pos=0;
  unsigned int peek=0;
  unsigned int count=0;
  char digit[2];
  bool bFirst=true;
  atomcount=0;
  *elementcount=0;
  while(pos<strlen(MF) && ERRFLAG==0){
    if(isupper(MF[pos])){
      //Add the last atom
      if(!bFirst) {
        AddElement(Atom,(*elementcount)++,atomcount);
        atomcount=0;
      } else {
        bFirst=false;
      }
      
      peek=pos+1;
      if(peek==strlen(MF)){
        //reached end of string, add this single atom
        Atom[0]=MF[pos];
        Atom[1]=Atom[2]='\0';
        atomcount=1;
        pos++;
        continue;
      }
       
      if(isupper(MF[peek])){
        //This is a single atom
        Atom[0]=MF[pos];
        Atom[1]=Atom[2]='\0';
        atomcount=1;
        pos++;
      } else if(islower(MF[peek])){
        //Set the atom name
        Atom[0]=MF[pos];
        Atom[1]=MF[peek];
        Atom[2]='\0';
        atomcount=0;
        pos+=2;
      } else if(isdigit(MF[peek])){
        //Set the atom name
        Atom[0]=MF[pos];
        Atom[1]=Atom[2]='\0';
        atomcount=0;
        pos++;
      } else {
        errorch=MF[peek];
        ERRFLAG=1;
      }
         
    } else if(isdigit(MF[pos])){
      digit[0]=MF[pos];
      digit[1]='\0';
      atomcount*=10;
      atomcount+=atoi(digit);
      pos++;
     } else {
      errorch=MF[pos];
      ERRFLAG=1;
    }
  }

  if(ERRFLAG==0) AddElement(Atom,(*elementcount)++,atomcount);
  else printf("There was an error\n");

  if (ERRFLAG) {
    printf("\nError in format of input...  The character '%c' is invalid\n",errorch);
    return -1;
  } else {
    return 0;
  }

}
 
/*************************************************/
/* FUNCTION CalcFreq - called by main()          */
/*    Could be done with less code, but this     */
/*    saves a few operations.                    */
/*************************************************/
void CMercury8::CalcFreq(complex* FreqData, int Ecount, int NumPoints, int MassRange, int MassShift) {
  
  int    i, j, k, Z;
  double real, imag, freq, X, theta, r, tempr;
  double a, b, c, d;
 
  /* Calculate first half of Frequency Domain (+)masses */
  for (i=0; i<NumPoints/2; i++) {
    
    freq = (double)i/MassRange;
    r = 1;
    theta = 0;
    for (j=0; j<Ecount; j++) {
      Z = AtomicNum[j];
      real = imag = 0;
      for (k=0; k<Element[Z].NumIsotopes; k++) {
				X = TWOPI * Element[Z].IntMass[k] * freq;
				real += Element[Z].IsoProb[k] * cos(X);
				imag += Element[Z].IsoProb[k] * sin(X);
      }
      
      /* Convert to polar coordinates, r then theta */
      tempr = sqrt(real*real+imag*imag);
      r *= pow(tempr,Element[Z].NumAtoms);
      if (real > 0) theta += Element[Z].NumAtoms * atan(imag/real);
      else if (real < 0) theta += Element[Z].NumAtoms * (atan(imag/real) + PI);
      else if (imag > 0) theta += Element[Z].NumAtoms * HALFPI;
      else theta += Element[Z].NumAtoms * -HALFPI;
      
    }  /* end for(j) */
    
    /* Convert back to real:imag coordinates and store */
    a = r * cos(theta);
    b = r * sin(theta);
    c = cos(TWOPI*MassShift*freq);
    d = sin(TWOPI*MassShift*freq);
    FreqData[i].real = a*c - b*d;
    FreqData[i].imag = b*c + a*d;
    
  }  /* end for(i) */
  
  /* Calculate second half of Frequency Domain (-)masses */
  for (i=NumPoints/2; i<NumPoints; i++) {
    
    freq = (double)(i-NumPoints)/MassRange;
    r = 1;
    theta = 0;
    for (j=0; j<Ecount; j++) {
      Z = AtomicNum[j];
      real = imag = 0;
      for (k=0; k<Element[Z].NumIsotopes; k++) {
				X = TWOPI * Element[Z].IntMass[k] * freq;
				real += Element[Z].IsoProb[k] * cos(X);
				imag += Element[Z].IsoProb[k] * sin(X);
      }
      
      /* Convert to polar coordinates, r then theta */
      tempr = sqrt(real*real+imag*imag);
      r *= pow(tempr,Element[Z].NumAtoms);
      if (real > 0) theta += Element[Z].NumAtoms * atan(imag/real);
      else if (real < 0) theta += Element[Z].NumAtoms * (atan(imag/real) + PI);
      else if (imag > 0) theta += Element[Z].NumAtoms * HALFPI;
      else theta += Element[Z].NumAtoms * -HALFPI;
      
    }  /* end for(j) */
    
    /* Convert back to real:imag coordinates and store */
    a = r * cos(theta);
    b = r * sin(theta);
    c = cos(TWOPI*MassShift*freq);
    d = sin(TWOPI*MassShift*freq);
    FreqData[i].real = a*c - b*d;
    FreqData[i].imag = b*c + a*d;
    
  }  /* end of for(i) */
  
}  /* End of CalcFreq() */
  
 
/*************************************************/
/* FUNCTION main() - main block of FFTISO        */
/*************************************************/
//Return Codes:
//	0: Success
//	1: Invalid molecular formula
//	2: Cannot write to file
int CMercury8::GoMercury(char* MolForm, int Charge, const char* filename) {
  
  unsigned int i;
  int	 NumElements=0;			/* Number of elements in molecular formula */
  FILE	 *outfile;			/* output file pointer */
  
  //MolForm is the only required data
  if (strlen(MolForm) == 0) {
    //printf("\nNo molecular formula!\n");
    return 1;
  }
  
  //Parse the formula, check for validity
  if (ParseMF(MolForm,&NumElements) == -1)     {
    MolForm[0] = '\0';
    NumElements = 0;
    return 1;
  }

  //Run the user requested Mercury
  if(bAccMass) AccurateMass(NumElements,Charge);
  else Mercury(NumElements,Charge);
  
  //If the user requested relative abundance, convert data
  if(bRelAbun) RelativeAbundance(FixedData);

  //If the user requested the data to file, output it here.
  if (filename[0]!=0){
    if ((outfile = fopen(filename,"w")) == NULL) {
      printf("\nError - Cannot create file %s\n",filename);
      return 2;
    }
    for(i=0;i<FixedData.size();i++){
      fprintf(outfile,"%lf %lf\n",FixedData[i].mass,FixedData[i].data);
    }
    fclose(outfile);
    
  }
  
  //If the user wants the data on the screen, let it be so.
  if(showOutput){
    for(i=0;i<FixedData.size();i++){
      printf("%.4f\t%.5f\n",FixedData[i].mass,FixedData[i].data);
    }
  }
  
  //Clear all non-user input so object can be reused:
  Reset();
  
  //Output a final, useful message
  if(showOutput) printf("Mercury successful!\n");
  
  return 0;
  
}


//This function resets all atomic states to those at initialization.
//This is so the same object can be reused after performing a calculation
//or after user intervention, such as Enrich().
void CMercury8::Reset(){
  unsigned int i;
	int j;
  
  for (i=0;i<20;i++) AtomicNum[i]=0;
  for (i=0; i<=MAXAtomNo; i++)  Element[i].NumAtoms = 0;
  
  for (i=0;i<(int)EnrichAtoms.size();i++){
    for (j=0;j<Element[EnrichAtoms[i]].NumIsotopes;j++){
      Element[EnrichAtoms[i]].IsoProb[j] = Orig[EnrichAtoms[i]].IsoProb[j];
    }
  }
  EnrichAtoms.clear();
  
}

//This function calculates the accurate mass. It's a bit slower, and it performs poorly
//on the fringes of large proteins because of the estimation of MassRange algorithm, and the
//fact that abundances would be so small as to be nearly indistinguishable from zero.
void CMercury8::AccurateMass(int NumElements, int Charge){
  int 	 i,j, k;
  int	  MassRange;
  int   PtsPerAmu;
  int   NumPoints;			/* Working # of datapoints (real:imag) */
  complex *FreqData;              /* Array of real:imaginary frequency values for FFT */
  double MW;
  double MIMW, tempMW, MolVar, IntMolVar;
  int intMW, MIintMW;
  int dummyLong;
  int dummyInt;


  complex *AltData;
  complex *AltData2;
  int MaxIntMW;
  int PMIintMW;
  int IsoShift;

  Result r;
  vector<Result> vParent;
  vector<Result> vProduct;
  
  //If we made it this far, we have valid input, so calculate molecular weight
  CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, MaxIntMW, IsoShift, NumElements);

  //If the user specified an Echo, output the data now.
  if (showOutput){
    if (Charge != 0) {
      printf("Average Molecular Weight: %.3lf, at m/z: %.3f\n",MW,MW/fabs((double)Charge));
      printf("Average Integer MW: %d, at m/z: %.3f\n\n",intMW,(float)intMW/fabs((double)Charge));
    } else {
      printf("Average Molecular Weight: %.3lf\n",MW);
      printf("Average Integer MW: %d\n\n",intMW);
    }
  }

  //Set our parental molecular weight. This will be used as the lower bounds.
  //PMIintMW = (int)round(MIMW);
  PMIintMW = (int)(MIMW+0.5);
  
  //Calculate mass range to use based on molecular variance 
  CalcVariances(&MolVar,&IntMolVar,NumElements);
  CalcMassRange(&MassRange,MolVar,1,1);
  PtsPerAmu = 1;
  
  //Allocate memory for Axis arrays
  NumPoints = MassRange * PtsPerAmu;
  FreqData = new complex[NumPoints];
  
  //Start isotope distribution calculation
  //MH notes: How is this different from using -MW instead of -intMW?
  CalcFreq(FreqData,NumElements,NumPoints,MassRange,-intMW);
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
  for(i=0;i<(int)vParent.size();i++) vParent[i].mass=0;

  //We have now completed the analysis on the actual protein.
  //Next we compute compositions for each element and each isotope
  for(i=0;i<NumElements;i++){
    
    //Subtract one atom
    Element[AtomicNum[i]].NumAtoms--;

    //Calculate the product masses and variances
    //However, use the same mass range as the parent (calculated above)
    CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, dummyLong, dummyInt, NumElements);
    CalcVariances(&MolVar,&IntMolVar,NumElements);

    //Allocate memory for Axis arrays
    AltData = new complex[NumPoints];
    AltData2 = new complex[NumPoints];
    
    //Start isotope distribution calculation
    CalcFreq(AltData,NumElements,NumPoints,MassRange,-intMW);
    FFT(AltData,NumPoints,false);

    ConvertMass(AltData,NumPoints,PtsPerAmu,MW,tempMW,intMW,MIintMW,1,MolVar,IntMolVar);
    MassToInt(AltData,NumPoints);


    //Add the integer isotope mass
    for(j=0;j<Element[AtomicNum[i]].NumIsotopes;j++){

      //add mass to each point
      for(k=0;k<NumPoints;k++) {
				AltData2[k].imag=AltData[k].imag+Element[AtomicNum[i]].IntMass[j];
				AltData2[k].real=AltData[k].real;
      }

      GetPeaks(AltData2,NumPoints,vProduct,PMIintMW,MaxIntMW);

      //find ratio of abundances, multiply by abundance & number of atoms
      for(k=0;k<(int)vProduct.size();k++){
				vProduct[k].data/=vParent[k].data;
				vProduct[k].data*=Element[AtomicNum[i]].IsoProb[j];
				vProduct[k].data*=(Element[AtomicNum[i]].NumAtoms+1);

				//Add to the real mass for the parent
				vParent[k].mass += vProduct[k].data * Element[AtomicNum[i]].IsoMass[j];
			}
      
    }

		delete [] AltData;
		delete [] AltData2;
    
    //Add back the atom
    Element[AtomicNum[i]].NumAtoms++;
    
  }

  //output accurate masses:
  FixedData.clear();
  for(i=0;i<(int)vParent.size();i++){
    if(vParent[i].data < 0.000001) continue;
    vParent[i].mass=(vParent[i].mass+ProtonMass*Charge)/Charge;
    FixedData.push_back(vParent[i]);
  }

	delete [] FreqData;

}


void CMercury8::ConvertMass(complex* Data, int NumPoints, int PtsPerAmu,
		double MW, double tempMW, int intMW, int MIintMW, int charge,
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

  unsigned int i;
  double max=0;
	double sum=0;

	FracAbunData.clear();
  
  /* Normalize intensity to 0%-100% scale */
  for (i=0; i<v.size(); i++) {
		if (v[i].data > max) max = v[i].data;
		//also, store fractional abundances anyway (useful for Hardklor, which uses both);
		FracAbunData.push_back(v[i]);
		sum+=v[i].data;
	}
  for (i=0; i<v.size(); i++) {
		v[i].data = 100 * v[i].data/max;
		FracAbunData[i].data/=sum;
	}

}

void CMercury8::CalcWeights(double& MW, double& MIMW, double& tempMW, int& intMW, 
			    int& MIintMW, int& MaxIntMW, int& IsoShift, int NumElements){

  int j, k, Z;

  MW = MIMW = tempMW = 0; intMW = MIintMW = MaxIntMW = 0;
  IsoShift=0;

  for (j=0; j<NumElements; j++) {
    Z = AtomicNum[j];
    for (k=0; k<Element[Z].NumIsotopes; k++) {
      MW += Element[Z].NumAtoms * Element[Z].IsoMass[k] * Element[Z].IsoProb[k];
      tempMW += Element[Z].NumAtoms * Element[Z].IntMass[k] * Element[Z].IsoProb[k];
      if (k==0) {
				MIMW += Element[Z].NumAtoms * Element[Z].IsoMass[k];
				MIintMW += Element[Z].NumAtoms * Element[Z].IntMass[k];
      }
      if(k==Element[Z].NumIsotopes-1){

				//IsoShift is only used for accurate masses as an indication of how
				//much each product distribution will differ in mass range from the
				//parent distribution.
				if((Element[Z].IntMass[k]-Element[Z].IntMass[0]) > IsoShift){
					IsoShift = Element[Z].IntMass[k]-Element[Z].IntMass[0];
				}

				MaxIntMW += Element[Z].NumAtoms * Element[Z].IntMass[k];
      }
    }
  }
  
  MW -= ElectronMass;
  tempMW -= ElectronMass;
  MIMW -= ElectronMass;
  intMW = (int)(tempMW+0.5);

};

//Calculates Mercury as done originally
void CMercury8::Mercury(int NumElements, int Charge) {
  
  int 	  j;
  int	  MassRange;
  int     PtsPerAmu;
  int    NumPoints;		       // Working # of datapoints (real:imag) 
  complex *FreqData;              // Array of real:imaginary frequency values for FFT 
  double   MW;
  double  MIMW, tempMW, MolVar, IntMolVar;
  int    intMW, MIintMW, MaxIntMW;
  clock_t start, end;
  float   timex, seconds;
  int	  minutes, dummyInt;
  Result r;

	//Calculate molecular weight
  CalcWeights(MW, MIMW, tempMW, intMW, MIintMW, MaxIntMW, dummyInt, NumElements);
   
  //If the user specified an Echo, output the data now.
  if (showOutput){
    if (Charge != 0) {
      printf("Average Molecular Weight: %.3lf, at m/z: %.3f\n",MW,MW/fabs((double)Charge));
      printf("Average Integer MW: %d, at m/z: %.3f\n\n",intMW,(float)intMW/fabs((double)Charge));
    } else {
      printf("Average Molecular Weight: %.3lf\n",MW);
      printf("Average Integer MW: %d\n\n",intMW);
    }
  }
 
  //Calculate mass range to use based on molecular variance 
  CalcVariances(&MolVar,&IntMolVar,NumElements);
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
  CalcFreq(FreqData,NumElements,NumPoints,MassRange,-intMW);
  FFT(FreqData,NumPoints,false);
  end = clock();

  //Output the results if the user requested an Echo.
  if(showOutput){
    timex = (float)(end - start) / CLOCKS_PER_SEC;
    minutes = (int)(timex/60);
    seconds = timex - (60*minutes);
    printf("Calculation performed in %d min %.3f sec\n",minutes,seconds);
  }
	
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
}

void CMercury8::RelAbun(bool b) {
  bRelAbun = b;
}

double CMercury8::getZeroMass(){
  return zeroMass;
}

double CMercury8::getMonoMass(){
  return monoMass;
}

void CMercury8::DefaultValues(){

  string el[MAXAtomNo+1] = {"X","H","He","Li","Be","B","C","N","O","F","Ne","Na","Mg","Al","Si","P","S","Cl","Ar",
    "K","Ca","Sc","Ti","V","Cr","Mn","Fe","Co","Ni","Cu","Zn","Ga","Ge","As","Se","Br","Kr","Rb","Sr","Y","Zr",
    "Nb","Mo","Tc","Ru","Rh","Pd","Ag","Cd","In","Sn","Sb","Te","I","Xe","Cs","Ba","La","Ce","Pr","Nd","Pm","Sm",
    "Eu","Gd","Tb","Dy","Ho","Er","Tm","Yb","Lu","Hf","Ta","W","Re","Os","Ir","Pt","Au","Hg","Tl","Pb","Bi","Po",
    "At","Rn","Fr","Ra","Ac","Th","Pa","U","Np","Pu","Am","Cm","Bk","Cf","Es","Fm","Md","No","Lr","Hx","Cx","Nx",
    "Ox","Sx"};
  int sz[MAXAtomNo+1] = {2,2,2,2,1,2,2,2,3,1,3,1,3,1,3,1,4,2,3,3,6,1,5,2,4,1,4,1,5,2,5,2,5,1,6,2,6,2,4,1,5,1,7,1,7,
    1,6,2,8,2,10,2,8,1,9,1,7,2,4,1,7,1,7,2,7,1,7,1,6,1,7,2,6,2,5,2,7,2,6,1,7,2,4,1,1,1,1,1,1,1,1,1,3,1,1,1,1,1,1,
    1,1,1,1,1,2,2,2,3,4};
  double m[322] = {1.000000000000,2.000000000000,1.007824600000,2.014102100000,3.016030000000,4.002600000000,
    6.015121000000,7.016003000000,9.012182000000,10.012937000000,11.009305000000,12.000000000000,13.003355400000,
    14.003073200000,15.000108800000,15.994914100000,16.999132200000,17.999161600000,18.998403200000,
    19.992435000000,20.993843000000,21.991383000000,22.989767000000,23.985042000000,24.985837000000,
    25.982593000000,26.981539000000,27.976927000000,28.976495000000,29.973770000000,30.973762000000,
    31.972070000000,32.971456000000,33.967866000000,35.967080000000,34.968853100000,36.965903400000,
    35.967545000000,37.962732000000,39.962384000000,38.963707000000,39.963999000000,40.961825000000,
    39.962591000000,41.958618000000,42.958766000000,43.955480000000,45.953689000000,47.952533000000,
    44.955910000000,45.952629000000,46.951764000000,47.947947000000,48.947871000000,49.944792000000,
    49.947161000000,50.943962000000,49.946046000000,51.940509000000,52.940651000000,53.938882000000,
    54.938047000000,53.939612000000,55.934939000000,56.935396000000,57.933277000000,58.933198000000,
    57.935346000000,59.930788000000,60.931058000000,61.928346000000,63.927968000000,62.939598000000,
    64.927793000000,63.929145000000,65.926034000000,66.927129000000,67.924846000000,69.925325000000,
    68.925580000000,70.924700000000,69.924250000000,71.922079000000,72.923463000000,73.921177000000,
    75.921401000000,74.921594000000,73.922475000000,75.919212000000,76.919912000000,77.919000000000,
    79.916520000000,81.916698000000,78.918336000000,80.916289000000,77.914000000000,79.916380000000,
    81.913482000000,82.914135000000,83.911507000000,85.910616000000,84.911794000000,86.909187000000,
    83.913430000000,85.909267000000,86.908884000000,87.905619000000,88.905849000000,89.904703000000,
    90.905644000000,91.905039000000,93.906314000000,95.908275000000,92.906377000000,91.906808000000,
    93.905085000000,94.905840000000,95.904678000000,96.906020000000,97.905406000000,99.907477000000,
    98.000000000000,95.907599000000,97.905287000000,98.905939000000,99.904219000000,100.905582000000,
    101.904348000000,103.905424000000,102.905500000000,101.905634000000,103.904029000000,104.905079000000,
    105.903478000000,107.903895000000,109.905167000000,106.905092000000,108.904757000000,105.906461000000,
    107.904176000000,109.903005000000,110.904182000000,111.902758000000,112.904400000000,113.903357000000,
    115.904754000000,112.904061000000,114.903880000000,111.904826000000,113.902784000000,114.903348000000,
    115.901747000000,116.902956000000,117.901609000000,118.903310000000,119.902200000000,121.903440000000,
    123.905274000000,120.903821000000,122.904216000000,119.904048000000,121.903054000000,122.904271000000,
    123.902823000000,124.904433000000,125.903314000000,127.904463000000,129.906229000000,126.904473000000,
    123.905894000000,125.904281000000,127.903531000000,128.904780000000,129.903509000000,130.905072000000,
    131.904144000000,133.905395000000,135.907214000000,132.905429000000,129.906282000000,131.905042000000,
    133.904486000000,134.905665000000,135.904553000000,136.905812000000,137.905232000000,137.907110000000,
    138.906347000000,135.907140000000,137.905985000000,139.905433000000,141.909241000000,140.907647000000,
    141.907719000000,142.909810000000,143.910083000000,144.912570000000,145.913113000000,147.916889000000,
    149.920887000000,145.000000000000,143.911998000000,146.914895000000,147.914820000000,148.917181000000,
    149.917273000000,151.919729000000,153.922206000000,150.919847000000,152.921225000000,151.919786000000,
    153.920861000000,154.922618000000,155.922118000000,156.923956000000,157.924099000000,159.927049000000,
    158.925342000000,155.925277000000,157.924403000000,159.925193000000,160.926930000000,161.926795000000,
    162.928728000000,163.929171000000,164.930319000000,161.928775000000,163.929198000000,165.930290000000,
    166.932046000000,167.932368000000,169.935461000000,168.934212000000,167.933894000000,169.934759000000,
    170.936323000000,171.936378000000,172.938208000000,173.938859000000,175.942564000000,174.940770000000,
    175.942679000000,173.940044000000,175.941406000000,176.943217000000,177.943696000000,178.945812000000,
    179.946545000000,179.947462000000,180.947992000000,179.946701000000,181.948202000000,182.950220000000,
    183.950928000000,185.954357000000,184.952951000000,186.955744000000,183.952488000000,185.953830000000,
    186.955741000000,187.955860000000,188.958137000000,189.958436000000,191.961467000000,190.960584000000,
    192.962917000000,189.959917000000,191.961019000000,193.962655000000,194.964766000000,195.964926000000,
    197.967869000000,196.966543000000,195.965807000000,197.966743000000,198.968254000000,199.968300000000,
    200.970277000000,201.970617000000,203.973467000000,202.972320000000,204.974401000000,203.973020000000,
    205.974440000000,206.975872000000,207.976627000000,208.980374000000,209.000000000000,210.000000000000,
    222.000000000000,223.000000000000,226.025000000000,227.028000000000,232.038054000000,231.035900000000,
    234.040946000000,235.043924000000,238.050784000000,237.048000000000,244.000000000000,243.000000000000,
    247.000000000000,247.000000000000,251.000000000000,252.000000000000,257.000000000000,258.000000000000,
    259.000000000000,260.000000000000,1.007824600000,2.014102100000,12.000000000000,13.003355400000,
    14.003073200000,15.000108800000,15.994914100000,16.999132200000,17.999161600000,31.972070000000,
    32.971456000000,33.967866000000,35.967080000000};
  double r[322] = {0.900000000000,0.100000000000,0.999855000000,0.000145000000,0.000001380000,0.999998620000,
    0.075000000000,0.925000000000,1.000000000000,0.199000000000,0.801000000000,0.989160000000,0.010840000000,
    0.996330000000,0.003660000000,0.997576009706,0.000378998479,0.002044991815,1.000000000000,0.904800000000,
    0.002700000000,0.092500000000,1.000000000000,0.789900000000,0.100000000000,0.110100000000,1.000000000000,
    0.922300000000,0.046700000000,0.031000000000,1.000000000000,0.950210000000,0.007450000000,0.042210000000,
    0.000130000000,0.755290000000,0.244710000000,0.003370000000,0.000630000000,0.996000000000,0.932581000000,
    0.000117000000,0.067302000000,0.969410000000,0.006470000000,0.001350000000,0.020860000000,0.000040000000,
    0.001870000000,1.000000000000,0.080000000000,0.073000000000,0.738000000000,0.055000000000,0.054000000000,
    0.002500000000,0.997500000000,0.043450000000,0.837900000000,0.095000000000,0.023650000000,1.000000000000,
    0.059000000000,0.917200000000,0.021000000000,0.002800000000,1.000000000000,0.682700000000,0.261000000000,
    0.011300000000,0.035900000000,0.009100000000,0.691700000000,0.308300000000,0.486000000000,0.279000000000,
    0.041000000000,0.188000000000,0.006000000000,0.601080000000,0.398920000000,0.205000000000,0.274000000000,
    0.078000000000,0.365000000000,0.078000000000,1.000000000000,0.009000000000,0.091000000000,0.076000000000,
    0.236000000000,0.499000000000,0.089000000000,0.506900000000,0.493100000000,0.003500000000,0.022500000000,
    0.116000000000,0.115000000000,0.570000000000,0.173000000000,0.721700000000,0.278300000000,0.005600000000,
    0.098600000000,0.070000000000,0.825800000000,1.000000000000,0.514500000000,0.112200000000,0.171500000000,
    0.173800000000,0.028000000000,1.000000000000,0.148400000000,0.092500000000,0.159200000000,0.166800000000,
    0.095500000000,0.241300000000,0.096300000000,1.000000000000,0.055400000000,0.018600000000,0.127000000000,
    0.126000000000,0.171000000000,0.316000000000,0.186000000000,1.000000000000,0.010200000000,0.111400000000,
    0.223300000000,0.273300000000,0.264600000000,0.117200000000,0.518390000000,0.481610000000,0.012500000000,
    0.008900000000,0.124900000000,0.128000000000,0.241300000000,0.122200000000,0.287300000000,0.074900000000,
    0.043000000000,0.957000000000,0.009700000000,0.006500000000,0.003600000000,0.145300000000,0.076800000000,
    0.242200000000,0.085800000000,0.325900000000,0.046300000000,0.057900000000,0.574000000000,0.426000000000,
    0.000950000000,0.025900000000,0.009050000000,0.047900000000,0.071200000000,0.189300000000,0.317000000000,
    0.338700000000,1.000000000000,0.001000000000,0.000900000000,0.019100000000,0.264000000000,0.041000000000,
    0.212000000000,0.269000000000,0.104000000000,0.089000000000,1.000000000000,0.001060000000,0.001010000000,
    0.024200000000,0.065930000000,0.078500000000,0.112300000000,0.717000000000,0.000900000000,0.999100000000,
    0.001900000000,0.002500000000,0.884300000000,0.111300000000,1.000000000000,0.271300000000,0.121800000000,
    0.238000000000,0.083000000000,0.171900000000,0.057600000000,0.056400000000,1.000000000000,0.031000000000,
    0.150000000000,0.113000000000,0.138000000000,0.074000000000,0.267000000000,0.227000000000,0.478000000000,
    0.522000000000,0.002000000000,0.021800000000,0.148000000000,0.204700000000,0.156500000000,0.248400000000,
    0.218600000000,1.000000000000,0.000600000000,0.001000000000,0.023400000000,0.189000000000,0.255000000000,
    0.249000000000,0.282000000000,1.000000000000,0.001400000000,0.016100000000,0.336000000000,0.229500000000,
    0.268000000000,0.149000000000,1.000000000000,0.001300000000,0.030500000000,0.143000000000,0.219000000000,
    0.161200000000,0.318000000000,0.127000000000,0.974100000000,0.025900000000,0.001620000000,0.052060000000,
    0.186060000000,0.272970000000,0.136290000000,0.351000000000,0.000120000000,0.999880000000,0.001200000000,
    0.263000000000,0.142800000000,0.307000000000,0.286000000000,0.374000000000,0.626000000000,0.000200000000,
    0.015800000000,0.016000000000,0.133000000000,0.161000000000,0.264000000000,0.410000000000,0.373000000000,
    0.627000000000,0.000100000000,0.007900000000,0.329000000000,0.338000000000,0.253000000000,0.072000000000,
    1.000000000000,0.001500000000,0.100000000000,0.169000000000,0.231000000000,0.132000000000,0.298000000000,
    0.068500000000,0.295240000000,0.704760000000,0.014000000000,0.241000000000,0.221000000000,0.524000000000,
    1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,
    1.000000000000,1.000000000000,0.000055000000,0.007200000000,0.992745000000,1.000000000000,1.000000000000,
    1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,1.000000000000,
    1.000000000000,1.000000000000,0.999855000000,0.000145000000,0.989160000000,0.010840000000,0.996330000000,
    0.003660000000,0.997576009706,0.000378998479,0.002044991815,0.950210000000,0.007450000000,0.042210000000,
    0.000130000000};
   
  int j=0;
  for (int Z=0; Z<=MAXAtomNo; Z++) {
    strcpy(Element[Z].Symbol,&el[Z][0]);
    strcpy(Orig[Z].Symbol,Element[Z].Symbol);    
    Orig[Z].NumIsotopes = Element[Z].NumIsotopes = sz[Z];

    Element[Z].IsoMass = new float[sz[Z]+1];
    Element[Z].IntMass = new int[sz[Z]+1];
    Element[Z].IsoProb = new float[sz[Z]+1];

    Orig[Z].IsoMass = new float[sz[Z]+1];
    Orig[Z].IntMass = new int[sz[Z]+1];
    Orig[Z].IsoProb = new float[sz[Z]+1];
    
    for (int i=0; i<Element[Z].NumIsotopes; i++) {
      Orig[Z].IsoMass[i]=Element[Z].IsoMass[i] = (float)m[j];
      Orig[Z].IsoProb[i]=Element[Z].IsoProb[i] = (float)r[j];
      Orig[Z].IntMass[i]=Element[Z].IntMass[i] = (int)(Element[Z].IsoMass[i]+0.5);
      j++;
    }
      
    Element[Z].NumAtoms = 0;
    Element[Z].IsoMass[Element[Z].NumIsotopes] = 0;
    Element[Z].IsoProb[Element[Z].NumIsotopes] = 0;
    Orig[Z].NumAtoms = 0;
    Orig[Z].IsoMass[Orig[Z].NumIsotopes] = 0;
    Orig[Z].IsoProb[Orig[Z].NumIsotopes] = 0;
  }

}

