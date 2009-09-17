# include "jam.h"
# include "pathsys.h"
# include "strings.h"
# include "newstr.h"
# include "filesys.h"
# include "lists.h"
# include "timestamp.h"

void file_build1( PATHNAME * f, string * file )
{
    if ( DEBUG_SEARCH )
    {
        printf("build file: ");
        if ( f->f_root.len )
            printf( "root = '%.*s' ", f->f_root.len, f->f_root.ptr );
        if ( f->f_dir.len )
            printf( "dir = '%.*s' ", f->f_dir.len, f->f_dir.ptr );
        if ( f->f_base.len )
            printf( "base = '%.*s' ", f->f_base.len, f->f_base.ptr );
        printf( "\n" );
    }

    /* Start with the grist.  If the current grist isn't */
    /* surrounded by <>'s, add them. */

    if ( f->f_grist.len )
    {
        if ( f->f_grist.ptr[0] != '<' )
            string_push_back( file, '<' );
        string_append_range(
            file, f->f_grist.ptr, f->f_grist.ptr + f->f_grist.len );
        if ( file->value[file->size - 1] != '>' )
            string_push_back( file, '>' );
    }
}

static struct hash * filecache_hash = 0;
static file_info_t filecache_finfo;

file_info_t * file_info(char * filename)
{
    file_info_t *finfo = &filecache_finfo;

    if ( !filecache_hash )
        filecache_hash = hashinit( sizeof( file_info_t ), "file_info" );

    finfo->name = filename;
    finfo->is_file = 0;
    finfo->is_dir = 0;
    finfo->size = 0;
    finfo->time = 0;
    finfo->files = 0;
    if ( hashenter( filecache_hash, (HASHDATA**)&finfo ) )
    {
        /* printf( "file_info: %s\n", filename ); */
        finfo->name = newstr( finfo->name );
    }

    return finfo;
}

void file_free(char * filename, int is_recursive)
{
    file_info_t * ff;

    /* do nothing if cache is uninitialized */
    if ( !filecache_hash )
        return;

    ff = file_info( filename );
    hash_free( filecache_hash, (HASHDATA*)ff );

    /* freed directories must free all their files and subdirectories */
    if ( ff->is_dir )
    {
        printf( "dir_free: %s\n", filename );
        if ( !ff->files )
            printf( "directory without files: %s\n", filename );
        else
        {
            LIST * files = ff->files;
            for ( ; files; files = list_next( files ) )
                if ( is_recursive || file_is_file( files->string ) )
                    file_free( files->string, is_recursive );
        }
    }
    else
    {
        printf( "file_free: %s\n", filename );
    }
}

void file_free_all()
{
    hashdone( filecache_hash );
    filecache_hash = hashinit( sizeof( file_info_t ), "file_info" );
}

static LIST * files_to_remove = L0;

static void remove_files_atexit(void)
{
    /* we do pop front in case this exit function is called
       more than once */
    while ( files_to_remove )
    {
        remove( files_to_remove->string );
        files_to_remove = list_pop_front( files_to_remove );
    }
}

void file_done()
{
    remove_files_atexit();
    hashdone( filecache_hash );
}

void file_remove_atexit( const char * path )
{
    files_to_remove = list_new( files_to_remove, newstr((char*)path) );
}
