/***************************************************************************
                             RAMP


Non sequential parser for mzXML files

                             -------------------
    begin                : Wed Oct 10
    copyright            : (C) 2003 by Pedrioli Patrick, ISB, Proteomics
    email                : ppatrick@student.ethz.ch
    additional work for C++, >2GB files in WIN32, and portability (C) 2004 by Brian Pratt, Insilicos LLC 
 ***************************************************************************/

/***************************************************************************
*                                                                          *
*  This program is free software; you can redistribute it and/or modify    *
*  it under the terms of the GNU Library or "Lesser" General Public        *
*  License (LGPL) as published by the Free Software Foundation;            *
*  either version 2 of the License, or (at your option) any later          *
*  version.                                                                *
***************************************************************************/

#ifndef _RAMP_H
#define _RAMP_H

#include <stdio.h>
#include <stdlib.h>

#ifdef __cplusplus
#define RAP_EXTERN_C extern "C"
#else 
#define RAP_EXTERN_C 
#endif

#if defined(_MSC_VER) || defined(_MINGW_) || defined(WIN32)
#include <winsock2.h>
#include <sys/types.h>
#include <fcntl.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <io.h>
#ifndef __INTTYPES_H_
typedef unsigned int uint32_t;
typedef unsigned __int64 uint64_t;
#endif
#ifndef strcasecmp
#define strcasecmp stricmp
#endif
#else
#include <stdint.h>
#include <netinet/in.h>
#endif

#ifndef _LARGEFILE_SOURCE 
typedef int ramp_filehandle_t; // use MSFT API for 64 bit file pointers
#else
typedef FILE * ramp_filehandle_t; // a real OS with real file handling
#endif

// set mz and intensity precision
#ifndef RAMPREAL_FLOAT
typedef double RAMPREAL; 
#else
typedef float RAMPREAL; 
#endif

typedef enum { mzInt = 0 , mzRuler } e_contentType;

#ifdef SWIG
%apply long long {ramp_fileoffset_t};
#else
#ifndef RAMP_STRUCT_DECL_ONLY  // useful for pwiz, which only wants to mimic ramp structs
//
// we use this struct instead of FILE* so we can track what kind of files we're parsing
//
typedef struct {
   ramp_filehandle_t fileHandle;
   int bIsMzData; // mzXML or mzData?
} RAMPFILE;
#endif

#ifndef _LARGEFILE_SOURCE // use MSFT API for 64 bit file pointers
typedef __int64 ramp_fileoffset_t;
#define ramp_fseek(a,b,c) _lseeki64((a)->fileHandle,b,c)
#define ramp_ftell(a) _lseeki64((a)->fileHandle,0,SEEK_CUR)
#define ramp_fread(buf,len,handle) read((handle)->fileHandle,buf,len)
#ifndef RAMP_STRUCT_DECL_ONLY  // useful for pwiz, which only wants to mimic ramp structs
RAP_EXTERN_C char *ramp_fgets(char *buf,int len,RAMPFILE *handle);
#endif
#define ramp_feof(handle) eof((handle)->fileHandle)
#define atoll(a) _atoi64(a)

#else // a real OS with real file handling
typedef off_t ramp_fileoffset_t;
#define ramp_fseek(a,b,c) fseeko((a)->fileHandle,b,c)
#define ramp_ftell(a) ftello((a)->fileHandle)
#define ramp_fread(buf,len,handle) fread(buf,1,len,(handle)->fileHandle)
#define ramp_fgets(buf,len,handle) fgets(buf, len, (handle)->fileHandle)
#define ramp_feof(handle) feof((handle)->fileHandle)
#endif
#endif // not SWIG

#include <string.h>
#include <ctype.h>


#ifdef __cplusplus
extern "C" {
#endif

#define INSTRUMENT_LENGTH 2000
#define SCANTYPE_LENGTH 32


struct ScanHeaderStruct
{
   int seqNum; // number in sequence observed file (1-based)
   int acquisitionNum; // scan number as declared in File (may be gaps)
   int  msLevel;
   int  peaksCount;
   double totIonCurrent;
   double retentionTime;        /* in seconds */
   double basePeakMZ;
   double basePeakIntensity;
   double collisionEnergy;
   double ionisationEnergy;
   double lowMZ;
   double highMZ;
   int precursorScanNum; /* only if MS level > 1 */
   double precursorMZ;  /* only if MS level > 1 */
   int precursorCharge;  /* only if MS level > 1 */
   double precursorIntensity;  /* only if MS level > 1 */
   char scanType[SCANTYPE_LENGTH];
   int mergedScan;  /* only if MS level > 1 */
   int mergedResultScanNum; /* scan number of the resultant merged scan */
   int mergedResultStartScanNum; /* smallest scan number of the scanOrigin for merged scan */
   int mergedResultEndScanNum; /* largest scan number of the scanOrigin for merged scan */
   ramp_fileoffset_t filePosition; /* where in the file is this header? */
};

struct RunHeaderStruct
{
  int scanCount;
  double lowMZ;
  double highMZ;
  double startMZ;
  double endMZ;
  double dStartTime;
  double dEndTime;
};

typedef struct InstrumentStruct
{
   char manufacturer[INSTRUMENT_LENGTH];
   char model[INSTRUMENT_LENGTH];
   char ionisation[INSTRUMENT_LENGTH];
   char analyzer[INSTRUMENT_LENGTH];
   char detector[INSTRUMENT_LENGTH];
   //char msType[INSTRUMENT_LENGTH];
} InstrumentStruct;

#ifndef RAMP_STRUCT_DECL_ONLY  // useful for pwiz, which only wants to mimic ramp structs
// file open/close
RAMPFILE *rampOpenFile(const char *filename);
void rampCloseFile(RAMPFILE *pFI);

// construct a filename in buf from a basename, adding .mzXML or .mzData
// as exists, or .mzXML if neither exists. returns buf, or NULL if buflen
// is too short
char *rampConstructInputFileName(char *buf,int buflen,const char *basename);
char *rampConstructInputPath(char *buf,int buflen,const char *dir,const char *basename);

// trim a filename of its .mzData or .mzXML extension
// return trimmed buffer, or null if no proper .ext found
char *rampTrimBaseName(char *buf);

// locate the .mzData or .mzXML extension in the buffer
// return pointer to extension, or NULL if not found
char *rampValidFileType(const char *buf);

// exercise at least some of the ramp interface - return non-0 on failure
int rampSelfTest(char *filename); // if filename is non-null we'll exercise reader with it

ramp_fileoffset_t getIndexOffset(RAMPFILE *pFI);
ramp_fileoffset_t *readIndex(RAMPFILE *pFI,
                ramp_fileoffset_t indexOffset,
                int *iLastScan);
void readHeader(RAMPFILE *pFI,
                ramp_fileoffset_t lScanIndex, // read from this file position
                struct ScanHeaderStruct *scanHeader);
int  readMsLevel(RAMPFILE *pFI,
                 ramp_fileoffset_t lScanIndex);
double readStartMz(RAMPFILE *pFI,
		   ramp_fileoffset_t lScanIndex);
double readEndMz(RAMPFILE *pFI,
		   ramp_fileoffset_t lScanIndex);
int readPeaksCount(RAMPFILE *pFI,
                 ramp_fileoffset_t lScanIndex);
RAMPREAL *readPeaks(RAMPFILE *pFI,
                 ramp_fileoffset_t lScanIndex);
void readRunHeader(RAMPFILE *pFI,
                   ramp_fileoffset_t *pScanIndex,
                   struct RunHeaderStruct *runHeader,
                   int iLastScan);
void readMSRun(RAMPFILE *pFI,
                   struct RunHeaderStruct *runHeader);

InstrumentStruct* getInstrumentStruct(RAMPFILE *pFI);

// for MS/MS averaged scan
enum {
  MASK_SCANS_TYPE = 0x0003,
  BIT_ORIGIN_SCANS = 0x0001,
  BIT_AVERAGE_SCANS = 0x0002,
  OPTION_AVERAGE_SCANS = BIT_AVERAGE_SCANS, // return scan including merged resultant scan
                                            // but exclude 'real' scan via peaksCount=0
  OPTION_ORIGIN_SCANS = BIT_ORIGIN_SCANS,   // return 'real' scan 
                                            // but exclude merged resultant scan via peaksCount=0
  OPTION_ALL_SCANS = BIT_ORIGIN_SCANS | BIT_AVERAGE_SCANS,
                                            // return 'real' scan + merged resultant scan
  DEFAULT_OPTION = OPTION_AVERAGE_SCANS
};
void setRampOption(long option);
// return 0 if the scan has not been used in merged scan
//        1 otherwise
int isScanAveraged(struct ScanHeaderStruct *scanHeader);
// return 1 if the scan is generated by merging other scans
//        0 otherwise
int isScanMergedResult(struct ScanHeaderStruct *scanHeader);
// return the scan range for a "raw" scan or merged scan
// return (<scan num>,<scan num>) in the case of "raw" (i.e. non-merged) scan
// return (<smallest scan num>,<highest scan num>) in the case of merged scan
void getScanSpanRange(struct ScanHeaderStruct *scanHeader, int *startScanNum, int *endScanNum);
// END - for MS/MS averaged scan

// Caching support
// Useful for working with a range of MS1 scans.  Code can just ask for scan
// headers and peaks as normal, and the cache takes care of shifting its range.

struct ScanCacheStruct
{
    int seqNumStart;    // scan at which the cache starts
    int size;           // number of scans in the cache
    struct ScanHeaderStruct *headers;
    RAMPREAL **peaks;
};

// create a chache struct
struct ScanCacheStruct *getScanCache(int size);

// free all memory held by a cache struct
void freeScanCache(struct ScanCacheStruct* cache);

void clearScanCache(struct ScanCacheStruct* cache);

// cached versions of standard ramp functions
struct ScanHeaderStruct* readHeaderCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex);
int readMsLevelCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex);
RAMPREAL *readPeaksCached(struct ScanCacheStruct* cache, int seqNum, RAMPFILE* pFI, ramp_fileoffset_t lScanIndex);

#endif // ifndef RAMP_STRUCT_DECL_ONLY  useful for pwiz, which only wants to mimic ramp structs

#ifdef __cplusplus
}
#endif

#endif
