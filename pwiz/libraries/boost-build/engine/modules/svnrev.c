/*  SvnRev
 *
 *  This utility retrieves the highest number that follows the "$Id: $" keyword
 *  or a combination of the $Rev: $ and $Date: $ keywords. The Subversion
 *  version control system expands these keywords and keeps them up to date.
 *
 *  The utility has been adapted from its original form as a command-line utility
 *  for use in Boost.Jam. The original utility is available at:
 *  http://www.compuphase.com/svnrev.htm
 *
 *  The original license follows:
 *
 *  License
 *
 *  Copyright (c) 2005-2009, ITB CompuPhase (www.compuphase.com).
 *  Copyright 2011 Vanderbilt University - Nashville, TN 37232
 *
 *  This software is provided "as-is", without any express or implied warranty.
 *  In no event will the authors be held liable for any damages arising from
 *  the use of this software.
 *
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *
 *  1.  The origin of this software must not be misrepresented; you must not
 *      claim that you wrote the original software. If you use this software in
 *      a product, an acknowledgment in the product documentation would be
 *      appreciated but is not required.
 *  2.  Altered source versions must be plainly marked as such, and must not be
 *      misrepresented as being the original software.
 *  3.  This notice may not be removed or altered from any source distribution.
 *
 */


#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../native.h"
#include "../output.h"
#include "../object.h"
#include "../strings.h"
#include "../newstr.h"


#define MAX_LINELENGTH      512
#define MAX_SYMBOLLENGTH    32


static void processfile(const char *name,
                        int failsilent, int warnonmissing, int printrevisioninfo,
                        int *max_build, int *accum_build,
                        int *max_year, int *max_month, int *max_day,
                        int *ismodified)
{
    char str[MAX_LINELENGTH], str_base[MAX_LINELENGTH];
    char name_base[MAX_LINELENGTH];
    char *p1;
    FILE *fp, *fp_base;
    int build, maj_build;
    int year, month, day;
    int cnt;
    char modchar;
    char *target;

#ifdef DOWNSHIFT_PATHS
    string path;
    char *p;
#endif

#ifdef DOWNSHIFT_PATHS
    string_copy( &path, name );
    p = path.value;

    do
    {
        *p = tolower( *p );
#ifdef NT
        /* On NT, we must use backslashes or the file will not be found. */
        if ( *p == '\\' )
            *p = '/';
#endif
    }
    while ( *p++ );

    target = path.value;
#endif  /* #ifdef DOWNSHIFT_PATHS */

    /* since we also want to verify whether the file is modified in version
    * control, get the path to the working copy name
    * for every source file "<path>\<filename>, the "working copy" base can
    * be found in "<path>\.svn\text-base\<filename>.svn-base"
    */
    if ((p1 = strrchr(name, '/')) != NULL) {
        ++p1; /* skip directory separator character ('\' in Windows, '/' in Linux) */
        strncpy(name_base, name, (int)(p1 - name));
        name_base[(int)(p1 - name)] = '\0';
    } else {
        name_base[0] = '\0';
        p1 = (char*)name;
    } /* if */
    sprintf(name_base + strlen(name_base), ".svn/text-base/%s.svn-base", p1);

    /* first extract the revision keywords */
    fp = fopen(name, "r");
    if (fp == NULL) {
        if (!failsilent)
            fprintf(stderr, "Failed to open input file '%s'\n", name);
        return;
    } /* if */
    fp_base = fopen(name_base, "r");  /* fail silently */
    build = 0;
    maj_build = 0;      /* RCS / CVS */
    year = month = day = 0;

    while (fgets(str, sizeof str, fp) != NULL) {
        if (fp_base == NULL || fgets(str_base, sizeof str_base, fp_base) == NULL)
            str_base[0] = '\0';
        if ((p1 = strstr(str, "$Id:")) != NULL && strchr(p1+1, '$') != NULL) {
            if (sscanf(p1, "$Id: %*s %d %d-%d-%d", &build, &year, &month, &day) < 4
                && sscanf(p1, "$Id: %*s %d %d/%d/%d", &build, &year, &month, &day) < 4)
                if (sscanf(p1, "$Id: %*s %d.%d %d-%d-%d", &maj_build, &build, &year, &month, &day) < 5)
                    sscanf(p1, "$Id: %*s %d.%d %d/%d/%d", &maj_build, &build, &year, &month, &day);
        } else if ((p1 = strstr(str, "$Rev:")) != NULL && strchr(p1+1, '$') != NULL) {
            if (sscanf(p1, "$Rev: %d.%d", &maj_build, &build) < 2) {
                sscanf(p1, "$Rev: %d", &build);
                maj_build = 0;
            } /* if */
        } else if ((p1 = strstr(str, "$Revision:")) != NULL && strchr(p1+1, '$') != NULL) {
            if (sscanf(p1, "$Revision: %d.%d", &maj_build, &build) < 2) {
                /* SvnRev also writes this keyword in its own generated file; read it
                * back for partial updates
                */
                cnt = sscanf(p1, "$Revision: %d%c", &build, &modchar);
                if (cnt == 2 && modchar == 'M' && ismodified != NULL)
                    *ismodified = 1;
                maj_build = 0;
            } /* if */
        } else if ((p1 = strstr(str, "$Date:")) != NULL && strchr(p1+1, '$') != NULL) {
            if (sscanf(p1, "$Date: %d-%d-%d", &year, &month, &day) < 3)
                sscanf(p1, "$Date: %d/%d/%d", &year, &month, &day);
        } else if (ismodified != NULL && *ismodified == 0 && fp_base != NULL) {
            /* no keyword present, compare the lines for equivalence */
            *ismodified = strcmp(str, str_base) != 0;
        } /* if */

        if (maj_build)
            *accum_build += build;            /* RCS / CVS */
        else if (build > *max_build)
            *max_build = build;               /* Subversion */
        if (year > *max_year
            || (year == *max_year && month > *max_month)
            || (year == *max_year && month == *max_month && day > *max_day))
        {
            *max_year = year;
            *max_month = month;
            *max_day = day;
        } /* if */
        if (build > 0 && year > 0 && (fp_base == NULL || ismodified == NULL || *ismodified != 0))
            break;      /* both build # and date found, not comparing or modification
                        * already found => no need to search further */

    } /* while */
    fclose(fp);
    if (fp_base != NULL)
        fclose(fp_base);

    if (build == 0 && warnonmissing)
        fprintf(stderr, "Missing revision info in: %s\n", name);
    else if (printrevisioninfo)
        printf("%d %4d-%02d-%02d %d %s\n", build, year, month, day, *ismodified, name);
}

LIST *svnrevinfo( FRAME *frame, int flags )
{
    LIST* filepaths = lol_get( frame->args, 0 );
    LIST* revision_info;
    LISTITER iter = list_begin( filepaths ), end = list_end( filepaths );

    int warnonmissing = lol_get( frame->args, 1 ) ? 1 : 0;
    int printrevisioninfo = lol_get( frame->args, 2 ) ? 1 : 0;

    int max_build, accum_build;
    int max_year, max_month, max_day;
    int modifiedfiles, filemodified;

    max_build = 0;
    accum_build = 0; /* for RCS / CVS */
    max_year = max_month = max_day = 0;
    modifiedfiles = 0;
    for (; iter != end; iter = list_next( iter ) )
    {
        /* phase 1: scan through all files and get the highest build number */

        filemodified = 0;
        processfile(object_str(list_item( iter )),
                    0, warnonmissing, printrevisioninfo, 
                    &max_build, &accum_build, &max_year, &max_month, &max_day,
                    &filemodified);
        modifiedfiles += filemodified;
    } /* for */

    revision_info = list_push_back(L0, outf_int(max_build));
    revision_info = list_push_back(revision_info, outf_int(max_year));
    revision_info = list_push_back(revision_info, outf_int(max_month));
    revision_info = list_push_back(revision_info, outf_int(max_day));
    revision_info = list_push_back(revision_info, outf_int(modifiedfiles));

    return revision_info;
}

void init_svnrev()
{
    const char* args[] = { "filepath", "+", ":", "warn-on-missing-info", "?", ":", "print-revision-info", "?", 0 };
    declare_native_rule("svnrev", "get-revision-info", args, svnrevinfo, 1);
}
