/*
##############################################################################
# file: ms_quant_normalisation_protein.hpp                                   #
# 'msparser' toolkit                                                         #
# Encapsulates protein-element from "quantitation.xml"-file                  #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2009 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_normalisation_protein.hpp,v $
 * @(#)$Revision: 1.5 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_NORMALISATION_PROTEIN_HPP
#define MS_QUANT_NORMALISATION_PROTEIN_HPP

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

    class ms_quant_modgroup; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single protein element in <tt>quantitation.xml</tt>.
    /*!
     * Identifies a protein to be used for normalisation.
     */
    class MS_MASCOTRESFILE_API ms_quant_normalisation_protein: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_normalisation_protein();

        //! Copying constructor.
        ms_quant_normalisation_protein(const ms_quant_normalisation_protein& src);

        //! Destructor.
        virtual ~ms_quant_normalisation_protein();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_normalisation_protein* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_normalisation_protein& operator=(const ms_quant_normalisation_protein& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Check for presence of the \c accession attribute.
        bool haveAccession() const;

        //! Returns the value of the \c accession attribute.
        std::string getAccession() const;

        //! Supply a custom content for \c accession attribute.
        void setAccession(const char * value);

        //! Delete the \c accession attribute.
        void dropAccession();

        //! Obtain a symbolic name for the \c accession attribute schema type.
        std::string getAccessionSchemaType() const;

    private:

        std::string _accession;
        bool _accession_set;
    }; // class ms_quant_normalisation_protein

    //! An object of this class represent a collection of peptide elements in <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_normalisation_proteins: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_normalisation_proteins();

        //! Copying constructor.
        ms_quant_normalisation_proteins(const ms_quant_normalisation_proteins& src);

        //! Destructor.
        virtual ~ms_quant_normalisation_proteins();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_normalisation_proteins* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_normalisation_proteins& operator=(const ms_quant_normalisation_proteins& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns a number of nested \c protein elements.
        int getNumberOfProteins() const;

        //! Deletes all \c protein elements from the list.
        void clearProteins();

        //! Adds a new \c protein element at the end of the list.
        void appendProtein(const ms_quant_normalisation_protein* protein);

        //! Returns the value of the \c protein element object by its number.
        const ms_quant_normalisation_protein* getProtein(const int idx) const;

        //! Supply new content for one of the \c protein elements in the list.
        bool updateProtein(const int idx, const ms_quant_normalisation_protein* protein);

        //! Removes a \c protein element from the list.
        bool deleteProtein(const int idx);

        //! Obtain a symbolic name for the \c protein element schema type.
        std::string getProteinSchemaType() const;

    private:

        typedef std::vector< ms_quant_normalisation_protein* > prot_vector;
        prot_vector _proteins;
    }; // class ms_quant_normalisation_proteins

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_NORMALISATION_PROTEIN_HPP

/*------------------------------- End of File -------------------------------*/

