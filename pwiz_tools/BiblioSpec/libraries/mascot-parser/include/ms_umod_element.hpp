/*
##############################################################################
# file: ms_umod_element.hpp                                                  #
# 'msparser' toolkit                                                         #
# Represents chemical element information from unimod.xml file               #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_element.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_ELEMENT_HPP
#define MS_UMOD_ELEMENT_HPP

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

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents an <tt>element</tt> object in <tt>unimod.xml</tt>.
    class MS_MASCOTRESFILE_API ms_umod_element: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_umod_xmlloader;
        friend class ms_umod_configfile;

    public:
        //! Default constructor.
        ms_umod_element();

        //! Copying constructor.
        ms_umod_element(const ms_umod_element& src);

        //! Destructor.
        virtual ~ms_umod_element();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_element* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_element& operator=(const ms_umod_element& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


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


        //! Indicates presence of the \c avge_mass attribute.
        bool haveAvgeMass() const;

        //! Returns the value of the \c avge_mass attribute as a string.
        std::string getAvgeMass() const;

        //! Returns the value of the \c avge_mass attribute as a floating point number.
        double getAvgeMassAsNumber() const;

        //! Set a custom string value for the \c avge_mass attribute.
        bool setAvgeMass(const char* value, ms_errs* err = NULL);

        //! Delete the \c avge_mass attribute.
        void dropAvgeMass();

        //! Obtain a symbolic name for the \c avge_mass attribute schema type.
        std::string getAvgeMassSchemaType() const;


        //! Indicates presence of the \c mono_mass attribute.
        bool haveMonoMass() const;

        //! Returns the value of the \c mono_mass attribute as a string.
        std::string getMonoMass() const;

        //! Returns the value of the \c mono_mass attribute as a floating point number.
        double getMonoMassAsNumber() const;

        //! Set a custom string value for the \c mono_mass attribute.
        bool setMonoMass(const char* value, ms_errs* err = NULL);

        //! Delete the \c mono_mass attribute.
        void dropMonoMass();

        //! Obtain a symbolic name for the \c mono_mass attribute schema type.
        std::string getMonoMassSchemaType() const;

    private:
        std::string _title;
        bool _title_set;

        std::string _fullName;
        bool _fullName_set;

        std::string _avgeMass;
        double _avgeMassDouble;
        bool _avgeMass_set;

        std::string _monoMass;
        double _monoMassDouble;
        bool _monoMass_set;
    }; // class ms_umod_element

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_ELEMENT_HPP

/*------------------------------- End of File -------------------------------*/
