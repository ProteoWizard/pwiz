/*
 * Copyright 1993-2002 Christopher Seiwald and Perforce Software, Inc.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*  This file is ALSO:
 *  Copyright 2001-2004 David Abrahams.
 *  Distributed under the Boost Software License, Version 1.0.
 *  (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt)
 */

#include "jam.h"
#include "lists.h"
#include "search.h"
#include "timestamp.h"
#include "pathsys.h"
#include "variable.h"
#include "object.h"
#include "compile.h"
#include "strings.h"
#include "hash.h"
#include "filesys.h"
#include <string.h>


typedef struct _binding
{
    OBJECT * binding;
    OBJECT * target;
} BINDING;

static struct hash *explicit_bindings = 0;


void call_bind_rule
(
    OBJECT * target_,
    OBJECT * boundname_
)
{
    OBJECT * varname = object_new( "BINDRULE" );
    LIST * bind_rule = var_get( varname );
    object_free( varname );
    if ( bind_rule )
    {
        OBJECT * target = object_copy( target_ );
        OBJECT * boundname = object_copy( boundname_ );
        if ( boundname && target )
        {
            /* Prepare the argument list. */
            FRAME frame[1];
            frame_init( frame );

            /* First argument is the target name. */
            lol_add( frame->args, list_new( L0, target ) );

            lol_add( frame->args, list_new( L0, boundname ) );
            if ( lol_get( frame->args, 1 ) )
                list_free( evaluate_rule( bind_rule->value, frame ) );

            /* Clean up */
            frame_free( frame );
        }
        else
        {
            if ( boundname )
                object_free( boundname );
            if ( target )
                object_free( target );
        }
    }
}

/*
 * search.c - find a target along $(SEARCH) or $(LOCATE)
 * First, check if LOCATE is set. If so, use it to determine
 * the location of target and return, regardless of whether anything
 * exists on that location.
 *
 * Second, examine all directories in SEARCH. If there's file already
 * or there's another target with the same name which was placed
 * to this location via LOCATE setting, stop and return the location.
 * In case of previous target, return it's name via the third argument.
 *
 * This bevahiour allow to handle dependency on generated files. If
 * caller does not expect that target is generated, 0 can be passed as
 * the third argument.
 */

OBJECT *
search(
    OBJECT * target,
    time_t *time,
    OBJECT * * another_target,
    int file
)
{
    PATHNAME f[1];
    LIST   * varlist;
    string   buf[1];
    int      found = 0;
    /* Will be set to 1 if target location is specified via LOCATE. */
    int      explicitly_located = 0;
    OBJECT * boundname = 0;
    OBJECT * varname;

    if ( another_target )
        *another_target = 0;

    if (! explicit_bindings )
        explicit_bindings = hashinit( sizeof(BINDING),
                                     "explicitly specified locations");

    string_new( buf );
    /* Parse the filename */

    path_parse( object_str( target ), f );

    f->f_grist.ptr = 0;
    f->f_grist.len = 0;

    varname = object_new( "LOCATE" );
    varlist = var_get( varname );
    object_free( varname );
    if ( varlist )
    {
        OBJECT * key;
        f->f_root.ptr = object_str( varlist->value );
        f->f_root.len = strlen( object_str( varlist->value ) );

        path_build( f, buf, 1 );

        if ( DEBUG_SEARCH )
            printf( "locate %s: %s\n", object_str( target ), buf->value );

        explicitly_located = 1;

        key = object_new( buf->value );
        timestamp( key, time );
        object_free( key );
        found = 1;
    }
    else if ( ( varname = object_new( "SEARCH" ),
                varlist = var_get( varname ),
                object_free( varname ),
                varlist ) )
    {
        while ( varlist )
        {
            BINDING b, *ba = &b;
            file_info_t *ff;
            OBJECT * key;

            f->f_root.ptr = object_str( varlist->value );
            f->f_root.len = strlen( object_str( varlist->value ) );

            string_truncate( buf, 0 );
            path_build( f, buf, 1 );

            if ( DEBUG_SEARCH )
                printf( "search %s: %s\n", object_str( target ), buf->value );

            key = object_new( buf->value );
            ff = file_query( key );
            timestamp( key, time );

            b.binding = key;

            if ( hashcheck( explicit_bindings, (HASHDATA**)&ba ) )
            {
                if ( DEBUG_SEARCH )
                    printf(" search %s: found explicitly located target %s\n",
                           object_str( target ), object_str( ba->target ) );
                if ( another_target )
                    *another_target = ba->target;
                found = 1;
                object_free( key );
                break;
            }
            else if ( ff && ff->time )
            {
                if ( !file || ff->is_file )
                {
                    found = 1;
                    object_free( key );
                    break;
                }
            }
            object_free( key );

            varlist = list_next( varlist );
        }
    }

    if ( !found )
    {
        /* Look for the obvious */
        /* This is a questionable move.  Should we look in the */
        /* obvious place if SEARCH is set? */
        OBJECT * key;

        f->f_root.ptr = 0;
        f->f_root.len = 0;

        string_truncate( buf, 0 );
        path_build( f, buf, 1 );

        if ( DEBUG_SEARCH )
            printf( "search %s: %s\n", object_str( target ), buf->value );

        key = object_new( buf->value );
        timestamp( key, time );
        object_free( key );
    }

    boundname = object_new( buf->value );
    string_free( buf );

    if ( explicitly_located )
    {
        BINDING b;
        BINDING * ba = &b;
        b.binding = boundname;
        b.target = target;
        /* CONSIDER: we probably should issue a warning is another file
           is explicitly bound to the same location. This might break
           compatibility, though. */
        if ( hashenter( explicit_bindings, (HASHDATA * *)&ba ) )
        {
            ba->binding = object_copy( boundname );
        }
    }

    /* prepare a call to BINDRULE if the variable is set */
    call_bind_rule( target, boundname );

    return boundname;
}


static void free_binding( void * xbinding, void * data )
{
    BINDING * binding = (BINDING *)xbinding;
    object_free( binding->binding );
}

void search_done( void )
{
    if ( explicit_bindings )
    {
        hashenumerate( explicit_bindings, free_binding, (void *)0 );
        hashdone( explicit_bindings );
    }
}
