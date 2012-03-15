/*
##############################################################################
# file: ms_errors.hpp                                                        #
# 'msparser' toolkit                                                         #
# Encapsulates a general-purpose error object that collects information      #
# about several consecutive erros                                            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_errors.hpp              $ #
#     $Author: davidc $ #
#       $Date: 2011-02-10 18:14:25 $ #
#   $Revision: 1.56 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_ERRORS_HPP
#define MS_ERRORS_HPP

#ifdef _WIN32
#pragma warning(disable:4251)   // Don't want all classes to be exported
#pragma warning(disable:4786)   // Debug symbols too long
#   ifndef _MATRIX_USE_STATIC_LIB
#       ifdef MS_MASCOTRESFILE_EXPORTS
#           define MS_MASCOTRESFILE_API __declspec(dllexport)
#       else
#           define MS_MASCOTRESFILE_API __declspec(dllimport)
#       endif
#   else
#       define MS_MASCOTRESFILE_API
#   endif
#else
#   define MS_MASCOTRESFILE_API
#endif

// for the sake of #include <string>
#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif

#include <vector>
#include <string>

/*
#include <istream>
#include <strstream>
#include <fstream>
*/
namespace matrix_science {
    /** @addtogroup common_group
     *  
     *  @{
     */

    //! All errors are collected in an instance of this class.
    class MS_MASCOTRESFILE_API ms_errs
    {
    public:
        //! Error severity levels
        /*!
         * See \ref DynLangEnums.
         */

        enum msg_sev {
            sev_fatal   = 1,  //!< A fatal error that results in the object being 'invalid'.
            sev_warn    = 2,  //!< A warning message.
            sev_info    = 3,  //!< Information message. Not available by default unless setLoggingLevel() called.
            sev_debug1  = 4,  //!< Debug level 1 message. Not available by default unless setLoggingLevel() called .
            sev_debug2  = 5,  //!< Debug level 2 message.  Not available by default unless setLoggingLevel() called.
            sev_debug3  = 6   //!< Debug level 3 message.  Not available by default unless setLoggingLevel() called.
        };

        //! Definitions for error numbers.
        /*!
         *  See \ref DynLangEnums.
         *
         *  Errors are classified as:
         *  - [F] fatal errors
         *  - [W] warnings
         *  - [I] information 
         *  - [D1] Debug level 1 message
         *  - [D2] Debug level 2 message
         *  - [D3] Debug level 3 message
         *
         *  A warning will not cause ms_errors#isValid() to return <b>FALSE</b>.
         */
        enum err
        {
            // errors for result files
            ERR_NO_ERROR                        = 0x0000, //!< [W] Success.
            ERR_NOMEM                           = 0x0001, //!< [F] Failed to allocate memory to load the file.
            ERR_NOSUCHFILE                      = 0x0002, //!< [F] The file passed in the constructor does not exist.
            ERR_READINGFILE                     = 0x0003, //!< [F] Opened the file successfully, but failed to read from it.
            ERR_QUERYOUTOFRANGE                 = 0x0004, //!< [F] Set if query < 1 or query > getNumQueries.
            ERR_MISSINGENTRY                    = 0x0005, //!< [F] Set if there is no qexp value in the file.
            ERR_PEPSUMMPEPGET                   = 0x0006, //!< [F] Value of q, p or h out of range, so cannot get peptide info.
            ERR_PEPTIDESTR                      = 0x0007, //!< [F] The string in the peptides block is not valid.
            ERR_ACCINPEPTIDESTR                 = 0x0008, //!< [F] Could not parse an item for a given accession in the peptide section.
            ERR_PROTSUMM                        = 0x0009, //!< [F] Error parsing a line in the protein summary.
            ERR_PROTSUMMPEP                     = 0x000A, //!< [F] Couldn't parse peptide information from the protein summary section..
            ERR_ADDPEPTIDES                     = 0x000B, //!< [F] Failed to add peptides when creating the peptide summary.
            ERR_MISSINGHIT                      = 0x000C, //!< [F] Missing hit in the summary section.
            ERR_MISSINGSECTION                  = 0x000D, //!< [F] Complete missing section in the file.
            ERR_MISSINGSECTIONEND               = 0x000E, //!< [F] Missing end of section in the file.
            ERR_MALFORMED_ERR_TOL               = 0x000F, //!< [W] Expecting a line of format <code>q1_p2_et_mods=0.984020,0.000000,Citrullination</code>.
            ERR_NO_ERR_TOL_PARENT               = 0x0010, //!< [F] No parent search file. See \ref errorTolerantPage.
            ERR_NULL_ACC_PEP_SUM                = 0x0011, //!< [W] An empty accession string has been found. Possible problem in database.
            ERR_NULL_ACC_PROT_SUM               = 0x0012, //!< [W] An empty accession string has been found. Possible problem in database.
            ERR_DUPE_ACCESSION                  = 0x0013, //!< [W] A possible duplicate accession string has been found. Possible problem in database.
            ERR_UNASSIGNED_PROG                 = 0x0014, //!< [F] Programming error! Calling getNumberOfUnassigned() or getUnassigned() before createUnassignedList().
            ERR_UNASSIGNED_RANGE                = 0x0015, //!< [F] Calling ms_mascotresults::getUnassigned() with out of range number.
            ERR_UNASSIGNED_UNK                  = 0x0016, //!< [F] Calling ms_mascotresults::getUnassigned() -- unable to retrieve value.
            ERR_NO_UNIGENE_FILE                 = 0x0017, //!< [F] Failed to open the UniGene file specified.
            ERR_DUPLICATE_KEY                   = 0x0018, //!< [W] Duplicate entries with the same key in the named section.
            ERR_OLDRESULTSFILE                  = 0x0019, //!< [F] Very old results file (last century!). Parser requires 1.02 or later.
            ERR_MALFORMED_TAG                   = 0x001A, //!< [W] Expecting a line in format <code>q1_p2_tag=1:3:5:6,2:4:12:6,...</code>.
            ERR_MALFORMED_DRANGE                = 0x001B, //!< [W] Expecting a line in format <code>q1_p2_drange=0,256</code>.
            ERR_INVALID_NUMQUERIES              = 0x001C, //!< [W] Invalid number of queries in results file has been corrected.
            ERR_MALFORMED_TERMS                 = 0x001D, //!< [W] Expecting a line in format <code>q1_p2_terms=A,B:-,I:...</code>.
            ERR_INVALID_RESFILE                 = 0x001E, //!< [F] Invalid results file format -- missing or corrupt headers.
            ERR_INVALID_PROTDB                  = 0x001F, //!< [W] Invalid h1_db-string format. Expecting an integer number.
            ERR_UNIGENE_MULTIDB                 = 0x0020, //!< [W] UniGene index is not supported in multi-database search.
            ERR_INVALID_CACHE_DIR               = 0x0021, //!< [F] Must specify a cache directory if using CDB cache files.
            ERR_FAIL_OPEN_DAT_FILE              = 0x0022, //!< [F] Failed to open the results file for reading.
            ERR_MISSING_CDB_FILE                = 0x0023, //!< [W] Cache file is missing or cannot be opened.
            ERR_FAIL_MK_CACHE_DIR               = 0x0024, //!< [F] Failed to create cache directory for cache files.
            ERR_FAIL_MK_CDB_FILE                = 0x0025, //!< [W] Failed to create an cache file.
            ERR_FAIL_CLOSE_FILE                 = 0x0026, //!< [W] Failed to close file.
            ERR_FAIL_CDB_INIT                   = 0x0027, //!< [W] Failed to initialise cache file (%%s). Error code %%d.
            ERR_INVALID_CDB_FILE                = 0x0028, //!< [W] Value in cdb cache file (%%s) is corrupt: %%s.
            ERR_WRITE_CDB_FILE                  = 0x0029, //!< [W] Failed to write to the cache file (%%s). Error %%d (%%s).
            ERR_CDB_TOO_LARGE                   = 0x002A, //!< [W] Cannot use cache file (%%s). Maximum size permitted is %%s.
            ERR_NEED_64_BIT                     = 0x002B, //!< [F] This results file (%%s) is too large for 32 bit Mascot Parser. Please upgrade to 64 bit.
            ERR_CDB_64_BIT_REMAKE               = 0x002C, //!< [W] Re-creating %%s. Was too large for 32 bit, but may succeed with 64 bit.
            ERR_CDB_OLD_VER_RETRY               = 0x002D, //!< [W] Cache file %%s is an old version. Creating new cache file.
            ERR_CDB_OLD_VER_NO_RETRY            = 0x002E, //!< [W] Cache file %%s is an old version. Continuing without cache.
            ERR_CDB_INCOMPLETE_RETRY            = 0x002F, //!< [W] Cache file %%s was not complete. Re-creating the cache file.
            ERR_CDB_INCOMPLETE_NO_RETRY         = 0x0030, //!< [W] Cache file %%s was not complete.  Continuing without cache.
            ERR_CDB_BEING_CREATED               = 0x0031, //!< [W] Cache file %% being created by another task. Continuing without cache.
            ERR_CDB_FAIL_REMOVE                 = 0x0032, //!< [W] Failed to remove old cache file %%s - error %%s. Continuing without cache.
            ERR_CDB_FAIL_LOCK                   = 0x0033, //!< [W] Failed to lock cache file %%s. Error code: %%d.
            ERR_CDB_FAIL_UNLOCK                 = 0x0034, //!< [W] Failed to unlock cache file %%s. Error code: %%d.
            ERR_CDB_SOURCE_CHANGE_RETRY         = 0x0035, //!< [W] %%s changed. %%s (was %%s), %%s bytes (was %%s). Re-creating the cache file.
            ERR_CDB_SOURCE_CHANGE_NO_RETRY      = 0x0036, //!< [W] %%s changed. %%s (was %%s), %%s bytes (was %%s). Continuing without cache.
            ERR_MISSING_PERCOLATOR_FILE         = 0x0037, //!< [F] Percolator file %%s is missing. Cannot continue.
            ERR_CANNOT_APPEND_RESFILE           = 0x0038, //!< [F] The file %%s cannot be appended to %%s because %%s values are different.
            ERR_CANNOT_APPEND_RESFILE_NO_FNAMES = 0x0039, //!< [F] The file cannot be appended because %%s values are different.
            ERR_RESULTS_NOT_CREATED             = 0x003A, //!< [W] Attempting to call Mascot Parser function %%s before createSummary() has completed.

            // OS errors
            ERR_MSP_FAILED_TO_OPEN_FILE         = 0x0100, //!< [F] Error when opening file.
            ERR_MSP_FAILED_TO_CLOSE_FILE        = 0X0101, //!< [W] Can happen everywhere.
            ERR_MSP_FAIL_STAT                   = 0x0102, //!< [W] OS-specific API-function stat() failed.
            ERR_MSP_GET_VOLUME_INFO             = 0x0103, //!< [W] Failed to get volume information.
            ERR_MSP_FILE_DOESNT_EXIST           = 0x0104, //!< [F] Cannot open file because it doesn't exist.
            ERR_MSP_FAIL_GET_PROCESS_AFFINITY   = 0x0105, //!< [F] Failed to find information about number of processors available.
            ERR_MSP_SYSMP_FAIL                  = 0x0106, //!< [F] Failed call to sysmp() for multi-processor support.
            ERR_MSP_FAIL_GET_SYSINFO            = 0x0107, //!< [F] Failed to obtain system information.
            ERR_MSP_FAILED_TO_WRITE_FILE        = 0x0108, //!< [F] Failed to wtite to file '%%s' (file not open or disk full).

            // fragmentation rules file related errors
            ERR_MSP_FRAGMENTATION_RULES         = 0x0200, //!< [F] Error when parsing fragmentation_rules file.

            // masses file related errors
            ERR_MSP_IN_MASSES_FILE              = 0x0300, //!< [F] Error when parsing "masses" file.
            ERR_MSP_INVALID_MASS_IN_MASSES_FILE = 0x0301, //!< [F] Invalid mass in "masses" file.

            // mod_file related errors
            ERR_MSP_DUPLICATE_MOD               = 0x0400, //!< [F] Duplicate modification names in mod_file.
            ERR_MSP_RESIDUE_AND_TERMINUS_MOD    = 0x0401, //!< [F] Bad mod combination.
            ERR_MSP_TOO_MANY_MODS_IN_MOD_FILE   = 0x0402, //!< [W] Too many modification have been loaded from file(s).
            ERR_MSP_NO_COMMAS_IN_MOD_NAME       = 0x0403, //!< [F] Comma is not allowed in mod name.
            ERR_MSP_IN_MODS_FILE                = 0x0404, //!< [F] Error in mod file.
            ERR_MSP_MISSING_DEFINITION_END      = 0x0405, //!< [F] Missing end of modification definition.
            ERR_MSP_NO_SUCH_MOD                 = 0x0406, //!< [F] Cannot find a modification.

            // enzymes file related errors
            ERR_MSP_ENZYME_FILE_FORMAT          = 0x0500, //!< [F] Error in enzymes file.
            ERR_MSP_MISSING_ENZYME_TITLE        = 0x0501, //!< [F] Non-comment line encountered while searching for a title.
            ERR_MSP_ENZYME_DEFINITION_PROBLEM   = 0x0502, //!< [F] Invalid enzyme definition.
            ERR_MSP_ENZYME_TOO_MANY_RULES       = 0x0503, //!< [F] Too many cleavage rules for enzyme.

            // mascot.dat file related errors
            ERR_MSP_MISSING_MASCOT_DAT          = 0x0601, //!< [F] Missing <tt>mascot.dat</tt> file.
            ERR_MSP_DB_USES_MISSING_RULE        = 0x0602, //!< [W] A database uses non-existent rule.
            ERR_MSP_COMPILE_PARSE_RULE          = 0x0603, //!< [W] Error while compiling a parse rule with regex.
            ERR_MSP_NEED_1_EXP_IN_PARSE_RULE    = 0x0604, //!< [W] Only one subexpression is allowed per parse rule.
            ERR_MSP_IN_MASCOT_DAT_DB_SECT       = 0x0605, //!< [W] Error in <tt>Databases</tt> section.
            ERR_MSP_IN_OPTIONS_SECTION          = 0x0606, //!< [W] Error in <tt>OPTIONS</tt> section.
            ERR_MSP_IN_CLUSTER_SECTION          = 0x0607, //!< [W] Error in <tt>CLUSTER</tt> section.
            ERR_MSP_TOO_MANY_CPUS_IN_SUB_CLUSTER = 0x0608,//!< [W] The number of CPUs for the sub cluster is too high.
            ERR_MSP_CRON_TOO_MANY_JOBS          = 0x0609, //!< [W] Too many cron jobs.
            ERR_MSP_INVALID_PARSE_RULE          = 0x060A, //!< [W] Invalid parsing rule.
            ERR_MSP_INVALID_PARSE_RULE_NO       = 0x060B, //!< [W] Parsing rule with invalid number.
            ERR_MSP_RULE_NO_ALREADY_DEFINED     = 0x060C, //!< [W] Parsing rule has been defined twice.
            ERR_MSP_MISSING_QUOTE_IN_PARSE_RULE = 0x060D, //!< [W] Missing quote character in a parse rule.
            ERR_MSP_PARSE_LEN_EXCEED            = 0x060E, //!< [W] Parse rule length exceeds the limit.
            ERR_MSP_IN_TAXONOMY_SECTION         = 0x060F, //!< [W] Error in <tt>Taxonomy</tt> section.
            ERR_MSP_INCOMPATIBLE_TAX_RULES      = 0x0610, //!< [W] Incompatible taxonomy rules.
            ERR_MSP_MAX_PREFIX_REMOVES          = 0x0611, //!< [W] Maximum number of prefixes is exceeded in <tt>Taxonomy</tt> section.
            ERR_MSP_MAX_SUFFIX_REMOVES          = 0x0612, //!< [W] Maximum number of suffices is exceeded in <tt>Taxonomy</tt> section.
            ERR_MSP_MAX_TAX_NO_BREAKS           = 0x0613, //!< [W] Maximum number of breaks is exceeded in <tt>Taxonomy</tt> section.
            ERR_MSP_CRON_INVALID_CHAR           = 0x0614, //!< [W] Invalid character in <tt>Cron</tt> section.
            ERR_MSP_CRON_INVALID_NUMBER         = 0x0615, //!< [W] Invalid number in <tt>Cron</tt> section.
            ERR_MSP_CRON_INVALID_STAR           = 0x0616, //!< [W] Invalid star-character in <tt>Cron</tt> section.
            ERR_MSP_CRON_NO_NUM_BEFORE_COMMA    = 0x0617, //!< [W] Expecting number before comma (,) in <tt>Cron</tt> section.
            ERR_MSP_CRON_NO_NUM_BEFORE_MINUS    = 0x0618, //!< [W] Expecting number before dash (-) in <tt>Cron</tt> section.
            ERR_MSP_INVALID_PROCESSOR_LINE      = 0x0619, //!< [W] Invalid line in <tt>mascot.dat</tt>.
            ERR_MSP_PROCESSOR_NOT_IN_SET        = 0x061A, //!< [W] Processor specified is not in the <tt>ProcessorSet</tt>.
            ERR_MSP_SPECIFY_UNAVAILABLE_PROCESSOR= 0x061B,//!< [W] <tt>ProcessorSet</tt> specifies unavailable processor.
            ERR_MSP_TOO_MANY_PROCESSORS         = 0x061C, //!< [W] More processors specified than licensed.
            ERR_MSP_TOO_MANY_TH_PROCESSORS      = 0x061D, //!< [W] Too many processors named for a smaller number of threads for a database.
            ERR_MSP_WWW_SECTION                 = 0x061E, //!< [W] Error in <tt>WWW</tt> section.
            ERR_MSP_DUP_TAXONOMYRULE            = 0x061F, //!< [W] A taxonomy rule with duplicate number has been encountered.
            ERR_MSP_WRONG_ICAT_FILTER           = 0x0620, //!< [W] Empty ICAT filter is not allowed.
            ERR_MSP_EXEC_AFTER_SEARCH_INVALID   = 0x0621, //!< [W] ExecAfterSearch must start with \c waitforX, where 0 <= X <= 10.
            ERR_MSP_EXEC_AFTER_SEARCH_DEPENDS   = 0x0622, //!< [W] ExecAfterSearch has waitfor %%d, but no %%d command.
            ERR_MSP_EXEC_AFTER_SEARCH_LOGGING   = 0x0623, //!< [W] ExecAfterSearch has an invalid logging level (should be 0..3).

            // license file related errors
            ERR_MSP_NO_LICENSE_FILE             = 0x0700, //!< [F] License file cannot be found or invalid.
            ERR_MSP_LICENSE_DES_CHECKSUM        = 0x0701, //!< [F] Check sum in the license file is invalid.
            ERR_MSP_LICENSE_LINE_CHECKSUM       = 0x0702, //!< [F] Check sum in the license file is invalid.
            ERR_MSP_LICENSE_NOT_YET_AVAIL       = 0x0703, //!< [F] Start date has not come yet.
            ERR_MSP_LICENSE_EXPIRED             = 0x0704, //!< [F] The license has already expired.
            ERR_MSP_LICENSE_INTERNAL_CONFIG     = 0x0705, //!< [F] Internal configuration error when reading a license file.
            ERR_MSP_LICENSE_LINE_INVALID        = 0x0706, //!< [F] Line [line number] of the license file is corrupt or this is not a license file.

            ERR_MSP_XML_SYSTEM_FAILED           = 0x0801, //!< [F] XML-library failure.
            ERR_MSP_XML_NO_ROOT_ELEMENT         = 0x0802, //!< [F] XML document doesn't contain a root element.
            ERR_MSP_XML_TABLE_NOTFOUND          = 0x0803, //!< [F] Table with a specific name is not found amongst children of the current node.
            ERR_MSP_XML_FIELD_NOTFOUND          = 0x0804, //!< [F] Records in a table doesn't have the required field.
            ERR_MSP_XML_INVALID_FIELD_FORMAT    = 0x0805, //!< [F] Field of a record contains data in wrong format.
            ERR_MSP_XML_LOCAL_SCHEMA_NOT_STORED = 0x0806, //!< [F] Cannot store XML-schema file locally.
            ERR_MSP_XML_ELEMENT_NOT_FOUND       = 0x0807, //!< [F] Cannot find element with the supplied name.
            ERR_MSP_XML_MEMORY_ERROR            = 0x0808, //!< [F] Memory allocation failed when working with XML.

            ERR_MSP_HTTP_TRANSMISSION_FAILED    = 0x0901, //!< [F] Http-transmission failed.
            ERR_MSP_HTTP_INVALID_URL            = 0x0902, //!< [F] Invalid web-address.
            ERR_MSP_MASCOT_NOT_RUNNING          = 0x0903, //!< [F] The Mascot service is not running. Unable to retrieve configuration file.
            ERR_MSP_HTTP_WININET_FAILED         = 0x0904, //!< [F] Wininet failure.

            ERR_MSP_CONFLICT_BETWEEN_MODS       = 0x0A01, //!< [F] A conflict between two or more modifications detected.
            ERR_MSP_WRONG_MOD_VECTOR            = 0x0A02, //!< [F] Variable mods vector cannot be applied to the peptide.
            ERR_MSP_MOD_MUST_BE_VAR             = 0x0A03, //!< [F] This modification can only be variable.
            ERR_MSP_MALFORMED_PEPTIDE           = 0x0A04, //!< [F] Empty or inconsistent peptide.
            ERR_MSP_DOUBLE_CHARGE_NOT_ALLOWED   = 0x0A05, //!< [F] Double charged ions are not allowed on this series.
            ERR_MSP_NO_ENZYME_SET               = 0x0A06, //!< [F] Cannot iterate peptides without enzyme specificity.
            ERR_MSP_EMPTY_MOD                   = 0x0A07, //!< [F] A modification is missing or incomplete (perhaps, wasn't found in configuration files).

            ERR_MSP_TAXONOMY_NO_TITLE           = 0x0B01, //!< [F] The first non-empty line is expected to be <code>Title:...</code>.
            ERR_MSP_TAXONOMY_NO_COMMAS          = 0x0B02, //!< [F] Comma is not allowed in taxonomy choice title.
            ERR_MSP_TAXONOMY_WRONG_LINE         = 0x0B03, //!< [F] Line cannot be parsed.
            ERR_MSP_TAXONOMY_DEFINITION_END     = 0x0B04, //!< [F] Missing end of taxonomy choice definition.
            ERR_MSP_TAXONOMY_CONFLICT_PARENTS   = 0x0B05, //!< [W] The parent for taxonomy id: %%d in %%s conflicts with another file.
            ERR_MSP_TAXONOMY_INVALID_NODE_FILE  = 0x0B06, //!< [F] Invalid line [%%s] in the node file [%%s].
            ERR_MSP_TAXONOMY_MISSING_NODE_FILE  = 0x0B07, //!< [F] The taxonomy node file [%%s] is missing.

            // Security related errors
            ERR_MSP_SECURITY_INVALID_SESSION_ID = 0x1000, //!< [W] An invalid session ID was passed to a function.
            ERR_MSP_SECURITY_NOT_ADMIN_SESSION  = 0x1001, //!< [W] Administrator rights are required to add, delete or change users or groups.
            ERR_MSP_SECURITY_DUPE_USER_ID       = 0x1002, //!< [W] Attempt to add a user with the same ID as an existing user.
            ERR_MSP_SECURITY_DUPE_USER_NAME     = 0x1003, //!< [W] Attempt to add a user with the same name as an existing user.
            ERR_MSP_SECURITY_BAD_USER_NAME      = 0x1004, //!< [W] Invalid user name - names cannot contain the following characters: \verbatim ><\\/`;,+*"\endverbatim
            ERR_MSP_SECURITY_USERNAME_NOT_FOUND = 0x1005, //!< [W] User name not found.
            ERR_MSP_SECURITY_USERID_NOT_FOUND   = 0x1006, //!< [W] User ID not found.
            ERR_MSP_SECURITY_DUPE_GROUP_ID      = 0x1007, //!< [W] Attempt to add a group with the same ID as an existing group.
            ERR_MSP_SECURITY_DUPE_GROUP_NAME    = 0x1008, //!< [W] Attempt to add a group with the same name as an existing group.
            ERR_MSP_SECURITY_BAD_GROUP_NAME     = 0x1009, //!< [W] Invalid group name - names cannot contain the following characters: \verbatim ><\\/`;,+*"\endverbatim
            ERR_MSP_SECURITY_GROUPNAME_NOT_FOUND= 0x100A, //!< [W] Group name not found.
            ERR_MSP_SECURITY_GROUPID_NOT_FOUND  = 0x100B, //!< [W] Group ID not found.
            ERR_MSP_SECURITY_FAIL_LOAD_SEC      = 0x100C, //!< [W] Failed to load the main security configuration files.
            ERR_MSP_SECURITY_FAIL_LOAD_USER     = 0x100D, //!< [W] Failed to load the user information for user.
            ERR_MSP_SECURITY_FAIL_LOAD_GROUP    = 0x100E, //!< [W] Failed to load the group information for group.
            ERR_MSP_SECURITY_DEL_SPECIAL_GROUP  = 0x100F, //!< [W] The special system group '%s' cannot be deleted.
            ERR_MSP_SECURITY_DEL_SPECIAL_USER   = 0x1010, //!< [W] The The special system user '%s' cannot be deleted.
            ERR_MSP_SECURITY_DISABLE_ADMIN      = 0x1011, //!< [W] The administrator user cannot be disabled.
            ERR_MSP_SECURITY_DEL_ADMIN_RIGHTS   = 0x1012, //!< [W] The administrator rights cannot be removed from the administrator group.
            ERR_MSP_SECURITY_DEL_ADMIN_FROM_GP  = 0x1013, //!< [W] The administrator user cannot be removed from the administrator group.
            ERR_MSP_SECURITY_DUPE_GROUP_NAME_U  = 0x1014, //!< [W] Failed to update group -- the name has been changed, and there is already another group with this name.
            ERR_MSP_SECURITY_DUPE_USER_NAME_U   = 0x1015, //!< [W] Failed to update user -- the name has been changed, and there is already another user with this name.
            ERR_MSP_SECURITY_OLD_PW_INVALID     = 0x1016, //!< [W] The old password is not correct and you don't have administrator rights. Password not changed.
            ERR_MSP_SECURITY_NO_GUEST_PWD       = 0x1017, //!< [W] You cannot set a password for the guest user.
            ERR_MSP_SECURITY_PASSWORD_TOO_SHORT = 0x1018, //!< [W] The password entered is too short. Minimum length required is defined in the options.
            ERR_MSP_SECURITY_FAIL_SAVE_SEC      = 0x1019, //!< [W] Failed to save the main security configuration files.
            ERR_MSP_SECURITY_NO_RIGHTS_UPD_USR  = 0x101A, //!< [W] Insufficient acccess rights to update user profile for %%s.
            
            ERR_MSP_SECURITY_INVALIDUSER        = 0x1101, //!< [F] No such user.
            ERR_MSP_SECURITY_INVALIDPASSWORD    = 0x1102, //!< [F] You have entered an invalid password, %%s - please try again [ec=%%d].
            ERR_MSP_SECURITY_MISSINGSESSIONFILE = 0x1103, //!< [F] Can't find the session file requested.
            ERR_MSP_SECURITY_SAVESESSIONFILE    = 0x1104, //!< [F] Can't save a new session file.
            ERR_MSP_SECURITY_TIMEDOUT           = 0x1105, //!< [F] Session has timed out.
            ERR_MSP_SECURITY_DIFFERENTIPADDR    = 0x1106, //!< [F] The option to check IP address is enabled and this is an attempt to open this session from a different ip address.
            ERR_MSP_SECURITY_PASSWORDEXPIRED    = 0x1107, //!< [F] Your password has expired, %%s - please enter a new password.
            ERR_MSP_SECURITY_NOTLOGGEDIN        = 0x1108, //!< [F] User is not logged in, so could not create a session.
            ERR_MSP_SECURITY_INVALIDSESSION     = 0x1109, //!< [F] The current session is invalid, but an attempt has been made to save parameters.
            ERR_MSP_SECURITY_NOSAVEPARAMS       = 0x110A, //!< [F] Cannot save parameters in this type of sessin (e.g. guest user, no authentication enabled).
            ERR_MSP_SECURITY_SESSIONDESTROYED   = 0x110B, //!< [D1]The session has been terminated and is therefore no longer valid.
            ERR_MSP_SECURITY_NOCREATEGUEST      = 0x110C, //!< [F] Unable to create a guest session.
            ERR_MSP_SECURITY_UPDATESESS         = 0x110D, //!< [F] Unable to update session %%s. Error %%s.
            ERR_MSP_SECURITY_ACCOUNT_DISABLED   = 0x110E, //!< [F] Cannot login, your account (%%s) has been disabled.
            ERR_MSP_SECURITY_NOTNORMALUSER      = 0x110F, //!< [F] Cannot login as this user (%%s) because the user type is not a 'normal' or 'integra' user.
            ERR_MSP_SECURITY_SAMEPASSWORD       = 0x1110, //!< [F] Cannot change your password to the same as the previous password.
            ERR_MSP_SECURITY_SPOOFATTEMPT1      = 0x1111, //!< [F] Trying to use session %%s from computer with ip address %%s - please contact your administrator.
            ERR_MSP_SECURITY_SPOOFATTEMPT2      = 0x1112, //!< [F] Trying to use session %%s when logged into the web server as %%s - please contact your administrator.
            ERR_MSP_SECURITY_SPOOFATTEMPT3      = 0x1113, //!< [F] Trying to use session %%s (USER_AGENT_STRING) when agent string is actually %%s - please contact your administrator.
            ERR_MSP_SECURITY_SPOOFATTEMPT4      = 0x1114, //!< [F] Trying to use command line session %%s from a cgi application at ip address %%s - please contact your administrator.
            ERR_MSP_SECURITY_FAILGETINTEGRAURL  = 0x1115, //!< [W] Failed to contact Integra server. See previous error in log file.
            ERR_MSP_SECURITY_FAILGETINTEGRA     = 0x1116, //!< [W] Failed to get any integra users - return from Integra server was: %%s.
            ERR_MSP_SECURITY_INTEGRACONNFORMAT  = 0x1117, //!< [F] Integra connection id (%%s) is not of the format expected.
            ERR_MSP_SECURITY_INTEGRAINVALIDPW   = 0x1118, //!< [F] Integra password is probably invalid. Error return is: %%s
            ERR_MSP_SECURITY_INTEGRAINVALIDCO   = 0x1119, //!< [F] Integra connection id has probably timed out. Error return is: %%s
            ERR_MSP_SECURITY_LOADSESSIONFILE    = 0x111A, //!< [F] The session file exists but we can't load it.
            ERR_MSP_SECURITY_NOT_INTEGRA_USER   = 0x111B, //!< [F] The 'type' of user (%%s) needs to be changed to a Mascot Integra user using the Mascot security administration application.
            ERR_MSP_SECURITY_NOT_ENABLED        = 0x111C, //!< [F] There is no need to login to this server - Mascot security is not enabled.
            ERR_MSP_SECURITY_NO_INTEGRA_LOGIN   = 0x111D, //!< [F] You cannot login from this screen. Please login to Mascot Integra first, and access the Mascot Home Page from there.
            ERR_MSP_SESSION_UTIME_FAIL          = 0x111E, //!< [W] Failed to update last modified time for %s. Error: %s


            // Security logging messages
            ERR_MSP_SECURITY_ADDUSER            = 0x1200, //!< [I] Add user: name=%%s, id=%%d, type=%%d, enabled=%%d.
            ERR_MSP_SECURITY_DELUSER            = 0x1201, //!< [I] Delete user: name=%%s, id=%%d.
            ERR_MSP_SECURITY_UPDATEUSER         = 0x1202, //!< [I] Update user %%d: [old,new]. Name=[%%s,%%s], FullName=[%%s,%%s], pw_exp=[%%ld,%%ld], type=[%%d,%%d], email=[%%s,%%s], enabled=[%%d,%%d].
            ERR_MSP_SECURITY_UPDATEPW           = 0x1203, //!< [I] Update user password for user id %%d, name %%s. Old p/w expiry=%%ld. New p/w expiry=%%ld.
            ERR_MSP_SECURITY_ADDGROUP           = 0x1204, //!< [I] Add group: name=%%s, id=%%d.
            ERR_MSP_SECURITY_DELETEGROUP        = 0x1205, //!< [I] Delete group: name=%%s, id=%%d.
            ERR_MSP_SECURITY_UPDATEGROUP        = 0x1206, //!< [I] Updated group: id=%%d, old name=%%s, new name=%%s, added users=%%s, deleted users=%%s. Changed tasks=%%s.

            ERR_MSP_SECURITY_GETALLGROUPIDS     = 0x1300, //!< [D3] Called getAllGroupIDs - list of %%d ids returned.
            ERR_MSP_SECURITY_GETGROUPOK         = 0x1301, //!< [D3] Called ms_security::getGroup(%%s) - returned successfully.
            ERR_MSP_SECURITY_GETGROUPFAIL       = 0x1302, //!< [D3] Called ms_security::getGroup(%%s) - group not found.
            ERR_MSP_SECURITY_GETUSEROK          = 0x1303, //!< [D3] Called ms_security::getUser('%%s') - returned successfully.
            ERR_MSP_SECURITY_GETUSERFAIL        = 0x1304, //!< [D3] Called ms_security::getUser('%%s') - user not found.
            ERR_MSP_SECURITY_GETUSERFROMIDOK    = 0x1305, //!< [D3] Called ms_security::getUserFromID('%%d') - returned user (%%s) successfully.
            ERR_MSP_SECURITY_GETUSERFROMIDFAIL  = 0x1306, //!< [D3] Called ms_security::getUserFromID('%%d') - user id not found..
            ERR_MSP_SECURITY_GETALLUSERIDS      = 0x1307, //!< [D3] Called ms_security::getAllUserIDs - list of %%d ids returned.
            ERR_MSP_SECURITY_GETGROUPFROMIDOK   = 0x1308, //!< [D3] Called ms_security::getGroupFromID('%%d') - returned group (%%s) successfully.
            ERR_MSP_SECURITY_GETGROUPFROMIDFAIL = 0x1309, //!< [D3] Called ms_security::getGroupFromID('%%d') - group id not found.
            ERR_MSP_SECURITY_GETPERMTASKSOK     = 0x130A, //!< [D3] Called ms_security::getPermittedTasksForUser('%%s') - user name found, %%d tasks returned.
            ERR_MSP_SECURITY_GETPERMTASKSFAIL   = 0x130B, //!< [D3] Called ms_security::getPermittedTasksForUser('%%s') - user name  not found.
            ERR_MSP_SECURITY_UPDATEALLSESSFILES = 0x130C, //!< [D3] Called ms_security::updateAllSessionFiles - %%d files potentially updated.
            ERR_MSP_SECURITY_GETINTEGRAUSERS1   = 0x130D, //!< [D3] Called ms_security::getIntegraUsers - url %%s.
            ERR_MSP_SECURITY_GETINTEGRAUSERS2   = 0x130E, //!< [D3] Called ms_security::getIntegraUsers - return string from URL is: %%s.
            ERR_MSP_SECURITY_GETINTEGRAUSERS3   = 0x130F, //!< [D3] Called ms_security::getIntegraUsers - list of %%d ids returned.
            ERR_MSP_SECURITY_VALIDATEINTEGRAPW1 = 0x1310, //!< [D3] Called ms_security_session::validateIntegraPassword - url %%s.
            ERR_MSP_SECURITY_VALIDATEINTEGRAPW2 = 0x1311, //!< [D3] Called ms_security_session::validateIntegraPassword - return string from URL is: %%s.
            ERR_MSP_SECURITY_VALIDATEINTEGRAPW3 = 0x1312, //!< [D3] Called ms_security_session::validateIntegraPassword - OK.
            ERR_MSP_SECURITY_VALIDATEINTEGRACO1 = 0x1313, //!< [D3] Called ms_security_session::verifyIntegraConnection - url %%s.
            ERR_MSP_SECURITY_VALIDATEINTEGRACO2 = 0x1314, //!< [D3] Called ms_security_session::verifyIntegraConnection - OK.

            ERR_MSP_SECURITY_MSSESSIONCTOR      = 0x1330, //!< [D3] Called ms_session::ms_session(%%s).
            ERR_MSP_SECURITY_MSSESSCOOKIE       = 0x1331, //!< [D3] Cookie from ms_session::ms_session() = %%s.
            ERR_MSP_SECURITY_LOADINGSESS        = 0x1332, //!< [D3] Loading session (%%s).
            ERR_MSP_SECURITY_MSSESSLOGIN        = 0x1333, //!< [I]  Called ms_session::ms_session(%%s,[password]).
            ERR_MSP_SECURITY_MSSESSINTEGRA      = 0x1334, //!< [I]  Called ms_session::ms_session(%%s,[connectionID],%%s).
            ERR_MSP_SECURITY_MSSESSRESULTSPERM  = 0x1335, //!< [D3] Called ms_session::canResultsFileBeViewed(%%d - return %%d).

            ERR_MSP_QUANT_FAILEDLOAD            = 0x1400, //!< [W] Failed to load quantitation configuration file.
            ERR_MSP_QUANT_FAILEDSAVE            = 0x1401, //!< [W] Failed to save quantitation configuration file.

            ERR_MSP_UMOD_FAILEDLOAD             = 0x1500, //!< [W] Failed to load unimod.xml file.
            ERR_MSP_UMOD_FAILEDSAVE             = 0x1501, //!< [W] Failed to save unimod.xml file.

            ERR_MSP_XMLSCHEMA_FAILEDLOAD        = 0x1600, //!< [W] Failed to load xml schema file.
            ERR_MSP_XMLSCHEMA_FAILEDSAVE        = 0x1601, //!< [W] Failed to save xml schema file.

            ERR_MSP_ZIP_ENDIAN                  = 0x1700, //!< [W] Failed to determine endian - assuming little endian.
            ERR_MSP_ZIP_OUTOFMEMORY             = 0x1701, //!< [F] Insufficient memory to compress/decompress the data.
            ERR_MSP_ZIP_BUFTOOSMALL             = 0x1702, //!< [F] Buffer too small - [value].
            ERR_MSP_ZIP_INVALIDDATA             = 0x1703, //!< [F] Compressed data is invalid.
            ERR_MSP_ZIP_UNKNOWN                 = 0x1704, //!< [F] Unknown zip error
            ERR_MSP_ZIP_LENGTHDIFFERS           = 0x1705, //!< [F] Expected length of uncompressed data ([value]) differs from actual length [value].
            ERR_MSP_ZIP_UNCOMPRESSEDDATATOOLONG = 0x1706, //!< [F] Uncompressed data longer ([value]) than maximum permitted ([value]).
            ERR_MSP_ZIP_ZEROLENGTH              = 0x1707, //!< [F] Cannot compress zero length data.

            // Installer logging messages
            ERR_MSP_INST_LOGICALPERPHYSICALCPU  = 0x1800, //!< [I] Detected %%d logical processors per physical CPU.
            ERR_MSP_INST_INVALIDCPUINFO         = 0x1801, //!< [W] Failed to get CPU information. Assuming single core and no hyper-threading.
            ERR_MSP_INST_NUMTHREADS             = 0x1802, //!< [I] Setting number of threads to [value].
            ERR_MSP_INST_NOTMASCOTDATOK         = 0x1803, //!< [I] Successfully loaded not.mascot.dat from [path].
            ERR_MSP_INST_MASCOTDATOK            = 0x1804, //!< [I] Successfully loaded existing mascot.dat from [path].
            ERR_MSP_INST_APPENDTAXONOMY         = 0x1805, //!< [I] Appended taxonomy section [value].
            ERR_MSP_INST_ADDINGSWISSPROT        = 0x1806, //!< [I] Adding entry for SwissProt to <tt>mascot.dat</tt>.
            ERR_MSP_INST_MISSINGSWISSPROT       = 0x1807, //!< [W] No entry for SwissProt found in <tt>not.mascot.dat</tt>.
            ERR_MSP_INST_CHANGEDBTAXONOMY       = 0x1808, //!< [I] Changed taxonomy entry for [database].
            ERR_MSP_INST_APPENDPARSERULE        = 0x1809, //!< [I] Added parse rule number [value].
            ERR_MSP_INST_FAILADDPARSERULE       = 0x180A, //!< [F] Failed to added parse rule required for SwissProt - too many rules already in <tt>mascot.dat</tt>?
            ERR_MSP_INST_ADDOPTIONSSECTION      = 0x180B, //!< [I} Added [value] to the options section of <tt>mascot.dat</tt>.
            ERR_MSP_INST_SETOPTIONSSECTION      = 0x180C, //!< [I] Set the value of [name] to [value] in the options section of <tt>mascot.dat</tt>.
            ERR_MSP_INST_CLUSTERSECTION         = 0x180D, //!< [I] Set the value of [name] to [value] in the cluster section of <tt>mascot.dat</tt>.
            ERR_MSP_INST_CANTGETHOSTNAME        = 0x180E, //!< [F] Failed to determine the hostname.
            ERR_MSP_INST_NOSUBCLUSTERSET        = 0x180F, //!< [F] No SubClusterSet entry in <tt>mascot.dat</tt>.
            ERR_MSP_INST_CHANGEPARSERULE        = 0x1810, //!< [i] Changed [value] parse rule from rule [value] to [value].

            ERR_MSP_LASTONE                     = 0xFFFFFFFF
        }; // enum err

    public:
        //! Constructor 
        ms_errs();

        //! Returns <b>TRUE</b> if no errors occurred, <b>FALSE</b> otherwise.
        bool isValid() const;

#ifndef SWIG
        // Internal usage only
        void setError(const int error, ...);

        // Internal usage only
        void setError(const std::string sessionID,
                      const int userID,
                      const int error, ...);

        // Internal usage only
        void setErrorPlusErrno(const int errorNumber, ...);

        bool anyIoErrors(FILE *f) const;
#endif

        //! Copies all errors from another instance and appends them at the end of own list.
        void appendErrors(const ms_errs& src);

        //! Return the number of errors since the last call to #clearAllErrors().
        /*!
        * This will be zero if there has been no error. 
        *
        * All errors are accumulated into a list in this object, until
        * #clearAllErrors() is called.  
        *
        * Errors in other classes are accumulated here. If, for example, there
        * is an error when creating a peptide summary, the errors need to be
        * accessed through this class.
        *
        * See \ref Errs.
        *
        * \sa #clearAllErrors(), #getErrorNumber() and #getErrorString()
        */
        int  getNumberOfErrors() const;

        //! Remove all errors from the current list of errors.
        /*!
        * All errors are accumulated into a list in this object, until
        * this function is called.
        *
        * \sa #getNumberOfErrors()
        */
        void clearAllErrors();

        //! Return a specific error number or #ERR_NO_ERROR.
        /*!
         *  To return a particular error, call this function with a number
         *  1..#getNumberOfErrors(). 
         *  Passing a value of <b>-1</b> will return the last error, or
         *  <b>ERR_NO_ERROR</b>. 
         *
         *  If an invalid number is passed, <b>ERR_NO_ERROR</b> will be
         *  returned.
         */
        int  getErrorNumber(const int num = -1) const;

        //! Return the last error number or #ERR_NO_ERROR if no errors.
        int  getLastError() const;

        //! Returns a specific error as a string.
        /*!
         *  To return a particular error string, call this function with
         *  a number 1..#getNumberOfErrors(). 
         *  Passing a value of <b>-1</b> will return the last error, or an
         *  empty string. 
         *
         *  If an invalid number is passed, an empty will be returned.
         */
        std::string getErrorString(const int num) const;

        //! Return the last error number or an empty string.
        /*!
        * Same as calling #getErrorString() with -1 as a parameter.
        */
        std::string getLastErrorString() const;

        //! Returns 0 for warnings and 1 and more for fatal errors.
        msg_sev getErrorSeverity(const int num = -1) const;

        //! Returns a number of time the error has been repeated.
        int getErrorRepeats(const int num = -1) const;

        //! Set the logging file.
        void setLoggingFile(const std::string filename,
                            const msg_sev level);

        //! Get the logging level.
        msg_sev getLoggingLevel() const;

        //! Set the logging level.
        void setLoggingLevel(const msg_sev level);

        //! Get the flag for whether to merge repeated error messages.
        bool getCombineRepeats() const;

        //! Set the flag for whether to merge repeated error messages.
        void setCombineRepeats(const bool flag);

    private:
        
        void _setError(const int error, const char* strBuffer, 
                       const msg_sev severity,
                       const std::string & sessionID = "",
                       const int userID = -1);
        std::string _lookUpError(const int error, ms_errs::msg_sev & severity);

        std::vector<int> errorNumbers_;
        std::vector<std::string>errorStrings_; // Will include 'user' data
        std::vector<msg_sev> errorSeverities_;
        std::vector<int> errorRepeats_;        // So that we don't get multiple errors
        std::string logfileName_;
        msg_sev loglevel_;
        bool combineRepeats_;
    }; // class ms_errs



    //! This class is used as a base class for several Mascot Parser classes.
    class MS_MASCOTRESFILE_API ms_errors
    {
    public:
        //! Default constructor.
        ms_errors();

        //! Destructor.
        ~ms_errors();

        //! Copies all errors from another instance and appends them at the end of own list.
        void appendErrors(const ms_errors& src);

        //! Remove all errors from the current list of errors.
        void clearAllErrors();

        //! Call this function to determine if there have been any errors.
        bool isValid() const;

        //! Return the error description of the last error that occurred.
        std::string getLastErrorString() const;

        //! Return the error description of the last error that occurred.
        int getLastError() const;

        //! Retrive the error object using this function to get access to all errors and error parameters.
        const ms_errs* getErrorHandler() const;

        ms_errs* getErrorHandler();

    protected:
        ms_errs   err_; // error object that does all the work
    };
    /** @} */ // end of common_group
} // namespace matrix_science

#endif // MS_ERRORS_HPP

/*------------------------------- End of File -------------------------------*/
