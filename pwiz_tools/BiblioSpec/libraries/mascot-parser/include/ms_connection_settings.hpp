/*
##############################################################################
# file: ms_connection_settings.hpp                                           #
# 'msparser' toolkit                                                         #
# Holds the proxy server and Mascot sessionID string                         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2005 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Source: /vol/cvsroot/parser/inc/ms_connection_settings.hpp,v $ #
#     $Author: davidc $                                                      #
#       $Date: 2010-12-08 13:28:42 $                                         #
#   $Revision: 1.10 $                                                        #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_connection_settings_INCLUDED_)
#define ms_connection_settings_INCLUDED_

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


namespace matrix_science {

    /** @addtogroup common_group
     *  
     *  @{
     */


    //! Used when configuration files are loaded from the Mascot server.
    /*! 
     * Proxy server and \a sessionID parameters may be required to access
     * configuration files on the Mascot server via HTTP.
     *
     * This class holds these settings and is used from:
     * <ul>
     * <li>ms_datfile::ms_datfile(const char *filename, const int timeoutSec, const ms_connection_settings *cs) </li>
     * <li>ms_umod_configfile::ms_umod_configfile(const char* filename, const ms_connection_settings * cs)</li>
     * <li>ms_modfile::ms_modfile(const char *filename, const ms_masses *massFile, const bool fromSubstitutions, const ms_connection_settings *cs)</li>
     * <li>ms_masses::ms_masses(const char* filename, const ms_connection_settings * cs)</li>
     * <li>ms_fragrulesfile::ms_fragrulesfile(const char *filename, const ms_connection_settings *cs)</li>
     * <li>ms_taxonomyfile::ms_taxonomyfile(const char *filename, const ms_connection_settings *cs)</li>
     * <li>ms_enzymefile::ms_enzymefile(const char *filename, const ms_connection_settings *cs)</li>
     * </ul>
     */
    class MS_MASCOTRESFILE_API ms_connection_settings
    {

    public:
        //! Definitions for types of setting.
        /*!
         * See \ref DynLangEnums.
        */
        enum PROXY_TYPE
        {
            PROXY_TYPE_NO_PROXY       = 0x0000, //!< No proxy server will be used. For Windows, the INTERNET_OPEN_TYPE_DIRECT flag is specified in the InternetOpen call.
            PROXY_TYPE_FROM_REGISTRY  = 0x0001, //!< Windows version of Mascot Parser only. The INTERNET_OPEN_TYPE_PRECONFIG flag is specified in the InternetOpen call.
            PROXY_TYPE_SPECIFY        = 0x0002  //!< The proxy server must be specified using setProxyServer(). The INTERNET_OPEN_TYPE_PROXY flag is specified in the InternetOpen call.
        };

        /*! Definitions for HTTP protocol to use.
         *
         * See \ref DynLangEnums.
        */
        enum HTTP_PROTOCOL
        {
            HTTP_1_0                = 0x0000, //!< Use HTTP 1.0.
            HTTP_1_1                = 0x0001, //!< Use HTTP 1.1.
            HTTP_SYSTEM_DEFAULT     = 0x0002  //!< Use system default. For Internet Explorer, this is the default set up in IE.
        };

        //! Default constructor.
	    ms_connection_settings();

        //! Constructor to set all parameters.
        ms_connection_settings(const std::string sessionID,
                               const std::string proxyServer,
                               const std::string proxyUserName,
                               const std::string proxyUserPassword);

        //! Destructor.
	    ~ms_connection_settings();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Call this member to copy all the information from another instance.
        void copyFrom(const ms_connection_settings* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_connection_settings& operator=(const ms_connection_settings & right);
#endif
        //! Sets the sessionID to be used for the URL connection.
        void setSessionID(const std::string sessionID);

        //! Specifies how proxy server information will be obtained.
        void setProxyServerType(ms_connection_settings::PROXY_TYPE proxyType);

        //! Returns how proxy server information will be obtained.
        ms_connection_settings::PROXY_TYPE getProxyServerType() const;

        //! Sets the version of the http protocol to be used.
        void setHttpProtocol(ms_connection_settings::HTTP_PROTOCOL httpProtocol);

        //! Returns the version of HTTP protocol to be used.
        ms_connection_settings::HTTP_PROTOCOL getHttpProtocol() const;

        //! Sets the name of the proxy server.
        void setProxyServer(const std::string proxyServer); 

        //! Sets the user name for the proxy server.
        void setProxyUsername(const std::string proxyUserName); 

        //! Sets the password for the proxy server.
        void setProxyPassword(const std::string proxyUserPassword); 

        //! Set the user agent string
        void setUserAgent(const std::string userAgent);

        //! Gets the sessionID to be used for the URL connection.
        std::string getSessionID() const;

        //! Gets the name of the proxy server.
        std::string getProxyServer() const; 

        //! Gets the user name for the proxy server.
        std::string getProxyUsername() const; 
        
        //! Gets the password for the proxy server.
        std::string getProxyPassword() const; 

        //! Get the combined username and password -- if any.
        std::string getProxyUserAndPassword() const;

        //! Get the user agent string
        std::string getUserAgent() const;

        //! Sets the HTTP connection timeout.
        void setConnectionTimeout(int timeout);

        //! Returns the HTTP connection timeout.
        int getConnectionTimeout() const;

    protected:

    private:
        std::string sessionID_;
        std::string proxyServer_;
        std::string proxyUserName_;
        std::string proxyUserPassword_;
        std::string userAgent_;
        PROXY_TYPE proxyType_;
        HTTP_PROTOCOL httpProtocol_;
        int connectionTimeout_;
    };
/** @} */ // end of common_group
}
#endif // !defined(ms_connection_settings_INCLUDED_)
