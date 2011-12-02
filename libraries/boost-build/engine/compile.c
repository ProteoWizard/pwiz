/*
 * Copyright 1993, 2000 Christopher Seiwald.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*  This file is ALSO:
 *  Copyright 2001-2004 David Abrahams.
 *  Distributed under the Boost Software License, Version 1.0.
 *  (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt)
 */

# include "jam.h"

# include "lists.h"
# include "parse.h"
# include "compile.h"
# include "variable.h"
# include "expand.h"
# include "rules.h"
# include "object.h"
# include "make.h"
# include "search.h"
# include "hdrmacro.h"
# include "hash.h"
# include "modules.h"
# include "strings.h"
# include "builtins.h"
# include "class.h"
# include "constants.h"

# include <assert.h>
# include <string.h>
# include <stdarg.h>

/*
 * compile.c - compile parsed jam statements
 *
 * External routines:
 *
 *  compile_append() - append list results of two statements
 *  compile_eval() - evaluate if to determine which leg to compile
 *  compile_foreach() - compile the "for x in y" statement
 *  compile_if() - compile 'if' rule
 *  compile_while() - compile 'while' rule
 *  compile_include() - support for 'include' - call include() on file
 *  compile_list() - expand and return a list
 *  compile_local() - declare (and set) local variables
 *  compile_null() - do nothing -- a stub for parsing
 *  compile_on() - run rule under influence of on-target variables
 *  compile_rule() - compile a single user defined rule
 *  compile_rules() - compile a chain of rules
 *  compile_set() - compile the "set variable" statement
 *  compile_setcomp() - support for `rule` - save parse tree
 *  compile_setexec() - support for `actions` - save execution string
 *  compile_settings() - compile the "on =" (set variable on exec) statement
 *  compile_switch() - compile 'switch' rule
 *
 * Internal routines:
 *
 *  debug_compile() - printf with indent to show rule expansion.
 *  evaluate_rule() - execute a rule invocation
 *
 *  builtin_depends() - DEPENDS/INCLUDES rule
 *  builtin_echo() - ECHO rule
 *  builtin_exit() - EXIT rule
 *  builtin_flags() - NOCARE, NOTFILE, TEMPORARY rule
 *
 * 02/03/94 (seiwald) - Changed trace output to read "setting" instead of
 *          the awkward sounding "settings".
 * 04/12/94 (seiwald) - Combined build_depends() with build_includes().
 * 04/12/94 (seiwald) - actionlist() now just appends a single action.
 * 04/13/94 (seiwald) - added shorthand L0 for null list pointer
 * 05/13/94 (seiwald) - include files are now bound as targets, and thus
 *          can make use of $(SEARCH)
 * 06/01/94 (seiwald) - new 'actions existing' does existing sources
 * 08/23/94 (seiwald) - Support for '+=' (append to variable)
 * 12/20/94 (seiwald) - NOTIME renamed NOTFILE.
 * 01/22/95 (seiwald) - Exit rule.
 * 02/02/95 (seiwald) - Always rule; LEAVES rule.
 * 02/14/95 (seiwald) - NoUpdate rule.
 * 09/11/00 (seiwald) - new evaluate_rule() for headers().
 * 09/11/00 (seiwald) - compile_xxx() now return LIST *.
 *          New compile_append() and compile_list() in
 *          support of building lists here, rather than
 *          in jamgram.yy.
 * 01/10/00 (seiwald) - built-ins split out to builtin.c.
 */

static void debug_compile( int which, const char * s, FRAME * frame );
int glob( const char * s, const char * c );
/* Internal functions from builtins.c */
void backtrace( FRAME * frame );
void backtrace_line( FRAME * frame );
void print_source_line( FRAME * frame );

struct frame * frame_before_python_call;

static OBJECT * module_scope;

void frame_init( FRAME* frame )
{
    frame->prev = 0;
    frame->prev_user = 0;
    lol_init(frame->args);
    frame->module = root_module();
    frame->rulename = "module scope";
    frame->file = 0;
    frame->line = -1;
}


void frame_free( FRAME* frame )
{
    lol_free( frame->args );
}


static void argument_error( const char * message, RULE * rule, FRAME * frame, LIST * arg )
{
    LOL * actual = frame->args;
    assert( rule->procedure != 0 );
    backtrace_line( frame->prev );
    printf( "*** argument error\n* rule %s ( ", frame->rulename );
    lol_print( rule->arguments->data );
    printf( " )\n* called with: ( " );
    lol_print( actual );
    printf( " )\n* %s %s\n", message, arg ? object_str ( arg->value ) : "" );
    function_location( rule->procedure, &frame->file, &frame->line );
    print_source_line( frame );
    printf( "see definition of rule '%s' being called\n", object_str( rule->name ) );
    backtrace( frame->prev );
    exit( 1 );
}


/* Define delimiters for type check elements in argument lists (and return type
 * specifications, eventually).
 */
# define TYPE_OPEN_DELIM '['
# define TYPE_CLOSE_DELIM ']'

/*
 * is_type_name() - true iff the given string represents a type check
 * specification.
 */

static int is_type_name( const char * s )
{
    return ( s[ 0 ] == TYPE_OPEN_DELIM ) &&
        ( s[ strlen( s ) - 1 ] == TYPE_CLOSE_DELIM );
}


/*
 * arg_modifier - if the next element of formal is a single character, return
 * that; return 0 otherwise. Used to extract "*+?" modifiers * from argument
 * lists.
 */

static char arg_modifier( LIST * formal )
{
    if ( formal->next )
    {
        const char * next = object_str( formal->next->value );
        if ( next && ( next[ 0 ] != 0 ) && ( next[ 1 ] == 0 ) )
            return next[ 0 ];
    }
    return 0;
}


/*
 * type_check() - checks that each element of values satisfies the requirements
 * of type_name.
 *
 *      caller   - the frame of the rule calling the rule whose arguments are
 *                 being checked
 *
 *      called   - the rule being called
 *
 *      arg_name - a list element containing the name of the argument being
 *                 checked
 */

static void type_check
(
    OBJECT  * type_name,
    LIST    * values,
    FRAME   * caller,
    RULE    * called,
    LIST    * arg_name
)
{
    static module_t * typecheck = 0;

    /* If nothing to check, bail now. */
    if ( !values || !type_name )
        return;

    if ( !typecheck )
    {
        OBJECT * str_typecheck = object_new( ".typecheck" );
        typecheck = bindmodule( str_typecheck );
        object_free( str_typecheck );
    }

    /* If the checking rule can not be found, also bail. */
    {
        RULE checker_, *checker = &checker_;

        checker->name = type_name;
        if ( !typecheck->rules || !hashcheck( typecheck->rules, (HASHDATA * *)&checker ) )
            return;
    }

    exit_module( caller->module );

    while ( values != 0 )
    {
        LIST *error;
        FRAME frame[1];
        frame_init( frame );
        frame->module = typecheck;
        frame->prev = caller;
        frame->prev_user = caller->module->user_module ? caller : caller->prev_user;

        enter_module( typecheck );
        /* Prepare the argument list */
        lol_add( frame->args, list_new( L0, object_copy( values->value ) ) );
        error = evaluate_rule( type_name, frame );

        exit_module( typecheck );

        if ( error )
            argument_error( object_str( error->value ), called, caller, arg_name );

        frame_free( frame );
        values = values->next;
    }

    enter_module( caller->module );
}

/*
 * collect_arguments() - local argument checking and collection
 */
static SETTINGS *
collect_arguments( RULE* rule, FRAME* frame )
{
    SETTINGS *locals = 0;

    LOL * all_actual = frame->args;
    LOL * all_formal = rule->arguments ? rule->arguments->data : 0;
    if ( all_formal ) /* Nothing to set; nothing to check */
    {
        int max = all_formal->count > all_actual->count
            ? all_formal->count
            : all_actual->count;

        int n;
        for ( n = 0; n < max ; ++n )
        {
            LIST *actual = lol_get( all_actual, n );
            OBJECT * type_name = 0;

            LIST *formal;
            for ( formal = lol_get( all_formal, n ); formal; formal = formal->next )
            {
                OBJECT * name = formal->value;

                if ( is_type_name( object_str( name ) ) )
                {
                    if ( type_name )
                        argument_error( "missing argument name before type name:", rule, frame, formal );

                    if ( !formal->next )
                        argument_error( "missing argument name after type name:", rule, frame, formal );

                    type_name = formal->value;
                }
                else
                {
                    LIST* value = 0;
                    char modifier;
                    LIST* arg_name = formal; /* hold the argument name for type checking */
                    int multiple = 0;

                    /* Stop now if a variable number of arguments are specified */
                    if ( object_str( name )[0] == '*' && object_str( name )[1] == 0 )
                        return locals;

                    modifier = arg_modifier( formal );

                    if ( !actual && modifier != '?' && modifier != '*' )
                        argument_error( "missing argument", rule, frame, formal );

                    switch ( modifier )
                    {
                    case '+':
                    case '*':
                        value = list_copy( 0, actual );
                        multiple = 1;
                        actual = 0;
                        /* skip an extra element for the modifier */
                        formal = formal->next;
                        break;
                    case '?':
                        /* skip an extra element for the modifier */
                        formal = formal->next;
                        /* fall through */
                    default:
                        if ( actual ) /* in case actual is missing */
                        {
                            value = list_new( 0, object_copy( actual->value ) );
                            actual = actual->next;
                        }
                    }

                    locals = addsettings(locals, VAR_SET, name, value);
                    locals->multiple = multiple;
                    type_check( type_name, value, frame, rule, arg_name );
                    type_name = 0;
                }
            }

            if ( actual )
            {
                argument_error( "extra argument", rule, frame, actual );
            }
        }
    }
    return locals;
}

RULE *
enter_rule( char *rulename, module_t *target_module );

#ifdef HAVE_PYTHON

static int python_instance_number = 0;


/* Given a Python object, return a string to use in Jam
   code instead of said object.
   If the object is string, use the string value
   If the object implemenets __jam_repr__ method, use that.
   Otherwise return 0. */
OBJECT *python_to_string(PyObject* value)
{
    if (PyString_Check(value))
    {
        return object_new(PyString_AsString(value));
    }
    else
    {
        /* See if this is an instance that defines special __jam_repr__
           method. */
        if (PyInstance_Check(value)
            && PyObject_HasAttrString(value, "__jam_repr__"))
        {
            PyObject* repr = PyObject_GetAttrString(value, "__jam_repr__");
            if (repr)
            {
                PyObject* arguments2 = PyTuple_New(0);
                PyObject* value2 = PyObject_Call(repr, arguments2, 0);
                Py_DECREF(repr);
                Py_DECREF(arguments2);
                if (PyString_Check(value2))
                {
                    return object_new(PyString_AsString(value2));
                }
                Py_DECREF(value2);
            }
        }
        return 0;
    }
}

static LIST*
call_python_function(RULE* r, FRAME* frame)
{
    LIST * result = 0;
    PyObject * arguments = 0;
    PyObject * kw = NULL;
    int i ;
    PyObject * py_result;
    FRAME * prev_frame_before_python_call;

    if (r->arguments)
    {
        SETTINGS * args;

        arguments = PyTuple_New(0);
        kw = PyDict_New();

        for (args = collect_arguments(r, frame); args; args = args->next)
        {
            PyObject *key = PyString_FromString(object_str(args->symbol));
            PyObject *value = 0;
            if (args->multiple)
                value = list_to_python(args->value);
            else {
                if (args->value)
                    value = PyString_FromString(object_str(args->value->value));
            }

            if (value)
                PyDict_SetItem(kw, key, value);
            Py_DECREF(key);
            Py_XDECREF(value);
        }
    }
    else
    {
        arguments = PyTuple_New( frame->args->count );
        for ( i = 0; i < frame->args->count; ++i )
        {
            PyObject * arg = PyList_New(0);
            LIST* l = lol_get( frame->args, i);
            
            for ( ; l; l = l->next )
            {
                PyObject * v = PyString_FromString(object_str(l->value));
                PyList_Append( arg, v );
                Py_DECREF(v);
            }
            /* Steals reference to 'arg' */
            PyTuple_SetItem( arguments, i, arg );
        }
    }

    prev_frame_before_python_call = frame_before_python_call;
    frame_before_python_call = frame;
    py_result = PyObject_Call( r->python_function, arguments, kw );
    frame_before_python_call = prev_frame_before_python_call;
    Py_DECREF(arguments);
    Py_XDECREF(kw);
    if ( py_result != NULL )
    {
        if ( PyList_Check( py_result ) )
        {
            int size = PyList_Size( py_result );
            int i;
            for ( i = 0; i < size; ++i )
            {
                PyObject * item = PyList_GetItem( py_result, i );
                OBJECT *s = python_to_string (item);
                if (!s) {
                    fprintf( stderr, "Non-string object returned by Python call.\n" );
                } else {
                    result = list_new (result, s);
                }
            }
        }
        else if ( py_result == Py_None )
        {
            result = L0;
        }
        else 
        {
            OBJECT *s = python_to_string(py_result);
            if (s)
                result = list_new(0, s);
            else 
                /* We have tried all we could.  Return empty list. There are
                   cases, e.g.  feature.feature function that should return
                   value for the benefit of Python code and which also can be
                   called by Jam code, where no sensible value can be
                   returned. We cannot even emit a warning, since there will
                   be a pile of them.  */                
                result = L0;                    
        }

        Py_DECREF( py_result );
    }
    else
    {
        PyErr_Print();
        fprintf(stderr,"Call failed\n");
    }

    return result;
}


module_t * python_module()
{
    static module_t * python = 0;
    if ( !python )
        python = bindmodule(constant_python);
    return python;
}

#endif


/*
 * evaluate_rule() - execute a rule invocation.
 */

LIST *
evaluate_rule(
    OBJECT * rulename,
    FRAME  * frame )
{
    LIST          * result = L0;
    RULE          * rule;
    profile_frame   prof[1];
    module_t      * prev_module = frame->module;

    rule = bindrule( rulename, frame->module );
    rulename = rule->name;

#ifdef HAVE_PYTHON
    if ( rule->python_function )
    {
        /* The below messing with modules is due to the way modules are
         * implemented in Jam. Suppose we are in module M1 now. The global
         * variable map actually holds 'M1' variables, and M1->variables hold
         * global variables.
         *
         * If we call Python right away, Python calls back Jam and then Jam
         * does 'module M1 { }' then Jam will try to swap the current global
         * variables with M1->variables. The result will be that global
         * variables map will hold global variables, and any variable settings
         * we do will go to the global module, not M1.
         *
         * By restoring basic state, where the global variable map holds global
         * variable, we make sure any future 'module M1' entry will work OK.
         */

        LIST * result;
        module_t * m = python_module();

        frame->module = m;

        exit_module( prev_module );
        enter_module( m );

        result = call_python_function( rule, frame );

        exit_module( m );
        enter_module ( prev_module );

        return result;
    }
#endif

    if ( DEBUG_COMPILE )
    {
        /* Try hard to indicate in which module the rule is going to execute. */
        if ( rule->module != frame->module
             && rule->procedure != 0 && !object_equal( rulename, function_rulename( rule->procedure ) ) )
        {
            char buf[256] = "";
            if ( rule->module->name )
            {
                strncat( buf, object_str( rule->module->name ), sizeof( buf ) - 1 );
                strncat( buf, ".", sizeof( buf ) - 1 );
            }
            strncat( buf, object_str( rule->name ), sizeof( buf ) - 1 );
            debug_compile( 1, buf, frame );
        }
        else
        {
            debug_compile( 1, object_str( rulename ), frame );
        }

        lol_print( frame->args );
        printf( "\n" );
    }

    if ( rule->procedure && rule->module != prev_module )
    {
        /* Propagate current module to nested rule invocations. */
        frame->module = rule->module;

        /* Swap variables. */
        exit_module( prev_module );
        enter_module( rule->module );
    }

    /* Record current rule name in frame. */
    if ( rule->procedure )
    {
        frame->rulename = object_str( rulename );
        /* And enter record profile info. */
        if ( DEBUG_PROFILE )
            profile_enter( function_rulename( rule->procedure ), prof );
    }

    /* Check traditional targets $(<) and sources $(>). */
    if ( !rule->actions && !rule->procedure )
    {
        backtrace_line( frame->prev );
        if ( frame->module->name )
        {
            printf( "rule %s unknown in module %s\n", object_str( rule->name ), object_str( frame->module->name ) );
        }
        else
        {
            printf( "rule %s unknown in module \n", object_str( rule->name ) );
        }
        backtrace( frame->prev );
        exit( 1 );
    }

    /* If this rule will be executed for updating the targets then construct the
     * action for make().
     */
    if ( rule->actions )
    {
        TARGETS * t;
        ACTION  * action;

        /* The action is associated with this instance of this rule. */
        action = (ACTION *)BJAM_MALLOC( sizeof( ACTION ) );
        memset( (char *)action, '\0', sizeof( *action ) );

        action->rule = rule;
        action->targets = targetlist( (TARGETS *)0, lol_get( frame->args, 0 ) );
        action->sources = targetlist( (TARGETS *)0, lol_get( frame->args, 1 ) );
        action->refs = 1;

        /* If we have a group of targets all being built using the same action
         * then we must not allow any of them to be used as sources unless they
         * had all already been built in the first place or their joined action
         * has had a chance to finish its work and build all of them anew.
         *
         * Without this it might be possible, in case of a multi-process build,
         * for their action, triggered by buiding one of the targets, to still
         * be running when another target in the group reports as done in order
         * to avoid triggering the same action again and gets used prematurely.
         *
         * As a quick-fix to achieve this effect we make all the targets list
         * each other as 'included targets'. More precisely, we mark the first
         * listed target as including all the other targets in the list and vice
         * versa. This makes anyone depending on any of those targets implicitly
         * depend on all of them, thus making sure none of those targets can be
         * used as sources until all of them have been built. Note that direct
         * dependencies could not have been used due to the 'circular
         * dependency' issue.
         *
         * TODO: Although the current implementation solves the problem of one
         * of the targets getting used before its action completes its work it
         * also forces the action to run whenever any of the targets in the
         * group is not up to date even though some of them might not actually
         * be used by the targets being built. We should see how we can
         * correctly recognize such cases and use that to avoid running the
         * action if possible and not rebuild targets not actually depending on
         * targets that are not up to date.
         *
         * TODO: Using the 'include' feature might have side-effects due to
         * interaction with the actual 'inclusion scanning' system. This should
         * be checked.
         */
        if ( action->targets )
        {
            TARGET * t0 = action->targets->target;
            for ( t = action->targets->next; t; t = t->next )
            {
                target_include( t->target, t0 );
                target_include( t0, t->target );
            }
        }

        /* Append this action to the actions of each target. */
        for ( t = action->targets; t; t = t->next )
            t->target->actions = actionlist( t->target->actions, action );

        action_free( action );
    }

    /* Now recursively compile any parse tree associated with this rule.
     * function_refer()/function_free() call pair added to ensure rule not freed
     * during use.
     */
    if ( rule->procedure )
    {
        SETTINGS * local_args = collect_arguments( rule, frame );
        FUNCTION * function = rule->procedure;

        function_refer( function );

        pushsettings( local_args );
        result = function_run( function, frame, stack_global() );
        popsettings( local_args );
        freesettings( local_args );

        function_free( function );
    }

    if ( frame->module != prev_module )
    {
        exit_module( frame->module );
        enter_module( prev_module );
    }

    if ( DEBUG_PROFILE && rule->procedure )
        profile_exit( prof );

    if ( DEBUG_COMPILE )
        debug_compile( -1, 0, frame);

    return result;
}


/*
 * Call the given rule with the specified parameters. The parameters should be
 * of type LIST* and end with a NULL pointer. This differs from 'evaluate_rule'
 * in that frame for the called rule is prepared inside 'call_rule'.
 *
 * This function is useful when a builtin rule (in C) wants to call another rule
 * which might be implemented in Jam.
 */

LIST * call_rule( OBJECT * rulename, FRAME * caller_frame, ... )
{
    va_list va;
    LIST * result;

    FRAME       inner[1];
    frame_init( inner );
    inner->prev = caller_frame;
    inner->prev_user = caller_frame->module->user_module ?
        caller_frame : caller_frame->prev_user;
    inner->module = caller_frame->module;

    va_start( va, caller_frame );
    for ( ; ; )
    {
        LIST * l = va_arg( va, LIST* );
        if ( !l )
            break;
        lol_add( inner->args, l );
    }
    va_end( va );

    result = evaluate_rule( rulename, inner );

    frame_free( inner );

    return result;
}



/*
 * debug_compile() - printf with indent to show rule expansion.
 */

static void debug_compile( int which, const char * s, FRAME * frame )
{
    static int level = 0;
    static char indent[36] = ">>>>|>>>>|>>>>|>>>>|>>>>|>>>>|>>>>|";

    if ( which >= 0 )
    {
        int i;

        print_source_line( frame );

        i = ( level + 1 ) * 2;
        while ( i > 35 )
        {
            fputs( indent, stdout );
            i -= 35;
        }

        printf( "%*.*s ", i, i, indent );
    }

    if ( s )
        printf( "%s ", s );

    level += which;
}
