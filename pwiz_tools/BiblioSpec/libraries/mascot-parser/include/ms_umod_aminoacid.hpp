/*
##############################################################################
# file: ms_umod_aminoacid.hpp                                                #
# 'msparser' toolkit                                                         #
# Represents 'amino acid' object from unimod.xml file                        #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_aminoacid.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_AMINOACID_HPP
#define MS_UMOD_AMINOACID_HPP

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

    class ms_umod_elemref; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents an "amino acid" object in <tt>unimod.xml</tt>.
    class MS_MASCOTRESFILE_API ms_umod_aminoacid: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_umod_xmlloader;
        friend class ms_umod_configfile;

    public:
        //! Default constructor.
        ms_umod_aminoacid();

        //! Copying constructor.
        ms_umod_aminoacid(const ms_umod_aminoacid& src);

        //! Destructor.
        virtual ~ms_umod_aminoacid();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_aminoacid* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_aminoacid& operator=(const ms_umod_aminoacid& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns a number of element reference objects currently held in memory.
        int getNumberOfElemRefs() const;

        //! Deletes all element reference objects from the list.
        void clearElemRefs();

        //! Adds a new element reference object at the end of the list.
        void appendElemRef(const ms_umod_elemref *elemref);

        //! Returns a read-only pointer to an element reference object by its number.
        const ms_umod_elemref* getElemRef(const int idx) const;

        //! Update the information for a particular element reference object.
        bool updateElemRef(const int idx, const ms_umod_elemref *elemref);

        //! Remove an element reference object from the list in memory.
        bool deleteElemRef(const int idx);

        //! Obtain a symbolic name for the element's schema type.
        std::string getElemRefSchemaType() const;


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


        //! Indicates presence of the \c three_letter attribute.
        bool haveThreeLetter() const;

        //! Returns the value of the \c three_letter attribute.
        std::string getThreeLetter() const;

        //! Set a custom value for the \c three_letter attribute.
        void setThreeLetter(const char* value);

        //! Delete the \c three_letter attribute.
        void dropThreeLetter();

        //! Obtain a symbolic name for the \c three_letter attribute schema type.
        std::string getThreeLetterSchemaType() const;


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


        //! Indicates presence of the \c avge_mass attribute.
        bool haveAvgeMass() const;

        //! Returns the value of the \c avge_mass attribute.
        double getAvgeMass() const;

        //! Set a custom value for the \c avge_mass attribute.
        void setAvgeMass(const double value);

        //! Delete the \c avge_mass attribute.
        void dropAvgeMass();

        //! Obtain a symbolic name for the \c avge_mass attribute schema type.
        std::string getAvgeMassSchemaType() const;


        //! Indicates presence of the \c mono_mass attribute.
        bool haveMonoMass() const;

        //! Returns the value of the \c mono_mass attribute.
        double getMonoMass() const;

        //! Set a custom value for the \c mono_mass attribute.
        void setMonoMass(const double value);

        //! Delete the \c mono_mass attribute.
        void dropMonoMass();

        //! Obtain a symbolic name for the \c mono_mass attribute schema type.
        std::string getMonoMassSchemaType() const;

    private:

        typedef std::vector< ms_umod_elemref* > elemref_vector;
        elemref_vector _elemRefs;

        std::string _title;
        bool _title_set;

        std::string _threeLetter;
        bool _threeLetter_set;

        std::string _fullName;
        bool _fullName_set;

        double _avgeMass;
        bool _avgeMass_set;

        double _monoMass;
        bool _monoMass_set;
    }; // class ms_umod_aminoacid

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_AMINOACID_HPP

/*------------------------------- End of File -------------------------------*/
