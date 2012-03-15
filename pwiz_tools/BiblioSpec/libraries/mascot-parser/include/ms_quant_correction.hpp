/*
##############################################################################
# file: ms_quant_correction.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates \c correction element from "quantitation.xml"-file             #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_correction.hpp,v $
 * @(#)$Revision: 1.10 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_CORRECTION_HPP
#define MS_QUANT_CORRECTION_HPP

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

    //! Represents a \c correction element.
    class MS_MASCOTRESFILE_API ms_quant_correction: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_correction();

        //! Copying constructor.
        ms_quant_correction(const ms_quant_correction& src);

        //! Destructor.
        virtual ~ms_quant_correction();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_correction* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_correction& operator=(const ms_quant_correction& right);
#endif

        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c shift attribute.
        bool haveShift() const;

        //! Returns the value of the \c shift attribute.
        int getShift() const;

        //! Set a custom value for the \c shift attribute.
        void setShift(const int value);

        //! Delete the \c shift attribute.
        void dropShift();

        //! Obtain a symbolic name for the \c shift attribute schema type.
        std::string getShiftSchemaType() const;


        //! Indicates presence of the \c type attribute.
        bool haveType() const;

        //! Returns the value of the \c type attribute.
        std::string getType() const;

        //! Set a custom value for the \c type attribute.
        void setType(const char* value);

        //! Delete the \c type attribute.
        void dropType();

        //! Obtain a symbolic name for the \c type attribute schema type.
        std::string getTypeSchemaType() const;


        //! Indicates presence of the \c element attribute.
        bool haveElement() const;

        //! Returns the value of the \c element attribute.
        std::string getElement() const;

        //! Set a custom value for the \c element attribute.
        void setElement(const char* value);

        //! Delete the \c element attribute.
        void dropElement();

        //! Obtain a symbolic name for the the \c element attribute schema type.
        std::string getElementSchemaType() const;


        //! Returns string value of the element.
        std::string getContent() const;

        //! Set a custom floating point number value as a string for the element content.
        void setContent(const char* value);

        //! Obtain a symbolic name for the element's schema type.
        std::string getContentSchemaType() const;

    private:
        int _shift;
        bool _shift_set;

        std::string _type;
        bool _type_set;

        std::string _element;
        bool _element_set;

        std::string _content;

    }; // class ms_quant_correction

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_CORRECTION_HPP

/*------------------------------- End of File -------------------------------*/
