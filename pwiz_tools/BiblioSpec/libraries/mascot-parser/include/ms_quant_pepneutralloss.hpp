/*
##############################################################################
# file: ms_quant_pepneutralloss.hpp                                          #
# 'msparser' toolkit                                                         #
# Encapsulates "PepNeutralLoss" element from "quantitation.xml"-file         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_pepneutralloss.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_PEPNEUTRALLOSS_HPP
#define MS_QUANT_PEPNEUTRALLOSS_HPP

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

    //! A neutral loss from the precursor.
    class MS_MASCOTRESFILE_API ms_quant_pepneutralloss: public ms_quant_composition
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_pepneutralloss();

        //! Copying constructor.
        ms_quant_pepneutralloss(const ms_quant_pepneutralloss& src);

        //! Destructor.
        virtual ~ms_quant_pepneutralloss();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_pepneutralloss* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_pepneutralloss& operator=(const ms_quant_pepneutralloss& right);
#endif        
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c required attribute.
        bool haveRequired() const;

        //! Returns the value of the \c required attribute.
        bool isRequired() const;

        //! Set a custom value for the \c required attribute.
        void setRequired(const bool required);

        //! Deletes the \c required attribute.
        void dropRequired();

        //! Obtain a symbolic name for the \c required attribute schema type.
        std::string getRequiredSchemaType() const;

    private:

        bool _required;
        bool _required_set;

    }; // class ms_quant_pepneutralloss

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_NEUTRALLOSS_HPP

/*------------------------------- End of File -------------------------------*/
