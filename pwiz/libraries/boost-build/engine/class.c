/* Copyright Vladimir Prus 2003. Distributed under the Boost */
/* Software License, Version 1.0. (See accompanying */
/* file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt) */

#include "class.h"
#include "strings.h"
#include "variable.h"
#include "frames.h"
#include "rules.h"
#include "object.h"

#include "hash.h"


static struct hash * classes = 0;


static void check_defined( LIST * class_names )
{
    for ( ; class_names; class_names = class_names->next )
    {
        OBJECT * * p = &class_names->value;
        if ( !hashcheck( classes, (HASHDATA * *)&p ) )
        {
            printf( "Class %s is not defined\n", object_str( class_names->value ) );
            abort();
        }
    }
}


static OBJECT * class_module_name( OBJECT * declared_name )
{
    string name[ 1 ];
    OBJECT * result;

    string_new( name );
    string_append( name, "class@" );
    string_append( name, object_str( declared_name ) );

    result = object_new( name->value );
    string_free( name );

    return result;
}


struct import_base_data
{
    OBJECT   * base_name;
    module_t * base_module;
    module_t * class_module;
};


static void import_base_rule( void * r_, void * d_ )
{
    RULE * r = (RULE *)r_;
    RULE * ir1;
    RULE * ir2;
    struct import_base_data * d = (struct import_base_data *)d_;
    string qualified_name[ 1 ];
    OBJECT * qname;

    string_new      ( qualified_name               );
    string_append   ( qualified_name, object_str( d->base_name ) );
    string_push_back( qualified_name, '.'          );
    string_append   ( qualified_name, object_str( r->name ) );

    qname = object_new( qualified_name->value );

    ir1 = import_rule( r, d->class_module, r->name );
    ir2 = import_rule( r, d->class_module, qname );

    object_free( qname );

    /* Copy 'exported' flag. */
    ir1->exported = ir2->exported = r->exported;

    /* If we are importing a class method, localize it. */
    if ( ( r->module == d->base_module ) || ( r->module->class_module &&
        ( r->module->class_module == d->base_module ) ) )
        ir1->module = ir2->module = d->class_module;

    string_free( qualified_name );
}


/*
 * For each exported rule 'n', declared in class module for base, imports that
 * rule in 'class' as 'n' and as 'base.n'. Imported rules are localized and
 * marked as exported.
 */

static void import_base_rules( module_t * class_, OBJECT * base )
{
    OBJECT * module_name = class_module_name( base );
    module_t * base_module = bindmodule( module_name );
    LIST * imported;
    struct import_base_data d;
    d.base_name = base;
    d.base_module = base_module;
    d.class_module = class_;
    object_free( module_name );

    if ( base_module->rules )
        hashenumerate( base_module->rules, import_base_rule, &d );

    imported = imported_modules( base_module );
    import_module( imported, class_ );
    list_free( imported );
}


OBJECT * make_class_module( LIST * xname, LIST * bases, FRAME * frame )
{
    OBJECT     * name = class_module_name( xname->value );
    OBJECT   * * pp = &xname->value;
    module_t   * class_module = 0;
    module_t   * outer_module = frame->module;
    OBJECT     * name_ = object_new( "__name__" );
    OBJECT     * bases_ = object_new( "__bases__" );

    if ( !classes )
        classes = hashinit( sizeof( OBJECT * ), "classes" );

    if ( hashenter( classes, (HASHDATA * *)&pp ) )
    {
        *pp = object_copy( xname->value );
    }
    else
    {
        printf( "Class %s already defined\n", object_str( xname->value ) );
        abort();
    }
    check_defined( bases );

    class_module = bindmodule( name );

    exit_module( outer_module );
    enter_module( class_module );

    var_set( name_, xname, VAR_SET );
    var_set( bases_, bases, VAR_SET );

    exit_module( class_module );
    enter_module( outer_module );

    for ( ; bases; bases = bases->next )
        import_base_rules( class_module, bases->value );

    object_free( bases_ );
    object_free( name_ );

    return name;
}

static void free_class( void * xclass, void * data )
{
    object_free( *(OBJECT * *)xclass );
}

void class_done( void )
{
    if( classes )
    {
        hashenumerate( classes, free_class, (void *)0 );
        hashdone( classes );
        classes = 0;
    }
}
