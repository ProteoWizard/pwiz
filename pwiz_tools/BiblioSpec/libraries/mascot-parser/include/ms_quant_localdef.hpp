/*
##############################################################################
# file: ms_quant_localdef.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates \c local_definition element from "quantitation.xml"-file       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_localdef.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_LOCALDEF_HPP
#define MS_QUANT_LOCALDEF_HPP

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

    class ms_quant_specificity; // forward declaration
    class ms_quant_composition; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents a <tt>local_definition</tt> element.
    class MS_MASCOTRESFILE_API ms_quant_localdef: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_localdef();

        //! Copying constructor.
        ms_quant_localdef(const ms_quant_localdef& src);

        //! Destructor.
        virtual ~ms_quant_localdef();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_localdef* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_localdef& operator=(const ms_quant_localdef& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of \c specificity elements held.
        int getNumberOfSpecificities() const;

        //! Deletes all \c specificity elements from the list.
        void clearSpecificities();

        //! Adds a new \c specificity element at the end of the list.
        void appendSpecificity(const ms_quant_specificity *specificity);

        //! Returns a \c specificity element object by its number.
        const ms_quant_specificity * getSpecificity(const int idx) const;

        //! Update the information for a specific \c specificity element.
        bool updateSpecificity(const int idx, const ms_quant_specificity* specificity);

        //! Remove a \c specificity element from the list.
        bool deleteSpecificity(const int idx);

        //! Obtain a symbolic name for the \c specificity element schema type.
        std::string getSpecificitySchemaType() const;


        //! Check presence of the \c delta element.
        bool haveDelta() const;

        //! Returns a pointer to the \c delta element.
        const ms_quant_composition* getDelta() const;

        //! Supply custom content for the \c delta element.
        void setDelta(const ms_quant_composition *delta);

        //! Deletes the \c delta element.
        void dropDelta();

        //! Obtain a symbolic name for the \c delta element schema type.
        std::string getDeltaSchemaType() const;


        //! Returns the number of \c Ignore elements held.
        int getNumberOfIgnores() const;

        //! Deletes all \c Ignore elements from the list.
        void clearIgnores();

        //! Adds a new \c Ignore element at the end of the list.
        void appendIgnore(const ms_quant_composition *ignore);

        //! Returns an \c Ignore element object by its number.
        const ms_quant_composition * getIgnore(const int idx) const;

        //! Update the information for a specific \c Ignore element.
        bool updateIgnore(const int idx, const ms_quant_composition* ignore);

        //! Remove an \c Ignore element from the list.
        bool deleteIgnore(const int idx);

        //! Obtain a symbolic name for the \c Ignore element schema type.
        std::string getIgnoreSchemaType() const;


        //! Check presence of the \c title attribute.
        bool haveTitle() const;

        //! Returns the value of the \c title attribute.
        std::string getTitle() const;

        //! Set a custom value for the \c title attribute.
        void setTitle(const char* value);

        //! Deletes the \c title attribute.
        void dropTitle();

        //! Obtain a symbolic name for the \c title attribute schema type.
        std::string getTitleSchemaType() const;


    private:
        // elements
        typedef std::vector< ms_quant_specificity* > specificity_vector;
        specificity_vector _specificities;

        ms_quant_composition* _pDelta;
        bool _delta_set;

        typedef std::vector< ms_quant_composition* > composition_vector;
        composition_vector _ignores;

        // attributes
        std::string _title;
        bool _title_set;

    }; // class ms_quant_localdef

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_LOCALDEF_HPP

/*------------------------------- End of File -------------------------------*/
