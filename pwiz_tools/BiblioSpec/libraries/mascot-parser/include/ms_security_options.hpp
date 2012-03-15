/*
##############################################################################
# file: ms_security_options.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates the global security options for Mascot authentication         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.13 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/


#if !defined(ms_security_options_INCLUDED_)
#define ms_security_options_INCLUDED_

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
     *  
     *  @{
     */

    class ms_security;

    //! Options for the Mascot security system.
    /*!
     * This class will generally only be used by the security administration
     * utility. It is used by the ms_security and ms_session classes.
     * 
     */
    class MS_MASCOTRESFILE_API ms_security_options: public ms_errors
    {

    public:
        //! Loads the options file into memory.
	    ms_security_options();

	    ~ms_security_options();

        //! Will return true if the Mascot security system is enabled.
        bool isSecurityEnabled() const;

        //! Enables the Mascot security system.
	    void setSecurityEnabled(bool newVal);

        //! Returns the time in seconds before a user is logged out.
        time_t getSessionTimeout() const;

        //! Sets the time in seconds before a user is logged out.
        void setSessionTimeout(time_t newVal);

        //! Returns the time in days before a password expires.
	    time_t getDefaultPasswordExpiryTime() const;

        //! Sets the time in days before a password expires.
        void setDefaultPasswordExpiryTime(time_t newVal);

        //! Returns the minimum password length.
	    unsigned int getMinimumPasswordLength() const;

        //! Sets the minimum password length.
	    void setMinimumPasswordLength(unsigned int newVal);

        //! Returns true if session (rather than file) cookies are to be used.
        bool getUseSessionCookies() const;

        //! Set whether session or file cookies should be used.
	    void setUseSessionCookies(bool newVal);

        //! Returns true if a session will be invalid if used from a different IP address from the original session request.
        bool getVerifySessionIPAddress() const;

        //! Set if a session will be invalid if used from a different IP address from the original session request.
        void setVerifySessionIPAddress(bool newVal);

        //! Returns the logging level.
        matrix_science::ms_errs::msg_sev getLoggingLevel() const;

        //! Sets the logging level.
        void setLoggingLevel(ms_errs::msg_sev newVal);

        //! Returns the URL to the Integra Application server host.
	    std::string getIntegraAppServerURL() const;

        //! Sets the URL to the Integra Application server host.
	    void setIntegraAppServerURL(std::string newVal);

        //! Returns the name of the Integra database.
        std::string getIntegraDatabaseName() const;

        //! Sets the name of the Integra database.
	    void setIntegraDatabaseName(std::string newVal);

        //! Returns the name of the oracle server for Mascot Integra.
	    std::string getIntegraOracleServerName() const;

        //! Sets the name of the oracle server for Mascot Integra.
	    void setIntegraOracleServerName(std::string newVal);

        //! Returns the log file name.
        std::string getLogFileName();

        //! Sets the log file name.
        void setLogFileName(std::string newVal);

    protected:
        bool loadFromFile();
        bool saveToFile();

    private:
        std::string filename_;

	    bool securityEnabled_;
	    time_t sessionTimeout_;
	    time_t defaultPasswordExpiryTime_;
	    unsigned int minimumPasswordLength_;
	    bool useSessionCookies_;
        bool verifySessionIPAddress_;
        ms_errs::msg_sev logLevel_;
        std::string logFile_;
	    std::string integraAppServerURL_;
	    std::string integraDatabaseName_;
	    std::string integraOracleServerName_;
    };
    /** @} */ // end of security_group 

}
#endif // !defined(ms_security_options_INCLUDED_)
