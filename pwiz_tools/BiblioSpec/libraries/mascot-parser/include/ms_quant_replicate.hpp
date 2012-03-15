/*
##############################################################################
# file: ms_quant_replicate.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates replicate-element from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_replicate.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_REPLICATE_HPP
#define MS_QUANT_REPLICATE_HPP

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

    //! An object of this class represent a single \c replicate element in <tt>quantitation.xml</tt>.
    /*!
     * Serves as a possible sub-element of the \c protocol element.
     */
    class MS_MASCOTRESFILE_API ms_quant_replicate: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_replicate();

        //! Copying constructor.
        ms_quant_replicate(const ms_quant_replicate& src);

        //! Destructor.
        virtual ~ms_quant_replicate();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_replicate* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_replicate& operator=(const ms_quant_replicate& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


    private:

    }; // class ms_quant_replicate

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_REPLICATE_HPP

/*------------------------------- End of File -------------------------------*/
