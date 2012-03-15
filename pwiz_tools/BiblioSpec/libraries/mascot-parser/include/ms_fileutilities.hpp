/*
##############################################################################
# file: ms_fileutilities.hpp                                                 #
# 'msparser' toolkit                                                         #
# Utilities class that encompasses all file-system-related functionality     #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_fileutilities.hpp       $ #
#     $Author: davidc $ #
#       $Date: 2010-09-14 16:23:15 $ #
#   $Revision: 1.16 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_FILEUTILITIES_HPP
#define MS_FILEUTILITIES_HPP

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
#include <string>

// For _finddata_t
#ifdef _WIN32
#include <io.h>
#endif

namespace matrix_science {
    class ms_mascotoptions;
    /** @addtogroup common_group
     *  
     *  @{
     */

#ifdef _WIN32
    typedef __int64 OFFSET64_T;
#else
    typedef off_t   OFFSET64_T;
#endif

    //! This utility class mainly contains static members.
    /*!
     *  For static members, creating an instance of this class is not required.
     *
     *  See \ref DynLangStaticFunctions
     */
    class MS_MASCOTRESFILE_API ms_fileutilities
    {
    public:
        //! Default constructor.
        ms_fileutilities();

        //! Returns the last file modification time.
        static time_t getLastModificationTime(const char* filename, ms_errs* err = NULL);

        //! Returns a correct path to <tt>mascot.dat</tt> if it can be found in one of the default places.
        static std::string findMascotDat(const char *szMascotDatFilename, 
                                         ms_errs * err = NULL,
                                         const int timeoutSec = 0 );

        //! Returns true if the specified file exists, false otherwise.
        static bool doesFileExist(const char* filename);

        //! setAccessRights() return value.
        enum err_sar {
            SAR_SUCCESS,                        //!< Success!
            SAR_FAIL_CHMOD,                     //!< Error returned by call to chmod().
            SAR_FAIL_GET_NAMED_SECURITY_INFO,   //!< Error returned by call to GetNamedSecurityInfo().
            SAR_FAIL_SET_ENTRIES_IN_ACL,        //!< Error returned by call to SetEntriesInAcl().
            SAR_FAIL_SET_NAMED_SECURITY_INFO,   //!< Error returned by call to SetNamedSecurityInfo().
            SAR_FAIL_CHOWN                      //!< Error returned by call to chown().
        };
        //! Sets access rights for a file according to settings in <tt>mascot.dat</tt>.
        /*!
         * See \ref DynLangEnums.
         */
        err_sar setAccessRights(const char * filename, 
                                const bool bWrite, 
                                const bool bExecute, 
                                const bool isFile,
                                const ms_mascotoptions & Options);
        unsigned long getSARExtendedErrorCode() const;

        //! Determine if the passed path is a directory or a file.
        static bool isDirectory(const char * path);

        //! Create a directory or directory tree.
        static int createDirectory(const std::string & directory, std::string & errorDirectory);

        //! Functions to search for all files that match a wildcard
        bool findOpen(const std::string directory,
                      const std::string wildcard);

        //! Functions to search for all files that match a wildcard
        bool findNext(std::string & fileName);

        //! Functions to search for all files that match a wildcard
        void findClose();

        //! Returns a pathname without last token, i.e. makes "C:\myfolder" from "C:\myfolder\XXX".
        static std::string stripLastFolder(const std::string pathname);

        //! Return an MD5 sum of a string.
        static std::string getMD5Sum(const std::string &str, const bool b32bitEncoding = true);

    private:
        unsigned long errorCode_;
#ifdef _WIN32
        struct _finddata_t fileFindInfo_;
#if defined(_MSC_VER) && ( _MSC_VER < 1300)
        int fileFindPtr_;
#else
        intptr_t fileFindPtr_;
#endif
#else
        void * fileFindDir_;     // void * rather than DIR * so no need to include dirent.h
#endif
        std::string fileFindWildcard_;
        std::string fileFindDirectory_;

    }; // class ms_fileutilities
    /** @} */ // end of common_group
} // namespace matrix_science
#endif // MS_FILEUTILITIES_HPP

/*------------------------------- End of File -------------------------------*/
