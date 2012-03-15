/*
##############################################################################
# file: ms_unigeneoptions.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates "UniGene" section of "mascot.dat" file                        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2007 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_unigeneoptions.hpp      $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.6 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_UNIGENEOPTIONS_HPP
#define MS_UNIGENEOPTIONS_HPP

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

    //! An instance of this class represents all the parameters specified in <b>UniGene</b> section of <tt>mascot.dat</tt>.
    /*!
     * An instance of this class is created and populated in ms_datfile
     * and is available by calling ms_datfile::getUniGeneOptions().
     * It can also be created separately and initialised with default values.
     * You can create an instance of the class or copy from another instance in
     * order to pass it then as an options-containing object.
     *
     * For detailed information on any of the options please consult Mascot
     * manual.
     *
     * Currently, access to the values is only provided through the
     * ms_customproperty class, from which this class is derived.
     * It facilitates the following tasks:
     *
     * <ul>
     * <li>Retrieving an unsupported property.</li>
     * <li>Retrieving a raw/text/XML property representation.</li>
     * <li>Checking for existence of a certain property rather than 
     * dealing with its default value.</li>
     *<li>Accessing commented lines in a section.</li>
     * </ul>
     *
     * More functionality is described in the documentation for
     * ms_customproperty.
     */
    class MS_MASCOTRESFILE_API ms_unigeneoptions: public ms_customproperty
    {
        friend class ms_datfile;

    public:

        //! Default constructor.
        ms_unigeneoptions();

        //! Copying constructor.
        ms_unigeneoptions(const ms_unigeneoptions& src);

        //! Destructor.
        virtual ~ms_unigeneoptions();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_unigeneoptions* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_unigeneoptions& operator=(const ms_unigeneoptions& right);
#endif
        //! Initialises the instance with default values.
        virtual void defaultValues();

        //! Indicates whether the section has been actually read from the file.
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. should it be saved in a file.
        void setSectionAvailable(const bool value);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif


    private:
        bool sectionAvailable_;

    }; // class options
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_UNIGEGEOPTIONS_HPP

/*------------------------------- End of File -------------------------------*/
