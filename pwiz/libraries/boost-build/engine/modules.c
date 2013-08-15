/*
 *  Copyright 2001-2004 David Abrahams.
 *  Distributed under the Boost Software License, Version 1.0.
 *  (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt)
 */
#include "jam.h"

#include "modules.h"
#include "string.h"
#include "hash.h"
#include "object.h"
#include "lists.h"
#include "parse.h"
#include "rules.h"
#include "variable.h"
#include "strings.h"
#include "native.h"
#include <assert.h>

static struct hash * module_hash = 0;
static module_t root;

module_t * bindmodule( OBJECT * name )
{

    if ( !name )
    {
        return &root;
    }
    else
    {
        PROFILE_ENTER( BINDMODULE );

        module_t m_;
        module_t * m = &m_;

        if ( !module_hash )
            module_hash = hashinit( sizeof( module_t ), "modules" );

        m->name = name;

        if ( hashenter( module_hash, (HASHDATA * *)&m ) )
        {
            m->name = object_copy( name );
            m->variables = 0;
            m->rules = 0;
            m->imported_modules = 0;
            m->class_module = 0;
            m->native_rules = 0;
            m->user_module = 0;
        }

        PROFILE_EXIT( BINDMODULE );

        return m;
    }
}

/*
 * demand_rules() - Get the module's "rules" hash on demand.
 */
struct hash * demand_rules( module_t * m )
{
    if ( !m->rules )
        m->rules = hashinit( sizeof( RULE ), "rules" );
    return m->rules;
}


/*
 * delete_module() - wipe out the module's rules and variables.
 */

static void delete_rule_( void * xrule, void * data )
{
    rule_free( (RULE *)xrule );
}


static void delete_native_rule( void * xrule, void * data )
{
    native_rule_t * rule = (native_rule_t *)xrule;
    if ( rule->arguments )
        args_free( rule->arguments );
    object_free( rule->name );
    if ( rule->procedure )
        function_free( rule->procedure );
}


static void delete_imported_modules( void * xmodule_name, void * data )
{
    object_free( *(OBJECT * *)xmodule_name );
}


void delete_module( module_t * m )
{
    /* Clear out all the rules. */
    if ( m->rules )
    {
        hashenumerate( m->rules, delete_rule_, (void *)0 );
        hashdone( m->rules );
        m->rules = 0;
    }

    if ( m->native_rules )
    {
        hashenumerate( m->native_rules, delete_native_rule, (void *)0 );
        hashdone( m->native_rules );
        m->native_rules = 0;
    }

    if ( m->variables )
    {
        var_hash_swap( &m->variables );
        var_done();
        var_hash_swap( &m->variables );
        m->variables = 0;
    }

    if ( m->imported_modules )
    {
        hashenumerate( m->imported_modules, delete_imported_modules, (void *)0 );
        hashdone( m->imported_modules );
        m->imported_modules = 0;
    }
}


static void delete_module_( void * xmodule, void * data )
{
    module_t *m = (module_t *)xmodule;

    delete_module( m );
    object_free( m->name );
}

void modules_done()
{
    hashenumerate( module_hash, delete_module_, (void *)0 ); 
    hashdone( module_hash );
    module_hash = 0;
    delete_module( &root );
}

module_t * root_module()
{
    return &root;
}

void enter_module( module_t * m )
{
    var_hash_swap( &m->variables );
}


void exit_module( module_t * m )
{
    var_hash_swap( &m->variables );
}


void import_module( LIST * module_names, module_t * target_module )
{
    PROFILE_ENTER( IMPORT_MODULE );

    struct hash * h;

    if ( !target_module->imported_modules )
        target_module->imported_modules = hashinit( sizeof( char * ), "imported" );
    h = target_module->imported_modules;

    for ( ; module_names; module_names = module_names->next )
    {
        OBJECT * s = module_names->value;
        OBJECT * * ss = &s;
        if( hashenter( h, (HASHDATA * *)&ss ) )
        {
            *ss = object_copy( s );
        }
    }

    PROFILE_EXIT( IMPORT_MODULE );
}


static void add_module_name( void * r_, void * result_ )
{
    OBJECT * * r = (OBJECT * *)r_;
    LIST * * result = (LIST * *)result_;

    *result = list_new( *result, object_copy( *r ) );
}


LIST * imported_modules( module_t * module )
{
    LIST * result = L0;
    if ( module->imported_modules )
        hashenumerate( module->imported_modules, add_module_name, &result );
    return result;
}
