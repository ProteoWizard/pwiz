/*
 * Copyright 2011 Steven Watanabe
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*
 * object.h - object manipulation routines
 */

#ifndef BOOST_JAM_OBJECT_H
#define BOOST_JAM_OBJECT_H

typedef struct _object OBJECT;

OBJECT *     object_new   ( const char * );
OBJECT *     object_copy  ( OBJECT *     );
void         object_free  ( OBJECT *     );
const char * object_str   ( OBJECT * );
void         object_done  ( void );
int          object_equal ( OBJECT *, OBJECT * );
unsigned int object_hash  ( OBJECT * );

#endif
