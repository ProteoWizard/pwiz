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


#include <stdio.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <ctype.h>
#include <math.h>

#define MAXREADBACKS 200


/*=========================================================*/
/*=========================================================*/


/*
 * addaline - open the named file and add thetext then close the file
 *            profiling suggested that lots of open/closes were consuming
 *            a fair amount of time resources
 */
addaline(char *thefilename, char *thetext)
{
   FILE * fp;
   fp = fopen(thefilename,"a+");
   fprintf(fp,"%s\n",thetext);
   fclose(fp);
}

/*
 * addaline_persistant_ -it says "values" but really it could be any
 *                       file. on first call, all the file pointers 
 *                       are initialized to NULL.  Each readback
 *                       is only opened once.  "thefilename" is
 *                       ignored if a file has already been opened
 *                       for that "readback"
 *
 *                       fp's are closed passively when the program
 *                       terminates....

% time     seconds  usecs/call     calls    errors syscall
------ ----------- ----------- --------- --------- ----------------
51.19    0.184211          16     11239           read
22.82    0.082122         207       396         1 open
12.07    0.043444         218       199           close
10.07    0.036253          69       524           write
1.45    0.005226          53        99           mkdir
0.99    0.003575          12       295           munmap
0.70    0.002516           6       393           mmap2
0.51    0.001852           5       396           fstat64
0.15    0.000532         532         1           execve
0.02    0.000057           7         8           old_mmap
0.01    0.000020           7         3         3 access
0.00    0.000017           4         4           brk
0.00    0.000005           5         1           uname
0.00    0.000004           4         1           set_thread_area
------ ----------- ----------- --------- --------- ----------------
100.00    0.359834                 13559         4 total

VERSUS 

% time     seconds  usecs/call     calls    errors syscall
------ ----------- ----------- --------- --------- ----------------
47.52  214.162355         335    639649           close
42.13  189.888046         297    639748         1 open
3.03   13.655805          21    639646           write
2.73   12.293394          19    639647           munmap
2.34   10.568895          17    639745           mmap2
2.18    9.837006          15    639748           fstat64
0.06    0.287479          26     11240           read
0.00    0.007198          73        99           mkdir
0.00    0.000534         534         1           execve
0.00    0.000057           7         8           old_mmap
0.00    0.000020           7         3         3 access
0.00    0.000017           4         4           brk
0.00    0.000005           5         1           uname
0.00    0.000004           4         1           set_thread_area
------ ----------- ----------- --------- --------- ----------------
100.00  450.700815               3849540         4 total

*/
addaline_persistant_values(char *thefilename, int readback, char *thetext)
{
   static int firstrun = 1;
   static FILE *fp[MAXREADBACKS];

   if(firstrun == 1)
   {
      int x;
      firstrun = 0;
      for(x=0;x<MAXREADBACKS;x++) fp[x] = NULL;
   }
   if(fp[readback] == NULL)  
      fp[readback] = fopen(thefilename,"a+");

   fprintf(fp[readback],"%s\n",thetext);
}


/*=========================================================*/
/*=========================================================*/


/*
 * dostats -- maintain a running calculation of the mean and 
 *            the sum of squared differences
 */
dostats(int readback, float thevalue,
		float themeans[],float thessds[],float thecounts[])
{
   float deltamean;

   deltamean = ( (thevalue - themeans[readback]) / (thecounts[readback]+1.0));
   thessds[readback] =  thessds[readback]
	               + ( thecounts[readback] * deltamean * deltamean );
   themeans[readback] = themeans[readback] + deltamean;
   thecounts[readback] = thecounts[readback] + 1.0;
   thessds[readback] = thessds[readback]
	               + (
		             (thevalue - themeans[readback])
		            *(thevalue - themeans[readback])
			 );

}


/*=========================================================*/

/*
 * parseline - pull out the scan/readback/name/value from a line
 *             and manage the storage of this information...
 *
 *             /BASENAME/READBACK is created if scan == 1
 *             name -> BASENAME/READBACK/name (only on scan 1)
 *             values -> BASENAME/READBACK/values (via addaline() )
 *             values -> means/ssds/counts ( via dostats() )
 */
parseline(char *theline,float themeans[],float thessds[],
                 float thecounts[],  char * basename)
{

   int scan, readback;
   float thevalue,deltamean;
   char *hop, *hop2;
   char buf[256];
   char thedir[256];
   char thefile[256];
   char outline[256];

   hop = theline;                /* _SCAN_23_3434_blah blah: 3.45 */

   while(!isdigit(*hop)) hop++;  /*       23_3434_blah blah: 3.45 */
    sscanf(hop,"%d",&scan);      /* scan <- 23                    */
   while(isdigit(*hop)) hop++;   /*         _3434_blah blah: 3.45 */
   while(!isdigit(*hop)) hop++;  /*          3434_blah blah: 3.45 */
    sscanf(hop,"%d",&readback);  /* readback <- 3434              */
   while(isdigit(*hop)) hop++;   /*              _blah blah: 3.45 */
   hop++;                        /*               blah blah: 3.45 */

   hop2 = hop;                   /* now split the remainder into  */
   while(*hop2 != ':') hop2++;   /*   two strings: name, val      */
   *hop2 = (char) 0;
   hop2++;                       /* hop="blah blah" hop2=" 3.45" */
   sscanf(hop2,"%f",&thevalue);

   sprintf(thedir,"%s/%d",basename,readback);

   if(scan == 1) 
   {
      mkdir(thedir,0755);
      sprintf(thefile,"%s/name",thedir);
      addaline(thefile,hop);
   }

   sprintf(thefile,"%s/values",thedir);
   sprintf(outline,"%d %f",scan,thevalue);
   /*
   addaline_persistant_values(thefile,readback,outline);
   */
   addaline(thefile,outline);
   
   dostats(readback,thevalue,themeans,thessds,thecounts);
}

/*=========================================================*/
/*=========================================================*/

CalcDevBins(char *basename,int readback, float mean,float stdev, int devbins[])
{
   FILE * fp;
   char filename[256];
   char theline[256];
   float theval;
   float delta;
   int x;

   for(x=0;x<5;x++) devbins[x] = 0;

   sprintf(filename,"%s/%d/values",basename,readback);
   fp = fopen(filename,"r");
   while( fgets(&(theline[0]),256,fp) != NULL)
   {
      sscanf(theline,"%*d %f",&theval);
      delta = fabsf(mean - theval);

           if (delta <= (stdev * 1.0)) devbins[0]++;
      else if (delta <= (stdev * 2.0)) devbins[1]++;
      else if (delta <= (stdev * 3.0)) devbins[2]++;
      else if (delta <= (stdev * 4.0)) devbins[3]++;
      else if (delta >  (stdev * 4.0)) devbins[4]++;
	       
   }

}

printstats(float themeans[],float thessds[], float thecounts[], char *basename)
{
   char thefile[256];
   char theline[256];
   int readback, devbins[5];
   float var;
   float stdev;


   for(readback=0;readback<MAXREADBACKS;readback++)
   {
      if(thecounts[readback] > 0)
      {
         var = thessds[readback]/(thecounts[readback]-1);
	 stdev = sqrt(var);
	 CalcDevBins(basename,readback,themeans[readback],stdev,devbins);

	 sprintf(theline,"%f %f %f %d %d %d %d %d",
			                        thecounts[readback],
			                        themeans[readback],
			                        stdev,
			                        devbins[0],
			                        devbins[1],
			                        devbins[2],
			                        devbins[3],
			                        devbins[4]);
	 sprintf(thefile,"%s/%d/stats",basename,readback);
	 addaline(thefile,theline);
      }
   }
}

/*=========================================================*/
/*=========================================================*/

main(int argc, char *argv[])
{
   float themeans[MAXREADBACKS];
   float thessds[MAXREADBACKS];
   float thecounts[MAXREADBACKS];

   int x;

   char theline[512];

   for(x=0;x<MAXREADBACKS; x++)
      themeans[x] = thessds[x] = thecounts[x] = 0.0;


   mkdir(argv[1],0755);

   while(gets(theline) != NULL)
   {
      if(
          (strncmp(theline,"_SCAN",5) == 0) &&     /* ONLY DO SCAN LINES  */
          ( isdigit(theline[strlen(theline)-2]) )  /* WITH NUMBERS...     */
        )                                          /* -2 for ^M or "3.23" */
        {                                          /* so is ok either way */
           parseline(theline,themeans,thessds,thecounts, argv[1]);
        }
   }
   printstats(themeans, thessds, thecounts, argv[1]);
}
