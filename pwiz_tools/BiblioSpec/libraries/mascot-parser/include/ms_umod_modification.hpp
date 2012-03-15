/*
##############################################################################
# file: ms_umod_modification.hpp                                             #
# 'msparser' toolkit                                                         #
# Represents modification object in unimod.xml file                          #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_modification.hpp,v $
 * @(#)$Revision: 1.13 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_MODIFICATION_HPP
#define MS_UMOD_MODIFICATION_HPP

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

    class ms_umod_composition; // forward declaration
    class ms_umod_specificity;  // forward declaration
    class ms_umod_xref; // forward declaration
    class ms_quant_localdef; // forward declaration
    class ms_quant_unmodified;  // forward declaration
    class ms_umod_configfile; // forward declaration
    class ms_quant_component; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents a <tt>modif</tt> object in <tt>unimod.xml</tt>.
    class MS_MASCOTRESFILE_API ms_umod_modification: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_umod_xmlloader;
        friend class ms_umod_configfile;

    public:
        //! Default constructor.
        ms_umod_modification();

        //! Copying constructor.
        ms_umod_modification(const ms_umod_modification& src);

        //! Copying constructor that copies the content from a <tt>quantitation.xml</tt> object.
        ms_umod_modification(const ms_quant_localdef& src, const ms_umod_configfile& umodFile);

        //! Copying constructor that copies the content from a <tt>quantitation.xml</tt> object.
        ms_umod_modification(const ms_quant_unmodified& src);

        //! Recalculates all deltas with isotope-substitution according to selected component in <tt>quantification.xml</tt>.
        void updateMasses(const ms_umod_configfile& umodFile, const ms_quant_component& quantComp);

        //! Destructor.
        virtual ~ms_umod_modification();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_modification* right);

        //! Copies all content from a <tt>quantitation.xml</tt> object.
        void copyFrom(const ms_quant_localdef* right, const ms_umod_configfile& umodFile);

        //! Copies all content from a <tt>quantitation.xml</tt> object.
        void copyFrom(const ms_quant_unmodified* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_modification& operator=(const ms_umod_modification& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns an ordered list of modification names in <tt>mod_file</tt>-style, i.e. "Acetyl (K)" instead of "Acetyl".
        std::vector< std::string > getModFileList(const unsigned int flags = ms_umod_configfile::MODFILE_FLAGS_ALL) const;

        //! Retrieves specificity group ID for <tt>mod_file</tt>-style modification name.
        int findSpecGroup(const char* modFileName) const;

        //! Returns the number of specificities currently held in memory.
        int getNumberOfSpecificities() const;

        //! Deletes all specificities from the list.
        void clearSpecificities();

        //! Adds a new specificity at the end of the list.
        void appendSpecificity(const ms_umod_specificity *specificity);

        //! Returns a specificity object by its number.
        const ms_umod_specificity * getSpecificity(const int idx) const;

        //! Update the information for a particular specificity.
        bool updateSpecificity(const int idx, const ms_umod_specificity *specificity);

        //! Remove a specificity from the list in memory.
        bool deleteSpecificity(const int idx);

        //! Obtain a symbolic name for the \c specificity element schema type.
        std::string getSpecificitySchemaType() const;


        //! Indicates presence of the \c delta element.
        bool haveDelta() const;

        //! Returns a read-only pointer to the object representing \c delta element.
        const ms_umod_composition* getDelta() const;

        //! Supply new content for the \c delta element.
        void setDelta(const ms_umod_composition* delta);

        //! Delete all information about the currently held \c delta element.
        void dropDelta();

        //! Obtain a symbolic name for the \c delta element schema type.
        std::string getDeltaSchemaType() const;


        //! Returns the number of \c Ignore objects currently held in memory.
        int getNumberOfIgnores() const;

        //! Deletes all \c Ignore elements from the list.
        void clearIgnores();

        //! Adds a new \c Ignore object at the end of the list.
        void appendIgnore(const ms_umod_composition *ignore);

        //! Returns an \c Ignore object by its number.
        const ms_umod_composition * getIgnore(const int idx) const;

        //! Update the information for a particular \c Ignore element.
        bool updateIgnore(const int idx, const ms_umod_composition *ignore);

        //! Remove an \c Ignore element from the list in memory.
        bool deleteIgnore(const int idx);

        //! Obtain a symbolic name for the \c Ignore element schema type.
        std::string getIgnoreSchemaType() const;


        //! Returns the number of \c alt_name elements currently held in memory.
        int getNumberOfAltNames() const;

        //! Deletes all \c alt_name elements from the list.
        void clearAltNames();

        //! Adds a new \c alt_name element at the end of the list.
        void appendAltName(const char *alt_name);

        //! Returns an \c alt_name element by its number.
        std::string getAltName(const int idx) const;

        //! Update the string for a particular \c alt_name element.
        bool updateAltName(const int idx, const char *alt_name);

        //! Remove an \c alt_name element from the list in memory.
        bool deleteAltName(const int idx);

        //! Obtain a symbolic name for the \c alt_name element schema type.
        std::string getAltNameSchemaType() const;


        //! Returns the number of \c xref elements currently held in memory.
        int getNumberOfXrefs() const;

        //! Deletes all \c xref elements from the list.
        void clearXrefs();

        //! Adds a new 'xref' object at the end of the list.
        void appendXref(const ms_umod_xref *xref);

        //! Returns a read-only pointer to \c xref element by its number.
        const ms_umod_xref* getXref(const int idx) const;

        //! Update the information for a particular \c xref element.
        bool updateXref(const int idx, const ms_umod_xref *xref);

        //! Remove an \c xref element from the list in memory.
        bool deleteXref(const int idx);

        //! Obtain a symbolic name for the \c xref element schema type.
        std::string getXrefSchemaType() const;


        //! Indicates presence of the \c misc_notes element.
        bool haveMiscNotes() const;

        //! Returns the \c misc_notes element or an empty string if not set.
        std::string getMiscNotes() const;

        //! Set a custom value for the \c misc_notes element.
        void setMiscNotes(const char* value);

        //! Delete the \c misc_notes element.
        void dropMiscNotes();

        //! Obtain a symbolic name for the \c misc_notes element schema type.
        std::string getMiscNotesSchemaType() const;


        //! Indicates presence of the \c title attribute.
        bool haveTitle() const;

        //! Returns the value of the \c title attribute.
        std::string getTitle() const;

        //! Set a custom value for the \c title attribute.
        void setTitle(const char* value);

        //! Delete the \c title attribute.
        void dropTitle();

        //! Obtain a symbolic name for the \c title attribute schema type.
        std::string getTitleSchemaType() const;


        //! Indicates presence of the \c full_name attribute.
        bool haveFullName() const;

        //! Returns the value of the \c full_name attribute.
        std::string getFullName() const;

        //! Set a custom value for the \c full_name attribute.
        void setFullName(const char* value);

        //! Delete the \c full_name attribute.
        void dropFullName();

        //! Obtain a symbolic name for the \c full_name attribute schema type.
        std::string getFullNameSchemaType() const;


        //! Indicates presence of the \c approved attribute.
        bool haveApproved() const;

        //! Returns the value of the \c approved attribute.
        bool isApproved() const;

        //! Set a custom value for the \c approved attribute.
        void setApproved(const bool value);

        //! Delete the \c approved attribute.
        void dropApproved();

        //! Obtain a symbolic name for the \c approved attribute schema type.
        std::string getApprovedSchemaType() const;


        //! Indicates presence of the \c username_of_poster attribute.
        bool haveUsernameOfPoster() const;

        //! Returns the value of the \c username_of_poster attribute.
        std::string getUsernameOfPoster() const;

        //! Set a custom value for the \c username_of_poster attribute.
        void setUsernameOfPoster(const char* value);

        //! Delete the \c username_of_poster attribute.
        void dropUsernameOfPoster();

        //! Obtain a symbolic name for the \c username_of_poster attribute schema type.
        std::string getUsernameOfPosterSchemaType() const;


        //! Indicates presence of the \c group_of_poster attribute.
        bool haveGroupOfPoster() const;

        //! Returns the value of the \c group_of_poster attribute.
        std::string getGroupOfPoster() const;

        //! Set a custom value for the \c group_of_poster attribute.
        void setGroupOfPoster(const char* value);

        //! Delete the \c group_of_poster attribute.
        void dropGroupOfPoster();

        //! Obtain a symbolic name for the \c group_of_poster attribute schema type.
        std::string getGroupOfPosterSchemaType() const;


        //! Indicates presence of the \c date_time_posted attribute.
        bool haveDateTimePosted() const;

        //! Returns the value of the \c date_time_posted attribute.
        std::string getDateTimePosted() const;

        //! Set a custom value for the \c date_time_posted attribute.
        void setDateTimePosted(const char* value);

        //! Delete the \c date_time_posted attribute.
        void dropDateTimePosted();

        //! Obtain a symbolic name for the \c date_time_posted attribute schema type.
        std::string getDateTimePostedSchemaType() const;


        //! Indicates presence of the \c date_time_modified attribute.
        bool haveDateTimeModified() const;

        //! Returns the value of the \c date_time_modified attribute.
        std::string getDateTimeModified() const;

        //! Set a custom value for the \c date_time_modified attribute.
        void setDateTimeModified(const char* value);

        //! Delete the \c date_time_modified attribute.
        void dropDateTimeModified();

        //! Obtain a symbolic name for the \c date_time_modified attribute schema type.
        std::string getDateTimeModifiedSchemaType() const;

        //! Indicates presence of the \c record_id attribute.
        bool haveRecordID() const;

        //! Returns the value of the \c record_id attribute.
        std::string getRecordID() const;

        //! Set a custom value for the \c record_id attribute.
        void setRecordID(const char* value);

        //! Delete the \c record_id attribute.
        void dropRecordID();

        //! Obtain a symbolic name for the \c record_id attribute schema type.
        std::string getRecordIDSchemaType() const;


    private:

        typedef std::vector< ms_umod_specificity * > specificity_vector;
        specificity_vector _specificities;

        ms_umod_composition* _delta;
        bool _delta_set;

        typedef std::vector< ms_umod_composition* > ignore_vector;
        ignore_vector _ignores;

        typedef std::vector< std::string > altNames_vector;
        altNames_vector _altnames;

        typedef std::vector< ms_umod_xref* > xref_vector;
        xref_vector _xrefs;

        std::string _miscNotes;
        bool _miscNotes_set;

        std::string _title;
        bool _title_set;

        std::string _fullName;
        bool _fullName_set;

        bool _approved;
        bool _approved_set;

        std::string _usernameOfPoster;
        bool _usernameOfPoster_set;

        std::string _groupOfPoster;
        bool _groupOfPoster_set;

        std::string _dateTimePosted;
        bool _dateTimePosted_set;

        std::string _dateTimeModified;
        bool _dateTimeModified_set;

        std::string _recordID;
        bool _recordID_set;
    }; // class ms_umod_modification

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_MODIFICATION_HPP

/*------------------------------- End of File -------------------------------*/
