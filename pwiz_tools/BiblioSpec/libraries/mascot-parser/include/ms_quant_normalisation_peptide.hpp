/*
##############################################################################
# file: ms_quant_normalisation_peptide.hpp                                   #
# 'msparser' toolkit                                                         #
# Encapsulates peptide-element from "quantitation.xml"-file                  #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2009 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_normalisation_peptide.hpp,v $
 * @(#)$Revision: 1.5 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_NORMALISATION_PEPTIDE_HPP
#define MS_QUANT_NORMALISATION_PEPTIDE_HPP

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

    //! An object of this class represent a single peptide element in <tt>quantitation.xml</tt>.
    /*!
     * Identifies a peptide sequence to be used for normalisation.
     */
    class MS_MASCOTRESFILE_API ms_quant_normalisation_peptide: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_normalisation_peptide();

        //! Copying constructor.
        ms_quant_normalisation_peptide(const ms_quant_normalisation_peptide& src);

        //! Destructor.
        virtual ~ms_quant_normalisation_peptide();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_normalisation_peptide* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_normalisation_peptide& operator=(const ms_quant_normalisation_peptide& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Check for presence of the \c sequence attribute.
        bool haveSequence() const;

        //! Returns the value of the \c sequence attribute.
        std::string getSequence() const;

        //! Supply custom content for \c sequence attribute.
        void setSequence(const char * value);

        //! Delete the \c sequence attribute.
        void dropSequence();

        //! Obtain a symbolic name for the \c sequence attribute schema type.
        std::string getSequenceSchemaType() const;

    private:

        std::string _sequence;
        bool _sequence_set;
    }; // class ms_quant_normalisation_peptide

    //! An object of this class represent a collection of peptide elements in <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_normalisation_peptides: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_normalisation_peptides();

        //! Copying constructor.
        ms_quant_normalisation_peptides(const ms_quant_normalisation_peptides& src);

        //! Destructor.
        virtual ~ms_quant_normalisation_peptides();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_normalisation_peptides* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_normalisation_peptides& operator=(const ms_quant_normalisation_peptides& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of nested \c peptide elements.
        int getNumberOfPeptides() const;

        //! Deletes all \c peptide elements from the list.
        void clearPeptides();

        //! Adds a new \c peptide element at the end of the list.
        void appendPeptide(const ms_quant_normalisation_peptide* pep);

        //! Returns the value of the \c peptide element object by its number.
        const ms_quant_normalisation_peptide* getPeptide(const int idx) const;

        //! Supply new content for one of the \c peptide elements in the list.
        bool updatePeptide(const int idx, const ms_quant_normalisation_peptide* pep);

        //! Removes \c peptide element from the list.
        bool deletePeptide(const int idx);

        //! Obtain a symbolic name for the \c peptide element schema type.
        std::string getPeptideSchemaType() const;

    private:

        typedef std::vector< ms_quant_normalisation_peptide* > pep_vector;
        pep_vector _peptides;
    }; // class ms_quant_normalisation_peptides

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_NORMALISATION_PEPTIDE_HPP

/*------------------------------- End of File -------------------------------*/

