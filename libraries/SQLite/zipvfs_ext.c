/*
** zipvfs_ext.c - a SQLite loadable extension that registers a read-only "offset shim" VFS
** named "skyzipvfs". It opens the underlying real file through the default VFS but adds a base
** byte-offset to every read, so a database (e.g. a .blib spectral library) that is stored
** UNCOMPRESSED at an offset inside a larger container file (a .sky.zip entry) can be opened in
** place without extracting it.
**
** The base offset and length are passed as URI parameters:
**     file:///C:/doc.sky.zip?ofs=NNN&len=NNN&vfs=skyzipvfs
** (len omitted or 0 means "to the end of the container file").
**
** Registration uses the sqlite3_api_routines table handed to the extension's init function, so
** it works regardless of how the host SQLite's symbols are exported. This is why a loadable
** extension is used instead of P/Invoking sqlite3_vfs_register directly: the System.Data.SQLite
** interop only exports hashed symbol names, and does not export sqlite3_vfs_register at all.
**
** IMPORTANT: the host must keep this DLL loaded for as long as the VFS is registered - the
** registered sqlite3_vfs struct and these functions live inside this DLL.
*/
#include "sqlite3ext.h"
SQLITE_EXTENSION_INIT1
#include <string.h>

typedef struct ZipFile ZipFile;
struct ZipFile {
  sqlite3_file base;      /* IO methods; must be first */
  sqlite3_file *pReal;    /* underlying file opened via the default VFS */
  sqlite3_int64 ofs;      /* base offset of the database inside the container */
  sqlite3_int64 len;      /* length of the database */
};

static sqlite3_vfs *gRoot;  /* the default VFS we delegate to */

static int zipClose(sqlite3_file *pFile){
  ZipFile *p = (ZipFile*)pFile;
  int rc = SQLITE_OK;
  if( p->pReal && p->pReal->pMethods ) rc = p->pReal->pMethods->xClose(p->pReal);
  if( p->pReal ) sqlite3_free(p->pReal);
  p->pReal = 0;
  return rc;
}
static int zipRead(sqlite3_file *pFile, void *zBuf, int iAmt, sqlite3_int64 iOfst){
  ZipFile *p = (ZipFile*)pFile;
  return p->pReal->pMethods->xRead(p->pReal, zBuf, iAmt, p->ofs + iOfst);
}
static int zipWrite(sqlite3_file *pFile, const void *z, int n, sqlite3_int64 o){ (void)pFile;(void)z;(void)n;(void)o; return SQLITE_READONLY; }
static int zipTruncate(sqlite3_file *pFile, sqlite3_int64 size){ (void)pFile;(void)size; return SQLITE_READONLY; }
static int zipSync(sqlite3_file *pFile, int flags){ (void)pFile;(void)flags; return SQLITE_OK; }
static int zipFileSize(sqlite3_file *pFile, sqlite3_int64 *pSize){ *pSize = ((ZipFile*)pFile)->len; return SQLITE_OK; }
static int zipLock(sqlite3_file *pFile, int e){ (void)pFile;(void)e; return SQLITE_OK; }
static int zipUnlock(sqlite3_file *pFile, int e){ (void)pFile;(void)e; return SQLITE_OK; }
static int zipCheckReservedLock(sqlite3_file *pFile, int *pResOut){ (void)pFile; *pResOut = 0; return SQLITE_OK; }
static int zipFileControl(sqlite3_file *pFile, int op, void *pArg){ (void)pFile;(void)op;(void)pArg; return SQLITE_NOTFOUND; }
static int zipSectorSize(sqlite3_file *pFile){ (void)pFile; return 4096; }
static int zipDeviceCharacteristics(sqlite3_file *pFile){ (void)pFile; return SQLITE_IOCAP_IMMUTABLE; }

static const sqlite3_io_methods zipIoMethods = {
  1,                          /* iVersion */
  zipClose, zipRead, zipWrite, zipTruncate, zipSync, zipFileSize,
  zipLock, zipUnlock, zipCheckReservedLock, zipFileControl,
  zipSectorSize, zipDeviceCharacteristics
};

static int zipOpen(sqlite3_vfs *pVfs, const char *zName, sqlite3_file *pFile,
                   int flags, int *pOutFlags){
  ZipFile *p = (ZipFile*)pFile;
  int rc;
  (void)pVfs;
  memset(p, 0, sizeof(*p));
  p->ofs = sqlite3_uri_int64(zName, "ofs", 0);
  p->len = sqlite3_uri_int64(zName, "len", 0);
  p->pReal = (sqlite3_file*)sqlite3_malloc(gRoot->szOsFile);
  if( p->pReal==0 ) return SQLITE_NOMEM;
  memset(p->pReal, 0, gRoot->szOsFile);
  rc = gRoot->xOpen(gRoot, zName, p->pReal,
                    (flags & ~SQLITE_OPEN_READWRITE) | SQLITE_OPEN_READONLY, pOutFlags);
  if( rc!=SQLITE_OK ){ sqlite3_free(p->pReal); p->pReal = 0; return rc; }
  if( p->len==0 ){
    sqlite3_int64 sz = 0;
    p->pReal->pMethods->xFileSize(p->pReal, &sz);
    p->len = sz - p->ofs;   /* to the end of the container */
  }
  p->base.pMethods = &zipIoMethods;
  return SQLITE_OK;
}

/* These delegate to the default VFS (passing gRoot explicitly). */
static int zipFullPathname(sqlite3_vfs *v, const char *z, int n, char *o){ (void)v; return gRoot->xFullPathname(gRoot, z, n, o); }
static int zipAccess(sqlite3_vfs *v, const char *z, int f, int *r){ (void)v; return gRoot->xAccess(gRoot, z, f, r); }
static int zipDelete(sqlite3_vfs *v, const char *z, int s){ (void)v; return gRoot->xDelete(gRoot, z, s); }

#ifdef _WIN32
__declspec(dllexport)
#endif
int sqlite3_zipvfs_init(sqlite3 *db, char **pzErrMsg, const sqlite3_api_routines *pApi){
  static sqlite3_vfs zipVfs;   /* static storage: persists for the DLL's lifetime */
  SQLITE_EXTENSION_INIT2(pApi);
  (void)db; (void)pzErrMsg;
  gRoot = sqlite3_vfs_find(0);
  if( gRoot==0 ) return SQLITE_ERROR;
  if( sqlite3_vfs_find("skyzipvfs")!=0 ) return SQLITE_OK;   /* already registered */
  /* Copy the default VFS (correct struct layout by construction), then override only what we
  ** need. All non-overridden methods stay the default VFS's own implementations. */
  memcpy(&zipVfs, gRoot, sizeof(zipVfs));
  zipVfs.szOsFile = sizeof(ZipFile);
  zipVfs.zName = "skyzipvfs";
  zipVfs.pNext = 0;
  zipVfs.xOpen = zipOpen;
  zipVfs.xFullPathname = zipFullPathname;
  zipVfs.xAccess = zipAccess;
  zipVfs.xDelete = zipDelete;
  return sqlite3_vfs_register(&zipVfs, 0);
}
