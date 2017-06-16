// Command line input:  peptideA, peptideB, pepAMod, pepBMod, crosslinker, reporterMass, precursorCharge

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <math.h>

#include "isotopes.h"

#define ION_SERIES_A                0
#define ION_SERIES_B                1
#define ION_SERIES_C                2
#define ION_SERIES_X                3
#define ION_SERIES_Y                4
#define ION_SERIES_Z                5

//#define ISOTOPE_TABLE    "/var/www/html/xlinkdb/react2prm_xlinkdb/isotopestable.txt"
#define ISOTOPE_TABLE    "isotopestable.txt"
#define PROTON_MASS      1.00727646688
#define DEFAULT_REPORTER 751.406065
#define SIZE_BUF         8192
#define _CRT_SECURE_NO_WARNINGS


struct OptionsStruct
{
   int iWhichPrecursor;  // 0=max isotope (default), 1=experimental, 2=monnoisotopic
   bool bInputFile;
} pOptions;

void ANALYZE(char *szPep1,
             char *szPep2,
             double dReporter,
             int iChargePrecursor,
             double *pdAAMass,
             bool bCleavableCrosslinker);
void USAGE(char *argv0);
void SET_OPTION(char *arg,
                struct OptionsStruct *pOptions);
void ENCODE_MOD(char *szPep,
                char *szMod,
                double *pdAAMass);
void READ_PRM(char *szFilePRM,
              int *iNumPRM);
void AssignMass(double *pdAAMass,
                int bMonoMasses);
double GetFragmentIonMass(int iWhichIonSeries,
                          int i,
                          int ctCharge,
                          double *pdAAforward,
                          double *pdAAreverse);
void CALCIONS(char *szPep,
              char *szPepString,
              double *pdAAMass,
              double dPrecursor,
              double dReporter,
              int iChargePrecursor,
              char szPepChar[],
              struct OptionsStruct pOptions,
              bool bCleavableCrosslinker);
void CALCPRECURSOR(double *dPrecursor,
                   char *szPep1,
                   char *szPep2,
                   double dReporter,
                   int iChargePrecursor,
                   double *pdAAMass,
                   int iWhichPrecursor);
bool SANITY_CHECK(char *szPep1,
                  char *szPep2,
                  char *szMod1,
                  char *szMod2,
                  double dReporter,
                  int iChargePrecursor);

#define MAX_SEQUENCE 256
int piCompC[MAX_SEQUENCE];
int piCompH[MAX_SEQUENCE];
int piCompN[MAX_SEQUENCE];
int piCompO[MAX_SEQUENCE];
int piCompS[MAX_SEQUENCE];

void INIT_COMP(int *piCompC,
      int *piCompH,
      int *piCompN,
      int *piCompO,
      int *piCompS);


int main(int argc, char **argv)
{
   int i;
   int iNumArg;
   int iStartArgc;
   int  iChargePrecursor;
   char *arg;
   char szPep1[1024];
   char szPep2[1024];
   char szMod1[1024];
   char szMod2[1024];
   double pdAAMass[128];
   double dReporter;

   iStartArgc = 1;
   iNumArg = 1;

   arg = argv[iNumArg];
   pOptions.bInputFile = false;

   // processing arguments
   while (iNumArg < argc)
   {
      if (arg[0] == '-')
         SET_OPTION(arg, &pOptions);
      else
         break;

      iStartArgc++;
      arg = argv[++iNumArg];
   }

   // check if proper number of command line inputes specified
   if (!(argc==7 && iStartArgc==1) && !(argc== 3 && iStartArgc==2))
   {
      USAGE(argv[0]);
   }

   AssignMass(pdAAMass, 1);

   if (pOptions.bInputFile)
   {
      FILE *fp;
      char szFile[1024];
      char szBuf[SIZE_BUF];

      strcpy(szFile, argv[iStartArgc++]);
      if ((fp = fopen(szFile, "r")) == NULL)
      {
         fprintf(stderr, " Error: could not open file %s\n", szFile);
         exit(1);
      }

      // get reporter mass on first line
      fgets(szBuf, SIZE_BUF, fp);
      sscanf(szBuf, "%lf", &dReporter);
      if (dReporter < 0.0)
      {
         fprintf(stderr, " Error: reporter mass is negative (mass=%s)\n", szBuf);
         exit(1);
      }

      bool bCleavableCrosslinker = true;
      if (dReporter < 0.1)
         bCleavableCrosslinker = false;

      printf("PrecursorName,PrecursorMz,ProductMz,PrecursorCharge,ProductCharge,MoleculeGroup,ProductName\n");
      while (fgets(szBuf, SIZE_BUF, fp))
      {
         sscanf(szBuf, "%s %s %s %s %d", szPep1, szPep2, szMod1, szMod2, &iChargePrecursor);

         if (SANITY_CHECK(szPep1, szPep2, szMod1, szMod2, dReporter, iChargePrecursor))
         {
            ENCODE_MOD(szPep1, szMod1, pdAAMass);
            ENCODE_MOD(szPep2, szMod2, pdAAMass);
            ANALYZE(szPep1, szPep2, dReporter, iChargePrecursor, pdAAMass, bCleavableCrosslinker);
         }
      }

      fclose(fp);
   }
   else
   {
      // Command line input:  peptideA, peptideB, pepAMod, pepBMod, crosslinker, reporterMass, precursorCharge
      sscanf(argv[iStartArgc], "%s", szPep1);
      iStartArgc++;
      sscanf(argv[iStartArgc], "%s", szPep2);
      iStartArgc++;
      sscanf(argv[iStartArgc], "%s", szMod1);
      iStartArgc++;
      sscanf(argv[iStartArgc], "%s", szMod2);
      iStartArgc++;
      sscanf(argv[iStartArgc], "%lf", &dReporter);
      iStartArgc++;
      sscanf(argv[iStartArgc], "%d", &iChargePrecursor);

      if (dReporter < 0.0)
      {
         fprintf(stderr, " Error: reporter mass is negative (mass=%f)\n", dReporter);
         exit(1);
      }

      bool bCleavableCrosslinker = true;
      if (dReporter < 0.1)
         bCleavableCrosslinker = false;

      if (SANITY_CHECK(szPep1, szPep2, szMod1, szMod2, dReporter, iChargePrecursor))
      {
         //Now must encode modifications into peptide string (only to be decoded again in CALCIONS)
         ENCODE_MOD(szPep1, szMod1, pdAAMass);
         ENCODE_MOD(szPep2, szMod2, pdAAMass);

         printf("PrecursorName,PrecursorMz,ProductMz,PrecursorCharge,ProductCharge,MoleculeGroup,ProductName\n");
         ANALYZE(szPep1, szPep2, dReporter, iChargePrecursor, pdAAMass, bCleavableCrosslinker);
      }
   }

   return(0);
}


void USAGE(char *argv0)
{
   fprintf(stderr, " USAGE:\n");
   fprintf(stderr, "         %s [options] pepA pepB modA modB xlink-string reporterMass precursorCharge\n", argv0);
   fprintf(stderr, "     or  %s -f /input/file.txt\n", argv0);
   fprintf(stderr, "\n");
   fprintf(stderr, "   [options] specifies:\n");
   fprintf(stderr, "           -m               report theoretical monoisotopic precursor m/z\n");
   fprintf(stderr, "                            default is to report theoretical most intense isotope m/z\n");
   fprintf(stderr, "\n");
   fprintf(stderr, " Input file format:\n");
   fprintf(stderr, "    released_reporter_mass\n");
   fprintf(stderr, "    pepA pepB modA modB precursorCharge\n");
   fprintf(stderr, "    pepA pepB modA modB precursorCharge\n");
   fprintf(stderr, "      :    :    :    :      :\n");
   fprintf(stderr, "\n");
   fprintf(stderr, " Download example file and see documentation at http://xlinkdb.gs.washington.edu/xlinkdb/prmTransitionForm.php\n");
   fprintf(stderr, "\n");
   fprintf(stderr, " Default is to report maximum isotope precursor m/z based on.\n");
   fprintf(stderr, " J.A.Yergey, Int. J. Mass Spectrom. Ion Phys. 1983, 52, 337-349\n");
   fprintf(stderr, "\n");

   exit(1);
}


void SET_OPTION(char *arg,
      struct OptionsStruct *pOptions)
{
   int bSkipK = 0;  // set to 1 if '-S' option used, then ignore '-K' option

   switch (arg[1])
   {
      case 'm':
         pOptions->iWhichPrecursor = 2;  // mono m/z
         break;
      case 'f':
         pOptions->bInputFile = true;
         break;
      default:
         break;
   }

   arg[0] = '\0';
}

void ENCODE_MOD(char *szPep,
                char *szMod,
                double *pdAAMass)
{
   // szMod has encoding of form:  <mod1>@<pos1>,<mod2>@<pos2>,...
   // So tokenize on comma

   char *tok;
   char *pStr;
   char delims[] = ",";

   char szPosition[128];
   int iPosition;
   double dMass;
   double pdMods[128];
   double dNtermMod = 0.0;
   double dCtermMod = 0.0;

   for (int i=0;i<128; i++)
      pdMods[i] = 0.0;

   tok = strtok(szMod, delims);
   while (tok != NULL)
   {
      if ( (pStr = strchr(tok, '@'))==NULL)
      {
         fprintf(stderr, " Error: modification string is missing @:  %s\n", szMod);
         exit(1);
      }

      *pStr = ' ';
      sscanf(tok, "%lf %s", &dMass, szPosition);

      if (szPosition[0] == '[') //n-term
         dNtermMod = dMass;
      if (szPosition[0] == ']') //n-term
         dCtermMod = dMass;
      else
      {
         sscanf(szPosition, "%d", &iPosition);
         if (iPosition > 0)
            pdMods[iPosition-1] = dMass;
         else
         {
            fprintf(stderr, " Error: modification position wrong (<= 0):  %s\n", szMod);
            exit(1);
         }
      }

//    printf("token '%s', mass %f, pos %d\n", tok, dMass, iPosition);

      tok = strtok(NULL, delims);
   }


   char szTmp[1024];
   strcpy(szTmp, szPep);
   szPep[0]='\0';

   if (dNtermMod > 0.0)
      sprintf(szPep, "n[%0.2f]", dNtermMod);  // n-term group H - H
   for (int i=0; i<strlen(szTmp); i++)
   {
      sprintf(szPep+strlen(szPep), "%c", szTmp[i]);
      if (pdMods[i] != 0.0)
         sprintf(szPep+strlen(szPep), "[%0.2lf]", pdMods[i] + pdAAMass[szTmp[i]]);
   }
   if (dCtermMod > 0.0)
      sprintf(szPep+strlen(szPep), "c[%0.2f]", dCtermMod + pdAAMass['o'] + pdAAMass['h'] + pdAAMass['h']); // c-term group OH + H

// printf("\npep:  %s\n\n", szPep);
}


bool SANITY_CHECK(char *szPep1,
                  char *szPep2,
                  char *szMod1,
                  char *szMod2,
                  double dReporter,
                  int iChargePrecursor)
{
   int i;

   if (strlen(szPep1)<3)
   {
      fprintf(stderr, " Error: peptide1 too short (%s)\n", szPep1);
      exit(1);
   }

   if (strlen(szPep2)<3)
   {
      fprintf(stderr, " Error: peptide2 too short (%s)\n", szPep2);
      exit(1);
   }

   if (strlen(szMod1)<3)
   {
      fprintf(stderr, " Error: modstring1 too short (%s)\n", szMod1);
      exit(1);
   }

   if (strlen(szMod2)<3)
   {
      fprintf(stderr, " Error: modstring2 too short (%s)\n", szMod2);
      exit(1);
   }

   if (iChargePrecursor<1 || iChargePrecursor>10)
   {
      fprintf(stderr, " Error: precursor charge out of range (%d)\n", iChargePrecursor);
      exit(1);
   }

   if (dReporter < 0.0 || dReporter > 5000.0)
   {
      fprintf(stderr, " Error: reporter mass out of range (%f)\n", dReporter);
      exit(1);
   }

   if (!strchr(szMod1, '@'))  // modifications must have @ character
   {
      fprintf(stderr, " Error: modstring1 missing '@' character (%s)\n", szMod1);
      exit(1);
   }

   if (!strchr(szMod2, '@'))  // modifications must have @ character
   {
      fprintf(stderr, " Error: modstring2 missing '@' character (%s)\n", szMod2);
      exit(1);
   }

   for (i=0; i<strlen(szPep1); i++) // peptides must just be alphabetical characters
   {
      if (isalpha(szPep1[i]))
         szPep1[i] = toupper(szPep1[i]);
      else
      {
         fprintf(stderr, "Error: peptide1 has non-alphabetical character (%s)\n", szPep1);
         exit(1);
      }
   }

   for (i=0; i<strlen(szPep2); i++)
   {
      if (isalpha(szPep2[i]))
         szPep2[i] = toupper(szPep2[i]);
      else
      {
         fprintf(stderr, "Error: peptide2 has non-alphabetical character (%s)\n", szPep2);
         exit(1);
      }
   }

   return true;
}


void ANALYZE(char *szPep1,
             char *szPep2,
             double dReporter,
             int iChargePrecursor,
             double *pdAAMass,
             bool bCleavableCrosslinker)
{
   if (pOptions.iWhichPrecursor == 0)  // most intense isotope peak
   {
      INIT_COMP(piCompC, piCompH, piCompN, piCompO, piCompS);
   }

   char szPepString[512];
   double dPrecursor;

   // Need these info:  dPrecursor, iChargePrecursor, szPep1, szPep2);

   sprintf(szPepString, "%s_%s", szPep1, szPep2);

   CALCPRECURSOR(&dPrecursor, szPep1, szPep2, dReporter, iChargePrecursor, pdAAMass, pOptions.iWhichPrecursor);

   // now that we have each peptide, need to create the PRM file to load into Skyline
   char szPepChar[12];
   strcpy(szPepChar, "A"); //\u03B1");
   CALCIONS(szPep1, szPepString, pdAAMass, dPrecursor, dReporter, iChargePrecursor, szPepChar, pOptions, bCleavableCrosslinker);
   strcpy(szPepChar, "B"); //\u03B2");
   CALCIONS(szPep2, szPepString, pdAAMass, dPrecursor, dReporter, iChargePrecursor, szPepChar, pOptions, bCleavableCrosslinker);
}


void CALCPRECURSOR(double *dPrecursor,
                   char *szPep1,
                   char *szPep2,
                   double dReporter,
                   int iChargePrecursor,
                   double *pdAAMass,
                   int iWhichPrecursor)
{
   double dMod;
   double dPepMass1;
   double dPepMass2;
   int i;
   int ii;
   int iLen;

   iLen = strlen(szPep1);

   // print out the peptide and peptide plus long arm masses
  
   dPepMass1 = pdAAMass['h'] + pdAAMass['o'] + pdAAMass['h']; // h + oh for cterm, h - h for n-term

   for (i=0; i<iLen; i++)
   {  

      dMod = 0.0;

      if (szPep1[i+1]=='[')
      {  
         sscanf(szPep1+i+2, "%lf]", &dMod);
         while (szPep1[i]!=']')
            i++;
      }

      if (dMod != 0.0)
      {  
         if (fabs(dMod - 325.129) < 0.1)        //BDP-NHP
            dPepMass1 += 325.12918305;
         else if (fabs(dMod - 182.106) < 0.1)   //DSSO
            dPepMass1 += 182.106076;
         else if (fabs(dMod - 170.106) < 0.1)   //??
            dPepMass1 += 170.10552805;
         else if (fabs(dMod - 329.155) < 0.1)   //silac heavy BDP-NHP
            dPepMass1 += 329.15468905;
         else
            dPepMass1 += dMod;
      }
      else
         dPepMass1 += pdAAMass[szPep1[i]];
   }
 
   iLen = strlen(szPep2);

   // print out the peptide and peptide plus long arm masses
  
   dPepMass2 = pdAAMass['h'] + pdAAMass['o'] + pdAAMass['h']; // h + oh for nterm, cterm

   for (i=0; i<iLen; i++)
   {  
      dMod = 0.0;

      if (szPep2[i+1]=='[')
      {  
         sscanf(szPep2+i+2, "%lf]", &dMod);
         while (szPep2[i]!=']')
            i++;
      }

      if (dMod != 0.0)
      {
         if (fabs(dMod - 325.129) < 0.1)       //BDP-NHP
            dPepMass2 += 325.12918305;
         else if (fabs(dMod - 182.106) < 0.1)  //DSSO
            dPepMass2 += 182.106076;
         else if (fabs(dMod - 170.106) < 0.1)
            dPepMass2 += 170.10552805;
         else if (fabs(dMod - 329.155) < 0.1)  //silac heavy BDP-NHP
            dPepMass2 += 329.15468905;
         else
            dPepMass2 += dMod;
      }
      else
         dPepMass2 += pdAAMass[szPep2[i]];
   }


   if (iWhichPrecursor == 0) // need to determine max isotope peak
   {
      char szComp[128];

      // figure out which peak is most intense; add appropriate number of C13 masses to dNeutralMass

      int iC = 50;  // cross linker BDP-NHP
      int iH = 71;
      int iN = 11;
      int iO = 18;
      int iS = 1;

      if (fabs(dReporter - 0.0) < 1.0)  // DSSO has a zero reporter mass
      {
         iC = 6;
         iH = 6;
         iN = 0;
         iO = 3;
         iS = 1;
      }

      for (i=0; i<strlen(szPep1); i++)
      {
         iC += piCompC[(int)szPep1[i]];
         iH += piCompH[(int)szPep1[i]];
         iN += piCompN[(int)szPep1[i]];
         iO += piCompO[(int)szPep1[i]];
         iS += piCompS[(int)szPep1[i]];
      }
      for (i=0; i<strlen(szPep2); i++)
      {
         iC += piCompC[(int)szPep2[i]];
         iH += piCompH[(int)szPep2[i]];
         iN += piCompN[(int)szPep2[i]];
         iO += piCompO[(int)szPep2[i]];
         iS += piCompS[(int)szPep2[i]];
      }

      szComp[0]='\0';
      sprintf(szComp, "C%dH%dN%dO%dS%d", iC, iH, iN, iO, iS);

      int errnr, prec = 8, z = 0;
      char  *end;
      bool normalize = false, masstocharge = false;
      double thr = 0.001, res = 0.1, filter = 0;        //do not output any peaks of intensity < filter
      IsoCalc mycalc;

      //normalize = true;        //if normalisation to 100% is required
      masstocharge = true;     //choose whether to output mass or mass-to-charge ratio
      z=1;

      errnr = mycalc.ReadAtomTable(ISOTOPE_TABLE);
      if (errnr)
      {
         fprintf(stderr, "Error: cannot read isotope table: %s\n", ISOTOPE_TABLE);
         exit(1);
      }

      errnr = mycalc.SetComposition(szComp);
      if (errnr)
      {
         fprintf(stderr, "Error:  cannot parse formula\n");
         exit(1);
      }
      mycalc.SetCharge(z);
      mycalc.SetThr(thr);
      mycalc.SetMassToCharge(masstocharge);
      mycalc.SetDegen(res);
      cout.precision(prec);     // set the precision
      errnr = mycalc.Calculate();
      if (errnr)
      {
         fprintf(stderr, "Error:  cannot calculate distributions\n");
         exit(1);
      }                         // this should never happen - but just in case

      int npeaks;
      double mass, abun;
      mycalc.GetNPeaks(npeaks);
      int iMaxIsotope = 0;
      double dMaxAbun = 0;

      for (int i = 0; i < npeaks; i++)
      {                         //output all the peaks, separated by tab or comma
         mycalc.Peak(i, mass, abun);
         if (abun > dMaxAbun)
         {
            iMaxIsotope = i;
            dMaxAbun = abun;
         }
      }

      *dPrecursor = (dPepMass1 + dPepMass2 + dReporter + iChargePrecursor*PROTON_MASS + 1.003355*iMaxIsotope) / iChargePrecursor;
   }
   else  // use monoisotopic m/z
   {
      *dPrecursor = (dPepMass1 + dPepMass2 + dReporter + iChargePrecursor*PROTON_MASS) / iChargePrecursor;
   }
}


void CALCIONS(char *szPep,
              char *szPepString,
              double *pdAAMass,
              double dPrecursor,
              double dReporter,
              int iChargePrecursor,
              char *szPepChar,
              struct OptionsStruct pOptions,
              bool bCleavableCrosslinker)
{
   double dMod;
   double dBion;
   double dYion;
   double dPepMass;
   int i;
   int ii;
   int iLen;

   iLen = strlen(szPep);

   // print out the peptide and peptide plus long arm masses
  
   dPepMass = pdAAMass['h'] + pdAAMass['o'] + pdAAMass['h']; // h + oh for nterm, cterm

   for (i=0; i<iLen; i++)
   {  
      dMod = 0.0;

      if (szPep[i+1]=='[')
      {  
         sscanf(szPep+i+2, "%lf]", &dMod);
         while (szPep[i]!=']')
            i++;
      }

      if (dMod != 0.0)
      {
         if (fabs(dMod - 325.129) < 0.1)
            dPepMass += 325.12918305;
         else if (fabs(dMod - 182.106) < 0.1)  //DSSO
            dPepMass += 182.106076;
         else if (fabs(dMod - 170.106) < 0.1)
            dPepMass += 170.10552805;
         else if (fabs(dMod - 329.155) < 0.1)
            dPepMass += 329.15468905;
         else
            dPepMass += dMod;
      }
      else
         dPepMass += pdAAMass[szPep[i]];
   }

   if (bCleavableCrosslinker)
   {
      // released precursor
      for (int i=1; i<=3; i++)
         printf("%s,%f,%f,%d,%d,peptide,%s precursor %d+\n", szPepString, dPrecursor, (dPepMass + i*PROTON_MASS)/i, iChargePrecursor, i, szPepChar, i);
      // released long arm
      for (int i=1; i<=3; i++)
         printf("%s,%f,%f,%d,%d,peptide,%s long arm %d+\n", szPepString, dPrecursor, (dPepMass + dReporter + i*PROTON_MASS)/i, iChargePrecursor, i, szPepChar, i);
   }
   else if (!strcmp(szPepChar, "A"))  // the first call for non-cleavable crosslinker
   {
      // for non-cleavable cross-linker, this is only printed once for the two calls to CALCIONS
      for (int i=1; i<=iChargePrecursor; i++)
         printf("%s,%f,%f,%d,%d,peptide,%s precursor %d+\n", szPepString, dPrecursor, (dPepMass + i*PROTON_MASS)/i, iChargePrecursor, i, szPepChar, i);
   }

   // Loop through peptide and calculate fragment ions now



   int iEndCharge;
   if (bCleavableCrosslinker)
      iEndCharge = 1;
   else
      iEndCharge = 3;

   int iResiduePos;
   for (int iCharge=1; iCharge<=iEndCharge; iCharge++)
   {
      dBion = 0.0;
      iResiduePos=1;

      for (i=0; i<iLen-1; i++)
      {
         dMod = 0.0;

         if (szPep[i+1]=='[')
         {
            sscanf(szPep+i+2, "%lf]", &dMod);
            while (szPep[i]!=']')
               i++;
         }

         if (dMod != 0.0)
         {
            if (fabs(dMod - 325.129) < 0.1)        //BDP-NHP
               dBion += 325.12918305;
            else if (fabs(dMod - 182.106) < 0.1)   //DSSO
               dBion += 182.106076;
            else if (fabs(dMod - 170.106) < 0.1)
               dBion += 170.10552805;
            else if (fabs(dMod - 329.155) < 0.1)   //silac heavy BDP-NHP
               dBion += 329.15468905;
            else
               dBion += dMod;
         }
         else
            dBion += pdAAMass[szPep[i]];

         if (dBion > 50.0)
            printf("%s,%f,%f,%d,%d,peptide,%s b%d", szPepString, dPrecursor, (dBion + iCharge*PROTON_MASS)/iCharge, iChargePrecursor, 1, szPepChar, iResiduePos);

         for (int ii=1; ii<=iCharge; ii++)
            printf("+");
         printf("\n");

         iResiduePos++;
      }

      dYion = pdAAMass['o'] + pdAAMass['h'] + pdAAMass['h'];
      iResiduePos=1;

      for (i=iLen-1; i>0; i--)
      {
         dMod = 0.0;

         if (szPep[i]==']')
         {
            while (szPep[i]!='[')
               i--;
            sscanf(szPep+i+1, "%lf]", &dMod);
            i--; // move i to point to the residue
         }

         if (dMod != 0.0)
         {
            if (fabs(dMod - 325.13) < 0.1)
               dYion += 325.12918305;
            else if (fabs(dMod - 182.106) < 0.1)
               dYion += 182.106076;
            else if (fabs(dMod - 170.106) < 0.1)
               dYion += 170.10552805;
            else if (fabs(dMod - 329.155) < 0.1)
               dYion += 329.15468905;
           else
               dYion += dMod;
         }
         else
            dYion += pdAAMass[szPep[i]];

         if (dYion > 50.0)
            printf("%s,%f,%f,%d,%d,peptide,%s y%d", szPepString, dPrecursor, (dYion + iCharge*PROTON_MASS)/iCharge, iChargePrecursor, 1, szPepChar, iResiduePos);

         for (int ii=1; ii<=iCharge; ii++)
            printf("+");
         printf("\n");

         iResiduePos++;
      }
   }
}


double GetFragmentIonMass(int iWhichIonSeries,
                          int i,
                          int ctCharge,
                          double *pdAAforward,
                          double *pdAAreverse)
{
   double dFragmentIonMass = 0.0;

   switch (iWhichIonSeries)
   {
      case ION_SERIES_B:
         dFragmentIonMass = pdAAforward[i];
         break;

      case ION_SERIES_Y:
         dFragmentIonMass = pdAAreverse[i];
         break;

      case ION_SERIES_A:
         dFragmentIonMass = pdAAforward[i] - pdAAforward['c'] - pdAAforward['o'];
         break;

      case ION_SERIES_C:
         dFragmentIonMass = pdAAforward[i] + pdAAforward['n'] + pdAAforward['h'] + pdAAforward['h'] + pdAAforward['h'];
         break;

      case ION_SERIES_X:
         dFragmentIonMass = pdAAreverse[i] + pdAAforward['c'] + pdAAforward['o'] - pdAAforward['h'] - pdAAforward['h'];
         break;

      case ION_SERIES_Z:
         dFragmentIonMass = pdAAreverse[i] - pdAAforward['n'] - pdAAforward['h'] - pdAAforward['h'];
         break;
   }

   return (dFragmentIonMass + (ctCharge-1)*PROTON_MASS)/ctCharge;
}


void AssignMass(double *pdAAMass,
                int bMonoMasses)
{
   double H, O, C, N, S;

   if (bMonoMasses) // monoisotopic masses
   {
      H = pdAAMass['h'] =  1.007825035; // hydrogen
      O = pdAAMass['o'] = 15.99491463;  // oxygen
      C = pdAAMass['c'] = 12.0000000;   // carbon
      N = pdAAMass['n'] = 14.0030740;   // nitrogen
      S = pdAAMass['s'] = 31.9720707;   // sulphur
   }
   else  // average masses
   {
      H = pdAAMass['h'] =  1.00794;
      O = pdAAMass['o'] = 15.9994;
      C = pdAAMass['c'] = 12.0107;
      N = pdAAMass['n'] = 14.0067;
      S = pdAAMass['s'] = 32.065;
   }

   pdAAMass['G'] = C*2  + H*3  + N   + O ;
   pdAAMass['A'] = C*3  + H*5  + N   + O ;
   pdAAMass['S'] = C*3  + H*5  + N   + O*2 ;
   pdAAMass['P'] = C*5  + H*7  + N   + O ;
   pdAAMass['V'] = C*5  + H*9  + N   + O ;
   pdAAMass['T'] = C*4  + H*7  + N   + O*2 ;
   pdAAMass['C'] = C*3  + H*5  + N   + O   + S ;
   pdAAMass['L'] = C*6  + H*11 + N   + O ;
   pdAAMass['I'] = C*6  + H*11 + N   + O ;
   pdAAMass['N'] = C*4  + H*6  + N*2 + O*2 ;
   pdAAMass['D'] = C*4  + H*5  + N   + O*3 ;
   pdAAMass['Q'] = C*5  + H*8  + N*2 + O*2 ;
   pdAAMass['K'] = C*6  + H*12 + N*2 + O ;
   pdAAMass['E'] = C*5  + H*7  + N   + O*3 ;
   pdAAMass['M'] = C*5  + H*9  + N   + O   + S ;
   pdAAMass['H'] = C*6  + H*7  + N*3 + O ;
   pdAAMass['F'] = C*9  + H*9  + N   + O ;
   pdAAMass['R'] = C*6  + H*12 + N*4 + O ;
   pdAAMass['Y'] = C*9  + H*9  + N   + O*2 ;
   pdAAMass['W'] = C*11 + H*10 + N*2 + O ;

   pdAAMass['O'] = C*5  + H*12 + N*2 + O*2 ;

   pdAAMass['B'] = 0.0;
   pdAAMass['J'] = 0.0;
   pdAAMass['U'] = 0.0;
   pdAAMass['X'] = 0.0;
   pdAAMass['Z'] = 0.0;
}


void INIT_COMP(int *piCompC,
      int *piCompH,
      int *piCompN,
      int *piCompO,
      int *piCompS)
{
   int i;

   for (i=0; i<128; i++)
   {  
      piCompC[i]=0;
      piCompH[i]=0;
      piCompN[i]=0;
      piCompO[i]=0;
      piCompS[i]=0;
   }

   piCompC['G'] = 2  ;
   piCompC['A'] = 3  ;
   piCompC['S'] = 3  ;
   piCompC['P'] = 5  ;
   piCompC['V'] = 5  ;
   piCompC['T'] = 4  ;
   piCompC['C'] = 3  ;
   piCompC['L'] = 6  ;
   piCompC['I'] = 6  ;
   piCompC['N'] = 4  ;
   piCompC['D'] = 4  ;
   piCompC['Q'] = 5  ;
   piCompC['K'] = 6  ;
   piCompC['E'] = 5  ;
   piCompC['M'] = 5  ;
   piCompC['H'] = 6  ;
   piCompC['F'] = 9  ;
   piCompC['R'] = 6  ;
   piCompC['Y'] = 9  ;
   piCompC['W'] = 11 ;
   piCompC['O'] = 5  ;

   piCompH['G'] = 3  ;
   piCompH['A'] = 5  ;
   piCompH['S'] = 5  ;
   piCompH['P'] = 7  ;
   piCompH['V'] = 9  ;
   piCompH['T'] = 7  ;
   piCompH['C'] = 5  ;
   piCompH['L'] = 11 ;
   piCompH['I'] = 11 ;
   piCompH['N'] = 6  ;
   piCompH['D'] = 5  ;
   piCompH['Q'] = 8  ;
   piCompH['K'] = 12 ;
   piCompH['E'] = 7  ;
   piCompH['M'] = 9  ;
   piCompH['H'] = 7  ;
   piCompH['F'] = 9  ;
   piCompH['R'] = 12 ;
   piCompH['Y'] = 9  ;
   piCompH['W'] = 10 ;
   piCompH['O'] = 12 ;

   piCompN['G'] = 1 ;
   piCompN['A'] = 1 ;
   piCompN['S'] = 1 ;
   piCompN['P'] = 1 ;
   piCompN['V'] = 1 ;
   piCompN['T'] = 1 ;
   piCompN['C'] = 1 ;
   piCompN['L'] = 1 ;
   piCompN['I'] = 1 ;
   piCompN['N'] = 2 ;
   piCompN['D'] = 1 ;
   piCompN['Q'] = 2 ;
   piCompN['K'] = 2 ;
   piCompN['E'] = 1 ;
   piCompN['M'] = 1 ;
   piCompN['H'] = 3 ;
   piCompN['F'] = 1 ;
   piCompN['R'] = 4 ;
   piCompN['Y'] = 1 ;
   piCompN['W'] = 2 ;
   piCompN['O'] = 2 ;

   piCompO['G'] = 1 ;
   piCompO['A'] = 1 ;
   piCompO['S'] = 2 ;
   piCompO['P'] = 1 ;
   piCompO['V'] = 1 ;
   piCompO['T'] = 2 ;
   piCompO['C'] = 1 ;
   piCompO['L'] = 1 ;
   piCompO['I'] = 1 ;
   piCompO['N'] = 2 ;
   piCompO['D'] = 3 ;
   piCompO['Q'] = 2 ;
   piCompO['K'] = 1 ;
   piCompO['E'] = 3 ;
   piCompO['M'] = 1 ;
   piCompO['H'] = 1 ;
   piCompO['F'] = 1 ;
   piCompO['R'] = 1 ;
   piCompO['Y'] = 2 ;
   piCompO['W'] = 1 ;
   piCompO['O'] = 2 ;

   piCompS['C'] = 1;
   piCompS['M'] = 1;

}

