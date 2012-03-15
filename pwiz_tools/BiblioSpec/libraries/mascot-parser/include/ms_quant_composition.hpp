/*
##############################################################################
# file: ms_quant_composition.hpp                                             #
# 'msparser' toolkit                                                         #
# Encapsulates "compositionType" from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_composition.hpp,v $
 * @(#)$Revision: 1.11 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_COMPOSITION_HPP
#define MS_QUANT_COMPOSITION_HPP

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

    //! The <tt>element</tt> sub-element of <tt>compositionType</tt> in <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_element: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
#ifdef __MINGW__
        //! Default constructor. 
        // MINGW compiler crashes without 'inline'.
        inline ms_quant_element();
#else
        //! Default constructor.
        ms_quant_element();
#endif

        //! Copying constructor.
        ms_quant_element(const ms_quant_element& src);

        //! Destructor.
        virtual ~ms_quant_element();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_element* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_element& operator=(const ms_quant_element& right);
#endif
        
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c symbol attribute.
        bool haveSymbol() const;

        //! Returns the value of the \c symbol attribute.
        std::string getSymbol() const;

        //! Set a custom value for the \c symbol attribute.
        void setSymbol(const char* symbol);

        //! Deletes the \c symbol attribute.
        void dropSymbol();

        //! Obtain a symbolic name for the \c symbol attribute schema type.
        std::string getSymbolSchemaType() const;

        
        //! Indicates presence of the \c number attribute.
        bool haveNumber() const;

        //! Returns the value of the \c number attribute.
        int getNumber() const;

        //! Set a custom value for the \c number attribute.
        void setNumber(const int number);

        //! Deletes the \c number attribute.
        void dropNumber();

        //! Obtain a symbolic name for the \c number attribute schema type.
        std::string getNumberSchemaType() const;


    private:
        std::string _symbol;
        bool _symbol_set;

        int _number;
        bool _number_set;

    }; // class ms_quant_element

    //! Describes the <tt>compositionType</tt> type in <tt>quantitation.xml</tt>.
    /*!
     * Objects of this type host a list of nested \c element elements.
     */
    class MS_MASCOTRESFILE_API ms_quant_composition: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_composition();

        //! Copying constructor.
        ms_quant_composition(const ms_quant_composition& src);

        //! Destructor.
        virtual ~ms_quant_composition();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_composition* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_composition& operator=(const ms_quant_composition& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns a number of elements held.
        int getNumberOfElements() const;

        //! Deletes all elements from the list.
        void clearElements();

        //! Adds a new element at the end of the list.
        void appendElement(const ms_quant_element *element);

        //! Returns an element object by its number.
        const ms_quant_element * getElement(const int idx) const;

        //! Update the information for a specific element.
        bool updateElement(const int idx, const ms_quant_element* element);

        //! Remove an element from the list.
        bool deleteElement(const int idx);

        //! Obtain a symbolic name for the element's schema type.
        std::string getElementSchemaType() const;

    private:
        typedef std::vector< ms_quant_element* > element_vector;
        element_vector _elements;

    }; // class ms_quant_composition

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_COMPOSITION_HPP

/*------------------------------- End of File -------------------------------*/
