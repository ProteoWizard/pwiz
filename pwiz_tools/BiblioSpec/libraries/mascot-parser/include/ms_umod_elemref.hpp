/*
##############################################################################
# file: ms_umod_elemref.hpp                                                  #
# 'msparser' toolkit                                                         #
# Represents 'elemref_t' type in unimod.xml file                             #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_elemref.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_ELEMREF_HPP
#define MS_UMOD_ELEMREF_HPP

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

    class ms_quant_element; // forward declaration;
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents the <tt>elemref_t</tt> type in <tt>unimod.xml</tt>.
    /*!
     *  This type is used for compositions in order to specify number of each
     *  chemical element used in a composition.
     */
    class MS_MASCOTRESFILE_API ms_umod_elemref: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_umod_xmlloader;
        friend class ms_umod_configfile;

    public:
        //! Default constructor.
        ms_umod_elemref();

        //! Copying constructor.
        ms_umod_elemref(const ms_umod_elemref& src);

        //! Copying constructor.
        ms_umod_elemref(const ms_quant_element& src);


        //! Destructor.
        virtual ~ms_umod_elemref();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_elemref* right);

        //! Copies all content from another object.
        void copyFrom(const ms_quant_element* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_elemref& operator=(const ms_umod_elemref& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c symbol attribute.
        bool haveSymbol() const;

        //! Returns the value of the \c symbol attribute.
        std::string getSymbol() const;

        //! Set a custom value for the \c symbol attribute.
        void setSymbol(const char* value);

        //! Delete the \c symbol attribute.
        void dropSymbol();

        //! Obtain a symbolic name for the \c symbol attribute schema type.
        std::string getSymbolSchemaType() const;


        //! Indicates presence of the \c number attribute.
        bool haveNumber() const;

        //! Returns the value of the \c number attribute.
        int getNumber() const;

        //! Set a custom value for the \c number attribute.
        void setNumber(const int value);

        //! Delete the \c number attribute.
        void dropNumber();

        //! Obtain a symbolic name for the \c number attribute schema type.
        std::string getNumberSchemaType() const;

    private:

        std::string _symbol;
        bool _symbol_set;

        int _number;
        bool _number_set;
    }; // class ms_umod_elemref

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_ELEMREF_HPP

/*------------------------------- End of File -------------------------------*/
