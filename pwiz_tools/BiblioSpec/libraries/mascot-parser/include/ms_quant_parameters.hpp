/*
##############################################################################
# file: ms_quant_parameters.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates parametersType-definition from "quantitation.xml"-file        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_parameters.hpp,v $
 * @(#)$Revision: 1.11 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_PARAMETERS_HPP
#define MS_QUANT_PARAMETERS_HPP

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
}

namespace matrix_science {

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Parameter name and value pair.
    class MS_MASCOTRESFILE_API ms_quant_parameter: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
#ifdef __MINGW__
        //! Default constructor. 
        // MINGW compiler crashes without 'inline'.
        inline ms_quant_parameter();
#else
        //! Default constructor.
        ms_quant_parameter();
#endif

        //! Copying constructor.
        ms_quant_parameter(const ms_quant_parameter& src);

        //! Destructor.
        virtual ~ms_quant_parameter();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_parameter* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_parameter& operator=(const ms_quant_parameter& right);
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


        //! Indicates presence of the \c description attribute.
        bool haveDescription() const;

        //! Returns the value of the \c description attribute.
        std::string getDescription() const;

        //! Set a custom value for the \c description attribute.
        void setDescription(const char* value);

        //! Delete the \c description attribute.
        void dropDescription();

        //! Obtain a symbolic name for the \c description attribute schema type.
        std::string getDescriptionSchemaType() const;


        //! Returns the value of the \c value attribute.
        std::string getValue() const;

        //! Set a custom value for the \c value attribute.
        void setValue(const char* value);

        //! Obtain a symbolic name for the \c value attribute schema type.
        std::string getValueSchemaType() const;

    private:
        std::string _name;
        bool _name_set;

        std::string _description;
        bool _description_set;

        std::string _value;

    }; // class ms_quant_parameter

    //! A class that represents base \c parametersType in <tt>quantitation.xml</tt>.
    /*!
     * Holds a list of parameters.
     */
    class MS_MASCOTRESFILE_API ms_quant_parameters: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_parameters();

        //! Copying constructor.
        ms_quant_parameters(const ms_quant_parameters& src);

        //! Destructor.
        virtual ~ms_quant_parameters();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_parameters* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_parameters& operator=(const ms_quant_parameters& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of parameters held.
        int getNumberOfParameters() const;

        //! Deletes all parameters from the list.
        void clearParameters();

        //! Adds a new parameter at the end of the list.
        void appendParameter(const ms_quant_parameter *item);

        //! Returns a parameter object by its number.
        const ms_quant_parameter * getParameterByNumber(const int idx) const;

        //! Returns a parameter object by its name or a null value in case of not found.
        const ms_quant_parameter * getParameterByName(const char *name) const;

        //! Update the information for a specific parameter refering to it by its index.
        bool updateParameterByNumber(const int idx, const ms_quant_parameter *param);

        //! Update the information for a specific parameter refering to it by its unique name.
        bool updateParameterByName(const char *name, const ms_quant_parameter *param);

        //! Remove a parameter from the list in memory by its index.
        bool deleteParameterByNumber(const int idx);

        //! Remove a parameter from the list in memory by its unique name.
        bool deleteParameterByName(const char *name);

        //! Obtain a symbolic name for the parameter element schema type.
        std::string getParameterSchemaType() const;

    private:
        typedef std::vector< ms_quant_parameter* > entries_vector;
        entries_vector _entries;

    }; // class ms_quant_parameters

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_PARAMETERS_HPP

/*------------------------------- End of File -------------------------------*/

