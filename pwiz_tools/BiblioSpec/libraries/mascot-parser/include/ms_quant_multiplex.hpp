/*
##############################################################################
# file: ms_quant_multiplex.hpp                                           #
# 'msparser' toolkit                                                         #
# Encapsulates normalisation-element from "quantitation.xml"-file            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_multiplex.hpp,v $
 * @(#)$Revision: 1.12 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_MULTIPLEX_HPP
#define MS_QUANT_MULTIPLEX_HPP

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

    //! An object of this class represent a \c multiplex element in <tt>quantitation.xml</tt>.
    /*!
     * Use intensities of sequence ion fragment peaks within an MS/MS spectrum.
     */
    class MS_MASCOTRESFILE_API ms_quant_multiplex: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_multiplex();

        //! Copying constructor.
        ms_quant_multiplex(const ms_quant_multiplex& src);

        //! Destructor.
        virtual ~ms_quant_multiplex();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_multiplex* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_multiplex& operator=(const ms_quant_multiplex& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Check for presence of the \c ion_series element.
        bool haveIonSeries() const;

        //! Supply custom content for the \c ion_series element.
        void setIonSeries(const std::string ionSeries);

        //! Delete the \c ion_series element.
        void dropIonSeries();

        //! Obtain a symbolic name for the \c ion_series element schema type.
        std::string getIonSeriesSchemaType() const;

        //! Returns the number of ions series.
        int getNumberOfIonSeries() const;

        //! Deletes all \c ions_series elements from the list.
        void clearIonSeries();

        //! Adds a new \c ion_series element at the end of the list.
        void appendIonSeries(const std::string ionSeries);

        //! Returns a \c ion_series element object by its number.
        const std::string getIonSeries(const int idx=0) const;

        //! Update the information for a specific \c ion_series element refering to it by its index.
        bool updateIonSeries(const int idx, const std::string ionSeries);

        //! Remove an \c ion_series element from the list in memory by its index.
        bool deleteIonSeries(const int idx);

        //! Indicates presence of the \c exclude_internal_label attribute.
        bool haveExcludeInternalLabel() const;

        //! Returns the value of the \c exclude_internal_label attribute value.
        bool isExcludeInternalLabel() const;

        //! Set a custom value for the \c exclude_internal_label attribute.
        void setExcludeInternalLabel(const bool value);

        //! Delete the \c exclude_internal_label attribute.
        void dropExcludeInternalLabel();

        //! Obtain a symbolic name for the \c exclude_internal_label element schema type.
        std::string getExcludeInternalLabelSchemaType() const;


        //! Indicates presence of the \c ion_intensity_threshold attribute.
        bool haveIonIntensityThreshold() const;

        //! Returns the value of the \c ion_intensity_threshold attribute.
        std::string getIonIntensityThreshold() const;

        //! Set a custom value for the \c ion_intensity_threshold attribute.
        void setIonIntensityThreshold(const char* value);

        //! Delete the \c ion_intensity_threshold attribute.
        void dropIonIntensityThreshold();

        //! Obtain a symbolic name for the \c ion_intensity_threshold attribute schema type.
        std::string getIonIntensityThresholdSchemaType() const;


        //! Indicates presence of the \c exclude_isobaric_fragments attribute.
        bool haveExcludeIsobaricFragments() const;

        //! Returns the value of the \c exclude_isobaric_fragments attribute.
        bool isExcludeIsobaricFragments() const;

        //! Set a custom value for the \c exclude_isobaric_fragments attribute.
        void setExcludeIsobaricFragments(const bool value);

        //! Delete the \c exclude_isobaric_fragments attribute.
        void dropExcludeIsobaricFragments();

        //! Obtain a symbolic name for the \c exclude_isobaric_fragments attribute schema type.
        std::string getExcludeIsobaricFragmentsSchemaType() const;


        //! Indicates presence of the \c min_ion_pairs attribute.
        bool haveMinIonPairs() const;

        //! Returns the value of the \c min_ion_pairs attribute.
        int getMinIonPairs() const;

        //! Set a custom value for the \c min_ion_pairs attribute.
        void setMinIonPairs(const int value);

        //! Delete the \c min_ion_pairs attribute.
        void dropMinIonPairs();

        //! Obtain a symbolic name for the \c min_ion_pairs attribute schema type.
        std::string getMinIonPairsSchemaType() const;

    private:

        typedef std::vector< std::string > string_vector;
        string_vector _ionSeries;

        bool _excludeInternalLabel;
        bool _excludeInternalLabel_set;

        std::string _ionIntensityThreshold;
        bool _ionIntensityThreshold_set;

        bool _excludeIsobaricFragments;
        bool _excludeIsobaricFragments_set;

        int _minIonPairs;
        bool _minIonPairs_set;
    }; // class ms_quant_multiplex

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_MULTIPLEX_HPP

/*------------------------------- End of File -------------------------------*/
