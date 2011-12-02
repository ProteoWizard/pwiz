/*
 * Copyright 2011 Steven Watanabe
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

# include "constants.h"
# include "object.h"

/*
 * constants.c - constant objects
 *
 * External functions:
 *
 *    constants_init() - initialize constants
 *    constants_done() - free constants
 *
 */

void constants_init( void )
{
    constant_empty              = object_new( "" );
    constant_builtin            = object_new( "(builtin)" );
    constant_other              = object_new( "[OTHER]" );
    constant_total              = object_new( "[TOTAL]" );
    constant_FILE_DIRSCAN       = object_new( "FILE_DIRSCAN" );
    constant_MAIN               = object_new( "MAIN" );
    constant_MAIN_MAKE          = object_new( "MAIN_MAKE" );
    constant_MAKE_MAKE0         = object_new( "MAKE_MAKE0" );
    constant_MAKE_MAKE1         = object_new( "MAKE_MAKE1" );
    constant_MAKE_MAKE0SORT     = object_new( "MAKE_MAKE0SORT" );
    constant_BINDMODULE         = object_new( "BINDMODULE" );
    constant_IMPORT_MODULE      = object_new( "IMPORT_MODULE" );
    constant_BUILTIN_GLOB_BACK  = object_new( "BUILTIN_GLOB_BACK" );
    constant_timestamp          = object_new( "timestamp" );
    constant_python             = object_new("__python__");
    constant_python_interface   = object_new( "python_interface" );
    constant_extra_pythonpath   = object_new( "EXTRA_PYTHONPATH" );
    constant_MAIN_PYTHON        = object_new( "MAIN_PYTHON" );
}

void constants_done( void )
{
    object_free( constant_empty );
    object_free( constant_builtin );
    object_free( constant_other );
    object_free( constant_total );
    object_free( constant_FILE_DIRSCAN );
    object_free( constant_MAIN );
    object_free( constant_MAIN_MAKE );
    object_free( constant_MAKE_MAKE0 );
    object_free( constant_MAKE_MAKE1 );
    object_free( constant_MAKE_MAKE0SORT );
    object_free( constant_BINDMODULE );
    object_free( constant_IMPORT_MODULE );
    object_free( constant_BUILTIN_GLOB_BACK );
    object_free( constant_timestamp );
    object_free( constant_python );
    object_free( constant_python_interface );
    object_free( constant_extra_pythonpath );
    object_free( constant_MAIN_PYTHON );
}

OBJECT * constant_empty;
OBJECT * constant_builtin;
OBJECT * constant_other;
OBJECT * constant_total;
OBJECT * constant_FILE_DIRSCAN;
OBJECT * constant_MAIN;
OBJECT * constant_MAIN_MAKE;
OBJECT * constant_MAKE_MAKE0;
OBJECT * constant_MAKE_MAKE1;
OBJECT * constant_MAKE_MAKE0SORT;
OBJECT * constant_BINDMODULE;
OBJECT * constant_IMPORT_MODULE;
OBJECT * constant_BUILTIN_GLOB_BACK;
OBJECT * constant_timestamp;
OBJECT * constant_python;
OBJECT * constant_python_interface;
OBJECT * constant_extra_pythonpath;
OBJECT * constant_MAIN_PYTHON;
