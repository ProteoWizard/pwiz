/*
##############################################################################
# file: ms_quant_normalisation.hpp                                           #
# 'msparser' toolkit                                                         #
# Encapsulates normalisation-element from "quantitation.xml"-file            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_normalisation.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_NORMALISATION_HPP
#define MS_QUANT_NORMALISATION_HPP

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
    class ms_quant_normalisation_peptides; // forward declaration
    class ms_quant_normalisation_proteins; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single \c normalisation element in <tt>quantitation.xml</tt>.
    /*!
     * Method of normalising ratios for a complete data set.
     */
    class MS_MASCOTRESFILE_API ms_quant_normalisation: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_normalisation();

        //! Copying constructor.
        ms_quant_normalisation(const ms_quant_normalisation& src);

        //! Destructor.
        virtual ~ms_quant_normalisation();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_normalisation* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_normalisation& operator=(const ms_quant_normalisation& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c method attribute.
        bool haveMethod() const;

        //! Returns the value of the \c method attribute.
        std::string getMethod() const;

        //! Set a custom value for the \c method attribute.
        void setMethod(const char* value);

        //! Delete the \c method attribute.
        void dropMethod();

        //! Obtain a symbolic name for the \c method attribute schema type.
        std::string getMethodSchemaType() const;


        //! Indicates presence of the \c peptides element
        bool havePeptides() const;

        //! Returns a pointer to the \c peptides element.
        const ms_quant_normalisation_peptides* getPeptides() const;

        //! Supply custom content for \c peptides element.
        void setPeptides(const ms_quant_normalisation_peptides* peptides);

        //! Delete the \c peptides element.
        void dropPeptides();

        //! Obtain a symbolic name for the \c peptides element schema type.
        std::string getPeptidesSchemaType() const;


        //! Indicates presence of the \c proteins element.
        bool haveProteins() const;

        //! Returns a pointer to the \c proteins element.
        const ms_quant_normalisation_proteins* getProteins() const;

        //! Supply custom content for the \c proteins element.
        void setProteins(const ms_quant_normalisation_proteins* proteins);

        //! Delete the \c proteins element.
        void dropProteins();

        //! Obtain a symbolic name for the \c proteins element schema type.
        std::string getProteinsSchemaType() const;

    private:

        std::string _method;
        bool _method_set;

        ms_quant_normalisation_peptides *_pPeptides;
        bool _peptides_set;

        ms_quant_normalisation_proteins *_pProteins;
        bool _proteins_set;
    }; // class ms_quant_normalisation

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_NORMALISATION_HPP

/*------------------------------- End of File -------------------------------*/

