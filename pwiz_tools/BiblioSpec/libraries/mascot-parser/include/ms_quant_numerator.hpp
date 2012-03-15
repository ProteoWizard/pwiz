/*
##############################################################################
# file: ms_quant_numerator.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates "numerator_component" and \c denominator_component elements    #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_numerator.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_NUMERATOR_HPP
#define MS_QUANT_NUMERATOR_HPP

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
#include <vector>

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

    //! Represent \c numerator_component and \c denominator_component elements.
    class MS_MASCOTRESFILE_API ms_quant_numerator: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_numerator();

        //! Copying constructor.
        ms_quant_numerator(const ms_quant_numerator& src);

        //! Destructor.
        virtual ~ms_quant_numerator();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_numerator* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_numerator& operator=(const ms_quant_numerator& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c name attribute.
        bool haveName() const;

        //! Returns the value of the \c name attribute.
        std::string getName() const;

        //! Set a custom value for the \c name attribute.
        void setName(const char* value);

        //! Delete the \c name attribute.
        void dropName();

        //! Obtain a symbolic name for the \c name attribute schema type.
        std::string getNameSchemaType() const;


        //! Indicates presence of the \c coefficient attribute.
        bool haveCoefficient() const;

        //! Returns the value of the \c coefficient attribute.
        std::string getCoefficient() const;

        //! Set a custom value for the \c coefficient attribute.
        void setCoefficient(const char* value);

        //! Delete the \c coefficient attribute.
        void dropCoefficient();

        //! Obtain a symbolic name for the \c coefficient attribute schema type.
        std::string getCoefficientSchemaType() const;


        //! Returns string value of the element.
        std::string getContent() const;

        //! Set a custom value for the the element.
        void setContent(const char* value);

        //! Obtain a symbolic name for the element's schema type.
        std::string getContentSchemaType() const;

    private:
        std::string _name;
        bool _name_set;

        std::string _coefficient;
        bool _coefficient_set;

        std::string _value;

    }; // class ms_quant_numerator

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_NUMERATOR_HPP

/*------------------------------- End of File -------------------------------*/
