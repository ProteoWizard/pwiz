/*
##############################################################################
# file: ms_quant_modgroup.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates modification_group-element from "quantitation.xml"-file       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_modgroup.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_MODGROUP_HPP
#define MS_QUANT_MODGROUP_HPP

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

    class ms_quant_unmodified; // forward declaration
    class ms_quant_localdef; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single modification group element in <tt>quantitation.xml</tt>.
    /*!
     * Grouping to enable mode constraint (e.g. exclusive) to apply to a set of
     * modifications.
     */
    class MS_MASCOTRESFILE_API ms_quant_modgroup: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_modgroup();

        //! Copying constructor.
        ms_quant_modgroup(const ms_quant_modgroup& src);

        //! Destructor.
        virtual ~ms_quant_modgroup();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_modgroup* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_modgroup& operator=(const ms_quant_modgroup& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of nested \c mod_file elements.
        int getNumberOfModFiles() const;

        //! Deletes all \c mod_file elements from the list.
        void clearModFiles();

        //! Adds a new \c mod_file at the end of the list.
        void appendModFile(const char* mod);

        //! Return zero-based index of a mod_file if it is found in the list, -1 otherwise.
        int findModFile(const char* mod) const;

        //! Returns \c mod_file element content by its number.
        std::string getModFile(const int idx) const;

        //! Removes \c mod_file element from the list.
        bool deleteModFile(const int idx);

        //! Obtain a symbolic name for the \c mod_file element schema type.
        std::string getModFileSchemaType() const;


        //! Returns the number of nested \c unmodified elements.
        int getNumberOfUnmodified() const;

        //! Deletes all \c unmodified elements from the list.
        void clearUnmodified();

        //! Adds a new \c unmodified element at the end of the list.
        void appendUnmodified(const ms_quant_unmodified* unmodified);

        //! Returns an \c unmodified element object by its number.
        const ms_quant_unmodified* getUnmodified(const int idx) const;

        //! Supply new content for one of the \c unmodified elements in the list.
        bool updateUnmodified(const int idx, const ms_quant_unmodified* unmodified);

        //! Removes an \c unmodified element from the list.
        bool deleteUnmodified(const int idx);

        //! Obtain a symbolic name for the \c unmodified element schema type.
        std::string getUnmodifiedSchemaType() const;


        //! Returns the number of nested \c local_definition elements.
        int getNumberOfLocalDefinitions() const;

        //! Deletes all \c local_definition elements from the list.
        void clearLocalDefinitions();

        //! Adds a new \c local_definition element at the end of the list.
        void appendLocalDefinition(const ms_quant_localdef* localdef);

        //! Returns a \c local_definition element object by its number.
        const ms_quant_localdef* getLocalDefinition(const int idx) const;

        //! Supply new content for one of the \c local_definition elements in the list.
        bool updateLocalDefinition(const int idx, const ms_quant_localdef* localdef);

        //! Removes a \c local_definition element from the list.
        bool deleteLocalDefinition(const int idx);

        //! Obtain a symbolic name for the \c local_definition element schema type.
        std::string getLocalDefinitionSchemaType() const;


        //! Check for presence of the \c name attribute.
        bool haveName() const;

        //! Returns the value of the \c name attribute.
        std::string getName() const;

        //! Set a custom value for the \c name attribute.
        void setName(const char* value);

        //! Delete the \c name attribute.
        void dropName();

        //! Obtain a symbolic name for the \c name attribute schema type.
        std::string getNameSchemaType() const;


        //! Indicates presence of the \c mode attribute.
        bool haveMode() const;

        //! Returns the value of the \c mode attribute.
        std::string getMode() const;

        //! Set a custom value for the \c mode attribute.
        void setMode(const char* value);

        //! Delete the \c mode attribute.
        void dropMode();

        //! Obtain a symbolic name for the \c mode attribute schema type.
        std::string getModeSchemaType() const;


        //! Indicates presence of the \c required attribute.
        bool haveRequired() const;

        //! Returns the value of the \c required attribute.
        bool isRequired() const;

        //! Set a custom value for the \c required attribute.
        void setRequired(bool value);

        //! Delete the \c required attribute
        void dropRequired();

        //! Obtain a symbolic name for the \c required attribute schema type.
        std::string getRequiredSchemaType() const;

    private:

        typedef std::vector< std::string > modfile_vector;
        modfile_vector _modFiles;

        typedef std::vector< ms_quant_unmodified* > unmodified_vector;
        unmodified_vector _unmodified;

        typedef std::vector< ms_quant_localdef* > localdef_vector;
        localdef_vector _localdefs;

        std::string _name;
        bool _name_set;

        std::string _mode;
        bool _mode_set;

        bool m_required;
        bool m_required_set;

    }; // class ms_quant_modgroup

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_MODGROUP_HPP

/*------------------------------- End of File -------------------------------*/

