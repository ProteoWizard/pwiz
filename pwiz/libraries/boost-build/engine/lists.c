/*
 * Copyright 1993, 1995 Christopher Seiwald.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

# include "jam.h"
# include "object.h"
# include "lists.h"

/*
 * lists.c - maintain lists of objects
 *
 * This implementation essentially uses a singly linked list, but
 * guarantees that the head element of every list has a valid pointer
 * to the tail of the list, so the new elements can efficiently and
 * properly be appended to the end of a list.
 *
 * To avoid massive allocation, list_free() just tacks the whole freed
 * chain onto freelist and list_new() looks on freelist first for an
 * available list struct.  list_free() does not free the strings in the
 * chain: it lazily lets list_new() do so.
 *
 * 08/23/94 (seiwald) - new list_append()
 * 09/07/00 (seiwald) - documented lol_*() functions
 */

static LIST *freelist = 0;  /* junkpile for list_free() */

/*
 * list_append() - append a list onto another one, returning total
 */

LIST * list_append( LIST * l, LIST * nl )
{
    if ( !nl )
    {
        /* Just return l */
    }
    else if ( !l )
    {
        l = nl;
    }
    else
    {
        /* Graft two non-empty lists. */
        l->tail->next = nl;
        l->tail = nl->tail;
    }

    return l;
}

/*
 * list_new() - tack a string onto the end of a list of strings
 */

LIST * list_new( LIST * head, OBJECT * value )
{
    LIST * l;

    if ( DEBUG_LISTS )
        printf( "list > %s <\n", object_str( value ) );

    /* Get list struct from freelist, if one available.  */
    /* Otherwise allocate. */
    /* If from freelist, must free string first */

    if ( freelist )
    {
        l = freelist;
        object_free( l->value );
        freelist = freelist->next;
    }
    else
    {
        l = (LIST *)BJAM_MALLOC( sizeof( LIST ) );
    }

    /* If first on chain, head points here. */
    /* If adding to chain, tack us on. */
    /* Tail must point to this new, last element. */

    if ( !head ) head = l;
    else head->tail->next = l;
    head->tail = l;
    l->next = 0;

    l->value = value;

    return head;
}


/*
 * list_copy() - copy a whole list of strings (nl) onto end of another (l).
 */

LIST * list_copy( LIST * l, LIST * nl )
{
    for ( ; nl; nl = list_next( nl ) )
        l = list_new( l, object_copy( nl->value ) );
    return l;
}


/*
 * list_sublist() - copy a subset of a list of strings.
 */

LIST * list_sublist( LIST * l, int start, int count )
{
    LIST * nl = 0;
    for ( ; l && start--; l = list_next( l ) );
    for ( ; l && count--; l = list_next( l ) )
        nl = list_new( nl, object_copy( l->value ) );
    return nl;
}


static int str_ptr_compare( void const * va, void const * vb )
{
    OBJECT * a = *( (OBJECT * *)va );
    OBJECT * b = *( (OBJECT * *)vb );
    return strcmp(object_str(a), object_str(b));
}


LIST * list_sort( LIST * l )
{
    int len;
    int ii;
    OBJECT * * objects;
    LIST * listp;
    LIST * result = 0;

    if ( !l )
        return L0;

    len = list_length( l );
    objects = (OBJECT * *)BJAM_MALLOC( len * sizeof(OBJECT*) );

    listp = l;
    for ( ii = 0; ii < len; ++ii )
    {
        objects[ ii ] = listp->value;
        listp = listp->next;
    }

    qsort( objects, len, sizeof( OBJECT * ), str_ptr_compare );

    for ( ii = 0; ii < len; ++ii )
        result = list_append( result, list_new( 0, object_copy( objects[ ii ] ) ) );

    BJAM_FREE( objects );

    return result;
}


/*
 * list_free() - free a list of strings
 */

void list_free( LIST * head )
{
#ifdef BJAM_NO_MEM_CACHE
    LIST *l, *tmp;
    for( l = head; l;  )
    {
        object_free( l->value );
        l->value = 0;
        tmp = l;
        l = l->next;
        BJAM_FREE( tmp );
    }
#else
    /* Just tack onto freelist. */
    if ( head )
    {
        head->tail->next = freelist;
        freelist = head;
    }
#endif
}


/*
 * list_pop_front() - remove the front element from a list of strings
 */

LIST * list_pop_front( LIST * l )
{
    LIST * result = l->next;
    if ( result )
    {
        result->tail = l->tail;
        l->next = L0;
        l->tail = l;
    }
    list_free( l );
    return result;
}

LIST *  list_reverse( LIST * l )
{
    LIST * result = L0;
    for ( ; l; l = l->next )
    {
        result = list_append( list_new(L0, object_copy( l->value ) ), result );
    }
    return result;
}

int list_cmp( LIST * t, LIST * s )
{
    int status = 0;

    while ( !status && ( t || s ) )
    {
        const char *st = t ? object_str( t->value ) : "";
        const char *ss = s ? object_str( s->value ) : "";

        status = strcmp( st, ss );

        t = t ? list_next( t ) : t;
        s = s ? list_next( s ) : s;
    }

    return status;
}

int list_is_sublist( LIST * sub, LIST * l )
{
    for ( ; sub; sub = sub->next )
    {
        if ( !list_in( l, sub->value ) )
            return 0;
    }
    return 1;
}

/*
 * list_print() - print a list of strings to stdout
 */

void list_print( LIST * l )
{
    LIST * p = 0;
    for ( ; l; p = l, l = list_next( l ) )
        if ( p )
            printf( "%s ", object_str( p->value ) );
    if ( p )
        printf( "%s", object_str( p->value ) );
}


/*
 * list_length() - return the number of items in the list
 */

int list_length( LIST * l )
{
    int n = 0;
    for ( ; l; l = list_next( l ), ++n );
    return n;
}


int list_in( LIST * l, OBJECT * value )
{
    for ( ; l; l = l->next )
        if ( object_equal( l->value, value ) )
            return 1;
    return 0;
}


LIST * list_unique( LIST * sorted_list )
{
    LIST * result = 0;
    LIST * last_added = 0;

    for ( ; sorted_list; sorted_list = sorted_list->next )
    {
        if ( !last_added || !object_equal( sorted_list->value, last_added->value ) )
        {
            result = list_new( result, object_copy( sorted_list->value ) );
            last_added = sorted_list;
        }
    }
    return result;
}

void list_done()
{
    LIST *l, *tmp;
    for( l = freelist; l;  )
    {
        object_free( l->value );
        l->value = 0;
        tmp = l;
        l = l->next;
        BJAM_FREE( tmp );
    }
}


/*
 * lol_init() - initialize a LOL (list of lists).
 */

void lol_init( LOL * lol )
{
    lol->count = 0;
}


/*
 * lol_add() - append a LIST onto an LOL.
 */

void lol_add( LOL * lol, LIST * l )
{
    if ( lol->count < LOL_MAX )
        lol->list[ lol->count++ ] = l;
}


/*
 * lol_free() - free the LOL and its LISTs.
 */

void lol_free( LOL * lol )
{
    int i;
    for ( i = 0; i < lol->count; ++i )
        list_free( lol->list[ i ] );
    lol->count = 0;
}


/*
 * lol_get() - return one of the LISTs in the LOL.
 */

LIST * lol_get( LOL * lol, int i )
{
    return i < lol->count ? lol->list[ i ] : 0;
}


/*
 * lol_print() - debug print LISTS separated by ":".
 */

void lol_print( LOL * lol )
{
    int i;

    for ( i = 0; i < lol->count; ++i )
    {
        if ( i )
            printf( " : " );
        list_print( lol->list[ i ] );
    }
}

#ifdef HAVE_PYTHON

PyObject *list_to_python(LIST *l)
{
    PyObject *result = PyList_New(0);

    for (; l; l = l->next)
    {
        PyObject* s = PyString_FromString(object_str(l->value));
        PyList_Append(result, s);
        Py_DECREF(s);
    }

    return result;
}

LIST *list_from_python(PyObject *l)
{
    LIST * result = 0;

    Py_ssize_t i, n;
    n = PySequence_Size(l);
    for (i = 0; i < n; ++i)
    {
        PyObject *v = PySequence_GetItem(l, i);        
        result = list_new (result, object_new (PyString_AsString(v)));
        Py_DECREF(v);
    }

    return result;
}

#endif
