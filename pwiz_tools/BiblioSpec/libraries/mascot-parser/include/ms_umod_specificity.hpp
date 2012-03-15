/*
##############################################################################
# file: ms_umod_specificity.hpp                                              #
# 'msparser' toolkit                                                         #
# Represents 'specificity' object from unimod.xml file                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_specificity.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_SPECIFICITY_HPP
#define MS_UMOD_SPECIFICITY_HPP

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
    class ms_umod_xmlloader;
}

namespace matrix_science {

    class ms_umod_neutralloss; // forward declaration
    class ms_quant_specificity; // forward declaration
    class ms_umod_configfile;  // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents modification specificity objects in <tt>unimod.xml</tt>.
    class MS_MASCOTRESFILE_API ms_umod_specificity
    {
        friend class msparser_internal::ms_umod_xmlloader;
        friend class ms_umod_modification;
        friend class ms_umod_configfile;

    public:
        //! Default constructor.
        ms_umod_specificity();

        //! Copying constructor.
        ms_umod_specificity(const ms_umod_specificity& src);

        //! Copying constructor that takes an object from <tt>quantitation.xml</tt>.
        ms_umod_specificity(const ms_quant_specificity& src, const ms_umod_configfile& umodFile);

        //! Destructor.
        virtual ~ms_umod_specificity();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_specificity* right);

        //! Copies all content from an object of <tt>quantitation.xml</tt>.
        void copyFrom(const ms_quant_specificity* right, const ms_umod_configfile& umodFile);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_specificity& operator=(const ms_umod_specificity& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of 'NeutralLoss' elements currently held in memory.
        int getNumberOfNeutralLosses() const;

        //! Deletes all 'NeutralLoss' elements from the list.
        void clearNeutralLosses();

        //! Adds a new 'NeutralLoss' element at the end of the list.
        void appendNeutralLoss(const ms_umod_neutralloss *nl);

        //! Returns a read-only pointer to a 'NeutralLoss' element by its number.
        const ms_umod_neutralloss* getNeutralLoss(const int idx) const;

        //! Update the information for a particular 'NeutralLoss' element.
        bool updateNeutralLoss(const int idx, const ms_umod_neutralloss *nl);

        //! Remove a 'NeutralLoss' element from the list in memory.
        bool deleteNeutralLoss(const int idx);

        //! Obtain a symbolic name for the 'NeutralLoss' element schema type.
        std::string getNeutralLossSchemaType() const;


        //! Returns the number of 'PepNeutralLoss' elements currently held in memory.
        int getNumberOfPepNeutralLosses() const;

        //! Deletes all 'PepNeutralLoss' elements from the list.
        void clearPepNeutralLosses();

        //! Adds a new 'PepNeutralLoss' element at the end of the list.
        void appendPepNeutralLoss(const ms_umod_neutralloss *pnl);

        //! Returns a read-only pointer to a 'PepNeutralLoss' element object by its number.
        const ms_umod_neutralloss* getPepNeutralLoss(const int idx) const;

        //! Update the information for a particular 'PepNeutralLoss' element.
        bool updatePepNeutralLoss(const int idx, const ms_umod_neutralloss *pnl);

        //! Remove a 'PepNeutralLoss' element object from the list in memory.
        bool deletePepNeutralLoss(const int idx);

        //! Obtain a symbolic name for the 'NeutralLoss' element schema type.
        std::string getPepNeutralLossSchemaType() const;


        //! Indicates presence of the \c misc_notes element.
        bool haveMiscNotes() const;

        //! Returns the value of the \c misc_notes element.
        std::string getMiscNotes() const;

        //! Set a custom value for the \c misc_notes element.
        void setMiscNotes(const char* value);

        //! Delete the \c misc_notes element.
        void dropMiscNotes();

        //! Obtain a symbolic name for the \c misc_notes element schema type.
        std::string getMiscNotesSchemaType() const;


        //! Indicates presence of the \c hidden attribute.
        bool haveHidden() const;

        //! Returns the value of the \c hidden attribute.
        bool isHidden() const;

        //! Set a custom value for the \c hidden attribute.
        void setHidden(const bool value);

        //! Delete the \c hidden attribute.
        void dropHidden();

        //! Obtain a symbolic name for the \c hidden attribute schema type.
        std::string getHiddenSchemaType() const;


        //! Indicates presence of the \c site attribute.
        bool haveSite() const;

        //! Returns the value of the \c site attribute.
        std::string getSite() const;

        //! Set a custom value for the \c site attribute.
        void setSite(const char* value);

        //! Delete the \c site attribute.
        void dropSite();

        //! Obtain a symbolic name for the \c site attribute schema type.
        std::string getSiteSchemaType() const;


        //! Indicates presence of the \c position attribute.
        bool havePosition() const;

        //! Returns the value of the \c position attribute.
        std::string getPosition() const;

        //! Set a custom value for the \c position attribute.
        void setPosition(const char* value);

        //! Delete the \c position attribute.
        void dropPosition();

        //! Obtain a symbolic name for the \c position attribute schema type.
        std::string getPositionSchemaType() const;


        //! Indicates presence of the \c classification attribute.
        bool haveClassification() const;

        //! Returns the value of the \c classification attribute.
        std::string getClassification() const;

        //! Set a custom value for the \c classification attribute.
        void setClassification(const char* value);

        //! Delete the \c classification attribute.
        void dropClassification();

        //! Obtain a symbolic name for the \c classification attribute schema type.
        std::string getClassificationSchemaType() const;


        //! Indicates presence of the \c spec_group attribute.
        bool haveSpecGroup() const;

        //! Returns the value of the \c spec_group attribute.
        int getSpecGroup() const;

        //! Set a custom value for the \c spec_group attribute.
        void setSpecGroup(const int value);

        //! Delete the \c spec_group attribute.
        void dropSpecGroup();

        //! Obtain a symbolic name for the \c spec_group attribute schema type.
        std::string getSpecGroupSchemaType() const;

    private:

        typedef std::vector< ms_umod_neutralloss* > nl_vector;
        nl_vector _neutrallosses;

        typedef std::vector< ms_umod_neutralloss* > pnl_vector;
        pnl_vector _pepneutrallosses;

        std::string _miscNotes;
        bool _miscNotes_set;

        bool _hidden;
        bool _hidden_set;

        std::string _site;
        bool _site_set;

        std::string _position;
        bool _position_set;

        std::string _classification;
        bool _classification_set;

        int _specGroup;
        bool _specGroup_set;

    }; // class ms_umod_specificity

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_SPECIFICITY_HPP

/*------------------------------- End of File -------------------------------*/
