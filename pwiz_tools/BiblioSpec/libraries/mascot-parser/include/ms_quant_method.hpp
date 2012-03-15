/*
##############################################################################
# file: ms_quant_method.hpp                                                  #
# 'msparser' toolkit                                                         #
# Encapsulates method-element from "quantitation.xml"-file                 #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_method.hpp,v $
 * @(#)$Revision: 1.11 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_METHOD_HPP
#define MS_QUANT_METHOD_HPP

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
    class ms_quant_method_impl;
    class ms_quant_configfile_impl;
}

namespace matrix_science {

    class ms_quant_modgroup; // forward declaration
    class ms_quant_component; // forward declaration
    class ms_quant_quality; // forward declaration
    class ms_quant_integration; // forward delcaration
    class ms_quant_outliers; // forward declaration
    class ms_quant_normalisation; // forward declaration
    class ms_quant_protocol; // forward declaration
    class ms_quant_ratio; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single quantitation method from <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_method: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;
        friend class msparser_internal::ms_quant_configfile_impl;

    public:
        //! Default constructor.
        ms_quant_method();

        //! Copying constructor.
        ms_quant_method(const ms_quant_method& src);

        //! Destructor.
        virtual ~ms_quant_method();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_method* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_method& operator=(const ms_quant_method& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of nested modification groups.
        int getNumberOfModificationGroups() const;

        //! Deletes all modification groups from the list.
        void clearModificationGroups();

        //! Adds a new modification group at the end of the list.
        void appendModificationGroup(const ms_quant_modgroup *item);

        //! Returns a modification group object by its number.
        const ms_quant_modgroup * getModificationGroupByNumber(const int idx) const;

        //! Returns a modification group object by its name or a null value in case of not found.
        const ms_quant_modgroup * getModificationGroupByName(const char *name) const;

        //! Update the information for a specific modification group refering to it by its index.
        bool updateModificationGroupByNumber(const int idx, const ms_quant_modgroup* modgroup);

        //! Update the information for a specific modification group refering to it by its unique name.
        bool updateModificationGroupByName(const char *name, const ms_quant_modgroup* modgroup);

        //! Remove a modification group from the list in memory by its index.
        bool deleteModificationGroupByNumber(const int idx);

        //! Remove a modification group from the list in memory by its unique name.
        bool deleteModificationGroupByName(const char *name);

        //! Obtain a symbolic name for the modification group element schema type.
        std::string getModificationGroupSchemaType() const;


        //! Returns the number of nested components.
        int getNumberOfComponents() const;

        //! Deletes all components from the list.
        void clearComponents();

        //! Adds a new component at the end of the list.
        void appendComponent(const ms_quant_component *item);

        //! Returns a component object by its number.
        const ms_quant_component * getComponentByNumber(const int idx) const;

        //! Returns a component object by its name or a null value in case of not found.
        const ms_quant_component * getComponentByName(const char *name) const;

        //! Update the information for a specific component refering to it by its index.
        bool updateComponentByNumber(const int idx, const ms_quant_component* comp);

        //! Update the information for a specific component refering to it by its unique name.
        bool updateComponentByName(const char *name, const ms_quant_component* comp);

        //! Remove a component from the list in memory by its index.
        bool deleteComponentByNumber(const int idx);

        //! Remove a component from the list in memory by its unique name.
        bool deleteComponentByName(const char *name);

        //! Obtain a symbolic name for the component element schema type.
        std::string getComponentSchemaType() const;


        //! Returns the number of nested \c report_ratio elements.
        int getNumberOfReportRatios() const;

        //! Deletes all \c report_ratio elements from the list.
        void clearReportRatios();

        //! Adds a new \c report_ratio element at the end of the list.
        void appendReportRatio(const ms_quant_ratio *ratio);

        //! Returns a \c report_ratio element object by its number.
        const ms_quant_ratio * getReportRatioByNumber(const int idx) const;

        //! Returns a \c report_ratio element object by its name or a null value in case of not found.
        const ms_quant_ratio * getReportRatioByName(const char *name) const;

        //! Update the information for a specific \c report_ratio element refering to it by its index.
        bool updateReportRatioByNumber(const int idx, const ms_quant_ratio* ratio);

        //! Update the information for a specific \c report_ratio element refering to it by its unique name.
        bool updateReportRatioByName(const char *name, const ms_quant_ratio* ratio);

        //! Remove a \c report_ratio element from the list in memory by its index.
        bool deleteReportRatioByNumber(const int idx);

        //! Remove a \c report_ratio element from the list in memory by its unique name.
        bool deleteReportRatioByName(const char *name);

        //! Obtain a symbolic name for the \c report_ratio element schema type.
        std::string getReportRatioSchemaType() const;


        //! Returns the number of nested \c exclusion elements.
        int getNumberOfExclusions() const;

        //! Deletes all \c exclusion elements from the list.
        void clearExclusions();

        //! Adds a new \c exclusion element at the end of the list.
        void appendExclusion(const char* exclusion);

        //! Returns a \c exclusion element object by its number.
        std::string getExclusion(const int idx) const;

        //! Update the information for a specific \c exclusion element refering to it by its index.
        bool updateExclusion(const int idx, const char* exclusion);

        //! Remove a \c exclusion element from the list in memory by its index.
        bool deleteExclusion(const int idx);

        //! Obtain a symbolic name for the \c exclusion element schema type.
        std::string getExclusionSchemaType() const;


        //! Returns the number of nested \c seq elements.
        int getNumberOfSeqs() const;

        //! Deletes all \c seq elements from the list.
        void clearSeqs();

        //! Adds a new \c seq element at the end of the list.
        void appendSeq(const char* seq);

        //! Returns a \c seq element object by its number.
        std::string getSeq(const int idx) const;

        //! Update the information for a specific \c seq element refering to it by its index.
        bool updateSeq(const int idx, const char* seq);

        //! Remove a \c seq element from the list in memory by its index.
        bool deleteSeq(const int idx);

        //! Obtain a symbolic name for the element's schema type.
        std::string getSeqSchemaType() const;


        //! Indicates whether the \c comp element is present.
        bool haveComp() const;

        //! Returns the value of the \c comp element.
        std::string getComp() const;

        //! Set a custom value for the \c comp element.
        void setComp(const char* value);

        //! Delete the \c comp element.
        void dropComp();

        //! Obtain a symbolic name for the \c comp element schema type.
        std::string getCompSchemaType() const;


        //! Indicates whether the \c quality element is present.
        bool haveQuality() const;

        //! Returns a pointer to the \c quality element.
        const ms_quant_quality* getQuality() const;

        //! Supply custom content for the \c quality element.
        void setQuality(const ms_quant_quality* quality);

        //! Delete the \c quality element.
        void dropQuality();

        //! Obtain a symbolic name for the \c quality element schema type.
        std::string getQualitySchemaType() const;


        //! Indicates whether the \c integration element is present.
        bool haveIntegration() const;

        //! Returns a pointer to the \c integration element.
        const ms_quant_integration* getIntegration() const;

        //! Supply custom content for the \c integration element.
        void setIntegration(const ms_quant_integration* integration);

        //! Delete the \c integration element.
        void dropIntegration();

        //! Obtain a symbolic name for the \c integration element schema type.
        std::string getIntegrationSchemaType() const;


        //! Indicates whether the \c outliers element is present.
        bool haveOutliers() const;

        //! Returns a pointer to the \c outliers element.
        const ms_quant_outliers* getOutliers() const;

        //! Supply custom content for the \c outliers element.
        void setOutliers(const ms_quant_outliers* outliers);

        //! Delete the \c outliers element.
        void dropOutliers();

        //! Obtain a symbolic name for the \c outliers element schema type.
        std::string getOutliersSchemaType() const;


        //! Indicates whether the \c normalisation element is present.
        bool haveNormalisation() const;

        //! Returns a pointer to the \c normalisation element.
        const ms_quant_normalisation* getNormalisation() const;

        //! Supply custom content for the \c normalisation element.
        void setNormalisation(const ms_quant_normalisation* normalisation);

        //! Delete the \c normalisation element.
        void dropNormalisation();

        //! Obtain a symbolic name for the \c normalisation element schema type.
        std::string getNormalisationSchemaType() const;


        //! Indicates whether the \c protocol element is present.
        bool haveProtocol() const;

        //! Returns a pointer to the \c protocol element.
        const ms_quant_protocol* getProtocol() const;

        //! Supply custom content for the \c protocol element.
        void setProtocol(const ms_quant_protocol* protocol);

        //! Delete the \c protocol element.
        void dropProtocol();

        //! Obtain a symbolic name for the \c protocol element schema type.
        std::string getProtocolSchemaType() const;


        //! Indicates whether the \c name attribute is present.
        bool haveName() const;

        //! Returns the value of the \c name attribute.
        std::string getName() const;

        //! Set a custom value for the \c name attribute.
        void setName(const char* value);

        //! Delete the \c name attribute.
        void dropName();

        //! Obtain a symbolic name for the "name attribute" schema type.
        std::string getNameSchemaType() const;


        //! Indicates whether the \c description attribute is present.
        bool haveDescription() const;

        //! Returns the value of the \c description attribute.
        std::string getDescription() const;

        //! Set a custom value for the \c description attribute.
        void setDescription(const char* value);

        //! Delete the \c description attribute.
        void dropDescription();

        //! Obtain a symbolic name for the \c description attribute schema type.
        std::string getDescriptionSchemaType() const;


        //! Indicates whether the \c constrain_search attribute is present.
        bool haveConstrainSearch() const;

        //! Returns the value of the \c constrain_search attribute.
        bool isConstrainSearch() const;

        //! Set a custom value for the \c constrain_search attribute.
        void setConstrainSearch(const bool value);

        //! Delete the \c constrain_search attribute.
        void dropConstrainSearch();

        //! Obtain a symbolic name for the \c constrain_search attribute schema type.
        std::string getConstrainSearchSchemaType() const;


        //! Indicates whether the \c protein_ratio_type attribute is present.
        bool haveProteinRatioType() const;

        //! Returns the value of the \c protein_ratio_type attribute.
        std::string getProteinRatioType() const;

        //! Set a custom value for the \c protein_ratio_type attribute.
        void setProteinRatioType(const char* value);

        //! Delete the \c protein_ratio_type attribute.
        void dropProteinRatioType();

        //! Obtain a symbolic name for the \c protein_ratio_type attribute schema type.
        std::string getProteinRatioTypeSchemaType() const;


        //! Indicates whether the \c report_detail attribute is present.
        bool haveReportDetail() const;

        //! Returns the value of the \c report_detail attribute.
        bool isReportDetail() const;

        //! Set a custom value for the \c report_detail attribute.
        void setReportDetail(const bool value);

        //! Delete the \c report_detail attribute.
        void dropReportDetail();

        //! Obtain a symbolic name for the \c report_detail attribute schema type.
        std::string getReportDetailSchemaType() const;


        //! Indicates whether the \c min_num_peptides attribute is present.
        bool haveMinNumPeptides() const;

        //! Returns the value of the \c min_num_peptides attribute.
        int getMinNumPeptides() const;

        //! Set a custom value for the \c min_num_peptides attribute.
        void setMinNumPeptides(const int value);

        //! Delete the \c min_num_peptides attribute.
        void dropMinNumPeptides();

        //! Obtain a symbolic name for the \c min_num_peptides attribute schema type.
        std::string getMinNumPeptidesSchemaType() const;


        //! Indicates whether the \c prot_score_type attribute is present.
        bool haveProtScoreType() const;

        //! Returns the value of the \c prot_score_type attribute.
        std::string getProtScoreType() const;

        //! Set a custom value for the \c prot_score_type attribute.
        void setProtScoreType(const char* value);

        //! Delete the \c prot_score_type attribute.
        void dropProtScoreType();

        //! Obtain a symbolic name for the \c prot_score_type attribute schema type.
        std::string getProtScoreTypeSchemaType() const;


        //! Indicates whether the \c sig_threshold_value attribute is present.
        bool haveSigThresholdValue() const;

        //! Returns the value of the \c sig_threshold_value attribute.
        std::string getSigThresholdValue() const;

        //! Set a custom value for the \c sig_threshold_value attribute.
        void setSigThresholdValue(const char* value);

        //! Delete the \c sig_threshold_value attribute.
        void dropSigThresholdValue();

        //! Obtain a symbolic name for the \c sig_threshold_value attribute schema type.
        std::string getSigThresholdValueSchemaType() const;


        //! Indicates whether the \c show_sub_sets attribute is present.
        bool haveShowSubSets() const;

        //! Returns the value of the \c show_sub_sets attribute.
        std::string getShowSubSets() const;

        //! Set a custom value for the \c show_sub_sets attribute.
        void setShowSubSets(const char* value);

        //! Delete the \c show_sub_sets attribute.
        void dropShowSubSets();

        //! Obtain a symbolic name for the \c show_sub_sets attribute schema type.
        std::string getShowSubSetsSchemaType() const;


        //! Indicates whether the \c require_bold_red attribute is present.
        bool haveRequireBoldRed() const;

        //! Returns the value of the \c require_bold_red attribute.
        bool isRequireBoldRed() const;

        //! Set a custom value for the \c require_bold_red attribute.
        void setRequireBoldRed(const bool value);

        //! Delete the \c require_bold_red attribute.
        void dropRequireBoldRed();

        //! Obtain a symbolic name for the \c require_bold_red attribute schema type.
        std::string getRequireBoldRedSchemaType() const;

    private:
        msparser_internal::ms_quant_method_impl * m_pImpl;
    }; // class ms_quant_method

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_METHOD_HPP

/*------------------------------- End of File -------------------------------*/

