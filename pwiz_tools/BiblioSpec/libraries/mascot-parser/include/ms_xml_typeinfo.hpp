/*
##############################################################################
# file: ms_xml_typeinfo.hpp                                                  #
# 'msparser' toolkit                                                         #
# Represents schema type info from .xsd-file                                 #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_xml_typeinfo.hpp,v $
 * @(#)$Revision: 1.7 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */
#ifndef MS_XML_TYPEINFO_HPP
#define MS_XML_TYPEINFO_HPP

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

namespace matrix_science {

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An abstract interface for classes that represent complex elements in an XML file.
    /*!
     * For languages that support polymorphism and virtual inheritance (like
     * C++ and Java), this interface can be used for uniform validation of all
     * XML objects. Every XML class is derived from this interface and,
     * therefore, an instance of such classes cal be passed to
     * validation methods of ms_xml_schema.
     *
     * Alternatively, values of simple types can be validated with the
     * validateSimpleXXX() methods of ms_xml_schema and complex objects
     * can be validated using their own validation methods.
     */
    class MS_MASCOTRESFILE_API ms_xml_IValidatable
    {
    public:
        virtual std::string getSchemaType() const = 0;
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const = 0;
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const = 0;
    }; // class ms_xml_IValidatable

    //! Defines possible values for attribute usage.
    /*!
     * See \ref DynLangEnums.
     */
    enum MS_XML_ATTRIBUTE_USE
    {
        MS_XML_ATTRIBUTE_REQUIRED = 0, //!< A required attribute.
        MS_XML_ATTRIBUTE_OPTIONAL = 1 //!< An optional attribute.
    };

    //! This class is used to represent a type for elements from XML files.
    /*!
     * Used in connection with ms_xml_schema.
     */
    class MS_MASCOTRESFILE_API ms_xml_typeinfo
    {
        friend class ms_xml_schema;
    public:
        //! Default constructor.
        ms_xml_typeinfo();

        //! Copying constructor.
        ms_xml_typeinfo(const ms_xml_typeinfo& src);

        //! Destructor.
        ~ms_xml_typeinfo();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_xml_typeinfo* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_xml_typeinfo& operator=(const ms_xml_typeinfo& right);
#endif
        //! Returns current type name.
        std::string getTypeName() const;

        //! Returns TRUE for complex types with children and attributes.
        bool isComplexSequence() const;

        //! Returns TRUE for complex types with mutually exclusive children.
        bool isComplexChoice() const;

        //! Returns TRUE if the current type is an extenstion of another (base) type.
        bool isExtensionType() const;

        //! Returns TRUE if the current type is a restriction of another (base) type.
        bool isRestrictionType() const;

        //! Base type for restrictions and extensions, otherwise an empty string.
        std::string getBaseType() const;

        //! Returns annotation string for the current type.
        std::string getTypeAnnotation() const;


        //! Does a restriction have minInclusive property for integers and doubles?
        bool hasMinInclusive() const;

        //! Returns double minInclusive property value that can be cast to an integer if needed.
        double getMinInclusive() const;

        //! Does a restriction have \a maxInclusive property for integers and doubles?
        bool hasMaxInclusive() const;

        //! Returns double \a maxInclusive property value that can be cast to an integer if needed.
        double getMaxInclusive() const;

        //! Does a restriction have \a minLength property for strings?
        bool hasMinLength() const;

        //! Returns integer \a minLength property value.
        int getMinLength() const;

        //! Does a restriction have \a maxLength property for strings?
        bool hasMaxLength() const;

        //! Returns integer \a maxLength property value.
        int getMaxLength() const;

        //! Returns number of enumeration values allowed for the current restriction.
        int getNumberOfEnumerations() const;

        //! Retrieves a specific string enumeration value by its index.
        std::string getEnumerationValue(const int idx) const;

        //! Retrieves an annotation for a specific enumeration value by enumeration's index.
        std::string getEnumerationAnnotation(const int idx) const;


        //! Returns number of child element definitions for extenstions, complex sequences or complex choices.
        int getNumberOfElements() const;

        //! Returns name for a specific child element by its index.
        std::string getElementName(const int idx) const;

        //! Retrieves element index by name.
        int findElement(const char* name) const;

        //! Returns type name for a specific child element by its index.
        std::string getElementType(const int idx) const;

        //! Returns a specific element annotation.
        std::string getElementAnnotation(const int idx) const;

        //! Returns minimal number of occurences for the element referenced by an index.
        int getElementMinOccurs(const int idx) const;

        //! Returns maximum number of occurences for the element referenced by an index.
        int getElementMaxOccurs(const int idx) const;


        //! Returns number of attribute definitions for extensions, complex sequences or complex choices.
        int getNumberOfAttributes() const;

        //! Returns name for a specific attribute by its index.
        std::string getAttributeName(const int idx) const;

        //! Retrieves attribute index by name.
        int findAttribute(const char* name) const;

        //! Returns type name for a specific attribute by its index.
        std::string getAttributeType(const int idx) const;

        //! Returns a specific attribute annotation.
        std::string getAttributeAnnotation(const int idx) const;

        //! Returns attribute usage.
        MS_XML_ATTRIBUTE_USE getAttributeUse(const int idx) const;

        //! Returns TRUE if a default value is specified for the attribute.
        bool hasAttributeDefault(const int idx) const;

        //! Retrieves the attribute default value.
        std::string getAttributeDefault(const int idx) const;

    private:

        std::string _typeName;

        bool _complexSequence;
        bool _complexChoice;
        bool _extensionType;
        bool _restrictionType;
        std::string _baseType;
        std::string _typeAnnotation;

        bool _hasMinInclusive;
        double _minInclusive;
        bool _hasMaxInclusive;
        double _maxInclusive;
        bool _hasMinLength;
        int _minLength;
        bool _hasMaxLength;
        int _maxLength;
        typedef std::vector< std::string > VectorString;
        VectorString _enumerations;
        VectorString _enumerationAnnotations;

        VectorString _elementNames;
        VectorString _elementTypes;
        VectorString _elementAnnotations;
        typedef std::vector< int > VectorInt;
        VectorInt _elementMinOccurs;
        VectorInt _elementMaxOccurs;

        VectorString _attributeNames;
        VectorString _attributeTypes;
        VectorString _attributeAnnotations;
        VectorInt   _attributeUses;
        typedef std::vector< bool > VectorBool;
        VectorBool _attributeHasDefault;
        VectorString _attributeDefaults;

    }; // class ms_xml_typeinfo

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_XML_TYPEINFO_HPP

/*------------------------------- End of File -------------------------------*/

