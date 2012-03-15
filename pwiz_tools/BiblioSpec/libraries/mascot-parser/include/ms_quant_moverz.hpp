/*
##############################################################################
# file: ms_quant_moverz.hpp                                                  #
# 'msparser' toolkit                                                         #
# Encapsulates "moverz" element from "quantitation.xml"-file                 #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_moverz.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_MOVERZ_HPP
#define MS_QUANT_MOVERZ_HPP

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

// forward declarations
namespace msparser_internal {
    class ms_quant_xmlloader;
}

namespace matrix_science {

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Parameter name and value pair.
    class MS_MASCOTRESFILE_API ms_quant_moverz: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_moverz();

        //! Copying constructor.
        ms_quant_moverz(const ms_quant_moverz& src);

        //! Destructor.
        virtual ~ms_quant_moverz();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_moverz* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_moverz& operator=(const ms_quant_moverz& right);
#endif

        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c monoisotopic attribute.
        bool haveMonoisotopic() const;

        //! Returns the value of the \c monoisotopic attribute.
        std::string getMonoisotopic() const;

        //! Set a custom value for the \c monoisotopic attribute.
        void setMonoisotopic(const char* value);

        //! Delete the \c monoisotopic attribute.
        void dropMonoisotopic();

        //! Obtain a symbolic name for the \c monoisotopic attribute schema type.
        std::string getMonoisotopicSchemaType() const;


        //! Indicates presence of the \c average attribute.
        bool haveAverage() const;

        //! Returns the value of the \c average attribute.
        std::string getAverage() const;

        //! Set a custom value for the \c average attribute.
        void setAverage(const char* value);

        //! Delete the \c average attribute.
        void dropAverage();

        //! Obtain a symbolic name for the \c average attribute schema type.
        std::string getAverageSchemaType() const;

    private:
        std::string _monoisotopic;
        bool _monoisotopic_set;

        std::string _average;
        bool _average_set;

    }; // class ms_quant_moverz

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_MOVERZ_HPP

/*------------------------------- End of File -------------------------------*/

