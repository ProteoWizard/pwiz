/*
##############################################################################
# file: ms_security_user.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates a mascot user as used in authentication                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.15 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_security_user_INCLUDED_)
#define ms_security_user_INCLUDED_

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

// Includes from the standard template library
#include <string>
#include <set>
#include <vector>
#include <map>
#include <time.h>


namespace matrix_science {
/** @addtogroup security_group
 *  @{
 **/

    //! This class will normally only be used by Mascot Security Administration applications.
    /*! 
     * Call ms_security::addNewUser() to create a new user or
     * ms_security::getUser() or ms_security::getUserFromID() to get an
     * existing user, and then use the ms_user member functions to query or
     * modify the ms_user object. 
     *
     * To make the changes permanent, use ms_security::updateUser().
     */

    class MS_MASCOTRESFILE_API ms_user
    {

    public:
        //! Definitions for types of user.
        /*!
         * See \ref DynLangEnums.
         */
        enum usertype
        {
            USER_SECURITY_DISABLED  = 0x0000, //!< Not a real user, but the USERID if security is disabled.
            USER_NORMAL             = 0x0001, //!< A 'normal' Mascot user, that doesn't fit into any of the other categories below.
            USER_INTEGRA            = 0x0002, //!< Login etc. is done through Mascot Integra.
            USER_COMPUTER_NAME      = 0x0003, //!< This type of user is defined by a computer name. Should only be used for 3rd party applications.
            USER_IP_ADDRESS         = 0x0004, //!< This type of user is defined by an IP address. Should only be used for 3rd party applications.
            USER_AGENT_STRING       = 0x0005, //!< This type of user is defined by a client agent string. Should only be used for 3rd party applications as a last resort.
            USER_WEBAUTH            = 0x0006  //!< The configuration is to use web server authentication, so this user has no password.
        };

        //! Definitions for predefined users.
        /*!
         * See \ref DynLangEnums.
         */
        enum systemids
        {
            USERID_SECURITY_DISABLED= 0x0000, //!< Only valid if security is disabled.
            USERID_GUEST            = 0x0001, //!< The built in guest user. Note that this accound is disabled by default.
            USERID_ADMINISTRATOR    = 0x0002, //!< The built in administrator account. Cannot be deleted or disabled.
            USERID_CMDLINE          = 0x0003, //!< Applications run from the command line rather than as a cgi application use this by default.
            USERID_DAEMON           = 0x0004, //!< Mascot Daemon will use this user to run searches.
            USERID_PUBLIC_SEARCHES  = 0x0005, //!< Example files use this user id. matrix_science::ms_session::canResultsFileBeViewed always returns true for this ID.
            USERID_INTEGRA_SYSTEM   = 0x0006, //!< The Mascot Integra system account. Users will never be able to log in as this user - it is only used from the Integra server.
            USERID_LAST             = 0x0007  //!< Placeholder.
        };
        
        //! Create a new user. 
	    ms_user(const int         userID,
                const std::string userName,
                const std::string password,
                const long        passwordExpiry,
                const std::string fullName,
                const std::string emailAddress,
                const usertype    userType,
                const bool        enabled);

        //! Create a new 'empty' user with no name, password, ID.
	    ms_user();

        // Copying constructor for c++ programs - don't document
        ms_user(const ms_user &src);

#ifndef SWIG
        // Assignment operator for c++ programs - don't document
        ms_user & operator=(const ms_user & right);
#endif
	    ~ms_user();

        //! Return the 'type' of user.
	    usertype  getUserType() const;

        //! Update the user type -- details are saved immediately.
        void setUserType(usertype newVal);

        //! Return the full user name.
        std::string getFullName() const;

        //! Update the full user name -- details are saved immediately.
	    void setFullName(std::string newVal);

        //! Returns the encrypted password.
        std::string getEncryptedPassword() const;

        //! Update the encrypted password - details are saved immediately.
	    void setEncryptedPassword(std::string newVal);

        //! Encrypt and save the passed password.
	    void setPassword(std::string barePassword);

        //! Checks to see if the password is correct for the user.
        bool validatePassword(const std::string pwd, int & errorFlag) const;

        //! Returns the time that the password will expire.
        time_t getPasswordExpiry() const;

        //! Set the password expiry time.
        void setPasswordExpiry(time_t newVal);

        //! Return the unique user ID.
        int getID() const;

        //! Set the unique user ID.
        void setID(int newVal);

        //! Return the user login name.
        std::string getName() const;

        //! Set the user login name.
	    void setName(std::string newVal);

        //! Return true if the account is enabled.
        bool isAccountEnabled() const;

        //! Pass true to enable the account.
	    void setAccountEnabled(bool newVal);

        //! Returns the email address of the user.
	    std::string getEmailAddress() const;

        //! Sets the email address of the user.
        void setEmailAddress(std::string newVal);

        //! Returns true if the password has expired.
        bool hasPasswordExpired() const;

    private:
	    int userID_;
	    std::string userName_;
	    std::string fullName_;
	    std::string encryptedPassword_;
	    time_t  passwordExpiry_;
	    std::string emailAddress_;
	    bool accountEnabled_;
	    usertype userType_;

        void copyFrom(const ms_user * src);
    };
    /** @} */ // end of security_group 
}
#endif // !defined(ms_security_user_INCLUDED_)
