// $Id$
/***************************************************************************
                             RAMP


Non sequential parser for mzXML files
and mzData files, too!
and mzML, if you have the ProteoWizard pwiz library from Spielberg Family Proteomics Center
and gzipped versions of all of these if you have pwiz

                             -------------------
    begin                : Wed Oct 10
    copyright            : (C) 2003 by Pedrioli Patrick, ISB, Proteomics
    email                : ppatrick@student.ethz.ch
    additional work for C++, >2GB files in WIN32, and portability (C) 2004 by Brian Pratt, Insilicos LLC 
    additional work for mzData input (C) 2005 Brian Pratt Insilicos LLC
 ***************************************************************************/

/***************************************************************************
*    This program is free software; you can redistribute it and/or modify  *
*    it under the terms of the GNU Library or "Lesser" General Public      *
*    License (LGPL) as published by the Free Software Foundation;          *
*    either version 2 of the License, or (at your option) any later        *
*    version.                                                              *
***************************************************************************/

// TODO:
// merged_scan stuff is only coded for mzXML - can it be applied to mzData and mzML?
//

#define RAMP_HOME

#include "ramp.h"

#undef SIZE_BUF
#define SIZE_BUF 512

#include <vector>
#include <algorithm>
#ifdef HAVE_PWIZ_MZML_LIB
#include <iostream>
#include <exception>
#include <pwiz/data/msdata/RAMPAdapter.hpp>
#ifdef HAVE_PWIZ_RAW_LIB  // use RAMP+pwiz+xcalibur to read .raw
#include <pwiz/data/vendor_readers/Reader_Thermo.hpp>
#endif
#define MZML_TRYBLOCK try {
#define MZML_CATCHBLOCK } catch (std::exception& e) { std::cout << e.what() << std::endl;  } catch (...) { std::cout << "Caught unknown exception." << std::endl;  }
#endif
#ifdef RAMP_HAVE_GZ_INPUT
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"  // for reading mzxml.gz
#endif
#ifdef WINDOWS_NATIVE
#include "wglob.h"		//glob for windows
#else
#include <glob.h>		//glob for real
#endif
#ifdef TPPLIB
#include "common/util.h"
#include <inttypes.h>
#else
// local copies of stuff in TPP's sysdepend.h, and empty macro versions of some stuff as well

#ifdef _MSC_VER
#if _MSC_VER < 1900
typedef unsigned long uint32_t;
typedef unsigned __int64 uint64_t;
#endif
#define S_ISREG(mode) ((mode)&_S_IFREG)
#define S_ISDIR(mode) ((mode)&_S_IFDIR) 
#pragma warning(disable:4305) // don't bark about double to float conversion
#pragma warning(disable:4244) // don't bark about double to float conversion
#pragma warning(disable:4786) // don't bark about "identifier was truncated to '255' characters in the browser information"
#pragma warning(disable:4996) // don't bark about "unsafe" functions
#pragma warning(disable:4189) // don't bark about "local variable is initialized but not referenced"
#pragma warning(disable:4706) // don't bark about "assignment within conditional expression"

#define strcasecmp stricmp
#endif
#define fixPath(a,b)
#define unCygwinify(a)
#endif

#if defined __LITTLE_ENDIAN
#define swapbytes(x) ntohl(x)  /* use system byteswap (ntohl is a noop on bigendian systems) */
#else
uint32_t swapbytes(uint32_t x) {
  return ((x & 0x000000ffU) << 24) | 
         ((x & 0x0000ff00U) <<  8) | 
         ((x & 0x00ff0000U) >>  8) | 
         ((x & 0xff000000U) >> 24);
}
#endif

uint64_t swapbytes64(uint64_t x) {
  return ((((uint64_t)swapbytes((uint32_t)(x & 0xffffffffU)) << 32) | 
            (uint64_t)swapbytes((uint32_t)(x >> 32))));
}

// 
// do casts through unions to avoid running afoul of gcc strict aliasing
//
typedef union {
   uint32_t u32;
   float flt;
} U32;

typedef union {
   uint64_t u64;
   double dbl;
} U64;

long G_RAMP_OPTION = DEFAULT_OPTION;

/****************************************************************
 * Utility functions					*
 ***************************************************************/


static const char *findquot(const char *cp) { /* " and ' are both valid attribute delimiters */
   const char *result = strchr(cp,'\"');
   if (!result) {
      result = strchr(cp,'\'');
   }
   return result;
}

#ifndef TPPLIB  // stuff TPPlib provides
static int isPathSeperator(char c) {
	return (('/'==c)||('\\'==c));
}

static const char *findRightmostPathSeperator_const(const char *path) { // return pointer to rightmost / or \ .
   const char *result = path+strlen(path);
   while (result-->path) {
      if (isPathSeperator(*result)) {
         return result;
      }
   }
   return NULL; // no match
}

static char *findRightmostPathSeperator(char *path) { // return pointer to rightmost / or \ .
   return (char *)findRightmostPathSeperator_const(path);
}
#endif

static int isquot(const char c) { /* " and ' are both valid attribute delimiters */
      return ('\"'==c)||('\''==c);
}

static int setTagValue(const char* text,
      char* storage,
      int maxlen,
      const char* lead);

const char* skipspace(const char* pStr)
{
	while (isspace(*pStr))
		pStr++;
	if (*pStr == '\0')
		return NULL;
	return pStr;
}

static void getIsLittleEndian(const char *buf, int *result) {
   const char *p = strstr(buf,"byteOrder");
   if (p) {
      p = findquot(p);
      if (p++) {
         *result = (0!=strncmp(p,"network",7));
      }
   }
   // leave *result alone if we don't see anything!
}

/**************************************************
* open and close files *
**************************************************/
RAMPFILE *rampOpenFile(const char *filename) {
	// verify that this is an existing ordinary file (not a dir)
	struct stat pFileStat;
	if (!filename ||
		!((!stat(filename, &pFileStat)) && S_ISREG(pFileStat.st_mode))) {
	   return NULL;
	}

   RAMPFILE *result = (RAMPFILE *)calloc(1,sizeof(RAMPFILE));
   if (result) {
      int bOK;
#ifdef RAMP_HAVE_GZ_INPUT
	 result->fileHandle = random_access_gzopen(filename);
     bOK = (result->fileHandle != NULL);
#elif defined(RAMP_NONNATIVE_LONGFILE)
     result->fileHandle = open(filename,_O_BINARY|_O_RDONLY);
     bOK = (result->fileHandle >= 0);
#else
     result->fileHandle = fopen(filename,"rb");
     bOK = (result->fileHandle != NULL);
#endif
     if (!bOK) {
        free(result);
        result = NULL;
     } else {
        char buf[1024];
		int bRecognizedFormat = 0;
		int n_nonempty_lines = 0;
        buf[sizeof(buf)-1] = 0;
        while (!ramp_feof(result)) {
           char *fgot=ramp_fgets(buf,sizeof(buf)-1,result);
           if (strstr(buf,"<msRun")) {
              result->bIsMzData = 0;
			  bRecognizedFormat = 1;
#ifndef HAVE_PWIZ_MZML_LIB  // use pwiz to read mzXML without newlines
			  break; // no pwiz, hope for the best
#endif
		   } else if (strstr(buf,"<mzData")) {
              result->bIsMzData = 1;
			  bRecognizedFormat = 1;
			  break;
		   }
#ifdef HAVE_PWIZ_MZML_LIB
           if ((bRecognizedFormat && !strchr(buf,'\n')) || // mzXML without newlines
			   strstr(buf,"<mzML") || strstr(buf,"<indexedmzML")
#ifdef HAVE_PWIZ_RAW_LIB  // use RAMP+pwiz+xcalibur to read .raw
			   || pwiz::msdata::Reader_Thermo::hasRAWHeader(std::string(buf,sizeof(buf)))
#endif
			   ) {
			  bRecognizedFormat = 1;
#ifdef RAMP_HAVE_GZ_INPUT
			  random_access_gzclose(result->fileHandle); // don't confuse pwiz by holding onto handle
			  result->fileHandle = NULL;
#elif defined(RAMP_NONNATIVE_LONGFILE)
			  close(result->fileHandle); // don't confuse pwiz by holding onto handle
			  result->fileHandle = -1;
#else
              fclose(result->fileHandle); // don't confuse pwiz by holding onto handle
			  result->fileHandle = NULL;
#endif
			  try {
			  result->mzML = new pwiz::msdata::RAMPAdapter(std::string(filename));
		      } 
			  catch (std::exception& e) { 
				  std::cout << e.what() << std::endl;  
			  } catch (...) { 
				  std::cout << "Caught unknown exception." << std::endl;  
			  }
			  if (!result->mzML) {
#ifdef HAVE_PWIZ_RAW_LIB  // use RAMP+pwiz+xcalibur to read .raw
				  if (pwiz::msdata::Reader_Thermo::hasRAWHeader(std::string(buf,sizeof(buf)))) {
					  std::cout << "could not read .raw file - missing Xcalibur DLLs?" << std::endl;
				  }
#endif
				  bRecognizedFormat = false; // something's amiss
			  }
              break;
		   } 
#endif
		   if (bRecognizedFormat) {
			   break;
		   }
		   if (buf[0]&&buf[1]&&(n_nonempty_lines++>5000)) {
			   break; // this far into the file, this can't be what we intended
           }
        }
		if (!bRecognizedFormat) {
			rampCloseFile(result); // this also frees the handle
			result = NULL; // return null to indicate read failure
		} else 
#ifdef HAVE_PWIZ_MZML_LIB
	 	if (!result->mzML) 
#endif
		{
        ramp_fseek(result,0,SEEK_SET); // rewind
		}
     }
   }
   return result;
}

void rampCloseFile(RAMPFILE *pFI) {
   if (pFI) {
#ifdef HAVE_PWIZ_MZML_LIB
	   if (pFI->mzML) {
		   MZML_TRYBLOCK
		   delete pFI->mzML;
		   MZML_CATCHBLOCK
	   } else
#endif
#ifdef RAMP_HAVE_GZ_INPUT
	  random_access_gzclose(pFI->fileHandle); // don't confuse pwiz by holding onto handle
#elif defined(RAMP_NONNATIVE_LONGFILE)
      close(pFI->fileHandle);
#else
      fclose(pFI->fileHandle);
#endif
      free(pFI);
   }
}

/**************************************************
* fgets() for win32 long files *
* TODO: this could be a LOT more efficient...
**************************************************/
#if defined(RAMP_NONNATIVE_LONGFILE) && (!defined(RAMP_HAVE_GZ_INPUT))
char *ramp_fgets(char *buf,int len,RAMPFILE *handle) {
   int nread=0;
   int chunk;
   ramp_fileoffset_t pos = ramp_ftell(handle);
   buf[--len]=0; // nullterm for safety
   chunk = std::max(len/4,1); // usually all that's needed is a short read
   while (nread <= len) {
      char *newline;
      int nread_now = ramp_fread(buf+nread,chunk,handle);
      buf[nread+nread_now] = 0;
      if (!nread_now) {
         return nread?buf:NULL;
      }
      newline = strchr(buf+nread,'\n');
      if (newline) {
         *(newline+1) = 0; // real fgets includes the newline
         ramp_fseek(handle,pos+(newline-buf)+1,SEEK_SET); // so next read is at next line
         break;
      }
      nread+=nread_now;
      if (nread >= len) {
         break;
      }
      // apparently we need bigger reads
      chunk = len-nread;
   }
   return buf;
}
#endif

/****************************************************************
 * Find the Offset of the index					*
 ***************************************************************/
ramp_fileoffset_t getIndexOffset(RAMPFILE *pFI)
{
   int  i;
   ramp_fileoffset_t  indexOffset, indexOffsetOffset;
   char indexOffsetTemp[SIZE_BUF+1], buf;
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) {
      return -1; // no direct index access in mzML
   }
#endif
   if (pFI->bIsMzData) {
      return -1; // no index in mzData
   }

   for (indexOffsetOffset = -120;  indexOffsetOffset++ < 0 ;)
   {
      char seekbuf[SIZE_BUF+1];
      const char *target = "<indexOffset>";
	  int tlen = (int)strlen(target);
      int  nread;

      ramp_fseek(pFI, indexOffsetOffset, SEEK_END);
      nread = ramp_fread(seekbuf, tlen, pFI);
      seekbuf[nread] = '\0';
      
      if (!strcmp(seekbuf, target))
      {
         break;
      }
   }

   if (indexOffsetOffset >= 0) {
      return -1; // no answer
   }

   indexOffset = ramp_ftell(pFI);

   i = 0;
   while (ramp_fread(&buf, 1, pFI) && buf != '<')
   {
      indexOffsetTemp[i] = buf;
      i++;
   }
   indexOffsetTemp[i] = '\0';

   indexOffset = (atoll(indexOffsetTemp));
   // now test this
   ramp_fseek(pFI, indexOffset, SEEK_SET);
   size_t nread = ramp_fread(indexOffsetTemp, sizeof(indexOffsetTemp), pFI);
   indexOffsetTemp[sizeof(indexOffsetTemp)-1] = 0;
   if (!strstr(indexOffsetTemp,"<index")) {
      indexOffset = -1; // broken somehow
   }
   return indexOffset;
}


/****************************************************************
 * Reads the Scan index in a list				*
 * Returns pScanIndex which becomes property of the caller	*
 * pScanIndex is -1 terminated					*
 ***************************************************************/
char buf[SIZE_BUF*16];

ramp_fileoffset_t *readIndex(RAMPFILE *pFI,
                ramp_fileoffset_t indexOffset,
                int *iLastScan)
{
   int  n=0, nread;
   int  reallocSize = 8000;    /* initial # of scan indexes to expect */
   char *beginScanOffset;
   
   int newN;
   char *beginOffsetId;
   
   ramp_fileoffset_t *pScanIndex=NULL;
   int retryLoop;
   char* s;
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) {
      // we're really building a table of scan numbers vs scan ids, not vs file offsets
      MZML_TRYBLOCK
	  int curscan=0;
	  n = 0; // no scans yet
      pScanIndex = (ramp_fileoffset_t *)malloc( sizeof(ramp_fileoffset_t)*reallocSize); // allocate space for the scan index info
	  for (int i = 0; i< (int)pFI->mzML->scanCount();i++) {
		 int newN = pFI->mzML->getScanNumber(i);
		 if (reallocSize <= newN) {
			 reallocSize = newN + 500; 
			 pScanIndex = (ramp_fileoffset_t *)realloc(pScanIndex, sizeof(ramp_fileoffset_t)*reallocSize);
		 }
         if (!pScanIndex) {
            printf("Cannot allocate memory\n");
            return NULL;
         }
		 while (curscan < newN) {
			 pScanIndex[curscan++] = -1; // meaning "there is no scan cur_scan"
		 }
         n = curscan; // for use below, where we set pScanIndex[n+1]=-1
		 pScanIndex[curscan++] = i; // ramp is 1-based
         (*iLastScan) = newN;
	  }
      MZML_CATCHBLOCK
   } else
#endif   
   for (retryLoop = 2;retryLoop--;) {
     n = 1; // ramp is one based
     *iLastScan = 0;
     free(pScanIndex);
      if ((indexOffset < 0) || (retryLoop==0)) { // derive the index by inspection
         // no index found, derive it
        
        // HENRY - look for <scan num" instead of just <scan - so that we can more easily access the actual scan number. same for mzData 
        const char *scantag = pFI->bIsMzData?"<spectrum id=\"":"<scan num=\"";
         int taglen = (int)strlen(scantag);
         ramp_fileoffset_t index = 0;
        // HENRY - in this new implementation, n should start at zero
        n = 0;
         pScanIndex = (ramp_fileoffset_t *)malloc( sizeof(ramp_fileoffset_t)*reallocSize); // allocate space for the scan index info
         if (!pScanIndex) {
            printf("Cannot allocate memory\n");
            return NULL;
         }
         ramp_fseek(pFI,0,SEEK_SET);
         buf[sizeof(buf)-1] = 0;
         while ((nread = (int)ramp_fread(buf,sizeof(buf)-1,pFI))>taglen) {
            char *find;
            int truncated = 0;
            char *look=buf;
            buf[nread] = 0;
            while (NULL != (find = strstr(look,scantag))) {
              int k,newN; 
              // HENRY - needs to read ahead a few chars to make sure the scan num is complete in this buf
              char *scanNumStr = find + taglen; // pointing to the first digit of the scan num
               while (++scanNumStr < buf + sizeof(buf) - 1 && *scanNumStr != '\"'); // increment until it hits the end quote or the end of buffer 
               if (scanNumStr >= buf + sizeof(buf) - 1) { 
                  // hitting the end of buffer, let's not read this scan; remember the length of the truncated piece
                  truncated = (int)(scanNumStr - find);
                 break;
               }
               
               // HENRY - reset scanNumStr to start of scan num
               scanNumStr = find + taglen;
               
               // HENRY - atoi will read until the end quote
               newN = atoi(scanNumStr);
 
               //              printf("newN = %d, offset = %lld\n", newN, index + (find - buf));
               // HENRY - realloc needs to make sure newN has a spot in pScanIndex
               if (reallocSize <= newN) {
                 reallocSize = newN + 500; 
                 pScanIndex = (ramp_fileoffset_t *)realloc(pScanIndex, sizeof(ramp_fileoffset_t)*reallocSize);
                 if (!pScanIndex) {
                   printf("Cannot allocate memory\n");
                   return NULL;
                 }
               }               
               
               // HENRY - sets all the skipped scans to offset -1 (here you see why I set n = 0 to begin, rather than n = 1)
               for (k = n + 1; k < newN; k++) {
                 pScanIndex[k] = -1;
               }
               
               // HENRY - puts the offset at pScanIndex[newN]
               pScanIndex[newN] = index+(find-buf); // ramp is 1-based
               n = newN;
               (*iLastScan) = newN;
               
               // HENRY - we can start looking from the end quote of the last scan number.
               look = scanNumStr;
               
               // HENRY - reallocation needs to happen earlier, before we set pScanIndex[newN], in case newN is already past the old alloc size
               /*
               if (reallocSize<=n) {
                  reallocSize+=500;
                  pScanIndex = (ramp_fileoffset_t *)realloc(pScanIndex, sizeof(ramp_fileoffset_t)*reallocSize); // allocate space for the scan index info
                  if (!pScanIndex) {
                     printf("Cannot allocate memory\n");
                     return NULL;
                  }
               }
               */
            }
            nread = (int)(strlen(look)+(look-buf));
            if (*look && strchr(scantag,buf[nread-1]) && !ramp_feof(pFI)) { // check last char of buffer
               // possible that next scantag overhangs end of buffer
               ramp_fseek(pFI,-taglen,SEEK_CUR); // so next get includes it
            
            // HENRY - if the scan number is truncated, we go back a few chars so that the next get will include it
            } else if (truncated != 0 && !ramp_feof(pFI)) {
               ramp_fseek(pFI, -truncated, SEEK_CUR);
            } 
            index = ramp_ftell(pFI);
         }
         break; // no need to retry
      } else {  // read the index out of the file (then check it!)
         struct ScanHeaderStruct scanHeader; // for test of index integrity
         int indexOK=1; // until we show otherwise

         // HENRY -- reset n to zero. Note that it should be zero here, not one -- as n points to the previous record in 
         // my nomenclature (and newN to the newly read record).
         n = 0;
         
         if ((pScanIndex = (ramp_fileoffset_t *) malloc(reallocSize * sizeof(ramp_fileoffset_t))) == NULL) {
            printf("Cannot allocate memory\n");
            return NULL;
         }
         
         ramp_fseek(pFI, indexOffset, SEEK_SET);
         
         s = ramp_fgets(buf, SIZE_BUF, pFI);
         while( s!=NULL && !strstr( buf , "<offset" ) ) {
            s = ramp_fgets(buf, SIZE_BUF, pFI);
         }
         
         if (s == NULL)
            break;   // end of file reached.
         
         while (!strstr(buf, "/index")) {
            int k;
            // HENRY -- also reads the "id" field, which is the scan num
            if ((beginOffsetId = (char *)(strstr(buf, "id=\""))) == NULL) {
               char *fgot=ramp_fgets(buf, SIZE_BUF, pFI);
               continue;
            }
            beginOffsetId += 4;
            
            newN = atol(beginOffsetId);
            
            // HENRY -- check if the new id is past the max size of the pScanIndex array
            // Note that it should be reallocSize - 1, because the very last record is set to offset=-1 
            // (see below)! In case newN is the very last record, we need to prepare the space for the offset=-1 thingy.
            if (newN >= reallocSize - 1) {
              ramp_fileoffset_t *pTmp;
              
              // HENRY -- we don't know how much newN is bigger than the old realloc size. In case it is more than 500 bigger,
              // then the old way of always reallocating for 500 more will break. Instead we jump to newN + 500.
              reallocSize = newN + 500;
              pTmp = (ramp_fileoffset_t*)realloc(pScanIndex, reallocSize * sizeof(ramp_fileoffset_t));
              if (pTmp == NULL) { 
                printf("Cannot allocate memory\n");
                return NULL;
              } else {
                pScanIndex=pTmp;
              }
            }
            
            // HENRY -- any scan number skipped between the last record and the new record will be assumed "missing"
            // the offset will be set to -1 for these scan numbers
            for (k = n + 1; k < newN; k++) {
              pScanIndex[k] = -1;
            }
            // HENRY -- this new record becomes the current one, and iLastScan gets the scan number of this new record
            n = newN;
            (*iLastScan) = n;
            
            // HENRY -- using merely the ">" as the beginning of the offset is somewhat scary, but I'm not changing it now.
            if ((beginScanOffset = (char *) (strstr(buf, ">"))) == NULL) {
               char *fgot=ramp_fgets(buf, SIZE_BUF, pFI);
               continue;
            }
            beginScanOffset++;
            
            pScanIndex[n] = atoll(beginScanOffset);
			if (pScanIndex[n] <= 0) { // the "X2XML" converter writes index entries for missing scans
			   pScanIndex[n] = -1; // meaning "there is no scan n"
			}

            // HENRY -- I have moved the following realloc piece earlier. The reason is:
            // In the old way, the scan numbers are assumed to be consecutive, so one can expect the next scan number
            // to be 1 + the current one. In this case, you only have to make sure space is allocated for one more record.
            // In the new way, the scan numbers are not consecutive, so how many 
            // more spaces we need here is unpredictable. (If the next scan number is 1000 + this current one, then we could be in 
            // trouble.) It then makes sense to realloc AFTER we read the next scan number, which is what I am doing.
            
            //            printf ("(%d, %ld) ", n, pScanIndex[n]);
            //            n++;
//            (*iLastScan)++;
/*            
            if (n >= reallocSize) {
               ramp_fileoffset_t *pTmp;
               
               reallocSize = reallocSize + 500;
               
               pTmp = (ramp_fileoffset_t*) realloc(pScanIndex, reallocSize*sizeof(ramp_fileoffset_t));
               
               if (pTmp==NULL) {
                  printf("Cannot allocate memory\n");
                  return NULL;
               } else {
                  pScanIndex=pTmp;
               }
            }
  */          
            char *fgot=ramp_fgets(buf, SIZE_BUF, pFI);
         }
         
         // HENRY -- We have no idea whether scan number 1, n/2 or n-1 is a missing scan or not. So we cannot just blindly test them.
         // Instead, we start from 1, n/2 and n to find a valid offset to test. (we can test n because n still points to the
         // last scan number (never n++ in this implementation). 
         
         if (n > 0) {
            // OK, now test that to see if it's a good index or not
            int testIndex = 1;   
            // HENRY -- iteratively finds the next valid offset
            while (testIndex <= n && pScanIndex[testIndex] <= 0) testIndex++;
            if (testIndex <= n) {
              readHeader(pFI,pScanIndex[testIndex],&scanHeader);
              if (scanHeader.acquisitionNum == -1) { // first
                 indexOK = 0; // bogus index
                 free(pScanIndex);
                 pScanIndex = NULL;
              }
            } 
            // HENRY -- n>3 is better. if n=2 or 3, n/2 is 1, which we just tested. 
            if (indexOK && (n>3)) { // middle
               testIndex = n/2;
               while (testIndex <= n && pScanIndex[testIndex] <= 0) testIndex++;
               if (testIndex <= n) {
                 readHeader(pFI,pScanIndex[testIndex],&scanHeader);
                 if (scanHeader.acquisitionNum == -1) {
                    indexOK = 0; // bogus index
                    free(pScanIndex);
                    pScanIndex = NULL;
                 }
              }
            }
            
            if (indexOK && (n>1)) { // last
               testIndex = n;
               while (testIndex >= 1 && pScanIndex[testIndex] <= 0) testIndex--;
               if (testIndex >= 1) {
                 readHeader(pFI,pScanIndex[testIndex],&scanHeader);
                 if (scanHeader.acquisitionNum == -1) {
                   indexOK = 0; // bogus index
                   free(pScanIndex);
                   pScanIndex = NULL;
                 }
               }
            }
     //       HENRY - Uncomment following to activate index creation from scratch
     //       indexOK = 0;
         }
         
         if (indexOK) {
            break; // no retry
         }
      } // end if we claim to have an index
   } // end for retryloop
   
   // HENRY -- Here we set the n+1 record to offset=-1. (Note that unlike in the old implementation, I have never n++,
   // so n still is the scan number of the last record.
   pScanIndex[n + 1] = -1;

   return (pScanIndex);
}

// helper func for reading mzData
static const char *findMzDataTagValue(const char *pStr, const char *tag) {
   const char *find = strstr(pStr,tag);
   if (find) {
      find = strstr(find+1,"value=");
      if (find) {
         find = findquot(find);
         if (find) {
            find++; // pointing at value string
         }
      }
   }
   return find;
}

#include <time.h>
/*
 * Reads a time string, returns time in seconds.
 */
static double rampReadTime(RAMPFILE *pFI,const char *pStr) {
   double t=0;
   if (pFI->bIsMzData) {
      const char *tag = findMzDataTagValue(pStr, "TimeInMinutes");
      if (tag) {
         t = 60.0*atof(tag);
      } else if (NULL!=(tag = findMzDataTagValue(pStr, "TimeInSeconds"))) { // von Steffan Neumann
         t = atof(tag);
      }
   } else if (!sscanf(pStr, "PT%lfS", &t)) {  // usually this is elapsed run time
      /* but could be stored in for PxYxMxDTxHxMxS */
      struct tm fullTime; // apologies to those working after January 18, 19:14:07, 2038
      double secondsFrac=0;
      int bDate = 1; 
      while (!isquot(*++pStr)) {
         double val;
         if ('T'==*pStr) {
            pStr++;
            bDate = 0; // we're into the minutes:seconds portion
         }
         val = atof(pStr);
         while (('.'==*pStr)||isdigit(*pStr)) {
            pStr++;
         }
         switch (*pStr) {
         case 'Y':
            fullTime.tm_year = (int)val-1900; // years since 1900
            break;
         case 'M':
            if (bDate) {
               fullTime.tm_mon = (int)val-1; // range 0-11
            } else {
               fullTime.tm_min = (int)val; // range 0-59
            }            
            break;
         case 'D':
            fullTime.tm_mday = (int)val; // range 1-31
            break;
         case 'H':
            fullTime.tm_hour = (int)val; // range 0-23
            break;
         case 'S':
            fullTime.tm_sec = (int)val;
            secondsFrac = val-(double)fullTime.tm_sec;
            break;
         }
      }
      t = (double)mktime(&fullTime)+secondsFrac;
   }
   return t;
}

/*
 * helper func for faster parsing
 */
static const char *matchAttr(const char *where,const char *attr,int len) {
   const char *look = where; // we assume this is pointed at '=', look back at attr
   while (len--) {
      if (*--look != attr[len]) {
         return NULL; // no match
      }
   }
   return where+2; // point past ="
}


//
// helper function to deal with mzdata with no newlines - breaks
// lines up at </...>
//
static char *ramp_nextTag(char *buf, int buflen, RAMPFILE *pFI) {
   char *result;
   result = ramp_fgets(buf,buflen,pFI);
   if (result && !strchr(buf,'\n')) { // no newline found
      char *closer = strstr(buf+1,"</");
      if (closer) {
         *closer = 0; // temp. nullterm
         ramp_fseek(pFI,(1+closer-buf)-buflen,SEEK_CUR); // reposition for next read
      }
   }
   return result;
}


/*
 * Reads scan header information.
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE
 *    RETURNING !
 */
void readHeader(RAMPFILE *pFI,
                ramp_fileoffset_t lScanIndex, // look here
                struct ScanHeaderStruct *scanHeader)
{
   char stringBuf[SIZE_BUF+1];
   char *pStr2;

   /*
    * initialize defaults
    */
   memset(scanHeader,0,sizeof(struct ScanHeaderStruct)); // mostly we want 0's
#define LOWMZ_UNINIT 1.111E6
   scanHeader->lowMZ =  LOWMZ_UNINIT;
   scanHeader->acquisitionNum = -1;
   scanHeader->seqNum = -1;
   scanHeader->retentionTime = -1;
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
     if (lScanIndex >= 0) { 
  		MZML_TRYBLOCK;
		pFI->mzML->getScanHeader((size_t)lScanIndex, *scanHeader);
		MZML_CATCHBLOCK;
     }
	 return;
   }
#endif

   // HENRY - missing scans due to dta2mzXML get offset of zero
   // missing scans without index entries get offset of -1
   // check for those cases and populate an empty scanHeader and return
   // if we fseek(-1) unexpected behavior will result!
   if (lScanIndex <= 0) { 
     return;
   }
   ramp_fseek(pFI, lScanIndex, SEEK_SET);



   if (pFI->bIsMzData) {
      int bHasPrecursor = 0;
      while (ramp_nextTag(stringBuf, SIZE_BUF, pFI))
      {
         const char *attrib;
         const char *pStr;
         const char *closeTag = strstr(stringBuf, "</spectrumSettings>");
         // find each attribute in stringBuf
         for (attrib=stringBuf-1;NULL!=(attrib=strchr(attrib+1,'='));) {
            if (closeTag && (closeTag < attrib)) {
               break; // into data territory now
            }
			if (matchAttr(attrib,"cvLabel",7)|| matchAttr(attrib,"accession",9)||
				matchAttr(attrib,"value",5)) {
			   ; // no info here
			} else if ((pStr = matchAttr(attrib, "spectrum id",11))) {
               sscanf(pStr, "%d", &(scanHeader->acquisitionNum));
            //} else if ((pStr = matchAttr(attrib, "basePeakMz",10)))  {
            //   sscanf(pStr, "%lf", &(scanHeader->basePeakMZ));      
            //} else if ((pStr = matchAttr(attrib, "totIonCurrent",13)))  {
            //   sscanf(pStr, "%lf", &(scanHeader->totIonCurrent));      
            //} else if ((pStr = matchAttr(attrib, "basePeakIntensity",17)))  {
            //   sscanf(pStr, "%lf", &(scanHeader->basePeakIntensity));      
            } else if ((pStr = matchAttr(attrib, "msLevel",7)))  {
               sscanf(pStr, "%d", &(scanHeader->msLevel));
            //} else if ((pStr = matchAttr(attrib, "length",6)))  { get this from array element
            //   sscanf(pStr, "%d", &(scanHeader->peaksCount));
            } else if ((pStr = findMzDataTagValue(attrib,"TimeInMinutes")))  {
               scanHeader->retentionTime = rampReadTime(pFI,stringBuf);
            } else if ((pStr = findMzDataTagValue(attrib,"TimeInSeconds")))  {
               scanHeader->retentionTime = rampReadTime(pFI,stringBuf);
            } else if ((pStr = matchAttr(attrib, "mzRangeStart",12)))  {
               sscanf(pStr, "%lf", &(scanHeader->lowMZ));
            } else if ((pStr = matchAttr(attrib, "mzRangeStop",11)))  {
               sscanf(pStr, "%lf", &(scanHeader->highMZ));
            } else if ((pStr = findMzDataTagValue(attrib, "ScanMode"))) { 
               if ((pStr2 = (char *) findquot(pStr))) {
                  memcpy(&(scanHeader->scanType), pStr, pStr2-pStr);
                  scanHeader->scanType[pStr2-pStr] = '\0';
               }
            }
         }
         if (closeTag) {
            break; // into data territory now
         }
      }
      do {
         
         /*
         * read precursor info
         */
         const char *pStr,*pStr2;
         if ((pStr = (char *) strstr(stringBuf, "<precursorList")))
         {
            bHasPrecursor = 1;
         } else if ((pStr = (char *) strstr(stringBuf, "</precursorList")))
         {
            bHasPrecursor = 0;
         }
         if (bHasPrecursor) { // in precursor section
            if (NULL!=(pStr2 = (char *) strstr(stringBuf, "spectrumRef="))) 
            {
               sscanf(pStr2 + 13, "%d", &(scanHeader->precursorScanNum));
            }
            if (NULL!=(pStr2 = findMzDataTagValue(stringBuf,"ChargeState"))) 
            {
               scanHeader->precursorCharge = atoi(pStr2);
            }
            //Paul Benton : added support for Collision Energy 25-01-08
            if (NULL!=(pStr2 = findMzDataTagValue(stringBuf,"CollisionEnergy")))
            {
               scanHeader->collisionEnergy = atof(pStr2);
            }
            //end of collision energy addition            
            if (NULL!=(pStr2 = findMzDataTagValue(stringBuf,"MassToChargeRatio"))) 
            {
               scanHeader->precursorMZ = atof(pStr2);
            }
            if (NULL!=(pStr2 = findMzDataTagValue(stringBuf,"mz"))) 
            {
               scanHeader->precursorMZ = atof(pStr2);
            }
            // bpratt for Steffen Neumann
            if (NULL!=(pStr2 = findMzDataTagValue(stringBuf,"Intensity")))
            {
               scanHeader->precursorIntensity = atof(pStr2);
            }
         }
         if (strstr(stringBuf, "</spectrumDesc>")) {
            break; // into data territory now
         }
         if (strstr(stringBuf, "</precursorList>")) {
            break; // into data territory now
         }
      } while (ramp_nextTag(stringBuf, SIZE_BUF, pFI));
      // now read peaks count
      do {
         if (strstr(stringBuf, "ArrayBinary>")) {
            do {
               char *cp=strstr(stringBuf,"length=");
               if (cp) {
                  scanHeader->peaksCount = atoi(cp+8);
                  break;
               }
            } while (ramp_nextTag(stringBuf, SIZE_BUF, pFI));
            break;
         }
      } while (ramp_nextTag(stringBuf, SIZE_BUF, pFI));
   } else { // mzXML
      while (ramp_fgets(stringBuf, SIZE_BUF, pFI))
      {
         const char *attrib;
         const char *pStr;
         // find each attribute in stringBuf
         for (attrib=stringBuf-1;NULL!=(attrib=strchr(attrib+1,'='));) {
            if ((pStr = matchAttr(attrib, "num",3))) {
               if (-1 == scanHeader->acquisitionNum) {
                  sscanf(pStr, "%d", &(scanHeader->acquisitionNum));
               } else {
                  // ASSUMPTION: only <scan num=...> and <scanOrigin num=...>
                  int scanOriginNum=0;
                  sscanf(pStr, "%d", &scanOriginNum);
                  if (scanOriginNum<scanHeader->mergedResultStartScanNum 
                     || 0==scanHeader->mergedResultStartScanNum ) {
                     scanHeader->mergedResultStartScanNum = scanOriginNum;
                  }
                  if (scanOriginNum>scanHeader->mergedResultEndScanNum 
                     || 0==scanHeader->mergedResultEndScanNum ) {
                     scanHeader->mergedResultEndScanNum = scanOriginNum;
                  }
               }
            } else if ((pStr = matchAttr(attrib, "basePeakMz",10)))  {
               sscanf(pStr, "%lf", &(scanHeader->basePeakMZ));      
            } else if ((pStr = matchAttr(attrib, "totIonCurrent",13)))  {
               sscanf(pStr, "%lf", &(scanHeader->totIonCurrent));      
            } else if ((pStr = matchAttr(attrib, "basePeakIntensity",17)))  {
               sscanf(pStr, "%lf", &(scanHeader->basePeakIntensity));      
            } else if ((pStr = matchAttr(attrib, "msLevel",7)))  {
               sscanf(pStr, "%d", &(scanHeader->msLevel));
            } else if ((pStr = matchAttr(attrib, "peaksCount",10)))  {
               sscanf(pStr, "%d", &(scanHeader->peaksCount));
            } else if ((pStr = matchAttr(attrib, "retentionTime",13)))  {
               scanHeader->retentionTime = rampReadTime(pFI,pStr);
            } else if ((pStr = matchAttr(attrib, "lowMz",5)))  {
               sscanf(pStr, "%lf", &(scanHeader->lowMZ));
            } else if ((pStr = matchAttr(attrib, "highMz",6)))  {
               sscanf(pStr, "%lf", &(scanHeader->highMZ));
            } else if ((scanHeader->lowMZ==LOWMZ_UNINIT) &&  
               ((pStr = matchAttr(attrib, "startMz",7)))) {
               sscanf(pStr, "%lf", &(scanHeader->lowMZ));  
            } else if ((!scanHeader->highMZ) &&
               ((pStr = matchAttr(attrib, "endMz",5)))) {
               sscanf(pStr, "%lf", &(scanHeader->highMZ));
            } else if ((pStr = matchAttr(attrib, "scanType", 8))) { 
               if ((pStr2 = (char *) findquot(pStr))) {
                  memcpy(&(scanHeader->scanType), pStr, sizeof(char)*((pStr2-pStr)));
                  scanHeader->scanType[pStr2-pStr] = '\0';
               }
            } else if ((pStr = matchAttr(attrib, "collisionEnergy", 15)))  {
                 sscanf(pStr, "%lf", &(scanHeader->collisionEnergy));
            } else if ((pStr = matchAttr(attrib, "merged", 6)))  {
                 sscanf(pStr, "%d", &(scanHeader->mergedScan));
            } else if ((pStr = matchAttr(attrib, "mergedScanNum", 13)))  {
                 sscanf(pStr, "%d", &(scanHeader->mergedResultScanNum));
            } else if ((pStr = matchAttr(attrib, "activationMethod", 16))) {
	      if ((pStr2 = (char *) findquot(pStr))) {
                  memcpy(&(scanHeader->activationMethod), pStr, sizeof(char)*((pStr2-pStr)));
                  scanHeader->activationMethod[pStr2-pStr] = '\0';
              }
            }
	    
	    
         }
         
         /*
         * read precursor mass
         */
         if ((pStr = (char *) strstr(stringBuf, "<precursorMz ")))
         {
            if ((pStr2 = (char *) strstr(stringBuf, "precursorScanNum="))) 
            {
               sscanf(pStr2 + 18, "%d", &(scanHeader->precursorScanNum));
            }
            
            /*
            * Check for precursor charge.
            */
            if ((pStr2 = (char *) strstr(pStr, "precursorCharge="))) 
            {
               sscanf(pStr2 + 17, "%d", &(scanHeader->precursorCharge));
            }
            if ((pStr2 = (char *) strstr(pStr, "precursorIntensity="))) {
               sscanf(pStr2 + 20, "%lf", &(scanHeader->precursorIntensity));
            }
            if ((pStr2 = (char *) strstr(pStr, "activationMethod="))) {
               char *pStr3;
               pStr3 = pStr2+18;
               if ((pStr2 = (char *) findquot(pStr3))) {
                  memcpy(&(scanHeader->activationMethod), pStr3, sizeof(char)*((pStr2-pStr3)));
                  scanHeader->activationMethod[pStr2-pStr3] = '\0';
               }
            }
	    if ((pStr2 = (char *) strstr(pStr, "possibleCharges="))) {
               char *pStr3;
               pStr3 = pStr2+17;
               if ((pStr2 = (char *) findquot(pStr3))) {
                  memcpy(&(scanHeader->possibleCharges), pStr3, sizeof(char)*((pStr2-pStr3)));
                  scanHeader->possibleCharges[pStr2-pStr3] = '\0';

		  // now that we have the possible charge string,
		  // parse it into the possibleChargesArray
		  
		  if (strcmp(scanHeader->possibleCharges, "") != 0) {
		    // parse the string, in format "2,4,7"
		    char* chargeList = strdup(scanHeader->possibleCharges);

		    char* curPos = chargeList;
		    bool done=false;
		    while (!done) {

		      
		      // manually do strsep(&curPos,",");
		      // (strsep is not available in mingw, possible others)

		      // advance curPos to the next separator,
		      // replace it with a string terminator character,
		      // and advance to one charater past the terminator.
		      
		      // on the next loop, 'token' is initially reset
		      // to this position (+1 from the last
		      // deliminator).
		      
		      // thus, 'token' will be a null-terminated
		      // substing of the original, for one
		      // separator-deliminated token.

		      char *token = curPos;
		      if (curPos != NULL) { // if NULL do nothing
			// otherwise
			char curChar = *curPos;
			while (curChar != ',' && curChar != '\0') {
			  ++curPos;
			  curChar = *curPos;	  
			}
			if (curChar == ',') {
			  *curPos = '\0';
			  curPos++;
			  if (*curPos == '\0') {
			    curPos = NULL;
			  }
			}
			else if (*curPos == '\0') {
			  curPos = NULL;
			}
		      }

		      if (token == NULL) {
			//done
			done = true;
			continue;
		      }
		      int curCharge = atoi(token);
		      //printf("found %d\n", curCharge);
		      if (curCharge > (CHARGEARRAY_LENGTH-1)) {
			printf("error, cannot handle precursor charges > %d (got %d)\n", CHARGEARRAY_LENGTH-1, curCharge);
			exit(-1);
		      }
		      scanHeader->possibleChargesArray[curCharge] = true;
		      scanHeader->numPossibleCharges++;
		    }
		    free(chargeList);
		  }
		  // end of possibleCharges parsing
               }
            }

            
            /*
            * Find end of tag.
            */
            while (!(pStr = strchr(pStr, '>')))
            {      
				if (!ramp_fgets(stringBuf, SIZE_BUF, pFI)) {
					break;
				}
               pStr = stringBuf;

               if ((pStr2 = (char *) strstr(stringBuf, "precursorScanNum="))) 
                  sscanf(pStr2 + 18, "%d", &(scanHeader->precursorScanNum));
               if ((pStr2 = (char *) strstr(stringBuf, "precursorCharge="))) 
               {
                  sscanf(pStr2 + 17, "%d", &(scanHeader->precursorCharge));
               }
               if ((pStr2 = (char *) strstr(pStr, "precursorIntensity="))) {
                  sscanf(pStr2 + 20, "%lf", &(scanHeader->precursorIntensity));
               }
            }
            pStr++;	// Skip >
            
            /*
             * Skip past white space.
            */
            while (!(pStr = skipspace(pStr)))
            {
               char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);
               pStr = stringBuf;
            }
            
            sscanf(pStr, "%lf<", &(scanHeader->precursorMZ));
            //         printf("precursorMass = %lf\n", scanHeader->precursorMZ);
         }
         if (strstr(stringBuf, "<peaks")) {
            break; // into data territory now
         }
         if ((-1==scanHeader->acquisitionNum) &&
             ((strstr(stringBuf, "</dataProcessing>")||
               strstr(stringBuf, "</msInstrument>")))) {
            break; // ??? we're before scan 1 - indicates a broken index
         }
      }
      switch(G_RAMP_OPTION & MASK_SCANS_TYPE) {
         case OPTION_ALL_SCANS:
            // return all scans (i.e. origin + averaged) as they are
            break;
         case OPTION_ORIGIN_SCANS:
            // return origin scan regardless of whether it has has been used in averaging
            //        DO NOT return averaged (resultant) scan
            if (scanHeader->mergedScan 
               && 0!=scanHeader->mergedResultStartScanNum && 0!=scanHeader->mergedResultEndScanNum) {
               scanHeader->peaksCount = 0;
            }
            break;
         case OPTION_AVERAGE_SCANS:
         default:
            // return origin scan if it has NOT been used in averaging
            //        also, return averaged scan
            if (scanHeader->mergedScan && 0!=scanHeader->mergedResultScanNum && scanHeader->acquisitionNum!=scanHeader->mergedResultScanNum) {
               scanHeader->peaksCount = 0;
            }
            break;
      }
   } // end else mzXML
   if (scanHeader->retentionTime<0) { 
      scanHeader->retentionTime = scanHeader->acquisitionNum; // just some unique nonzero value
   }
   scanHeader->seqNum = scanHeader->acquisitionNum; //  default sequence number
   scanHeader->filePosition = lScanIndex;
}

/****************************************************************
 * Reads the MS level of the scan.				*
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE	*
 *    RETURNING !!						*
 ***************************************************************/

int readMsLevel(RAMPFILE *pFI,
      ramp_fileoffset_t lScanIndex)
{
   int  msLevelLen;
   char stringBuf[SIZE_BUF+1];
   char szLevel[12];
   char *beginMsLevel, *endMsLevel;

   // HENRY - check if index is valid. the uninitialized value of ms level is probably zero.
   if (lScanIndex <= 0) {
     return (0);
   }
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
	   struct ScanHeaderStruct scanHeader;
	   MZML_TRYBLOCK
	   pFI->mzML->getScanHeader((size_t)lScanIndex, scanHeader);
	   MZML_CATCHBLOCK
	   return(scanHeader.msLevel);
   }
#endif
   ramp_fseek(pFI, lScanIndex, SEEK_SET);

   char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);

   while (!(beginMsLevel = (char *) strstr(stringBuf, "msLevel=")))
   {
      char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);
   }

   beginMsLevel += 9;           // We need to move the length of msLevel="
   endMsLevel = (char *) findquot(beginMsLevel);
   msLevelLen = (int)(endMsLevel - beginMsLevel);

   strncpy(szLevel, beginMsLevel, msLevelLen);
   szLevel[msLevelLen] = '\0';

   return atoi(szLevel);
}


/****************************************************************
 * Reads startMz and endMz of the scan.				*
 * Returns 1.E6 if startMz was not set.*
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE	*
 *    RETURNING !!						*
 ***************************************************************/

double readStartMz(RAMPFILE *pFI,
		   ramp_fileoffset_t lScanIndex)
{
  char stringBuf[SIZE_BUF+1];
  double startMz = 1.E6;
  char *pStr;
  const char *tag = pFI->bIsMzData?"mzRangeStart":"startMz";

  // HENRY -- again, check for invalid offset first. Is startMz = 1.E6 a good uninitialized value?
  if (lScanIndex <= 0) {
    return (startMz);
  }
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
	   struct ScanHeaderStruct scanHeader;
	   MZML_TRYBLOCK;
	   pFI->mzML->getScanHeader((size_t)lScanIndex, scanHeader);
	   MZML_CATCHBLOCK;
	   return(scanHeader.lowMZ);
   }
#endif
  ramp_fseek(pFI, lScanIndex, SEEK_SET);
   
  while (ramp_fgets(stringBuf, SIZE_BUF, pFI))
  {
     if (strstr(stringBuf, pFI->bIsMzData?"</spectrumDesc":"<peaks"))
        break; // ran to end
      if ((pStr = strstr(stringBuf, tag))){
        sscanf(pStr + strlen(tag)+2, "%lf", &startMz);
        break;
      }
  }

  return startMz;
}


/****************************************************************
 * Reads startMz and endMz of the scan.				*
 * Returns 1.E6 if startMz was not set.	*
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE	*
 *    RETURNING !!						*
 ***************************************************************/

double readEndMz(RAMPFILE *pFI,
		   ramp_fileoffset_t lScanIndex)
{
  char stringBuf[SIZE_BUF+1];
  double endMz = 0.0;
  char *pStr;
  const char *tag = pFI->bIsMzData?"mzRangeStop":"endMz";

  // HENRY -- again, check for invalid offset first. is endMz = 0 a good uninitialized value?
  if (lScanIndex <= 0) {
    return (endMz);
  }
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
	   struct ScanHeaderStruct scanHeader;
	   MZML_TRYBLOCK
	   pFI->mzML->getScanHeader((size_t)lScanIndex, scanHeader);
	   MZML_CATCHBLOCK
	   return(scanHeader.highMZ);
   }
#endif
  ramp_fseek(pFI, lScanIndex, SEEK_SET);
   
  while (ramp_fgets(stringBuf, SIZE_BUF, pFI))
  {
     if (strstr(stringBuf, pFI->bIsMzData?"</spectrumDesc":"<peaks"))
        break; // ran to end
      if ((pStr = strstr(stringBuf, tag))){
        sscanf(pStr + strlen(tag)+2, "%lf", &endMz);
        break;
      }
  }

  return endMz;
}

// list of supported filetypes
static std::vector<const char *> data_Ext;
// returns a null-terminated array of const ptrs
const char **rampListSupportedFileTypes() {
	if (!data_Ext.size()) { // needs init
		data_Ext.push_back(".mzXML");
		data_Ext.push_back(".mzData");
#ifdef HAVE_PWIZ_MZML_LIB
		data_Ext.push_back(".mzML");
#endif
#ifdef RAMP_HAVE_GZ_INPUT
		// add these filetypes again, gzipped
		int n=(int)data_Ext.size();
		for (int i=0;i<n;i++) {
			std::string extGZ(data_Ext[i]);
			extGZ+=".gz";
			data_Ext.push_back(strdup(extGZ.c_str())); // yeah, this leaks
		}
#endif
		data_Ext.push_back(NULL); // end of list
	}
	return &(data_Ext[0]);
}

// construct a filename in inbuf from a basename, adding .mzXML or .mzData as exists
// returns inbuf, or NULL if neither .mzXML or .mzData file exists
char *rampConstructInputFileName(char *inbuf,int inbuflen,const char *basename_in) {
   return rampConstructInputPath(inbuf, inbuflen, "", basename_in);
}
// std::string equivalent
std::string rampConstructInputFileName(const std::string &basename) {
	int len;
	char *buf = new char[len = (int)(basename.length()+100)]; 
	rampConstructInputPath(buf, len, "", basename.c_str());
	std::string result(buf);
	delete[] buf;
	return result;
}


char *rampConstructInputPath(char *inbuf, // put the result here
							 int inbuflen, // max result length
							 const char *dir_in, // use this as a directory hint if basename does not contain valid dir info
							 const char *basename_in) { // we'll try adding various filename extensions to this
 char *result = NULL;
 for (int loop = (dir_in&&*dir_in)?2:1;loop--&&!result;) { // try it first without directory hint in case basename has full path
   int i;
   const char *basename = basename_in;
   char *dir = strdup((dir_in&&!loop)?dir_in:"");
   char *tmpbuf = (char *)malloc(strlen(dir) + strlen(basename) + 20);
   char *append;
   if (*dir)
   {  // make sure this is a directory, and not a directory+filename
      struct stat buf;
	  if ((!stat(dir,&buf)) && !S_ISDIR(buf.st_mode)) { // exists, but isn't a dir
		  char *slash = findRightmostPathSeperator(dir);
		  if (slash) {
			  *(slash+1) = 0;
		  }
	  }
   }
   if (dir != NULL && *dir != '\0')
   {
       // If directory for mzXML was supplied, strip off directory part.
	   const char *basename_sep = findRightmostPathSeperator_const(basename);
	   if (basename_sep) {
           basename = basename_sep + 1;
   }
   }

   if (basename_in==inbuf) { // same pointer
      char *basename_buff = (char *)malloc(inbuflen);
      strncpy(basename_buff,basename,inbuflen);
      basename = basename_buff;
   }

   *tmpbuf= 0;
   if (dir != NULL && *dir != '\0')
   {
       int len_dir = (int)strlen(dir);
   strcpy(tmpbuf, dir);
       if (!isPathSeperator(tmpbuf[len_dir - 1]))
       {
           tmpbuf[len_dir] = '/';
           tmpbuf[len_dir+1] = 0;
       }
   }
   strcat(tmpbuf, basename);
   append = tmpbuf+strlen(tmpbuf);
   strcat(tmpbuf, ".*");
   unCygwinify(tmpbuf); // no effect in Cygwin build
   glob_t g;
   glob(tmpbuf,0, NULL, &g);
   for (int j= 0; j< g.gl_pathc; j++) { // for each file in directory
      int flen = (int)strlen((g.gl_pathv)[j]);
      for (i=0;rampListSupportedFileTypes()[i];i++) { // compare to supported .ext's
		  int tlen = (int)strlen(rampListSupportedFileTypes()[i]);
		  if ((flen > tlen) && 
			  !strcasecmp((g.gl_pathv)[j]+flen-tlen,rampListSupportedFileTypes()[i])) {
			  // this is a file of a supported type
			  if (!result) {
			     result = strdup((g.gl_pathv)[j]);
			  } else if (strcasecmp((g.gl_pathv)[j],result)) { // win32 isn't case sensitive
                 printf("found both %s and %s, using %s\n",
                  (g.gl_pathv)[j],result,result);
            }
		  } // end if supported filetype
      } // end for each supported filetype
   } // end for each file in directory
   globfree(&g);
   if (!result) { // failed - caller can complain about lack of .mzXML
      strcpy(append,rampListSupportedFileTypes()[0]);
      result = strdup(tmpbuf);
   }
   if (basename_in==inbuf) { // same pointer
      free((void *)basename); // we allocated it
   }
   free(tmpbuf);

   if ((int) strlen(result) < inbuflen) {
      strcpy(inbuf, result);
      free(result);
      result = inbuf;
   } else {
      printf("buffer too small for file %s\n",
         result);
      free(result);
      result = NULL;
   }
   free(dir); // we allocated this to trim a filename off the directory hint
 } // end for (int loop...
 return result;
}

// construct a filename in inbuf from a basename and taking hints from a named
// spectrum, adding .mzXML or .mzData as exists
// return true on success
int rampValidateOrDeriveInputFilename(char *inbuf, int inbuflen, char *spectrumName) {
   struct stat buf;
   char *result = NULL;
   char *dot,*slash,*tryName;
   size_t len;
   if (!stat(inbuf,&buf)) {
      return 1;
   }
   tryName = (char *)malloc(len=strlen(inbuf)+strlen(spectrumName)+12);
   strcpy(tryName,inbuf);
   fixPath(tryName,1); // do any desired tweaks, searches etc - expect existence
   slash=findRightmostPathSeperator(tryName);
   if (!slash) {
      slash = tryName-1;
   }
   strcpy(slash+1,spectrumName);
   dot = strchr(slash+1,'.');
   if (dot) {
      *dot = 0;
   }
   rampConstructInputFileName(tryName,(int)len,tryName); // .mzXML or .mzData
   if (((int)strlen(tryName) < inbuflen) && !stat(tryName,&buf)) {
      // success!
      strncpy(inbuf,tryName,inbuflen);
      result = inbuf;
   }
   free(tryName);
   return result!=NULL;

}

// return NULL if fname is not of extension type we handle,
// otherwise return pointer to .ext
char *rampValidFileType(const char *fname) {
   const char *result=NULL;
   int flen = (int)strlen(fname);
   for (int i=0;rampListSupportedFileTypes()[i];i++) {
      int eend=(int)strlen(rampListSupportedFileTypes()[i]);
	  for (int fend=flen;fend-- && eend--;) {
		  result = fname+fend;
		  if (tolower(rampListSupportedFileTypes()[i][eend])!=
			  tolower(fname[fend])) {
		    result = NULL; // try again
		    break;
		  }
	  }
      if (result) {
         break;
      }
   }
   return (char *)result;
}

// remove the filename .ext, if found
// return NULL if no .ext found, else return fname
char *rampTrimBaseName(char *fname) {
   char *ext = rampValidFileType(fname);
   if (ext) {
      *ext = 0; // trim the extension
   }
   return ext?fname:NULL;
}


/****************************************************************
 * READS the number of peaks.			*
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE	*
 *    RETURNING !!						*
 ***************************************************************/
int readPeaksCount(RAMPFILE *pFI,
      ramp_fileoffset_t lScanIndex)
{

#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
	   if (lScanIndex < 0) {
         return (0);
       }
       struct ScanHeaderStruct scanHeader;
	   MZML_TRYBLOCK
	   pFI->mzML->getScanHeader((size_t)lScanIndex, scanHeader);
	   MZML_CATCHBLOCK
	   return scanHeader.peaksCount;
   }
#endif   
   // HENRY -- check invalid offset. is 0 a good uninitialized value for peakCount?
   if (lScanIndex <= 0) {
     return (0);
   }
   char *stringBuf=(char *)malloc(SIZE_BUF+1);
   char *beginPeaksCount, *peaks;
   int result = 0;
   const char *tag = pFI->bIsMzData?"length=":"peaksCount=";
   ramp_fileoffset_t in_lScanIndex = lScanIndex;


   ramp_fseek(pFI, lScanIndex, SEEK_SET);

   // Get the num of peaks in the scan and allocate the space we need
   ramp_nextTag(stringBuf, SIZE_BUF, pFI);
   while (!(beginPeaksCount = (char *) strstr(stringBuf, tag)))
   {
      lScanIndex = ramp_ftell(pFI);
      ramp_nextTag(stringBuf, SIZE_BUF, pFI);
   }

   // We need to move forward the length of the tag
   beginPeaksCount += (strlen(tag)+1);
   result = atoi(beginPeaksCount);

   // mext call is probably to read the <peaks> section, position there if needed
   if (pFI->bIsMzData) {
      ramp_fseek(pFI, in_lScanIndex, SEEK_SET);
   } else {
      peaks = strstr(stringBuf,"<peaks");
      if (peaks) {
         ramp_fseek(pFI, lScanIndex+(peaks-stringBuf), SEEK_SET);
      }
   }
   free(stringBuf);
   return result;
}


/****************************************************************
 * READS the base64 encoded list of peaks.			*
 * Return a RAMPREAL* that becomes property of the caller!		*
 * The list is terminated by -1					*
 * !! THE STREAM IS NOT RESET AT THE INITIAL POSITION BEFORE	*
 *    RETURNING !!						*
 ***************************************************************/
#include <zlib.h>
RAMPREAL *readPeaks(RAMPFILE *pFI,
      ramp_fileoffset_t lScanIndex)
{
   RAMPREAL *pPeaks = NULL;
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) { // use pwiz lib to read mzML
	   MZML_TRYBLOCK;
	   std::vector<double> vec;
	   pFI->mzML->getScanPeaks((size_t)lScanIndex, vec);
	   int peaksCount = (int)vec.size()/2; // vec contains mz/int pairs
	   pPeaks = (RAMPREAL *) malloc((peaksCount+1) * 2 * sizeof(RAMPREAL) + 1);
	   if (!pPeaks) {
		   printf("Cannot allocate memory\n");
		   return NULL;
	   }
	   size_t rsize=sizeof(RAMPREAL);
	   if (rsize==sizeof(double)) {
		   memmove(pPeaks,&(vec[0]),2*peaksCount*sizeof(double));
	   } else for (int p = 2*peaksCount;p--;) {
		   pPeaks[p] = (RAMPREAL)vec[p];
	   }
       pPeaks[peaksCount*2] = -1; // some callers want a terminator
       pPeaks[peaksCount*2+1] = -1; // some callers want a terminator
	   MZML_CATCHBLOCK;
	   return pPeaks;
   }
#endif
   int  n=0;
   int  peaksCount=0;
   int  peaksLen;       // The length of the base64 section
   int precision = 0;
   RAMPREAL *pPeaksDeRuled = NULL;
   
   int  endtest = 1;
   int  weAreLittleEndian = *((char *)&endtest);

   char *pData = NULL;
   const char *pBeginData;
   char *pDecoded = NULL;

   char buf[1000];
   buf[sizeof(buf)-1] = 0;

   // HENRY - check invalid offset... is returning NULL here okay? I think it should be because
   // NULL is also returned later in this function when there is no peak found.
   if (lScanIndex <= 0) {
     return (NULL);
   }

   if (pFI->bIsMzData) {
      // intensity and mz are written in two different arrays
      int bGotInten = 0;
      int bGotMZ = 0;
      ramp_fseek(pFI,lScanIndex,SEEK_SET);
      while ((!(bGotInten && bGotMZ)) &&
           ramp_nextTag(buf,sizeof(buf)-1,pFI)) {
         int isArray = 0;
         int isInten = 0;
         int isLittleEndian = 0;
         int byteOrderOK;
         if (strstr(buf,"<mzArrayBinary")) {
            isArray = bGotMZ = 1;
         } else if (strstr(buf,"<intenArrayBinary")) {
            isArray = isInten = bGotInten = 1;
         }
         if (isArray) {
            int partial,triplets,bytes;
            const char *datastart;
            // now determine peaks count, precision
            while (!(datastart= (char *) strstr(buf, "<data")))
            {
               ramp_nextTag(buf, sizeof(buf)-1, pFI);
            }
            
            // find precision="xx"
            if( !(pBeginData = strstr( buf , "precision=" )))
            {
               precision = 32; // default value
            } else { // we found declaration
               precision = atoi(findquot(pBeginData)+1);
            }
            
            // find length="xx"
            if((!peaksCount) && (pBeginData = strstr( buf , "length=" )))
            {
               peaksCount = atoi(findquot(pBeginData)+1);
            }

            if (peaksCount <= 0)
            { // No peaks in this scan!!
               return NULL;
            }

            // find endian="xx"
            if((pBeginData = strstr( buf , "endian=" )))
            {
               isLittleEndian = !strncmp("little",findquot(pBeginData)+1,6);
            }
            
            // find close of <data>
            while( !(pBeginData = strstr( datastart , ">" )))
            {
               ramp_nextTag(buf, sizeof(buf)-1 , pFI);
               datastart = buf;
            }
            pBeginData++;	// skip the >
            
            // base64 has 4:3 bloat, precision/8 bytes per value
            bytes = (peaksCount*(precision/8));
            // for every 3 bytes base64 emits 4 characters - 1, 2 or 3 byte input emits 4 bytes
            triplets = (bytes/3)+((bytes%3)!=0);
            peaksLen = (4*triplets)+1; // read the "<" from </data> too, to confirm lack of whitespace
            
            if ((pData = (char *) realloc(pData,1 + peaksLen)) == NULL)
            {
               printf("Cannot allocate memory\n");
               return NULL;
            }
            
            // copy in any partial read of peak data, and complete the read
            strncpy(pData,pBeginData,peaksLen);
            pData[peaksLen] = 0;
            partial = (int)strlen(pData);
            if (partial < peaksLen) {              
               size_t nread = ramp_fread(pData+partial,(int)(peaksLen-partial), pFI);
            }

            // whitespace may be present in base64 char stream
            while (pData[peaksLen-1]!='<') {
               char *cp;
               partial = 0;
               // didn't read all the peak info - must be whitespace
               for (cp=pData;*cp;) {
                  if (strchr("\t\n\r ",*cp)) {
                     memmove(cp,cp+1,peaksLen-(partial+cp-pData));
                     partial++;
                  } else {
                     cp++;
                  }
               }
               if (!ramp_fread(pData+peaksLen-partial,partial, pFI)) {
                  break;
               }
            }
            pData[peaksLen-1] = 0; // pure base64 now
                        
            if ((pDecoded = (char *) realloc(pDecoded,peaksCount * (precision/8) + 1)) == NULL)
               {
                  printf("Cannot allocate memory\n");
                  return NULL;
               }
               // Base64 decoding
            b64_decode(pDecoded, pData, peaksCount * (precision/8));
            
            if ((!pPeaks) && ((pPeaks = (RAMPREAL *) malloc((peaksCount+1) * 2 * sizeof(RAMPREAL) + 1)) == NULL))
            {
               printf("Cannot allocate memory\n");
               return NULL;
            }
            
            // And byte order correction
            byteOrderOK = (isLittleEndian==weAreLittleEndian);
            if (32==precision) { // floats
               if (byteOrderOK) {
                  float *f = (float *) pDecoded;
                  for (n = 0; n < peaksCount; n++) {
                     pPeaks[isInten+(2*n)] = (RAMPREAL)*f++;
                  }
               } else {
                  uint32_t *u = (uint32_t *) pDecoded;
                  U32 tmp;
                  for (n = 0; n < peaksCount; n++) {
                     tmp.u32 = swapbytes( *u++ );
                     pPeaks[isInten+(2*n)] = (RAMPREAL) tmp.flt;
                  }
               }
            } else { // doubles
               if (byteOrderOK) {
                  double *d = (double *)pDecoded;
                  for (n = 0; n < peaksCount; n++) {
                     pPeaks[isInten+(2*n)] = (RAMPREAL)*d++;
                  }
               } else {
                  uint64_t *u = (uint64_t *) pDecoded;
                  U64 tmp;
                  for (n = 0; n < peaksCount; n++) {
                     tmp.u64 = swapbytes64( *u++ );
                     pPeaks[isInten+(2*n)] = (RAMPREAL) tmp.dbl;
                  }
               }
            }
            if (bGotInten && bGotMZ) {
               break;
            }
         } // end if isArray
      } // end while we haven't got both inten and mz
      free(pData);
      free(pDecoded);
      pPeaks[peaksCount*2] = -1; // some callers want a terminator
   } else { // mzXML
     peaksCount = readPeaksCount(pFI, lScanIndex);
     if (peaksCount <= 0)
     { // No peaks in this scan!!
        return NULL;
     }
	 // handle possible mz/intensity in seperate arrays
	 bool gotMZ=false;
	 bool gotIntensity=false;
	 while ((!gotMZ) || (!gotIntensity)) {
       Byte *pUncompr;
       int isCompressed = 0;
       bool readingMZ = false;
       bool readingIntensity = false;
      int partial,bytes,triplets;
      int isLittleEndian = 0; // default is network byte order (Big endian)
      int byteOrderOK;
      int       compressedLen = 0;
      int       decodedSize;
      char      *pToBeCorrected;
      e_contentType contType = mzInt; // default to m/z-int

      // now determine peaks precision
      char *fgot=ramp_fgets(buf, sizeof(buf)-1, pFI);
      while (!(pBeginData = (char *) strstr(buf, "<peaks")))
      {
		  if (!ramp_fgets(buf, sizeof(buf)-1, pFI)) {
			  break;
		  }
      }
      getIsLittleEndian(buf,&isLittleEndian);

          // TODO ALL OF THE FOLLOWING CHECKS ASSUME THAT THE NAME AND THE VALUE OF THE
          // ATTRIBUTE ARE PRESENT AT THE SAME TIME IN THE BUFFER.
          // ADD A CHECK FOR THAT!
      while( 1 )
      { // Untill the end of the peaks element
          if( (pBeginData = strstr( buf , "precision=" )))
          { // read the precision attribute
              precision = atoi(strchr(pBeginData,'\"')+1);
          }
          if( (pBeginData = strstr( buf , "contentType=" )))
          { // read the contentType attribute
                  // we are only supporting m/z-int for the moment > return if it is something else
                  // TODO add support for the other content types
              if( (pBeginData = strstr( buf , "m/z-int" )))
              {
                  contType = mzInt;
              }
			  else if( (pBeginData = strstr( buf , "m/z ruler" )))
              {
                  contType = mzRuler;
				  readingMZ = readingIntensity = gotMZ = gotIntensity = true; // they're munged together
              }
			  else if( (pBeginData = strstr( buf , "m/z" )))
              {
                  contType = mzOnly;
				  readingMZ = gotMZ = true; 
              }
              else if( (pBeginData = strstr( buf , "intensity" )))
              {
                  contType = intensityOnly;
				  readingIntensity = gotIntensity = true; 
              }
              else
              {
                  const char* pEndAttrValue;
                  pEndAttrValue = strchr( pBeginData + strlen( "contentType=\"") + 1 , '\"' );
#if defined(__clang__)
                  pEndAttrValue = 0; //change for C++-11
#else
                  pEndAttrValue = '\0'; //change for C++-11
#endif
                  fprintf(stderr, "%s Unsupported content type\n" , pBeginData ); 
                  return NULL;
              }
          }
          if( (pBeginData = strstr( buf , "compressionType=" )))
          { // read the compressionType attribute.
              if( (pBeginData = strstr( buf , "zlib" )))
              {
                  isCompressed = 1;
              }
              else if( (pBeginData = strstr( buf , "none" )))
              {
                  isCompressed = 0;
              }
              else
              {
                  const char* pEndAttrValue;
                  pEndAttrValue = strchr( pBeginData + strlen( "compressionType=\"") + 1 , '\"' );
#if defined(__clang__)
                  pEndAttrValue = 0; //change for C++-11
#else
                  pEndAttrValue = '\0'; //change for C++-11
#endif
                  fprintf(stderr, "%s Unsupported compression type\n" , pBeginData );
                  return NULL;
              }
          }
          if( (pBeginData = strstr( buf , "compressedLen=\"")))
          {
              compressedLen = atoi( pBeginData + strlen( "compressedLen=\"" ) );
          }
          if( !(pBeginData = strstr( buf , ">" )))
          { // There is more to read
              char *fgot=ramp_fgets(buf, sizeof(buf)-1 , pFI);
              getIsLittleEndian(buf,&isLittleEndian);
          }
          else
          {
              pBeginData++;	// skip the >
              break;
          }
      }
      if( !precision )
      { // precision attribute was not defined assume 32 by default
          precision = 32;
      }
	  if (mzInt==contType) {
		  readingMZ = readingIntensity = gotMZ = gotIntensity = true; // they're munged together
	  }

	  int dataPerPeak = 1+(readingMZ&&readingIntensity);

      if( isCompressed )
      {
          bytes = compressedLen;
      }
      else
      {
          bytes = (dataPerPeak*peaksCount*(precision/8));
      }
      
      // base64 has 4:3 bloat, precision/8 bytes per value, 2 values per peak
      // for every 3 bytes base64 emits 4 characters - 1, 2 or 3 byte input emits 4 bytes
      triplets = (bytes/3)+((bytes%3)!=0);
      peaksLen = (4*triplets)+1; // read the "<" from </data> too, to confirm lack of whitespace
      
       if ((pData = (char *) malloc(1 + peaksLen)) == NULL)
      {
         printf("Cannot allocate memory\n");
         return NULL;
      }
      pData[peaksLen] = 0;
      
      // copy in any partial read of peak data, and complete the read
      strncpy(pData,pBeginData,peaksLen);
      partial = (int)strlen(pData);
      if (partial < peaksLen) {
         size_t nread = ramp_fread(pData+partial,peaksLen-partial, pFI);
      }
      // whitespace may be present in base64 char stream
      while (pData[peaksLen-1]!='<') {
         char *cp;
         partial = 0;
         // didn't read all the peak info - must be whitespace
         for (cp=pData;*cp;) {
            if (strchr("\t\n\r ",*cp)) {
               memmove(cp,cp+1,peaksLen+1-(partial+cp-pData));
               partial++;
            } else {
               cp++;
            }
         }
         if (!ramp_fread(pData+peaksLen-partial,partial, pFI)) {
            break;
         }
      }
      if( isCompressed )
      {
          decodedSize = compressedLen + 1;
      }
      else
      {
          // dataPerPeak values per peak, precision/8 bytes per value
          decodedSize = dataPerPeak * peaksCount * (precision/8) + 1;
      }
      pData[peaksLen-1] = 0; // pure base64 now
      
      if ((pDecoded = (char *) malloc( decodedSize )) == NULL)
         {
            printf("Cannot allocate memory\n");
            return NULL;
         }
      // Base64 decoding
      b64_decode(pDecoded, pData, decodedSize-1);
      free(pData);
      
      if ((!pPeaks) && ((pPeaks = (RAMPREAL *) malloc((peaksCount+1) * 2 * sizeof(RAMPREAL) + 1)) == NULL))
      {
         printf("Cannot allocate memory\n");
         return NULL;
      }

          //Zlib decompression 
      if( isCompressed )
      {
          int err;
//        printf("Decompressing data\n");
          uLong uncomprLen = (dataPerPeak * peaksCount * (precision/8) + 1);
			
          pUncompr = (Byte*)calloc((uInt) uncomprLen , 1);
			
          err = uncompress( pUncompr , &uncomprLen , (const Bytef*)pDecoded , decodedSize );
          free( pDecoded );
          pToBeCorrected = (char *)pUncompr;
      }
      else
      {
          pToBeCorrected = pDecoded;
      }
      
      // And byte order correction
      byteOrderOK = (isLittleEndian==weAreLittleEndian);
	  int beginAt = readingMZ?0:1;
	  int step = 1+(readingMZ!=readingIntensity);
	  int m=0;
      if (32==precision) { // floats
         if (byteOrderOK) {
            for (n = beginAt; n < (2 * peaksCount); n+=step) {
               pPeaks[n] = (RAMPREAL) ((float *) pToBeCorrected)[m++];
            } 
         } else {
            U32 tmp;
            for (n = beginAt; n < (2 * peaksCount); n+=step) {
               tmp.u32 = swapbytes(((uint32_t *) pToBeCorrected)[m++]);
               pPeaks[n] = (RAMPREAL) tmp.flt;
            } 
         }
      } else { // doubles
         if (byteOrderOK) {
            for (n = beginAt; n < (2 * peaksCount); n+=step) {
               pPeaks[n] = (RAMPREAL)((double *) pToBeCorrected)[m++];
            }
         } else {
            U64 tmp;
            for (n = beginAt; n < (2 * peaksCount); n+=step) {
               tmp.u64 = swapbytes64((uint64_t) ((uint64_t *) pToBeCorrected)[m++]);
               pPeaks[n] = (RAMPREAL) tmp.dbl;
            }
         }
      }

      if( contType == mzRuler )
      { // Convert back from m/z ruler contentType into m/z - int pairs
		RAMPREAL lastMass=0;
		RAMPREAL  deltaMass=0;
		  int multiplier=0;
          int j = 0;

          if ((pPeaksDeRuled = (RAMPREAL *) malloc((peaksCount+1) * 2 * sizeof(RAMPREAL) + 1)) == NULL)
          {
              printf("Cannot allocate memory\n");
              return NULL;
          }
         
          for (n = 0; n < (2 * peaksCount); )
          {
// printf("%f\n" , pPeaks[j] );
              if( (int) pPeaks[j] == -1 )
              { // Change in delta m/z
				++j;
				lastMass = (RAMPREAL) pPeaks[j++];
                deltaMass = pPeaks[j++];
				multiplier = 0;
//printf("%f %f\n" , lastMass , deltaMass );
              }
   		    pPeaksDeRuled[n++] = lastMass + (RAMPREAL) multiplier * deltaMass;
			++multiplier;
            pPeaksDeRuled[n++] = pPeaks[j++];
          }
          
          free(pToBeCorrected);
          pPeaksDeRuled[n] = -1;

          free( pPeaks );
          return (pPeaksDeRuled); // caller must free this pointer
      }
      
      free(pToBeCorrected);
      pPeaks[n] = -1;
   }
	} // end while((!gotMZ)||(!gotIntensity))
   return (pPeaks); // caller must free this pointer
}


/*
 * read just the info available in the msRun element
 */
void readMSRun(RAMPFILE *pFI,
                   struct RunHeaderStruct *runHeader)
{
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) {
	   MZML_TRYBLOCK;
	   pFI->mzML->getRunHeader(*runHeader);
	   MZML_CATCHBLOCK;
	   return;
   }
#endif
   char stringBuf[SIZE_BUF+1];
   ramp_fseek(pFI, 0 , SEEK_SET); // rewind
   char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);

   while((!strstr( stringBuf , pFI->bIsMzData?"<mzData":"<msRun" )) && !ramp_feof(pFI))  /* this should not be needed if index offset points to correct location */
   {
      char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);
   }
   while(!ramp_feof(pFI))
   {
      const char *cp;
      if (NULL != (cp=strstr( stringBuf , pFI->bIsMzData?"spectrumList count":"scanCount" ))) {
         cp = findquot(cp);
         runHeader->scanCount = atoi(cp+1);
      }
      if (NULL != (cp=strstr( stringBuf , "startTime" ))) {
         cp = findquot(cp);
         runHeader->dStartTime = rampReadTime(pFI,cp+1);
      } 
      if (NULL != (cp=strstr( stringBuf , "endTime" ))) {
         cp = findquot(cp);
         runHeader->dEndTime = rampReadTime(pFI,cp+1);
      } 
      if (NULL != (cp=strstr( stringBuf , pFI->bIsMzData?"<spectrumDesc":"<scan" ))) {
         break; /* we're into data territory now */
      } 
      char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);
   }

}

/*
 * walk through each scan to find overall lowMZ, highMZ
 * sets overall start and end times also
 */
void readRunHeader(RAMPFILE *pFI,
                   ramp_fileoffset_t *pScanIndex,
                   struct RunHeaderStruct *runHeader,
                   int iLastScan)
{

   int i;
   struct ScanHeaderStruct scanHeader;
      
   double startMz = 0.0;
   double endMz = 0.0;
   int firstScan = 1;

   // HENRY -- initialize runHeader to some uninitialized values in case of failure
   runHeader->lowMZ = 0;
   runHeader->highMZ = 0;
   runHeader->dStartTime = 0;
   runHeader->startMZ = 1.E6;
   runHeader->endMZ = 0;
   
   // HENRY -- skipping over all the "missing scans"
   while (firstScan <= iLastScan && pScanIndex[firstScan] <= 0) { 
     firstScan++;
   }
   if (firstScan > iLastScan) {
     // HENRY -- this means there is no scan! do we need to initialize runHeader to something so that the caller
     // can check for this?
     return;
   }
   
   readHeader(pFI, pScanIndex[firstScan], &scanHeader);

   /*
    * initialize values to first scan
    */
   runHeader->lowMZ = scanHeader.lowMZ;
   runHeader->highMZ = scanHeader.highMZ;
   runHeader->dStartTime = scanHeader.retentionTime;
   runHeader->startMZ = readStartMz( pFI , pScanIndex[1] );
   runHeader->endMZ = readEndMz( pFI , pScanIndex[1] );
   for (i = 2; i <= iLastScan; i++)
   {
     // HENRY -- skipping over all the missing scans
     if (pScanIndex[i] <= 0) {
       continue;
     }
      readHeader(pFI, pScanIndex[i], &scanHeader);

      if (scanHeader.lowMZ < runHeader->lowMZ)
         runHeader->lowMZ = scanHeader.lowMZ;
      if (scanHeader.highMZ > runHeader->highMZ)
         runHeader->highMZ = scanHeader.highMZ;
      if( (startMz = readStartMz( pFI , pScanIndex[i] )) < runHeader->startMZ )
	runHeader->startMZ = startMz;
      if( (endMz = readEndMz( pFI , pScanIndex[i] )) > runHeader->endMZ )
	runHeader->endMZ = endMz;   
   }

   runHeader->dEndTime = scanHeader.retentionTime;
}



static int setTagValue(const char* text,
   char* storage,
   int maxlen,
   const char* lead)
{
  const char* result = NULL;
  const char* term = NULL;
  int len = maxlen - 1;
  int leadlen = (int)strlen(lead)+1; // include the opening quote

  result = strstr(text, lead);
  if(result != NULL)
  {
    char tail = *(result+leadlen-1); // nab the quote char (is it single or double quote?)
    term = strchr(result + leadlen, tail);
    if(term != NULL)
    {
      if((int)(strlen(result) - strlen(term) - leadlen) < len)
        len = (int)strlen(result) - (int)strlen(term) - leadlen;

      strncpy(storage, result + leadlen , len);
      storage[len] = 0;
      return 1;
    } // if term
  }
  return 0;
}


InstrumentStruct* getInstrumentStruct(RAMPFILE *pFI)
{
  InstrumentStruct* output = NULL;
  char* result = NULL;
  int found[] = {0, 0, 0, 0, 0};
  char stringBuf[SIZE_BUF+1];
   if ((output = (InstrumentStruct *) calloc(1,sizeof(InstrumentStruct))) == NULL)
   {
      printf("Cannot allocate memory\n");
      return NULL;
   } else {
      const char *cpUnknown="UNKNOWN";
      strncpy(output->analyzer,cpUnknown,sizeof(output->analyzer));
      strncpy(output->detector,cpUnknown,sizeof(output->detector));
      strncpy(output->ionisation,cpUnknown,sizeof(output->ionisation));
      strncpy(output->manufacturer,cpUnknown,sizeof(output->manufacturer));
      strncpy(output->model,cpUnknown,sizeof(output->model));
   }
#ifdef HAVE_PWIZ_MZML_LIB
   if (pFI->mzML) {
	   MZML_TRYBLOCK;
	   pFI->mzML->getInstrument(*output);
	   MZML_CATCHBLOCK;
	   return output;
   }
#endif   
  // HENRY - need to rewind to get instrument info
  ramp_fseek(pFI, 0 , SEEK_SET);
 


   char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);

   if (pFI->bIsMzData) {  // TODO
   } else {
      int isAncient=0;
      while( !strstr( stringBuf , "<msInstrument" ) && 
            !(isAncient=(NULL!=strstr( stringBuf , "<instrument" ))) && 
            !strstr(stringBuf, "<dataProcessing") && 
            !ramp_feof(pFI))  /* this should not be needed if index offset points to correct location */
      {
         char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);
      }
            
      while(! strstr(stringBuf, isAncient?"</instrument":"</msInstrument") &&  ! strstr(stringBuf, "</dataProcessing") && !ramp_feof(pFI))
      {
        if(! found[0])
        {
          result = strstr(stringBuf, isAncient?"manufacturer=":"<msManufacturer");
          if(result != NULL && setTagValue(result, output->manufacturer, INSTRUMENT_LENGTH, isAncient?"manufacturer=":"value="))
	      found[0] = 1;
      
        }
        if(! found[1])
        {
           result = strstr(stringBuf, isAncient?"model=":"<msModel");
          if(result != NULL && setTagValue(result, output->model, INSTRUMENT_LENGTH, isAncient?"model=":"value="))
	      found[1] = 1;
        }
        if(! found[2])
        {
          result = strstr(stringBuf, isAncient?"ionisation=":"<msIonisation");
          if(result != NULL && setTagValue(result, output->ionisation, INSTRUMENT_LENGTH, isAncient?"ionisation=":"value="))
	      found[2] = 1;
        }
        if(! found[3])
        {
           result = strstr(stringBuf, isAncient?"msType=":"<msMassAnalyzer");
          if(result != NULL && setTagValue(result, output->analyzer, INSTRUMENT_LENGTH, isAncient?"msType=":"value="))
	      found[3] = 1;
        }
        if(! found[4])
        {
          result = strstr(stringBuf, "<msDetector");
          if(result != NULL && setTagValue(result, output->detector, INSTRUMENT_LENGTH, "value="))
	      found[4] = 1;
        }
        char *fgot=ramp_fgets(stringBuf, SIZE_BUF, pFI);

      } // while
   }

   if(found[0] || found[1] || found[2] || found[3] || found[4])
     return output;

   return NULL; // no data
}

void setRampOption(long option) {
   G_RAMP_OPTION = option;
}

int isScanAveraged(struct ScanHeaderStruct *scanHeader) {
   return (scanHeader->mergedScan);
}

int isScanMergedResult(struct ScanHeaderStruct *scanHeader) {
   return ((scanHeader->mergedScan 
      && 0 != scanHeader->mergedResultStartScanNum
      && 0 != scanHeader->mergedResultEndScanNum) ? 1 : 0);
}

void getScanSpanRange(const struct ScanHeaderStruct *scanHeader, int *startScanNum, int *endScanNum) {
   if (0 == scanHeader->mergedResultStartScanNum || 0 == scanHeader->mergedResultEndScanNum) {
      *startScanNum = scanHeader->acquisitionNum;
      *endScanNum = scanHeader->acquisitionNum;
   } else {
      *startScanNum = scanHeader->mergedResultStartScanNum;
      *endScanNum = scanHeader->mergedResultEndScanNum;
   }
}

// exercise at least some of the ramp interface - return non-0 on failure
int rampSelfTest(char *filename) { // if filename is non-null we'll exercise reader with it
   int result = 0; // assume success
   char buf[256];
   char buf2[256];
   int i;

   const char *testname[] = 
   {"foo.bar","foo.mzxml","foo.mzdata","foo.mzXML","foo.mzData","foo.mzML",
      "foo.mzxml.gz","foo.mzdata.gz","foo.mzXML.gz","foo.mzData.gz","foo.mzML.gz",
   NULL};

   // locate the .mzData or .mzXML extension in the buffer
   // return pointer to extension, or NULL if not found
   for (i=0;testname[i];i++) {
      result |= (!i) != !rampValidFileType(testname[i]); // 0th one in not a valid file type
   }

   // trim a filename of its .mzData or .mzXML extension
   // return trimmed buffer, or null if no proper .ext found
   for (i=0;testname[i];i++) {
      strncpy(buf,testname[i],sizeof(buf));
      result |= ((!i) != !rampTrimBaseName(buf));
      if (i) {
         result |= (strcmp(buf,"foo")!=0);
      }
   }

   if (filename && rampValidFileType(filename)) {
      // construct a filename in buf from a basename, adding .mzXML or .mzData as exists
      // returns buf, or NULL if neither .mzXML or .mzData file exists
      char *name;
      strncpy(buf,filename,sizeof(buf));
      rampTrimBaseName(buf);
      name = rampConstructInputFileName(buf,sizeof(buf),buf); // basename is in buf
      result |= (name==NULL);
      strncpy(buf,filename,sizeof(buf));
      rampTrimBaseName(buf);
      name = rampConstructInputFileName(buf2,sizeof(buf2),buf); // different buf, basename
      result |= (name==NULL);
	  if (name) {
		  struct stat statbuf;
		  result |= stat(name,&statbuf);
	  }
   }
   return result;
}

// Cache support

// Get a new cache instance with a specified window size.  A larger window
// requires more memory, obviously.  Too small a window for the required
// function can lead perf comparable to no caching at all.

// Iterating over a range of 200 scans with a cache that contains 100 or
// fewer scans will yield no cache hits on each iteration.  Pick a cache
// size slightly larger than the window you expect to cover.

struct ScanCacheStruct *getScanCache(int size)
{
    struct ScanCacheStruct* cache = (struct ScanCacheStruct*) malloc(sizeof(struct ScanCacheStruct));
    cache->seqNumStart = 0;
    cache->size = size;
    cache->headers = (struct ScanHeaderStruct*) calloc(size, sizeof(struct ScanHeaderStruct));
    cache->peaks = (RAMPREAL**) calloc(size, sizeof(RAMPREAL*));
    return cache;
}

// Free all memory associated with a cache struct.
void freeScanCache(struct ScanCacheStruct* cache)
{
   if (cache) {
    int i;
    for (i = 0; i < cache->size; i++)
    {
        if (cache->peaks[i] != NULL)
            free(cache->peaks[i]);
    }
    free(cache->peaks);
    free(cache->headers);
    free(cache);
   }
}

// Clear all cached values, freeing peaks, but not the cache arrays themselves.
void clearScanCache(struct ScanCacheStruct* cache)
{
    int i;
    for (i = 0; i < cache->size; i++)
    {
        if (cache->peaks[i] == NULL)
            continue;

        free(cache->peaks[i]);
        cache->peaks[i] = NULL;
    }
    memset(cache->headers, 0, cache->size * sizeof(struct ScanHeaderStruct));
}

// Shift the cache start index by a number of scans.  This moves the cache
// window left (negative) or right (positive).
void shiftScanCache(struct ScanCacheStruct* cache, int nScans)
{
    int i;
    cache->seqNumStart += nScans;
    if (abs(nScans) > cache->size)
    {
        // If the shift is larger than the size of the cache window,
        // just clear the whole cache.
        clearScanCache(cache);
    }
    else if (nScans > 0)
    {
        // Shifting window to the right.  Memory moves right, with new
        // empty scans on the end.

        // Free the peaks that memmove will overwrite.
        for (i = 0; i < nScans; i++)
        {
            if (cache->peaks[i] != NULL)
                free(cache->peaks[i]);
        }
        memmove(cache->peaks, cache->peaks + nScans,
            (cache->size - nScans) * sizeof(RAMPREAL*));
        memset(cache->peaks + cache->size - nScans, 0, nScans * sizeof(RAMPREAL*));
        memmove(cache->headers, cache->headers + nScans,
            (cache->size - nScans) * sizeof(struct ScanHeaderStruct));
        memset(cache->headers + cache->size - nScans, 0, nScans * sizeof(struct ScanHeaderStruct));
    }
    else if (nScans < 0)
    {
        // Shifting window to the left.  Memory moves right, with new
        // empty scans at the beginning.
        nScans = -nScans;

        // Free the peaks that memmove will overwrite.
        for (i = 0; i < nScans; i++)
        {
            if (cache->peaks[cache->size - 1 - i] != NULL)
                free(cache->peaks[cache->size - 1 - i]);
        }
        memmove(cache->peaks + nScans, cache->peaks,
            (cache->size - nScans) * sizeof(RAMPREAL*));
        memset(cache->peaks, 0, nScans * sizeof(RAMPREAL*));
        memmove(cache->headers  + nScans, cache->headers,
            (cache->size - nScans) * sizeof(struct ScanHeaderStruct));
        memset(cache->headers, 0, nScans * sizeof(struct ScanHeaderStruct));
    }
}

// Convert a scan index into a cache index, adjusting the cache window
// if necessary.
int getCacheIndex(struct ScanCacheStruct* cache, int seqNum)
{
    int seqNumStart = cache->seqNumStart;
    int size = cache->size;

    // First access, just set the start to seqNum.
    if (seqNumStart == 0)
        cache->seqNumStart = seqNum;
    // If requested scan is less than cache start, shift cache window
    // left to start at requested scan.
    else if (seqNum < seqNumStart)
        shiftScanCache(cache, (int) (seqNum - seqNumStart));
    // If requested scan is greater than cache end, shift cache window
    // right so last entry is requested scan.
    else if (seqNum >= seqNumStart + size)
        shiftScanCache(cache, (int) (seqNum - (seqNumStart + size - 1)));

    return (int) (seqNum - cache->seqNumStart);
}

const struct ScanHeaderStruct* readHeaderCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex)
{
    int i = getCacheIndex(cache, seqNum);
    if (cache->headers[i].msLevel == 0)
        readHeader(pFI, lScanIndex, cache->headers + i);
    return cache->headers + i;
}

int readMsLevelCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex)
{
    const struct ScanHeaderStruct* header = readHeaderCached(cache, seqNum, pFI, lScanIndex);
    return header->msLevel;
}

const RAMPREAL *readPeaksCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex)
{
    int i = getCacheIndex(cache, seqNum);
    if (cache->peaks[i] == NULL)
        cache->peaks[i] = readPeaks(pFI, lScanIndex);
    return cache->peaks[i];
}

