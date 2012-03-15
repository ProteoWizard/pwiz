/*
##############################################################################
# file: ms_mascotresfile.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates a  mascot results file                                        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: davidc $ #
#       $Date: 2011-03-09 17:08:00 $ #
#   $Revision: 1.68 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESFILE_HPP
#define MS_MASCOTRESFILE_HPP


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
#include <varargs.h>
#else
#include <stdarg.h>
#endif


// Includes from the standard template library
#include <time.h>
#include <string>
#include <list>
#include <vector>
#include <set>

namespace msparser_internal {
    class ms_cdb;
    class ms_peptidesumcdb;
}

namespace matrix_science {
    class ms_searchparams;

    /** @addtogroup resfile_group
     *  
     *  @{
     */

#ifndef SWIG
    class multiBuf {
    public:
        multiBuf()  {pMem_ = 0; len_ = 0; pEnd_ = 0;}
    public :
        char *pMem_;  // pointer to first block of memory
        int  len_;    // excluding any null terminator
        char *pEnd_;  // Pointer to last byte (not the null terminator)
    private:
    };
    typedef std::vector<multiBuf> multiBuf_v;  

    class multiBufMemPtr {
    public:
        enum MBMP { MBMP_INVALID = -1, MBMP_USING_CDB = -2 };
        multiBufMemPtr(int bufNum, char *pMem)
            : bufNum_(bufNum), pMem_(pMem) {};
        multiBufMemPtr() : bufNum_(MBMP_INVALID), pMem_(0) {};
        void decrement(const multiBuf_v & buffers);
        void decrementUntil(const multiBuf_v & buffers, const char * chars);
        void increment(const multiBuf_v & buffers);
        bool isValid() const {return bufNum_ != MBMP_INVALID;}
        bool operator<(const multiBufMemPtr & rhs);
            
    public:
        int    bufNum_;
        char * pMem_;
    };

    // ms_sortByKeyCriterion class is used internally in the library
    // to give fast access to the keys. Don't use it from outside the DLL
    class ms_sortByKeyCriterion
    {
        public:
            enum CMP_MODE {CASE_INSENSITIVE=0x01, CASE_SENSITIVE=0x02, QUOTED=0x04};
            ms_sortByKeyCriterion(int m=(int)CASE_INSENSITIVE) : mode_(m) {}
            bool operator() (const char * p1, const char * p2) const;

        private:
            int mode_;
            // Unfortunately, when called from Perl in Windows, 
            // the toupper function ends up calling extra functions
            // to convert to wide char and back again. We don't
            // want language independent conversion here - the
            // key names are all single byte.
            inline char my_toupper(char ch) const
            {
                if (mode_ & CASE_INSENSITIVE && ch >= 'a' && ch <= 'z')
                    return 'A' + (ch-'a');
                else
                    return ch;
            }

    };
#endif

    //! The first requirement before using any other functions or classes is to create an ms_mascotresfile object.
    /*!
     * You must create an object of this class before anything else. 
     * The constructor takes the file name as a parameter. Note that all
     * key names are case insensitive.
     */
    class MS_MASCOTRESFILE_API ms_mascotresfile
    {
        friend class msparser_internal::ms_peptidesumcdb;
        public:
            //! Section names in the standard mascot results files.
            /*!
             * See \ref DynLangEnums.
             */
            // Developer note: Add new sections to this enum just before
            // SEC_INDEX just in case a perl programmer has used constant
            // numbers instead of SEC_ values in their code.
            enum section 
            { 
                SEC_PARAMETERS,       //!< parameters section 
                SEC_HEADER,           //!< header section 
                SEC_MASSES,           //!< masses section
                SEC_SUMMARY,          //!< summary section
                SEC_MIXTURE,          //!< mixture section (pmf mixture)
                SEC_PEPTIDES,         //!< peptides section
                SEC_PROTEINS,         //!< proteins section
                SEC_QUERY1,           //!< query1 section. Don't use, see getQuerySectionValueStr() etc.
                SEC_QUANTITATION,     //!< quantitation section
                SEC_UNIMOD,           //!< unimod section
                SEC_ENZYME,           //!< enzyme section
                SEC_TAXONOMY,         //!< taxonomy section
                SEC_DECOYSUMMARY,     //!< decoy_summary section.  See also \ref decoySearchPage.
                SEC_DECOYMIXTURE,     //!< decoy_mixture section.  See also \ref decoySearchPage.
                SEC_DECOYPEPTIDES,    //!< decoy_peptides section. See also \ref decoySearchPage.
                SEC_DECOYPROTEINS,    //!< decoy_proteins section. See also \ref decoySearchPage.
                SEC_ERRTOLSUMMARY,    //!< error tolerant summary section.  See also \ref errorTolerantPage.
                SEC_ERRTOLPEPTIDES,   //!< error tolerant peptides section. See also \ref errorTolerantPage.
                SEC_INDEX,            //!< index section
                SEC_NUMSECTIONS       //!< !!don't use - place holder
            };

            //! Definitions for error numbers.
            /*!
             * See \ref DynLangEnums.
             * Messages are classified as fatal errors [F] or warnings [W]. A warning
             * will not cause ms_mascotresfile::isValid() to return false.
             */
            enum err
            {
                ERR_NO_ERROR                        = 0x0000, //!< [W] Success
                ERR_NOMEM                           = 0x0001, //!< [F] Failed to allocate memory to load the file
                ERR_NOSUCHFILE                      = 0x0002, //!< [F] The file passed in the constructor does not exist
                ERR_READINGFILE                     = 0x0003, //!< [F] Opened the file successfully, but failed to read from it
                ERR_QUERYOUTOFRANGE                 = 0x0004, //!< [F] Set if query < 1 or query > getNumQueries
                ERR_MISSINGENTRY                    = 0x0005, //!< [F] Set if there is no qexp value in the file
                ERR_PEPSUMMPEPGET                   = 0x0006, //!< [F] Value of q, p or h out of range, so cannot get peptide info
                ERR_PEPTIDESTR                      = 0x0007, //!< [F] The string in the peptides block is not valid
                ERR_ACCINPEPTIDESTR                 = 0x0008, //!< [F] Could not parse an item for a given accession in the peptide section
                ERR_PROTSUMM                        = 0x0009, //!< [F] Error parsing a line in the protein summary
                ERR_PROTSUMMPEP                     = 0x000A, //!< [F] Couldn't parse peptide information from the protein summary section
                ERR_ADDPEPTIDES                     = 0x000B, //!< [F] Failed to add peptides when creating the peptide summary
                ERR_MISSINGHIT                      = 0x000C, //!< [F] Missing hit in the summary section
                ERR_MISSINGSECTION                  = 0x000D, //!< [F] Complete missing section in the file
                ERR_MISSINGSECTIONEND               = 0x000E, //!< [F] Missing end of section in the file
                ERR_MALFORMED_ERR_TOL               = 0x000F, //!< [W] Expecting a line of format: q1_p2_et_mods=0.984020,0.000000,Citrullination
                ERR_NO_ERR_TOL_PARENT               = 0x0010, //!< [F] No parent search file. See \ref errorTolerantPage
                ERR_NULL_ACC_PEP_SUM                = 0x0011, //!< [W] An empty accession string has been found. Possible problem in database
                ERR_NULL_ACC_PROT_SUM               = 0x0012, //!< [W] An empty accession string has been found. Possible problem in database
                ERR_DUPE_ACCESSION                  = 0x0013, //!< [W] A possible duplicate accession string has been found. Possible problem in database.
                ERR_UNASSIGNED_PROG                 = 0x0014, //!< [F] Programming error! Calling getNumberOfUnassigned() or getUnassigned() before createUnassignedList()
                ERR_UNASSIGNED_RANGE                = 0x0015, //!< [F] Calling ms_mascotresults::getUnassigned() with out of range number
                ERR_UNASSIGNED_UNK                  = 0x0016, //!< [F] Calling ms_mascotresults::getUnassigned() - unable to retrieve value
                ERR_NO_UNIGENE_FILE                 = 0x0017, //!< [F] Failed to open the UniGene file specified
                ERR_DUPLICATE_KEY                   = 0x0018, //!< [W] Duplicate entries with the same key in the named section.
                ERR_OLDRESULTSFILE                  = 0x0019, //!< [F] Very old results file (last century!). Parser requires 1.02 or later
                ERR_MALFORMED_TAG                   = 0x001A, //!< [W] Expecting a line in format: q1_p2_tag=1:3:5:6,2:4:12:6,...
                ERR_MALFORMED_DRANGE                = 0x001B, //!< [W] Expecting a line in format: q1_p2_drange=0,256
                ERR_INVALID_NUMQUERIES              = 0x001C, //!< [W] Invalid number of queries in results file has been corrected.
                ERR_MALFORMED_TERMS                 = 0x001D, //!< [W] Expecting a line in format: q1_p2_terms=A,B:-,I:...
                ERR_INVALID_RESFILE                 = 0x001E, //!< [F] Invalid results file format - missing or corrupt headers
                ERR_INVALID_PROTDB                  = 0x001F, //!< [W] Invalid h1_db-string format. Expecting an integer number.
                ERR_UNIGENE_MULTIDB                 = 0x0020, //!< [W] UniGene index is not supported in multi-database search
                ERR_INVALID_CACHE_DIR               = 0x0021, //!< [F] Must specify a cache directory if using CDB cache files
                ERR_FAIL_OPEN_DAT_FILE              = 0x0022, //!< [F] Failed to open the results file for reading
                ERR_MISSING_CDB_FILE                = 0x0023, //!< [W] Cache file is missing or cannot be opened
                ERR_FAIL_MK_CACHE_DIR               = 0x0024, //!< [F] Failed to create cache directory for cache files
                ERR_FAIL_MK_CDB_FILE                = 0x0025, //!< [W] Failed to create an cache file
                ERR_FAIL_CLOSE_FILE                 = 0x0026, //!< [W] Failed to close file
                ERR_FAIL_CDB_INIT                   = 0x0027, //!< [W] Failed to initialise cache file (%%s). Error code %%d.
                ERR_INVALID_CDB_FILE                = 0x0028, //!< [W] Value in cdb cache file (%%s) is corrupt: %%s
                ERR_WRITE_CDB_FILE                  = 0x0029, //!< [W] Failed to write to the cache file (%%s). Error %%d (%%s)
                ERR_CDB_TOO_LARGE                   = 0x002A, //!< [W] Cannot use cache file (%%s). Maximum size permitted is %%s
                ERR_NEED_64_BIT                     = 0x002B, //!< [F] This results file (%%s) is too large for 32 bit Mascot Parser. Please upgrade to 64 bit.
                ERR_CDB_64_BIT_REMAKE               = 0x002C, //!< [W] Re-creating %%s. Was too large for 32 bit, but may succeed with 64 bit
                ERR_CDB_OLD_VER_RETRY               = 0x002D, //!< [W] Cache file %%s is an old version. Creating new cache file
                ERR_CDB_OLD_VER_NO_RETRY            = 0x002E, //!< [W] Cache file %%s is an old version. Continuing without cache
                ERR_CDB_INCOMPLETE_RETRY            = 0x002F, //!< [W] Cache file %%s was not complete. Re-creating the cache file
                ERR_CDB_INCOMPLETE_NO_RETRY         = 0x0030, //!< [W] Cache file %%s was not complete.  Continuing without cache
                ERR_CDB_BEING_CREATED               = 0x0031, //!< [W] Cache file %%s being created by another task. Continuing without cache
                ERR_CDB_FAIL_REMOVE                 = 0x0032, //!< [W] Failed to remove old cache file %%s - error %%s. Continuing without cache
                ERR_CDB_FAIL_LOCK                   = 0x0033, //!< [W] Failed to lock cache file %%s. Error code: %%d
                ERR_CDB_FAIL_UNLOCK                 = 0x0034, //!< [W] Failed to unlock cache file %%s. Error code: %%d
                ERR_CDB_SOURCE_CHANGE_RETRY         = 0x0035, //!< [W] %%s changed. %%s (was %%s), %%s bytes (was %%s). Re-creating the cache file
                ERR_CDB_SOURCE_CHANGE_NO_RETRY      = 0x0036, //!< [W] %%s changed. %%s (was %%s), %%s bytes (was %%s). Continuing without cache
                ERR_MISSING_PERCOLATOR_FILE         = 0x0037, //!< [F] Percolator file %%s is missing. Cannot continue
                ERR_CANNOT_APPEND_RESFILE           = 0x0038, //!< [F] The file %%s cannot be appended to %%s because %%s values are different
                ERR_CANNOT_APPEND_RESFILE_NO_FNAMES = 0x0039, //!< [F] The file cannot be appended because %%s values are different
                ERR_RESULTS_NOT_CREATED             = 0x003A, //!< [W] Attempting to call function %%s before createSummary() has completed.
                ERR_LASTONE                         = 0x003B
            };
            // this list has to be included into ms_errors.hpp file too !!!

            
            //! Flags for opening the results file.
            /*!
             * See \ref DynLangEnums and \ref indexAndCache.
             *
             */
            enum FLAGS 
            { 

                RESFILE_NOFLAG                 = 0x00000000, //!< The standard original functionality. Read the whole file into memory.
                RESFILE_USE_CACHE              = 0x00000001, //!< Create the cache if it doesn't already exist. Use the cache rather than reading the whole .dat file into memory.
                RESFILE_CACHE_IGNORE_ACC_DUPES = 0x00000002, //!< When <b>creating</b> a cache file, don't check for duplicate accessions in the SEC_PROTEINS and SEC_DECOYPROTEINS sections which can save some time. Strongly recommend that this flag is never used unless performance becomes a real issue and it is known that ms_mascotoptions::getIgnoreDupeAccession was not defined for the relevant database(s) when they were compressed.
                RESFILE_USE_PARENT_PARAMS      = 0x00000004  //!< For use when \ref combiningMultipleDatFiles. The flags and parameters are then inherited from the parent search.
            };

            //! Offsets into a vector of Percolator filenames.
            /*!
             * See \ref DynLangEnums.
             *
             * Used with getPercolatorFileNames().
             */
            enum PERCOLATOR_FILE_NAMES
            {
                PERCOLATOR_INPUT_FILE       = 0,  //!< This file is created by ms-createpip.exe and read by percolator
                PERCOLATOR_OUTPUT_TARGET    = 1,  //!< From std::out of percolator.exe
                PERCOLATOR_OUTPUT_DECOY     = 2   //!< Specified using the -B flag when calling percolator
            };

            //! Processing some results files is computationally intensive. These are the tasks that can be performed.
            /*!
             * See \ref DynLangEnums.
             *
             * Used with getKeepAlive(), but also see outputKeepAlive()
             */
            enum KA_TASK
            {
                KA_CREATEINDEX_CI    = 0,  //!< Creating a cache file when \ref usingIndexAndCache
                KA_READFILE_RF       = 1,  //!< Reading the results file into memory when not using a cache
                KA_ASSIGNPROTEINS_AP = 2,  //!< Assigning peptides to proteins to get a list of all possible proteins
                KA_GROUPPROTEINS_GP  = 3,  //!< Grouping proteins using ms_mascotresults::MSRES_GROUP_PROTEINS or ms_mascotresults::MSRES_CLUSTER_PROTEINS
                KA_UNASSIGNEDLIST_UL = 4,  //!< Creating the unassigned list - see ms_mascotresults::createUnassignedList
                KA_CREATECACHE_CC    = 5,  //!< Creating a cache file when \ref Caching_peptide_summary
                KA_LAST              = 6   //!< Placeholder that is equal to the number of possible tasks
            };


            //! Constructor to open a Mascot results file.
            ms_mascotresfile(const char * szFileName,
                             const int    keepAliveInterval = 0,
                             const char * keepAliveText = "<!-- %d seconds -->\n",
                             const unsigned int flags = RESFILE_NOFLAG,
                             const char * cacheDirectory = "../data/cache/%Y/%m");

            ~ms_mascotresfile();

            // ------------------- Basic generic functions -------------------
            
            //! Returns the version number of the Mascot Parser library.
            std::string getMSParserVersion() const;
            
            //! Compare the value returned by getMascotVer() with the passed version number.
            bool versionGreaterOrEqual(int major, int minor, int revision) const;

            //! Multiple results files can be summed together and treated as 'one'.
            int appendResfile(const char * filename,
                              int flags=RESFILE_USE_PARENT_PARAMS,
                              const char * cacheDirectory = 0);  // returns 'id' of added file

            //! Returns a pointer to the resfile created by calling appendResfile.
            const ms_mascotresfile * getResfile(int id) const;

            //! Multiple results files can be summed together and treated as 'one'.
            int getNumberOfResfiles() const; 

            //! Returns true if there is an entry for the passed section.
            bool doesSectionExist(const section sec) const;

            //! Returns true if there is a peptides section, and if there are <I>any</I> results in it.
            bool anyPeptideSummaryMatches(const section sec=SEC_PEPTIDES) const;

            // When calling 'getSectionValue' from outside the dll, be careful
            // that enough space is 'reserved' in the string.
            // Return value is length of the actual string that it wanted to
            // to return, so if this is larger than maxLen, then you are 
            // missing some of the string. Best to call getSectionValueStr
            //! Return the string value from any line in the results file.
            int  getSectionValue(const section sec, const char * key, char * str, int maxLen) const;

            //! Return the integer value from any line in the results file.
            int  getSectionValueInt(const section sec, const char * key) const;

            //! Return the floating point value from any line in the results file.
            double getSectionValueDouble(const section sec, const char * key) const;

            //! Return the string value from any line in the results file.
            std::string getSectionValueStr(const section sec, const char * key) const;

            //! Return the string value from a query in the results file.
            int  getQuerySectionValue(const int query, const char * key, char * str, int maxLen) const;

            //! Return the integer value from a query in the results file.
            int  getQuerySectionValueInt(const int query, const char * key) const;

            //! Return the floating point value from a query in the results file.
            double getQuerySectionValueDouble(const int query, const char * key) const;

            //! Return the string value from a query in the results file.
            std::string getQuerySectionValueStr(const int query, const char * key) const;

            //! Return the job number for this file - obtained from the file name.
            int getJobNumber(const int resfileID = 1) const;

            //! Get the key name for each item in a section.
            std::string enumerateSectionKeys(const section sec, 
                                             const int num,
                                             int        * pPreviousNum    = 0,
                                             OFFSET64_T * pPreviousOffset = 0) const;

            //! Get the key name for each item in a query section.
            std::string enumerateQuerySectionKeys(const int query,
                                                  const int num,
                                                  int        * pPreviousNum    = 0,
                                                  OFFSET64_T * pPreviousOffset = 0) const;


            //! Has the file loaded properly?
            bool isValid() const;

#ifndef SWIG
            // Not used from the outside world...
            void setError(int error, ...);
#endif

            //! Return the number of errors since the last call to clearAllErrors.
            int  getNumberOfErrors() const;

            //! Remove all errors from the current list of errors.
            void clearAllErrors();

            //! Return a specific error number - or ms_mascotresfile::ERR_NO_ERROR.
            int  getErrorNumber(const int num = -1) const;

            //! Return the last error number - or ms_mascotresfile::ERR_NO_ERROR.
            int  getLastError() const;

            //! Return a specific error as a string. 
            std::string getErrorString(const int num) const;

            //! Return the last error number - or an empty string.
            std::string getLastErrorString() const;

            //! Replace the existing keepAlive values with new values.
            void resetKeepAlive(const int keepAliveInterval, const char * keepAliveText, 
                                const bool propagateToAppended = true, const bool resetStartTime = false);

            // ------------------- Basic helper  functions -------------------
            // In a long string, comma separated, this function can be
            // used to retrieve the sub strings. It also tries to retrieve an
            // accession string properly - assuming that is has quotes around 
            // it. 
            //! Helper function - mainly for internal library use.
            bool getNextSubStr(const std::string & input, 
                               std::string::size_type & idx,
                               std::string & output,
                               const char * separator = ",",
                               const bool removeQuotes = false) const;


            // ----------------- Specific results functions ------------------
            //! Returns the number of queries (peptide masses or ms-ms spectra).
            int    getNumQueries(const int resfileID = 0) const;

            //! Returns the maximum number of hits possible for a protein summary.
            int    getNumHits(const section sec=SEC_SUMMARY) const;

            //! Returns the number of sequences in the FASTA file(s) searched.
            int getNumSeqs(const int idx = 0) const;

            //! Returns the number of sequences that passed the taxonomy filter in the FASTA file(s) searched.
            int getNumSeqsAfterTax(const int idx = 0) const;

            //! Returns the number of residues in the FASTA file(s) searched.
            double getNumResidues(const int idx = 0) const;

            //! Returns the time taken for the search.
            /*!
             * Obtained from the \c exec_time= line in the header section.
             * This is the 'wall clock' time, not the CPU time.
             * \return execution time in seconds
             */
            int    getExecTime()        const { return execTime_;             }

            //! Returns the date and time of the search in seconds since midnight January 1st 1970.
            /*!
             * Obtained from the \c date= line in the header section of the
             * file.  Can be converted to day, month, year etc. using gmtime or
             * similar functions.
             * \return the date and time of the search in seconds since midnight January 1st 1970.
             */
            int    getDate()            const { return searchDate_;           }

            //! Returns the version of Mascot used to perform the search.
            /*!
             * Obtained from the \c version= entry in the header section of the
             * file.
             * \return the version of Mascot used to perform the search.
             */
            std::string getMascotVer()  const { return version_;              }

            //! Returns the FASTA file version.
            std::string getFastaVer(int idx = 1) const;

            //! Returns the path to the FASTA file used.
            std::string getFastaPath(int idx = 1) const;

            //! Returns the unique task ID used by Mascot Daemon.
            std::string getUniqueTaskID() const;

            //! Returns true if the search was a PMF search (\c SEARCH=PMF). 
            bool isPMF()            const;

            //! Returns true if the search was an MSMS search (\c SEARCH=MIS).
            bool isMSMS()           const;

            //! Returns true if the search was a sequence query search (\c SEARCH=SQ).
            bool isSQ()             const;

            //! Returns true if the search was an error tolerant search.
            bool isErrorTolerant()  const;

            //! Returns true if any of the queries in the search just contain a single peptide mass.
            bool anyPMF();

            //! Returns true if any of the queries in the search contain ions data.
            bool anyMSMS();

            //! Returns true if any of the queries in the search contain \c seq or \c comp commands.
            bool anySQ();

            //! Returns true if any of the queries in the search contain \c tag or \c etag commands.
            bool anyTag();

            //! Returns the experimental mass value as entered by the user.
            double getObservedMass(const int query);

            //! The 'charge' returned will be 0 for Mr, otherwise it will be 1, -1, 2, -2, 3, -3 etc. and -100 for an error.
            int    getObservedCharge(const int query);
            
            //! Returns the experimental mass value (as a relative mass) as entered by the user.
            double getObservedMrValue(const int query);

            //! Returns the experimental intensity for the peptide.
            double getObservedIntensity(const int query);

            //! To perform a repeat search need to build up appropriate string.
            std::string getRepeatSearchString(const int query, const bool fullQuery = false);

            //! Returns the name of the results file passed into the constructor.
            std::string getFileName(const int id = 1) const;

            //! Returns a reference to the search parameters class.
            ms_searchparams & params() const { return *params_;              }

            //! Returns an object that represents quantitation-section as a reduced \c quantitation.xml file.
            bool getQuantitation(ms_quant_configfile *qfile) const;

            //! Returns an object that represents unimod-section as a reduced \c unimod_2.xml file.
            bool getUnimod(ms_umod_configfile *ufile) const;

            //! Returns an ms_masses object from the mass values in the results file.
            bool getMasses(ms_masses  * masses) const;

            //! Returns an object that represents enzyme-section as a reduced \c enzymes file.
            bool getEnzyme(ms_enzymefile *efile, const char * enzymeFileName = 0) const;

            //! Returns an object that represents taxonomy-section as a reduced \c taxonomy file.
            bool getTaxonomy(ms_taxonomyfile *tfile) const;

            //! Outputs the "keep-alive" string during time-consuming operations.
            bool outputKeepAlive() const;

            //! Return the progress indicators used by the keepAlive functions.
            void getKeepAlive(KA_TASK     & kaTask, 
                              int         & kaPercentage, 
                              std::string & kaAccession, 
                              int         & kaHit, 
                              int         & kaQuery,
                              std::string & kaText) const;

            // Internal use only
            bool outputKeepAlive(KA_TASK kaTask, int percentageComplete, const char * accession, int hit, int query) const;

            //! Returns the directory being used for cache files (if any).
            std::string getCacheDirectory(bool processed = true) const;

			//! Returns the filename of the cache file.
			std::string getCacheFileName() const;

            //! Returns true if a cache file will be created when the ms_mascotresfile constructor is called.
            static bool willCreateCache(const char * szFileName,
                                        const unsigned int flags,
                                        const char * cacheDirectory,
                                        std::string * cacheFileName);

            //! Return default flags and parameters for creating an ms_peptidesummary or ms_proteinsummary object.
            std::string get_ms_mascotresults_params(const ms_mascotoptions & opts,  
                                                    unsigned int * gpFlags,                    
                                                    double       * gpMinProbability,        
                                                    int          * gpMaxHitsToReport,       
                                                    double       * gpIgnoreIonsScoreBelow,  
                                                    unsigned int * gpMinPepLenInPepSummary,
                                                    bool         * gpUsePeptideSummary,
                                                    unsigned int * gpFlags2);

            //! Returns a list of the Percolator input and output files for the specified data file.
            static bool staticGetPercolatorFileNames(const char * szDatFileName,
                                                     const char * cacheDirectory,
                                                     const char * percolatorFeatures,
                                                     const char * additionalFeatures,
                                                     const bool   useRetentionTimes,
                                                     std::vector<std::string> & filenames,
                                                     std::vector<bool> & exists);

            //! Must call this before creating an ms_peptidesummary with Percolator scoring.
            void setPercolatorFeatures(const char * percolatorFeatures,
                                       const char * additionalFeatures,
                                       const bool   useRetentionTimes);

            //! Retrieve the filenames use for percolator input and output.
            std::vector<std::string> getPercolatorFileNames() const;

            //! Return the query number and file ID in the source .dat file.
            bool getSrcQueryAndFileIdForMultiFile(const int q, int & gsqNewQuery, int & gsqFileId) const;

            //! Return the multi-file query number from the local query number in an appended file
            int getMultiFileQueryNumber(const int localQuery, const int fileId) const;

            //! Used internally.
            void appendErrors(ms_errs & errs);
            std::string getEncodedPercolatorFeatures() { return percolatorFeatures_; }

        protected:  
#ifndef SWIG
            // Not safe to copy or assign this object.
            ms_mascotresfile(const ms_mascotresfile & rhs);
            ms_mascotresfile & operator=(const ms_mascotresfile & rhs);
#endif

        private:
            std::string  fileName_;
            int          numQueries_;              // For multifile, this is the total of all queries for all files.
            std::vector<int> multifileNumQueries_; // 0 based vector, with 0 being the primary file
            typedef std::vector<ms_mascotresfile *> resfileV_t; // 0 based vector, with 0 being the primary file
            resfileV_t   multifileResfiles_;
            int          protSummaryHits_;
            int          numSequences_;        
            int          numSequencesAfterTax_;
            double       numResidues_;         
            int          execTime_;            
            int          searchDate_;          
            std::string  version_;

            int  keepAliveInterval_;
            std::string  keepAliveTextStr_;
            std::string  keepAliveText_[KA_LAST];
            time_t       keepAliveStartTime_;
            // Following are mutable so outputKeepAlive can be a const function
            mutable KA_TASK     keepAliveTask_;
            mutable time_t      lastKeepAliveTime_; 
            mutable int         keepAlivePercentage_;
            mutable std::string keepAliveAccession_;
            mutable int         keepAliveHit_;
            mutable int         keepAliveQuery_;

            bool         anyMSMS_;
            bool         cachedAnyMSMS_;
            bool         anyPMF_;
            bool         cachedAnyPMF_;
            bool         anySQ_;
            bool         cachedAnySQ_;
            bool         anyTag_;
            bool         cachedAnyTag_;
            mutable bool isErrorTolerant_;
            mutable bool cachedIsErrorTolerant_;

            unsigned int flags_;
            msparser_internal::ms_cdb * pIndexFile_;
            bool         useIndexFile_;
            std::string  cacheDirectory_;
            int          hFile_;
            mutable char *       readlnBuf_;
            mutable unsigned int readlnBufSize_;

            std::vector<int> errorNumbers_;
            std::vector<std::string>errorStrings_; // Will include 'user' data
            std::vector<int> errorRepeats_;        // So that we don't get strings of data

            ms_searchparams *params_;

            std::string percolatorFeatures_;       // Encoded string
            std::vector<std::string> percolatorFileNames_;

            const char * sectionTitles_[SEC_NUMSECTIONS];

            multiBufMemPtr sectionStart_[SEC_NUMSECTIONS];
            multiBufMemPtr sectionEnd_  [SEC_NUMSECTIONS];

            multiBuf_v buffers_;

            std::string endSectionKey_;
            std::string lineBasedEndSectionKey_;
            std::string genericQuerySectionKey_;
            bool   isWinIniFormat_;
            std::vector<double> cachedMrValues_;
            std::vector<double> cachedExpValues_;
            std::vector<short>  cachedCharges_;

            multiBufMemPtr findSectionStart(const char * szSectionName,
                                           const multiBufMemPtr * startLookingAt = 0);
            multiBufMemPtr findSectionEnd(const multiBufMemPtr sectionStart);

            // Array of maps for each section
            typedef std::set<const char *,ms_sortByKeyCriterion> sortedKeys;
            sortedKeys sorted_[SEC_NUMSECTIONS];
            bool fillUpSortedList(const int section,
                                  const multiBufMemPtr sectionStart,
                                  const multiBufMemPtr sectionEnd,
                                  sortedKeys & sorted_keys);

            // There are an 'unknown' number of query sections
            bool hasQuerySectionBeenIndexed;
            std::vector<multiBufMemPtr> querySectionStart_;
            std::vector<multiBufMemPtr> querySectionEnd_;
            std::vector<sortedKeys> sortedQueries_;

            // Private function to get string
            bool inDLLgetSectVal(const section sec,
                                 const int queryNumber,
                                 const multiBufMemPtr sectionStart,
                                 const multiBufMemPtr sectionEnd,
                                 sortedKeys & sorted_keys,
                                 const char * key,
                                 std::string & result) const;

            // Private function to get string
            bool inDLLgetSectionAsString(const section sec,
                                         std::string & result) const;

            bool readFile(const char * szFileName);
            bool readLine(char * & buf, unsigned int & bufSize) const;
            void getSectionTitles();
            void debugCheckReadFileOK();
            bool createCDBFile();
            void prepareKeepAlive(const char * keepAliveText, const bool resetStartTime);
            std::string getKeepAliveString(const double elapsedTime) const;
            static std::string getSortedPercolatorFeatures(const char * percolatorFeatures,
                                                           const char * additionalFeatures,
                                                           const bool   useRetentionTimes);
            bool setErrorInfoFromString(const std::string & e);
            std::string getErrorInfoAsString(const int num) const;
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace
#endif // MS_MASCOTRESFILE_HPP

/*------------------------------- End of File -------------------------------*/
