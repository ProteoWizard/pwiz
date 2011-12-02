/*
 * Copyright 1993, 1995 Christopher Seiwald.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

# include "jam.h"
# include "hash.h"
# include "compile.h"
# include "object.h"
# include <assert.h>

/*
 * hash.c - simple in-memory hashing routines
 *
 * External routines:
 *
 *     hashinit() - initialize a hash table, returning a handle
 *     hashitem() - find a record in the table, and optionally enter a new one
 *     hashdone() - free a hash table, given its handle
 *
 * Internal routines:
 *
 *     hashrehash() - resize and rebuild hp->tab, the hash table
 *
 * 4/29/93 - ensure ITEM's are aligned
 */

/* */
#define HASH_DEBUG_PROFILE 1
/* */

/* Header attached to all data items entered into a hash table. */

struct hashhdr
{
    struct item  * next;
};

/* This structure overlays the one handed to hashenter(). Its actual size is
 * given to hashinit().
 */

struct hashdata
{
    OBJECT * key;
    /* rest of user data */
};

typedef struct item
{
    struct hashhdr  hdr;
    struct hashdata data;
} ITEM ;

# define MAX_LISTS 32

struct hash
{
    /*
     * the hash table, just an array of item pointers
     */
    struct {
        int nel;
        ITEM **base;
    } tab;

    int bloat;  /* tab.nel / items.nel */
    int inel;   /* initial number of elements */

    /*
     * the array of records, maintained by these routines
     * essentially a microallocator
     */
    struct {
        int more;   /* how many more ITEMs fit in lists[ list ] */
        ITEM *free; /* free list of items */
        char *next; /* where to put more ITEMs in lists[ list ] */
        int datalen;    /* length of records in this hash table */
        int size;   /* sizeof( ITEM ) + aligned datalen */
        int nel;    /* total ITEMs held by all lists[] */
        int list;   /* index into lists[] */

        struct {
            int nel;    /* total ITEMs held by this list */
            char *base; /* base of ITEMs array */
        } lists[ MAX_LISTS ];
    } items;

    const char * name; /* just for hashstats() */
};

static void hashrehash( struct hash *hp );
static void hashstat( struct hash *hp );
static void * hash_mem_alloc(size_t datalen, size_t size);
static void hash_mem_free(size_t datalen, void * data);
#ifdef OPT_BOEHM_GC
static void hash_mem_finalizer(char * key, struct hash * hp);
#endif

static unsigned int hash_keyval( OBJECT * key )
{
    return object_hash( key );
}

#define hash_bucket(hp,keyval) ((hp)->tab.base + ( (keyval) % (hp)->tab.nel ))

/*  Find the hash item for the given data. Returns pointer to the
    item and if given a pointer to the item before the found item.
    If it's the first item in a bucket, there is no previous item,
    and zero is returned for the previous item instead.
*/
static ITEM * hash_search(
    struct hash *hp,
    unsigned int keyval,
    OBJECT * keydata,
    ITEM * * previous )
{
    ITEM * i = *hash_bucket(hp,keyval);
    ITEM * p = 0;

    for ( ; i; i = i->hdr.next )
    {
        if ( object_equal( i->data.key, keydata ) )
        {
            if (previous)
            {
                *previous = p;
            }
            return i;
        }
        p = i;
    }

    return 0;
}

/*
 * hash_free() - remove the given item from the table if it's there.
 * Returns 1 if found, 0 otherwise.
 *
 * NOTE: 2nd argument is HASHDATA*, not HASHDATA** as elsewhere.
 */
int
hash_free(
    register struct hash *hp,
    HASHDATA *data)
{
    ITEM * i = 0;
    ITEM * prev = 0;
    unsigned int keyval = hash_keyval(data->key);

    i = hash_search( hp, keyval, data->key, &prev );
    if (i)
    {
        /* mark it free so we skip it during enumeration */
        i->data.key = 0;
        /* unlink the record from the hash chain */
        if (prev) prev->hdr.next = i->hdr.next;
        else *hash_bucket(hp,keyval) = i->hdr.next;
        /* link it into the freelist */
        i->hdr.next = hp->items.free;
        hp->items.free = i;
        /* we have another item */
        hp->items.more++;

        return 1;
    }
    return 0;
}

/*
 * hashitem() - find a record in the table, and optionally enter a new one
 */

int
hashitem(
    register struct hash *hp,
    HASHDATA **data,
    int enter )
{
    register ITEM *i;
    OBJECT *b = (*data)->key;
    unsigned int keyval = hash_keyval(b);

    #ifdef HASH_DEBUG_PROFILE
    profile_frame prof[1];
    if ( DEBUG_PROFILE )
        profile_enter( 0, prof );
    #endif

    if ( enter && !hp->items.more )
        hashrehash( hp );

    if ( !enter && !hp->items.nel )
    {
        #ifdef HASH_DEBUG_PROFILE
        if ( DEBUG_PROFILE )
            profile_exit( prof );
        #endif
        return 0;
    }

    i = hash_search( hp, keyval, (*data)->key, 0 );
    if (i)
    {
        *data = &i->data;
        #ifdef HASH_DEBUG_PROFILE
        if ( DEBUG_PROFILE ) profile_exit( prof );
        #endif
        return !0;
    }

    if ( enter )
    {
        ITEM * * base = hash_bucket(hp,keyval);

        /* try to grab one from the free list */
        if ( hp->items.free )
        {
            i = hp->items.free;
            hp->items.free = i->hdr.next;
            assert( i->data.key == 0 );
        }
        else
        {
            i = (ITEM *)hp->items.next;
            hp->items.next += hp->items.size;
        }
        hp->items.more--;
        memcpy( (char *)&i->data, (char *)*data, hp->items.datalen );
        i->hdr.next = *base;
        *base = i;
        *data = &i->data;
        #ifdef OPT_BOEHM_GC
        if (sizeof(HASHDATA) == hp->items.datalen)
        {
            GC_REGISTER_FINALIZER(i->data.key,&hash_mem_finalizer,hp,0,0);
        }
        #endif
    }

    #ifdef HASH_DEBUG_PROFILE
    if ( DEBUG_PROFILE )
        profile_exit( prof );
    #endif
    return 0;
}

/*
 * hashrehash() - resize and rebuild hp->tab, the hash table
 */

static void hashrehash( register struct hash *hp )
{
    int i = ++hp->items.list;
    hp->items.more = i ? 2 * hp->items.nel : hp->inel;
    hp->items.next = (char *)hash_mem_alloc( hp->items.datalen, hp->items.more * hp->items.size );
    hp->items.free = 0;

    hp->items.lists[i].nel = hp->items.more;
    hp->items.lists[i].base = hp->items.next;
    hp->items.nel += hp->items.more;

    if ( hp->tab.base )
        hash_mem_free( hp->items.datalen, (char *)hp->tab.base );

    hp->tab.nel = hp->items.nel * hp->bloat;
    hp->tab.base = (ITEM **)hash_mem_alloc( hp->items.datalen, hp->tab.nel * sizeof(ITEM **) );

    memset( (char *)hp->tab.base, '\0', hp->tab.nel * sizeof( ITEM * ) );

    for ( i = 0; i < hp->items.list; ++i )
    {
        int nel = hp->items.lists[i].nel;
        char *next = hp->items.lists[i].base;

        for ( ; nel--; next += hp->items.size )
        {
            register ITEM *i = (ITEM *)next;
            ITEM **ip = hp->tab.base + object_hash( i->data.key ) % hp->tab.nel;
            /* code currently assumes rehashing only when there are no free items */
            assert( i->data.key != 0 );

            i->hdr.next = *ip;
            *ip = i;
        }
    }
}

void hashenumerate( struct hash * hp, void (* f)( void *, void * ), void * data )
{
    int i;
    for ( i = 0; i <= hp->items.list; ++i )
    {
        char * next = hp->items.lists[i].base;
        int nel = hp->items.lists[i].nel;
        if ( i == hp->items.list )
            nel -= hp->items.more;

        for ( ; nel--; next += hp->items.size )
        {
            ITEM * i = (ITEM *)next;
            if ( i->data.key != 0 )  /* DO not enumerate freed items. */
                f( &i->data, data );
        }
    }
}

/* --- */

# define ALIGNED(x) ( ( x + sizeof( ITEM ) - 1 ) & ~( sizeof( ITEM ) - 1 ) )

/*
 * hashinit() - initialize a hash table, returning a handle
 */

struct hash *
hashinit(
    int datalen,
    const char *name )
{
    struct hash *hp = (struct hash *)hash_mem_alloc( datalen, sizeof( *hp ) );

    hp->bloat = 3;
    hp->tab.nel = 0;
    hp->tab.base = (ITEM **)0;
    hp->items.more = 0;
    hp->items.free = 0;
    hp->items.datalen = datalen;
    hp->items.size = sizeof( struct hashhdr ) + ALIGNED( datalen );
    hp->items.list = -1;
    hp->items.nel = 0;
    hp->inel = 11 /* 47 */;
    hp->name = name;

    return hp;
}

/*
 * hashdone() - free a hash table, given its handle
 */

void
hashdone( struct hash * hp )
{
    int i;

    if ( !hp )
        return;

    if ( DEBUG_MEM || DEBUG_PROFILE )
        hashstat( hp );

    if ( hp->tab.base )
        hash_mem_free( hp->items.datalen, (char *)hp->tab.base );
    for ( i = 0; i <= hp->items.list; ++i )
        hash_mem_free( hp->items.datalen, hp->items.lists[i].base );
    hash_mem_free( hp->items.datalen, (char *)hp );
}

const char *
hashname ( struct hash * hp )
{
    return hp->name;
}

static void * hash_mem_alloc(size_t datalen, size_t size)
{
    if (sizeof(HASHDATA) == datalen)
    {
        return BJAM_MALLOC_RAW(size);
    }
    else
    {
        return BJAM_MALLOC(size);
    }
}

static void hash_mem_free(size_t datalen, void * data)
{
    if (sizeof(HASHDATA) == datalen)
    {
        BJAM_FREE_RAW(data);
    }
    else
    {
        BJAM_FREE(data);
    }
}

#ifdef OPT_BOEHM_GC
static void hash_mem_finalizer(OBJECT * key, struct hash * hp)
{
    HASHDATA d;
    d.key = key;
    hash_free(hp,&d);
}
#endif


/* ---- */

static void hashstat( struct hash * hp )
{
    ITEM * * tab = hp->tab.base;
    int nel = hp->tab.nel;
    int count = 0;
    int sets = 0;
    int run = tab && ( tab[ nel - 1 ] != (ITEM *)0 );
    int i;
    int here;

    for ( i = nel; i > 0; --i )
    {
        if ( ( here = ( *tab++ != (ITEM *)0 ) ) )
            count++;
        if ( here && !run )
            sets++;
        run = here;
    }

    printf( "%s table: %d+%d+%d (%dK+%luK) items+table+hash, %f density\n",
        hp->name,
        count,
        hp->items.nel,
        hp->tab.nel,
        hp->items.nel * hp->items.size / 1024,
        (long unsigned)hp->tab.nel * sizeof( ITEM ** ) / 1024,
        (float)count / (float)sets );
}
