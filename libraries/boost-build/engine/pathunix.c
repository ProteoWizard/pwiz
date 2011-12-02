/*
 * Copyright 1993-2002 Christopher Seiwald and Perforce Software, Inc.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*  This file is ALSO:
 *  Copyright 2001-2004 David Abrahams.
 *  Copyright 2005 Rene Rivera.
 *  Distributed under the Boost Software License, Version 1.0.
 *  (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt)
 */

# include "jam.h"
# include "pathsys.h"
# include "strings.h"
# include "object.h"
# include "filesys.h"
# include <time.h>
# include <stdlib.h>
# ifndef OS_NT
# include <unistd.h>
# endif

# ifdef USE_PATHUNIX

/*
 * pathunix.c - manipulate file names on UNIX, NT, OS2, AmigaOS
 *
 * External routines:
 *
 *  path_parse() - split a file name into dir/base/suffix/member
 *  path_build() - build a filename given dir/base/suffix/member
 *  path_parent() - make a PATHNAME point to its parent dir
 *
 * File_parse() and path_build() just manipuate a string and a structure;
 * they do not make system calls.
 *
 * 04/08/94 (seiwald) - Coherent/386 support added.
 * 12/26/93 (seiwald) - handle dir/.suffix properly in path_build()
 * 12/19/94 (mikem) - solaris string table insanity support
 * 12/21/94 (wingerd) Use backslashes for pathnames - the NT way.
 * 02/14/95 (seiwald) - parse and build /xxx properly
 * 02/23/95 (wingerd) Compilers on NT can handle "/" in pathnames, so we
 *                    should expect hdr searches to come up with strings
 *                    like "thing/thing.h". So we need to test for "/" as
 *                    well as "\" when parsing pathnames.
 * 03/16/95 (seiwald) - fixed accursed typo on line 69.
 * 05/03/96 (seiwald) - split from filent.c, fileunix.c
 * 12/20/96 (seiwald) - when looking for the rightmost . in a file name,
 *            don't include the archive member name.
 * 01/13/01 (seiwald) - turn on \ handling on UNIX, on by accident
 */

/*
 * path_parse() - split a file name into dir/base/suffix/member
 */

void path_parse( const char * file, PATHNAME * f )
{
    const char * p;
    const char * q;
    const char * end;

    memset( (char *)f, 0, sizeof( *f ) );

    /* Look for <grist> */

    if ( ( file[0] == '<' ) && ( p = strchr( file, '>' ) ) )
    {
        f->f_grist.ptr = file;
        f->f_grist.len = p - file;
        file = p + 1;
    }

    /* Look for dir/ */

    p = strrchr( file, '/' );

# if PATH_DELIM == '\\'
    /* On NT, look for dir\ as well */
    {
        char *p1 = strrchr( file, '\\' );
        p = p1 > p ? p1 : p;
    }
# endif

    if ( p )
    {
        f->f_dir.ptr = file;
        f->f_dir.len = p - file;

        /* Special case for / - dirname is /, not "" */

        if ( !f->f_dir.len )
        f->f_dir.len = 1;

# if PATH_DELIM == '\\'
        /* Special case for D:/ - dirname is D:/, not "D:" */

        if ( f->f_dir.len == 2 && file[1] == ':' )
            f->f_dir.len = 3;
# endif

        file = p + 1;
    }

    end = file + strlen( file );

    /* Look for (member) */

    if ( ( p = strchr( file, '(' ) ) && ( end[ -1 ] == ')' ) )
    {
        f->f_member.ptr = p + 1;
        f->f_member.len = end - p - 2;
        end = p;
    }

    /* Look for .suffix */
    /* This would be memrchr() */

    p = 0;
    q = file;

    while ( ( q = (char *)memchr( q, '.', end - q ) ) )
        p = q++;

    if ( p )
    {
        f->f_suffix.ptr = p;
        f->f_suffix.len = end - p;
        end = p;
    }

    /* Leaves base */

    f->f_base.ptr = file;
    f->f_base.len = end - file;
}

/*
 * path_delims - the string of legal path delimiters
 */
static char path_delims[] = {
    PATH_DELIM,
#  if PATH_DELIM == '\\'
    '/',
#  endif
    0
};

/*
 * is_path_delim() - true iff c is a path delimiter
 */
static int is_path_delim( char c )
{
    char* p = strchr( path_delims, c );
    return p && *p;
}

/*
 * as_path_delim() - convert c to a path delimiter if it isn't one
 * already
 */
static char as_path_delim( char c )
{
    return is_path_delim( c ) ? c : PATH_DELIM;
}

/*
 * path_build() - build a filename given dir/base/suffix/member
 *
 * To avoid changing slash direction on NT when reconstituting paths,
 * instead of unconditionally appending PATH_DELIM we check the
 * past-the-end character of the previous path element.  If it is in
 * path_delims, we append that, and only append PATH_DELIM as a last
 * resort.  This heuristic is based on the fact that PATHNAME objects
 * are usually the result of calling path_parse, which leaves the
 * original slashes in the past-the-end position. Correctness depends
 * on the assumption that all strings are zero terminated, so a
 * past-the-end character will always be available.
 *
 * As an attendant patch, we had to ensure that backslashes are used
 * explicitly in timestamp.c
 */

void
path_build(
    PATHNAME *f,
    string  *file,
    int binding )
{
    file_build1( f, file );

    /* Don't prepend root if it's . or directory is rooted */
# if PATH_DELIM == '/'

    if ( f->f_root.len
        && !( f->f_root.len == 1 && f->f_root.ptr[0] == '.' )
        && !( f->f_dir.len && f->f_dir.ptr[0] == '/' ) )

# else /* unix */

    if ( f->f_root.len
        && !( f->f_root.len == 1 && f->f_root.ptr[0] == '.' )
        && !( f->f_dir.len && f->f_dir.ptr[0] == '/' )
        && !( f->f_dir.len && f->f_dir.ptr[0] == '\\' )
        && !( f->f_dir.len && f->f_dir.ptr[1] == ':' ) )

# endif /* unix */

    {
        string_append_range( file, f->f_root.ptr, f->f_root.ptr + f->f_root.len  );
        /* If 'root' already ends with path delimeter,
           don't add yet another one. */
        if ( ! is_path_delim( f->f_root.ptr[f->f_root.len-1] ) )
            string_push_back( file, as_path_delim( f->f_root.ptr[f->f_root.len] ) );
    }

    if ( f->f_dir.len )
        string_append_range( file, f->f_dir.ptr, f->f_dir.ptr + f->f_dir.len  );

    /* UNIX: Put / between dir and file */
    /* NT:   Put \ between dir and file */

    if ( f->f_dir.len && ( f->f_base.len || f->f_suffix.len ) )
    {
        /* UNIX: Special case for dir \ : don't add another \ */
        /* NT:   Special case for dir / : don't add another / */

# if PATH_DELIM == '\\'
        if ( !( f->f_dir.len == 3 && f->f_dir.ptr[1] == ':' ) )
# endif
            if ( !( f->f_dir.len == 1 && is_path_delim( f->f_dir.ptr[0] ) ) )
                string_push_back( file, as_path_delim( f->f_dir.ptr[f->f_dir.len] ) );
    }

    if ( f->f_base.len )
    {
        string_append_range( file, f->f_base.ptr, f->f_base.ptr + f->f_base.len  );
    }

    if ( f->f_suffix.len )
    {
        string_append_range( file, f->f_suffix.ptr, f->f_suffix.ptr + f->f_suffix.len  );
    }

    if ( f->f_member.len )
    {
        string_push_back( file, '(' );
        string_append_range( file, f->f_member.ptr, f->f_member.ptr + f->f_member.len  );
        string_push_back( file, ')' );
    }
}

/*
 *  path_parent() - make a PATHNAME point to its parent dir
 */

void
path_parent( PATHNAME *f )
{
    /* just set everything else to nothing */

    f->f_base.ptr =
    f->f_suffix.ptr =
    f->f_member.ptr = "";

    f->f_base.len =
    f->f_suffix.len =
    f->f_member.len = 0;
}

#ifdef NT
#include <windows.h>
#include <tchar.h>

/* The definition of this in winnt.h is not ANSI-C compatible. */
#undef INVALID_FILE_ATTRIBUTES
#define INVALID_FILE_ATTRIBUTES ((DWORD)-1)


DWORD ShortPathToLongPath(LPCTSTR lpszShortPath,LPTSTR lpszLongPath,DWORD
                          cchBuffer)
{
    LONG i=0;
    TCHAR path[_MAX_PATH]={0};
    TCHAR ret[_MAX_PATH]={0};
    LONG pos=0, prev_pos=0;
    LONG len=_tcslen(lpszShortPath);

    /* Is the string valid? */
    if (!lpszShortPath) {
        SetLastError(ERROR_INVALID_PARAMETER);
        return 0;
    }

    /* Is the path valid? */
    if (GetFileAttributes(lpszShortPath)==INVALID_FILE_ATTRIBUTES)
        return 0;

    /* Convert "/" to "\" */
    for (i=0;i<len;++i) {
        if (lpszShortPath[i]==_T('/'))
            path[i]=_T('\\');
        else
            path[i]=lpszShortPath[i];
    }

    /* UNC path? */
    if (path[0]==_T('\\') && path[1]==_T('\\')) {
        pos=2;
        for (i=0;i<2;++i) {
            while (path[pos]!=_T('\\') && path[pos]!=_T('\0'))
                ++pos;
            ++pos;
        }
        _tcsncpy(ret,path,pos-1);
    } /* Drive letter? */
    else if (path[1]==_T(':')) {
        if (path[2]==_T('\\'))
            pos=3;
        if (len==3) {
            if (cchBuffer>3)
                _tcscpy(lpszLongPath,lpszShortPath);
            return len;
        }
        _tcsncpy(ret,path,2);
        ret[0] = toupper(ret[0]);
    }

    /* Expand the path for each subpath, and strip trailing backslashes */
    for (prev_pos = pos-1;pos<=len;++pos) {
        if (path[pos]==_T('\\') || (path[pos]==_T('\0') &&
                                    path[pos-1]!=_T('\\'))) {
            WIN32_FIND_DATA fd;
            HANDLE hf=0;
            TCHAR c=path[pos];
            char* new_element;
            path[pos]=_T('\0');

            /* the path[prev_pos+1]... path[pos] range is the part of
               path we're handling right now. We need to find long
               name for that element and add it. */
            new_element = path + prev_pos + 1;

            /* First add separator, but only if there's something in result already. */
            if (ret[0] != _T('\0'))
            {
                _tcscat(ret,_T("\\"));
            }

            /* If it's ".." element, we need to append it, not
               the name in parent that FindFirstFile will return.
               Same goes for "." */

            if (new_element[0] == _T('.') && new_element[1] == _T('\0') ||
                new_element[0] == _T('.') && new_element[1] == _T('.')
                && new_element[2] == _T('\0'))
            {
                _tcscat(ret, new_element);
            }
            else
            {
                hf=FindFirstFile(path, &fd);
                if (hf==INVALID_HANDLE_VALUE)
                    return 0;

                _tcscat(ret,fd.cFileName);
                FindClose(hf);
            }

            path[pos]=c;

            prev_pos = pos;
        }
    }

    len=_tcslen(ret)+1;
    if (cchBuffer>=len)
        _tcscpy(lpszLongPath,ret);

    return len;
}

OBJECT * short_path_to_long_path( OBJECT * short_path )
{
    char buffer2[_MAX_PATH];
    int ret = ShortPathToLongPath( object_str( short_path ), buffer2, _MAX_PATH );

    if (ret)
        return object_new( buffer2 );
    else
        return object_copy( short_path );
}

#endif

#ifdef NT

struct path_key_entry
{
    OBJECT * path;
    OBJECT * key;
};

static struct hash * path_key_cache;

OBJECT * path_as_key( OBJECT * path )
{
    struct path_key_entry e, *result = &e;

    if ( ! path_key_cache )
        path_key_cache = hashinit( sizeof( struct path_key_entry ), "path to key" );

    result->path = path;
    if ( hashenter( path_key_cache, (HASHDATA * *)&result ) )
    {
        string buf[1];
        char * s;
        string_copy( buf, object_str( path ) );
        for ( s = buf->value; s < buf->value + buf->size; ++s )
        {
            if ( *s == '/' )
                *s = '\\';
            else
                *s = tolower( *s );
        }
        result->path = object_copy( path );
        result->key = object_new( buf->value );
        string_free( buf );
    }

    return result->key;
}

static void free_path_key_entry( void * xentry, void * data )
{
    struct path_key_entry * entry = (struct path_key_entry *)xentry;
    object_free( entry->path );
    object_free( entry->key );
}

void path_done( void )
{
    if ( path_key_cache )
    {
        hashenumerate( path_key_cache, &free_path_key_entry, (void *)0 );
        hashdone( path_key_cache );
    }
}

#else

OBJECT * path_as_key( OBJECT * path )
{
    return object_copy( path );
}

void path_done( void )
{
}

#endif

static string path_tmpdir_buffer[1];
static const char * path_tmpdir_result = 0;

const char * path_tmpdir()
{
    if (!path_tmpdir_result)
    {
        # ifdef OS_NT
        DWORD pathLength = 0;
        pathLength = GetTempPath(pathLength,NULL);
        string_new(path_tmpdir_buffer);
        string_reserve(path_tmpdir_buffer,pathLength);
        pathLength = GetTempPathA(pathLength,path_tmpdir_buffer[0].value);
        path_tmpdir_buffer[0].value[pathLength-1] = '\0';
        path_tmpdir_buffer[0].size = pathLength-1;
        # else
        const char * t = getenv("TMPDIR");
        if (!t)
        {
            t = "/tmp";
        }
        string_new(path_tmpdir_buffer);
        string_append(path_tmpdir_buffer,t);
        # endif
        path_tmpdir_result = path_tmpdir_buffer[0].value;
    }
    return path_tmpdir_result;
}

OBJECT * path_tmpnam(void)
{
    char name_buffer[64];
    # ifdef OS_NT
    unsigned long c0 = GetCurrentProcessId();
    # else
    unsigned long c0 = getpid();
    # endif
    static unsigned long c1 = 0;
    if (0 == c1) c1 = time(0)&0xffff;
    c1 += 1;
    sprintf(name_buffer,"jam%lx%lx.000",c0,c1);
    return object_new(name_buffer);
}

OBJECT * path_tmpfile(void)
{
    OBJECT * result = 0;
    OBJECT * tmpnam;

    string file_path;
    string_copy(&file_path,path_tmpdir());
    string_push_back(&file_path,PATH_DELIM);
    tmpnam = path_tmpnam();
    string_append(&file_path,object_str(tmpnam));
    object_free(tmpnam);
    result = object_new(file_path.value);
    string_free(&file_path);

    return result;
}


# endif /* unix, NT, OS/2, AmigaOS */
