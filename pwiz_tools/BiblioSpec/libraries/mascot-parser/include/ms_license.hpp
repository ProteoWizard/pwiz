/*
##############################################################################
# file: ms_license.hpp                                                       #
# 'msparser' toolkit                                                         #
# Provides read-only interface for mascot license file                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_license.hpp             $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.9 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_LICENSE_HPP
#define MS_LICENSE_HPP

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


namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! The class provides access license details in read-only mode.
    /*! 
     *  This class doesn't contain functions to create a new license or 
     *  read details of why a license may be invalid.
     *
     *  To use this class:
     *
     *  <UL>
     *  <LI>Create an instance of this class.</LI>
     *  <LI>Set an explicit file name if needed.</LI>
     *  <LI>Call #read_file().</LI>
     *  <LI>Check for possible file reading errors with #isValid().</LI>
     *  <LI>Check license validity with #isLicenseValid().</LI>
     *  <LI>If the license is valid, then access individual license properties.</LI>
     *  <LI>Re-use the instance (if you need to read multiple licenses) after calling #defaultValues().</LI>
     *  </UL>
     */
    class MS_MASCOTRESFILE_API ms_license: public ms_errors
    {
    public:
        //! Default constructor.
        ms_license();

        //! Copying constructor.
        ms_license(const ms_license& src);

        //! Immediate action constructor.
        ms_license(const char* filename);

        //! Destructor.
        ~ms_license();

        //! Can be called before re-using the instance in order to erase the previous license details.
        void defaultValues();

        //! Copies all information from another instance.
        void copyFrom(const ms_license* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_license& operator=(const ms_license& right);
#endif
        //! Returns a file name being used as a license.
        std::string getFileName() const;

        //! Set the license file name explicitly rather than use a default location.
        void setFileName(const char* filename);

        //! Main method that reads in the license file.
        void read_file();

        //! Returns TRUE if the license has been sucessfully read, parsed and checked.
        bool isLicenseValid() const;

        //! Returns a compound license string that can be used for displaying license information.
        std::string getLicenseString() const;

        //! Returns a license version as a string.
        std::string getLicenseVersion() const;

        //! Returns a date the license is valid from as a string.
        std::string getStartDate() const;

        //! Returns a date the license is valid until as a string.
        std::string getEndDate() const;

        //! Returns a number of processors licensed.
        int getNumProcessorsLicensed() const;

        //! Returns a name of the licensee.
        std::string getLicensee() const;

        //! Returns Mascot distributor information.
        std::string getDistributor() const;

        //! Returns a string which encodes feature information.
        std::string getFeatures() const;

        int getInternalConfigurationCode() const;

    private:
        bool loadLicenseFile(FILE *f);
        bool checkLicenseValid(const char* startDate, const char* endDate);
        int  calcChecksum(void* entity);
        void formLicenseString();
        void checkInternalConfiguration();

        bool bLicenseValid_;
        std::string filename_;
        std::string strLicenseString_;

        std::string licenseVer_;
        std::string startDate_;
        std::string endDate_;
        int processors_;
        std::string licensee_;
        std::string distributor_;
        std::string features_;

        int internalConfigurationCode_;
    }; // class ms_license
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_LICENSE_HPP

/*------------------------------- End of File -------------------------------*/
