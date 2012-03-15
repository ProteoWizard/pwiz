/*
##############################################################################
# file: ms_quant_component.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates component-element from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_component.hpp,v $
 * @(#)$Revision: 1.11 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_COMPONENT_HPP
#define MS_QUANT_COMPONENT_HPP

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

    class ms_quant_modgroup; // forward declaration
    class ms_quant_moverz; // forward declaration
    class ms_quant_correction; // forward declaration
    class ms_quant_isotope; // forward declaration
    class ms_quant_satellite; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single component element in <tt>quantitation.xml</tt>.
    /*!
     * Identifies a component used to calculate a ratio.
     */
    class MS_MASCOTRESFILE_API ms_quant_component: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_component();

        //! Copying constructor.
        ms_quant_component(const ms_quant_component& src);

        //! Destructor.
        virtual ~ms_quant_component();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_component* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_component& operator=(const ms_quant_component& right);
#endif

        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Check for presence of the \c moverz element.
        bool haveMoverz() const;

        //! Returns the value of the \c moverz element.
        const ms_quant_moverz* getMoverz() const;

        //! Supply a custom content for the \c moverz element.
        void setMoverz(const ms_quant_moverz* moverz);

        //! Delete the \c moverz element.
        void dropMoverz();

        //! Obtain a symbolic name for the \c moverz element schema type.
        std::string getMoverzSchemaType() const;


        //! Returns the number of nested modification groups.
        int getNumberOfModificationGroups() const;

        //! Deletes all modification groups from the list.
        void clearModificationGroups();

        //! Adds a new modification group at the end of the list.
        void appendModificationGroup(const ms_quant_modgroup *item);

        //! Returns a modification group object by its number.
        const ms_quant_modgroup * getModificationGroupByNumber(const int idx) const;

        //! Returns a modification group object by its name or a null value in case it is not found.
        const ms_quant_modgroup * getModificationGroupByName(const char *name) const;

        //! Update the information for a specific modification group refering to it by its index.
        bool updateModificationGroupByNumber(const int idx, const ms_quant_modgroup* modgroup);

        //! Update the information for a specific modification group refering to it by its unique name.
        bool updateModificationGroupByName(const char *name, const ms_quant_modgroup* modgroup);

        //! Remove a modification group from the list in memory by its index.
        bool deleteModificationGroupByNumber(const int idx);

        //! Remove a modification group from the list in memory by its unique name.
        bool deleteModificationGroupByName(const char *name);

        //! Obtain a symbolic name for the \c modification_group element schema type.
        std::string getModificationGroupSchemaType() const;


        //! Returns the number of \c isotope elements held.
        int getNumberOfIsotopes() const;

        //! Deletes all \c isotope elements from the list.
        void clearIsotopes();

        //! Adds a new \c isotope element at the end of the list.
        void appendIsotope(const ms_quant_isotope* isotope);

        //! Returns a \c isotope element object by its number.
        const ms_quant_isotope* getIsotope(const int idx) const;

        //! Update the information for a specific \c isotope element.
        bool updateIsotope(const int idx, const ms_quant_isotope* isotope);

        //! Remove a \c isotope element from the list.
        bool deleteIsotope(const int idx);

        //! Obtain a symbolic name for the \c isotope element schema type.
        std::string getIsotopeSchemaType() const;



        //! Check for presence of the \c file_index element.
        bool haveFileIndex() const;

        //! Returns the value of the \c file_index element.
        int getFileIndex() const;

        //! Supply a custom content for the \c file_index element.
        void setFileIndex(const int file_index);

        //! Delete the \c file_index element.
        void dropFileIndex();

        //! Obtain a symbolic name for the \c file_index element schema type.
        std::string getFileIndexSchemaType() const;


        //! Returns the number of \c correction elements held.
        int getNumberOfCorrections() const;

        //! Deletes all \c correction elements from the list.
        void clearCorrections();

        //! Adds a new \c correction element at the end of the list.
        void appendCorrection(const ms_quant_correction* correction);

        //! Returns a \c correction element object by its number.
        const ms_quant_correction* getCorrection(const int idx) const;

        //! Update the information for a specific \c correction element.
        bool updateCorrection(const int idx, const ms_quant_correction* isotope);

        //! Remove a \c correction element from the list.
        bool deleteCorrection(const int idx);

        //! Obtain a symbolic name for the \c correction element schema type.
        std::string getCorrectionSchemaType() const;


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


        //! Check for presence of the \c satellite element.
        bool haveSatellite() const;

        //! Returns the value of the \c satellite element.
        const ms_quant_satellite* getSatellite() const;

        //! Supply a custom content for the \c satellite element.
        void setSatellite(const ms_quant_satellite* satellite);

        //! Delete the \c satellite element.
        void dropSatellite();

        //! Obtain a symbolic name for the \c satellite element schema type.
        std::string getSatelliteSchemaType() const;

    private:
        // elements
        ms_quant_moverz *_pMoverz;
        bool _moverz_set;

        typedef std::vector< ms_quant_modgroup* > modgroup_vector;
        modgroup_vector _modgroups;

        typedef std::vector< ms_quant_isotope* > isotope_vector;
        isotope_vector _isotopes;

        int _fileIndex;
        bool _fileIndex_set;

        typedef std::vector< ms_quant_correction* > correction_vector;
        correction_vector _corrections;

        // attributes
        std::string _name;
        bool _name_set;

        ms_quant_satellite *_pSatellite;
        bool _satellite_set;
    }; // class ms_quant_component

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_COMPONENT_HPP

/*------------------------------- End of File -------------------------------*/

