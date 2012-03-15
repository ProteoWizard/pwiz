/*
##############################################################################
# file: ms_tinycdb.hpp                                                      #
# 'msparser' toolkit                                                         #
# Used internally in Mascot Parser as well as being available externally     #
#                                                                            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2009 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: davidc $ #
#       $Date: 2011-07-20 14:31:03 $ #
#   $Revision: 1.10 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_TINYCDB_HPP
#define MS_TINYCDB_HPP

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


// Includes from the standard template library
#include <string>

// These values are the key names for special cases
// User defined keys must not begin with "=0."
#define CDBIDX_DUPE_ACCESSION   "=0.1"
#define CDBIDX_VERSION          "=0.2"
#define CDBIDX_FILE_TOO_LARGE   "=0.3"
#define CDBIDX_SRC_FILE_SIZE    "=0.4"
#define CDBIDX_SRC_FILE_DATE    "=0.5"
#define CDBIDX_BUGFIX_10627     "=0.6"


// Need to be a bit less than 2Gb / 4Gb so that we can write the error strings...
#define MAX_CDB_SIZE      (0xFFFFFFFF - 0x1000000)
#define MAX_CDB_SIZE_STR  "4Gb"
#define OLD_MAX_32_BIT_CDB_SIZE      (0x7FFFFFFF - 0x1000000)
#define OLD_MAX_32_BIT_CDB_SIZE_STR  "2Gb"


// Forward declarations, so no need to include cdb.h
struct cdb;
struct cdb_make;


namespace matrix_science {

    /** @addtogroup tools_group
     *  
     *  @{
     */
    //! Wrapper for the public domain tinycdb package http://www.corpit.ru/mjt/tinycdb.html by Michael Tokarev.
    /*!
     * ms_tinycdb is a utility for creating and using a 'static' or 'constant'
     * (read-only) database of arbitrary key/value pairs.
     *
     * After creating the ms_tinycdb object, call openIndexFile(). If this 
     * returns true, then the index file exists, is complete, and values can be
     * retrieved by calling the getValueFromKey() function.
     *
     * If openIndexFile() returns false, it is because the index file does not
     * exist, is incomplete, out of date or corrupt. To determine the reason, 
     * call getLastError() or getLastErrorString(). If the file exists, but for
     * example is incomplete, it is possible that it is worth trying to create 
     * it again. The function isPossibleToCreate() will return true if it is 
     * worth trying to (re-)create the file. See the documentation for 
     * openIndexFile() for a full list of errors.
     * 
     * To create an index file, call prepareToCreate() first, and then call 
     * saveValueForKey(), saveFileOffsetForKey() or saveIntForKey() to
     * to save all the key/value pairs that are required. Finally, call 
     * finishCreate() to close the index file. Once finishCreate() has been 
     * called, the file is then automatically opened again for reading.
     *
     * The \a keyName values can be any text, except values starting with
     * =0. which are reserved for internal use: 
     *
     * <ul>
     * <li>=0.1 : For duplicate accessions with .cdb files created for Mascot
     * results files.</li>
     * <li>=0.2 : The version string (if any) supplied to the constructor.</li>
     * <li>=0.3 : Used for cases where the CDB file is larger than the maximum
     * allowed.</li>
     * <li>=0.4 : The 'source' file size.</li>
     * <li>=0.5 : The 'source' file date.</li>
     * </ul>
     *
     * These values can be retrieved, for example by passing a value of "=0.2"
     * to getValueFromKey().
     *
     * Pseudo code for opening/creaing a file with just one value: \verbatim

     create new ms_tinycdb
     if !tinycdb->openIndexFile() then
       if tinycdb->getLastError() ==  ERR_CDB_BEING_CREATED) then
         print "Another task is creating the index file, try again later"
       else if tinycdb->isPossibleToCreate() then
         print "(Re)-creating index file because : " . tinycdb->getLastErrorString()
         prepareToCreate()
         saveValueForKey("MyKey", "MyValue");
         finishCreate()
       else
         print "Cannot create index file because : " . tinycdb->getLastErrorString()
     end if

     # We've either just created the index file or it was created some time ago
     if tinycdb->isValid() then
       print "My value = " . tinycdb->getValueFromKey("MyKey");
     end if
     \endverbatim
     * 
     * See also the \ref tools_examples_group.
     */

    class MS_MASCOTRESFILE_API ms_tinycdb : public ms_errors
    {
        public:
            //! Constructor for creating or reading a CDB index file.
            ms_tinycdb(const char * indexFileName,
                       const char * versionNumber, 
                       const char * sourceFileName);

            //! Destructor closes any files and frees memory.
            ~ms_tinycdb();


            //! Return the full path to index file.
            std::string getIndexFileName() const;

            //! Set the index file name.
            void setIndexFileName(const char * filename);

            //! Open, or try to open an index file.
            bool openIndexFile(const bool mayRetryBuilding);

            //! Close index file.
            void closeIndexFile();

            //! Check to see if the index file can be created.
            bool isPossibleToCreate() const;

            //! Check to see that the file is valid and open for reading.
            bool isOpenForReading() const;

            //! Start creating the index file.
            bool prepareToCreate();

            //! Add a key/value pair to the file.
            bool saveValueForKey(const char * keyName, 
                                 const char * value,
                                 const unsigned int keyNameLen = 0,
                                 const unsigned int valueLen = 0);

            //! Finish creating the index file and open it for reading.
            bool finishCreate();

            //! Returns the text value of a key associated with the key name.
            std::string getValueFromKey(const std::string & keyName, const int count = 0);

            //! A useful function for returning file indexes.
            OFFSET64_T getFileOffsetFromKey(const std::string & keyName);

            //! A useful function for storing file indexes.
            bool saveFileOffsetForKey(const std::string & keyName, OFFSET64_T offset);

            //! Return an integer value associated with a key.
            int getIntFromKey(const std::string & keyName);

            //! Return an integer value associated with a key and flag to indicate if the value was found in the CDB file.
            int getIntFromKey(const std::string & keyName, bool & found);

            //! Store an integer value associated with a key.
            bool saveIntForKey(const std::string & keyName, int value);

            //! See if a key exists while making the file.
            int makeExists(const char * key) const;


        protected:  
            // Not safe to copy or assign this object.
            ms_tinycdb(const ms_tinycdb & rhs);
#ifndef SWIG
            ms_tinycdb & operator=(const ms_tinycdb & rhs);
#endif
        private:
            std::string  indexFileName_;
            std::string  versionNumber_;
            std::string  sourceFileName_;
            bool         isPossibleToCreate_;
            int          fdCDB_;
            struct cdb      * cdb_;
            struct cdb_make * cdbm_;
            bool         creatingCDB_;
            OFFSET64_T   calcFileSize_;
            bool         addKeyErrorReported_;
            bool         tooLargeErrorReported_;
            bool         cdbInitOK_;

            bool lockFile(int hFile);
            bool unlockFile(int hFile);
    };
    /** @} */ // end of tools_group
} // matrix_science

#endif // MS_TINYCDB_HPP

/*------------------------------- End of File -------------------------------*/
