/*
##############################################################################
# file: ms_xml_schema.hpp                                                    #
# 'msparser' toolkit                                                         #
# Can be used to extract type info from .xsd-file                            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_xml_schema.hpp,v $
 * @(#)$Revision: 1.7 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */
#ifndef MS_XML_SCHEMA_HPP
#define MS_XML_SCHEMA_HPP

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

namespace msparser_internal {
    class ms_quant_xmlloader;
    class ms_umod_xmlloader;
}

namespace matrix_science {

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! This class is used to retrieve type information for elements from XML files.
    /*!
     * Type information is retrieved from the XML schema file by type name. Use
     * specific XML objects to find out their types.
     */
    class MS_MASCOTRESFILE_API ms_xml_schema: public ms_errors
    {
        friend class msparser_internal::ms_quant_xmlloader;
        friend class msparser_internal::ms_umod_xmlloader;

    public:
        //! Default constructor.
        ms_xml_schema();

        //! Copying constructor.
        ms_xml_schema(const ms_xml_schema& src);

        //! Destructor.
        ~ms_xml_schema();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_xml_schema* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_xml_schema& operator=(const ms_xml_schema& right);
#endif
        //! Set custom file path.
        void setFileName(const char* str);

        //! Get current file path.
        std::string getFileName() const;

        //! Read and parse the whole schema-file at once.
        void read_file();

        //! Returns total number of type definitions in the schema file.
        int getNumberOfTypes() const;

        //! Retrieves a type name by its zero-based index.
        std::string getTypeName(const int idx) const;

        //! Returns an object that represents the requested type information.
        const ms_xml_typeinfo* getTypeInfoByName(const char* typeName) const;

        //! Returns an object that represents the requested type information.
        const ms_xml_typeinfo* getTypeInfoByNumber(const int idx) const;


        //! Returns TRUE for types explicitly defined in the schema file and FALSE for standard types.
        bool isCustomType(const char* typeName) const;

        //! Can be used for validating values of standard types that are not represented by an library class.
        std::string validateSimpleString(const char* strValue, const char* typeName) const;

        //! Can be used for validating values of standard integer types that are not represented by a library class.
        std::string validateSimpleInteger(const int intValue, const char* typeName) const;

        //! Can be used for validating values of standard floating point types that are not represented by a library class.
        std::string validateSimpleDouble(const double dblValue, const char* typeName) const;

        //! Can be used for validating values of boolean types or types derived from boolean.
        std::string validateSimpleBool(const bool boolValue, const char* typeName) const;

        //! Can be used for validating complex type objects uniformly.
        std::string validateComplexObject(const ms_xml_IValidatable* pObj, const bool bDeep = false) const;

    public:
        int _schemaFor;

    private:

        void clearTypes();

        std::string _targetFile;

        std::vector< ms_xml_typeinfo* > _vecTypeInfo;


    }; // class ms_xml_schema

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_XML_SCHEMA_HPP

/*------------------------------- End of File -------------------------------*/

