/*
 * Copyright 1993, 2000 Christopher Seiwald.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*
 * variable.h - handle jam multi-element variables
 */

#ifndef VARIABLE_SW20111119_H
#define VARIABLE_SW20111119_H

struct hash;

void    var_defines( char* const *e, int preprocess );
int     var_string( const char * in, char * out, int outsize, LOL * lol );
LIST *  var_get( OBJECT * symbol );
void    var_set( OBJECT * symbol, LIST * value, int flag );
LIST *  var_swap( OBJECT * symbol, LIST *value );
void    var_done();
void    var_hash_swap( struct hash * * );

/** Expands the "in" expression directly into the "out" file.
    The file can be one of: a path, STDOUT, or STDERR to send
    the output to a file overwriting previous content, to
    the console, or to the error output respectively.
*/
void var_string_to_file( const char * in, int insize, const char * out, LOL * lol );

/*
 * Defines for var_set().
 */

# define VAR_SET    0   /* override previous value */
# define VAR_APPEND 1   /* append to previous value */
# define VAR_DEFAULT    2   /* set only if no previous value */

#endif
