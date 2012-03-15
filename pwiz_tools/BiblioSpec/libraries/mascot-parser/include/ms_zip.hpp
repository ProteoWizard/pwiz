/*
##############################################################################
# file: ms_zip.hpp                                                           #
# 'msparser' toolkit                                                         #
# Utilities class for zipping and unzipping a buffer                         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_zip.hpp       $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.4 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef ms_zip_HPP
#define ms_zip_HPP

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

#include <string>


namespace matrix_science {
    /** @addtogroup common_group
     *  
     *  @{
     */

    //! This utility class can be used for compressing and decompressing a buffer.
    /*!
     * \internal
     *
     *  This is used internally in Mascot Parser for decompressing 
     *  <tt>unimod.xml</tt> and other configuration files when they are
     *  downloaded from a remote web site.
     */
    class MS_MASCOTRESFILE_API ms_zip : public ms_errors
    {
    public:
        //! Constructor that can only be used from C++.
        ms_zip(const bool isZipped, const unsigned char * buffer, const unsigned long len);

        //! Constructor that can be used from any language.
        ms_zip(const bool isZipped, const std::string & buffer);

        //! Copying constructor.
        ms_zip(const ms_zip& src);

        //! Call this member to copy all the information from another instance.
        void copyFrom(const ms_zip* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_zip& operator=(const ms_zip& right);
#endif
        ~ms_zip();

        //! Return the zipped buffer.
        unsigned long getZipped(unsigned char * buffer, const unsigned long len) const;

        //! Return the un-zipped buffer.
        unsigned long getUnZipped(unsigned char * buffer, const unsigned long len) const;

        //! Return the zipped buffer.
        std::string getZipped() const;

        //! Return the un-zipped buffer.
        std::string getUnZipped() const;

        //! Return the length of the zipped buffer.
        unsigned long getZippedLen() const;

        //! Return the length of the un-zipped buffer.
        unsigned long getUnZippedLen() const;

        enum { MAX_UNCOMPRESSED_SIZE = (1024 * 1024 * 100) };
    private:
        void init(const bool isZipped, const unsigned char * buffer, const unsigned long len);
        unsigned long unZippedLen_;
        unsigned long zippedLen_;
        unsigned char * pUnZipped_;
        unsigned char * pZipped_;
    }; // class ms_zip
    /** @} */ // end of common_group
} // namespace matrix_science
#endif // ms_zip_HPP

/*------------------------------- End of File -------------------------------*/
