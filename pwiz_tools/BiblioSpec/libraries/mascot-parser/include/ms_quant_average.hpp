/*
##############################################################################
# file: ms_quant_average.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates average-element from "quantitation.xml"-file                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_average.hpp,v $
 * @(#)$Revision: 1.9 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_AVERAGE_HPP
#define MS_QUANT_AVERAGE_HPP

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

    //! An object of this class represent a single "average" element in <tt>quantitation.xml</tt>.
    /*!
     * Serves as a possible sub-element of the "protocol" element.
     */
    class MS_MASCOTRESFILE_API ms_quant_average: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_average();

        //! Copying constructor.
        ms_quant_average(const ms_quant_average& src);

        //! Destructor.
        virtual ~ms_quant_average();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_average* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_average& operator=(const ms_quant_average& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c num_peptides attribute.
        bool haveNumPeptides() const;

        //! Returns the value of the \c num_peptides attribute.
        int getNumPeptides() const;

        //! Set a custom value for the \c num_peptides attribute.
        void setNumPeptides(int value);

        //! Delete the \c num_peptides attribute.
        void dropNumPeptides();

        //! Obtain a symbolic name for the \c num_peptides attribute schema type.
        std::string getNumPeptidesSchemaType() const;


        //! Indicates presence of the \c selection attribute.
        bool haveSelection() const;

        //! Returns the value of the \c selection attribute.
        std::string getSelection() const;

        //! Set a custom value for the \c selection attribute.
        void setSelection(const char* value);

        //! Delete the \c selection attribute.
        void dropSelection();

        //! Obtain a symbolic name for the \c selection attribute schema type.
        std::string getSelectionSchemaType() const;


        //! Indicates presence of the \c reference_accession attribute.
        bool haveReferenceAccession() const;

        //! Returns the value of the \c reference_accession attribute.
        std::string getReferenceAccession() const;

        //! Set a custom value for the \c reference_accession attribute.
        void setReferenceAccession(const char* value);

        //! Delete the \c reference_accession attribute.
        void dropReferenceAccession();

        //! Obtain a symbolic name for the \c reference_accession attribute schema type.
        std::string getReferenceAccessionSchemaType() const;


        //! Indicates presence of the \c reference_amount attribute.
        bool haveReferenceAmount() const;

        //! Returns the value of the \c reference_amount attribute.
        std::string getReferenceAmount() const;

        //! Set a custom value for the \c reference_amount attribute.
        void setReferenceAmount(const char* value);

        //! Delete the \c reference_amount attribute.
        void dropReferenceAmount();

        //! Obtain a symbolic name for the \c reference_amount attribute schema type.
        std::string getReferenceAmountSchemaType() const;
    private:

        int _numPeptides;
        bool _numPeptides_set;

        std::string _selection;
        bool _selection_set;

        std::string _referenceAccession;
        bool _referenceAccession_set;

        std::string _referenceAmount;
        bool _referenceAmount_set;
    }; // class ms_quant_average

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_AVERAGE_HPP

/*------------------------------- End of File -------------------------------*/
