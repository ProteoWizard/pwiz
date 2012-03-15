/*
##############################################################################
# file: ms_umod_xref.hpp                                                     #
# 'msparser' toolkit                                                         #
# Represents 'xref_t' type in unimod.xml file                                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_umod_xref.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_UMOD_XREF_HPP
#define MS_UMOD_XREF_HPP

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

    //! Represents the cross references objects in <tt>unimod.xml</tt>.
    class MS_MASCOTRESFILE_API ms_umod_xref: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_umod_xmlloader;

    public:
        //! Default constructor.
        ms_umod_xref();

        //! Copying constructor.
        ms_umod_xref(const ms_umod_xref& src);

        //! Destructor.
        virtual ~ms_umod_xref();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_umod_xref* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_umod_xref& operator=(const ms_umod_xref& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c text element.
        bool haveText() const;

        //! Returns the value of the \c text element.
        std::string getText() const;

        //! Set a custom value for the \c text element.
        void setText(const char* value);

        //! Delete the \c text element.
        void dropText();

        //! Obtain a symbolic name for the \c text element schema type.
        std::string getTextSchemaType() const;


        //! Indicates presence of the \c source element.
        bool haveSource() const;

        //! Returns the value of the \c source element.
        std::string getSource() const;

        //! Set a custom value for the \c source element.
        void setSource(const char* value);

        //! Delete the \c source element.
        void dropSource();

        //! Obtain a symbolic name for the \c source element schema type.
        std::string getSourceSchemaType() const;


        //! Indicates presence of the \c url element.
        bool haveUrl() const;

        //! Returns the value of the \c url element.
        std::string getUrl() const;

        //! Set a custom value for the \c url element.
        void setUrl(const char* value);

        //! Delete the \c url element.
        void dropUrl();

        //! Obtain a symbolic name for the \c url element schema type.
        std::string getUrlSchemaType() const;

    private:

        std::string _text;
        bool _text_set;

        std::string _source;
        bool _source_set;

        std::string _url;
        bool _url_set;
    }; // class ms_umod_xref

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_XREF_HPP

/*------------------------------- End of File -------------------------------*/
