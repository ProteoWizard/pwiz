/*
##############################################################################
# file: ms_quant_isotope.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates \c isotope element from "quantitation.xml"-file             #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_isotope.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_ISOTOPE_HPP
#define MS_QUANT_ISOTOPE_HPP

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

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents an <tt>isotope</tt> element.
    class MS_MASCOTRESFILE_API ms_quant_isotope: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_isotope();

        //! Copying constructor.
        ms_quant_isotope(const ms_quant_isotope& src);

        //! Destructor.
        virtual ~ms_quant_isotope();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_isotope* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_isotope& operator=(const ms_quant_isotope& right);
#endif

        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c old element.
        bool haveOld() const;

        //! Returns the value of the \c old element.
        std::string getOld() const;

        //! Set a custom value for the \c old element.
        void setOld(const char* value);

        //! Delete the \c old element.
        void dropOld();

        //! Obtain a symbolic name for the \c old element schema type.
        std::string getOldSchemaType() const;


        //! Indicates presence of the \c new element.
        bool haveNew() const;

        //! Returns the value of the \c new element.
        std::string getNew() const;

        //! Set a custom value for the \c new element.
        void setNew(const char* value);

        //! Delete the \c new element.
        void dropNew();

        //! Obtain a symbolic name for the \c new element schema type.
        std::string getNewSchemaType() const;

    private:

        std::string _old;
        bool _old_set;

        std::string _new;
        bool _new_set;

    }; // class ms_quant_isotope

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_ISOTOPE_HPP

/*------------------------------- End of File -------------------------------*/
