/*
##############################################################################
# file: ms_computeraddress.hpp                                               #
# 'msparser' toolkit                                                         #
# Contains computer address parameters required by cluster mode              #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_computeraddress.hpp     $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.6 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_COMPUTERADDRESS_HPP
#define MS_COMPUTERADDRESS_HPP

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
#include <vector>

namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! The class serves as a container for computer address parameters for cluster mode.
    class MS_MASCOTRESFILE_API ms_computeraddress
    {
        friend class ms_datfile;
        friend class ms_clusterparams;

    public:
        //! Default constructor.
        ms_computeraddress();

        //! Copying constructor.
        ms_computeraddress(const ms_computeraddress& src);

        //! Destructor.
        ~ms_computeraddress();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_computeraddress* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_computeraddress& operator=(const ms_computeraddress& right);
#endif
        //! Returns IP address of the host.
        /*!
         *  Default is <b>"127.0.0.1"</b>.
         */
        std::string getInetAddress() const;

        //! Change the IP address of the host.
        void setInetAddress(const char* address);

        //! Returns the host name.
        /*!
         *  Default is <b>localhost</b>.
         */
        std::string getHostName() const;

        //! Change the host name.
        void setHostName(const char* name);

        //! Returns the port number for the host.
        /*!
         *  Default is <b>0</b>.
         */
        unsigned int getPort() const;

        //! Change the port number for the host.
        void setPort(const unsigned int value);

    private:
        std::string     inetAddress_;
        std::string     hostName_;
        unsigned short  port_;

        void setCustomString();
        std::string     customString_;
    }; // class ms_computeraddress
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_COMPUTERADDRESS_HPP

/*------------------------------- End of File -------------------------------*/
