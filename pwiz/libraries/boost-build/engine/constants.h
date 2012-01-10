/*
 * Copyright 2011 Steven Watanabe
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*
 * constants.h - constant objects
 */

#ifndef BOOST_JAM_CONSTANTS_H
#define BOOST_JAM_CONSTANTS_H

#include "object.h"

void constants_init( void );
void constants_done( void );

extern OBJECT * constant_empty;             /* "" */
extern OBJECT * constant_builtin;           /* "(builtin)" */
extern OBJECT * constant_other;             /* "[OTHER]" */
extern OBJECT * constant_total;             /* "[TOTAL]" */
extern OBJECT * constant_FILE_DIRSCAN;      /* "FILE_DIRSCAN" */
extern OBJECT * constant_MAIN;              /* "MAIN" */
extern OBJECT * constant_MAIN_MAKE;         /* "MAIN_MAKE" */
extern OBJECT * constant_MAKE_MAKE0;        /* "MAKE_MAKE0" */
extern OBJECT * constant_MAKE_MAKE1;        /* "MAKE_MAKE1" */
extern OBJECT * constant_MAKE_MAKE0SORT;    /* "MAKE_MAKE0SORT" */
extern OBJECT * constant_BINDMODULE;        /* "BINDMODULE" */
extern OBJECT * constant_IMPORT_MODULE;     /* "IMPORT_MODULE" */
extern OBJECT * constant_BUILTIN_GLOB_BACK; /* "BUILTIN_GLOB_BACK" */
extern OBJECT * constant_timestamp;         /* "timestamp" */
extern OBJECT * constant_python;            /* "__python__" */
extern OBJECT * constant_python_interface;  /* "python_interface" */
extern OBJECT * constant_extra_pythonpath;  /* "EXTRA_PYTHONPATH" */
extern OBJECT * constant_MAIN_PYTHON;       /* "MAIN_PYTHON" */

#endif
