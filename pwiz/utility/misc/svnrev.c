/*  SvnRev
 *
 *  This utility retrieves the highest number that follows the "$Id: $" keyword
 *  or a combination of the $Rev: $ and $Date: $ keywords. The Subversion
 *  version control system expands these keywords and keeps them up to date.
 *  For an example of the tag, see the end of this comment.
 *
 *  Details on the usage and the operation of this utility is available on-line
 *  at http://www.compuphase.com/svnrev.htm.
 *
 *
 *  Acknowledgements
 *
 *  The support for .java files is contributed by Tom McCann (tommc@spoken.com).
 *  The option for prefixing and/or suffixing the build number (in the string 
 *  constant SVN_REVSTR) was suggested by Robert Nitzel.
 *
 *
 *  License
 *
 *  Copyright (c) 2005-2009, ITB CompuPhase (www.compuphase.com).
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
 * Version: $Id: svnrev.c 4098 2009-03-30 08:34:07Z thiadmer $
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "svnrev.h"


#if defined __WIN32__ || defined _Win32 || defined _WIN32
  #define DIRSEP '\\'
  #define strcasecmp  stricmp
#elif defined macintosh
  #define DIRSEP ':'
#else
  /* assume Linux/Unix */
  #define DIRSEP '/'
#endif

#define MAX_LINELENGTH      512
#define MAX_SYMBOLLENGTH    32

static void about(void)
{
  printf("svnrev 1.7." SVN_REVSTR "\n\n");
  printf("Usage: svnrev [options] <input> [input [...]]\n\n"
         "Options:\n"
         "-ofilename\tOutput filename for the file with the build number. When no\n"
         "\t\tfilename follows \"-o\", the result is written to stdout. The\n"
         "\t\tdefault filename is \"svnrev.h\" for C/C++ and \"VersionInfo.java\"\n"
         "\t\tfor Java.\n\n"
         "-fpattern\tFormat: Adds text before or after the build number in the\n"
         "\t\tconstant SVN_REVSTR. The pattern has the form \"text#text\"\n"
         "\t\t(without the quotes) where \"text\" is arbitrary text and \"#\"\n"
         "\t\twill be replaced by the build number.\n\n"
         "-i\t\tIncremental: this option should be used when the list of input\n"
         "\t\tfiles is a subset of all files in the project. When -i is\n"
         "\t\tpresent, svnrev also scans the output file that was generated\n"
         "\t\ton a previous run.\n\n"
         "-jname\t\tJava: this option writes a java package file instead of a C/C++\n"
         "\t\theader file. The name of the Java package must follow the\n"
         "\t\toption (this is not the filename).\n\n"
         "-v\t\tVerbose: prints the names of files that are modified since the\n"
         "\t\tlast commit (into version control) to stderr.\n");
  exit(1);
}

static void processfile(const char *name, int failsilent,
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

  /* since we also want to verify whether the file is modified in version
   * control, get the path to the working copy name
   * for every source file "<path>\<filename>, the "working copy" base can
   * be found in "<path>\.svn\text-base\<filename>.svn-base"
   */
  if ((p1 = strrchr(name, DIRSEP)) != NULL) {
    ++p1; /* skip directory separator character ('\' in Windows, '/' in Linux) */
    strncpy(name_base, name, (int)(p1 - name));
    name_base[(int)(p1 - name)] = '\0';
  } else {
    name_base[0] = '\0';
    p1 = (char*)name;
  } /* if */
  sprintf(name_base + strlen(name_base), ".svn%ctext-base%c%s.svn-base",
          DIRSEP, DIRSEP, p1);

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
}

int main(int argc, char *argv[])
{
  char *outname = NULL;
  FILE *fp;
  int index;
  int process_self = 0;
  int verbose = 0;
  int max_build, accum_build, existing_max_build;
  int max_year, max_month, max_day;
  int ismodified, filemodified;
  char prefix[MAX_SYMBOLLENGTH], suffix[MAX_SYMBOLLENGTH];
  char modified_suffix[2];
  int write_java = 0;   /* flag for Java output, 0=.h output, 1=.java output */
  /* java package to put revision info in.
   * REVIEW - I assume if you want Java output you will specify a package. */
  char *java_package = NULL;

  if (argc <= 1)
    about();

  /* collect the options */
  prefix[0] = '\0';
  suffix[0] = '\0';
  
  for (index = 1; index < argc; index++) {
    /* check for options */
    if (argv[index][0] == '-'
#if defined __WIN32__ || defined _Win32 || defined _WIN32
     || argv[index][0] == '/'
#endif
    )
    {
      switch (argv[index][1]) {
      case 'f': {
        int len;
        char *ptr = strchr(&argv[index][2], '#');
        len = (ptr != NULL) ? (int)(ptr - &argv[index][2]) : (int)strlen(&argv[index][2]);
        if (len >= MAX_SYMBOLLENGTH)
          len = MAX_SYMBOLLENGTH - 1;
        strncpy(prefix, &argv[index][2], len);
        prefix[len] = '\0';
        ptr = (ptr != NULL) ? ptr + 1 : strchr(argv[index], '\0');
        len = strlen(ptr);
        if (len >= MAX_SYMBOLLENGTH)
          len = MAX_SYMBOLLENGTH - 1;
        strncpy(suffix, ptr, len);        
        suffix[len] = '\0';
        break;
      } /* case */
      case 'i':
        process_self = 1;
        break;
      case 'j':
        write_java=1;
        java_package = &argv[index][2];
        break;
      case 'o':
        outname = &argv[index][2];
        break;
      case 'v':
        verbose = 1;
        break;
      default:
        fprintf(stderr, "Invalid option '%s'\n", argv[index]);
        about();
      } /* switch */
    } /* if */
  } /* for */

  if (outname == NULL)
    outname = write_java ? "SvnRevision.java" : "svnrev.h";
  if (!process_self && *outname != '\0')
    remove(outname);

  /* phase 1: scan through all files and get the highest build number */

  max_build = 0;
  accum_build = 0;      /* for RCS / CVS */
  max_year = max_month = max_day = 0;
  ismodified = 0;
  for (index = 1; index < argc; index++) {
    /* skip the options (already handled) */
    if (argv[index][0] == '-'
#if defined __WIN32__ || defined _Win32 || defined _WIN32
     || argv[index][0] == '/'
#endif
    )
      continue;

    filemodified = 0;
    if (strcasecmp(argv[index], outname)!=0)
      processfile(argv[index], 0, &max_build, &accum_build, &max_year, &max_month, &max_day, &filemodified);
    if (filemodified && verbose)
      fprintf(stderr, "\tNotice: modified file '%s'\n", argv[index]);
    ismodified = ismodified || filemodified;
  } /* for */

  /* if no version information is found, do not create header */
  if (max_build == 0)
    return 0;

  existing_max_build = 0;
  /* also run over the existing header file, if any */
  if (process_self && *outname != '\0')
    processfile(outname, 1, &existing_max_build, &accum_build, &max_year, &max_month, &max_day, NULL/*&ismodified*/);

  /* if existing header has the same build, exit early to prevent unnecessary recompiles */
  if (existing_max_build == max_build)
      return 0;

  if (accum_build > max_build)
    max_build = accum_build;
  modified_suffix[0] = ismodified ? 'M' : '\0';
  modified_suffix[1] = '\0';

  /* phase 2: write a file with this highest build number */
  if (*outname == '\0') {
    fp = stdout;
  } else if ((fp = fopen(outname, "w")) == NULL) {
    fprintf(stderr, "Failed to create output file '%s'\n", outname);
    return 2;
  } /* if */
  if (*outname != '\0') {
    /* don't print the comments to stdout */
    fprintf(fp, "/* This file was generated by the \"svnrev\" utility\n"
                " * (http://www.compuphase.com/svnrev.htm).\n"
                " * You should not modify it manually, as it may be re-generated.\n"
                " *\n"
                " * $Revision: %d%s$\n"
                " * $Date: %04d-%02d-%02d$\n"
                " */\n\n", max_build, modified_suffix, max_year, max_month, max_day);
  } /* if */

  if (write_java) {
    if (java_package == NULL || *java_package == '\0')
      java_package = "com.compuphase";
    fprintf(fp, "package %s;\n\n", java_package);
    fprintf(fp, "public interface SvnRevision\n");
    fprintf(fp, "{\n");
    fprintf(fp, "    public final static int SVN_REV = %d;\n", max_build);
    fprintf(fp, "    public final static String SVN_REVSTR = \"%s%d%s%s\";\n", prefix, max_build, modified_suffix, suffix);
    fprintf(fp, "    public final static String SVN_REVDATE = \"%04d-%02d-%02d\";\n", max_year, max_month, max_day);
    fprintf(fp, "    public final static long SVN_REVSTAMP = %04d%02d%02dL;\n", max_year, max_month, max_day);
    fprintf(fp, "    public final static int SVN_MODIFIED = %d;\n", ismodified);
    fprintf(fp, "}\n\n");
  } else {
    fprintf(fp, "#ifndef SVNREH_H\n");
    fprintf(fp, "#define SVNREV_H\n\n");
    fprintf(fp, "#define SVN_REV\t\t%d\n", max_build);
    fprintf(fp, "#define SVN_REVSTR\t\"%s%d%s%s\"\n", prefix, max_build, modified_suffix, suffix);
    fprintf(fp, "#define SVN_REVDATE\t\"%04d-%02d-%02d\"\n", max_year, max_month, max_day);
    fprintf(fp, "#define SVN_REVSTAMP\t%04d%02d%02dL\n", max_year, max_month, max_day);
    fprintf(fp, "#define SVN_MODIFIED\t%d\n", ismodified);
    fprintf(fp, "\n#endif /* SVNREV_H */\n");
  } /* if */

  if (*outname != '\0')
    fclose(fp);

  return 0;
}
