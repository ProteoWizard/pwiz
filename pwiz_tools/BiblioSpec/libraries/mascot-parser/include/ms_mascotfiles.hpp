/*
##############################################################################
# file: ms_mascotfiles.hpp                                                   #
# 'msparser' toolkit                                                         #
# Contains pathes to some configuration files                                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_mascotfiles.hpp         $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.8 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTFILES_HPP
#define MS_MASCOTFILES_HPP

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

    //! An instance of this class contains configuration files paths (except for <tt>mascot.dat</tt>).
    /*!
     *  An instance of this class is normally created and populated within
     *  ms_datfile.  It contains filepaths for <tt>mod_file</tt>,
     *  <tt>enzymes</tt>, <tt>frequencies</tt> and <tt>nodelist.txt</tt>.
     *
     *  Also get yourselves acquainted with the base class ms_customproperty. 
     *  It facilitates the following tasks:
     *
     *  <ul>
     *  <li>Retrieving an unsupported property.</li>
     *  <li>Retrieving a raw/text/XML property representation.</li>
     *  <li>Checking for existence of a certain property rather than 
     *  dealing with its default value.</li>
     *  <li>Accessing commented lines in a section.</li>
     *  </ul>
     *
     *  More functionality is described in the documentation for
     *  ms_customproperty.
     */
    class MS_MASCOTRESFILE_API ms_mascotfiles: public ms_customproperty
    {
        friend class ms_datfile;
    public:

        //! Default constructor.
        ms_mascotfiles(); 

        //! Copying constructor.
        ms_mascotfiles(const ms_mascotfiles& src);

        //! Destructor.
        ~ms_mascotfiles();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_mascotfiles* right);

#ifndef SWIG
        //! Assignment operator for C++ client application.
        ms_mascotfiles& operator=(const ms_mascotfiles& right);
#endif
        //! Returns <tt>mod_file</tt> path.
        std::string getModifications() const;

        //! Change <tt>mod_file</tt> path.
        void setModifications(const char* filename);

        //! Returns <tt>enzymes</tt> path.
        std::string getEnzymes() const;

        //! Change <tt>enzymes</tt> path.
        void setEnzymes(const char* filename);

        //! Returns <tt>freqs.dat</tt> path.
        std::string getFrequencies() const;

        //! Change <tt>freqs.dat</tt> path.
        void setFrequencies(const char* filename);

        //! Returns <tt>nodelist.txt</tt> path.
        std::string getNodeListFile() const;

        //! Change <tt>nodelist.txt</tt> path.
        void setNodeListFile(const char* filename);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        std::string mods_;
        std::string enzymes_;
        std::string freqs_;
        std::string nodeListFile_;
    };// class ms_mascotfiles
    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_MASCOTFILES_HPP

/*------------------------------- End of File -------------------------------*/
