/*
##############################################################################
# file: ms_quant_quality.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates quality-element from "quantitation.xml"-file                  #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_quality.hpp,v $
 * @(#)$Revision: 1.10 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_QUALITY_HPP
#define MS_QUANT_QUALITY_HPP

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

    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! An object of this class represent a single \c quality element in <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_quality: public ms_quant_parameters
    {
        friend class msparser_internal::ms_quant_xmlloader;
    public:
        //! Default constructor.
        ms_quant_quality();

        //! Copying constructor.
        ms_quant_quality(const ms_quant_quality& src);

        //! Destructor.
        virtual ~ms_quant_quality();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_quality* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_quality& operator=(const ms_quant_quality& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Indicates presence of the \c min_precursor_charge attribute.
        bool haveMinPrecursorCharge() const;

        //! Returns the value of the \c min_precursor_charge attribute.
        int getMinPrecursorCharge() const;

        //! Set a custom value for the \c min_precursor_charge attribute.
        void setMinPrecursorCharge(const int value);

        //! Delete the \c min_precursor_charge attribute.
        void dropMinPrecursorCharge();

        //! Obtain a symbolic name for the \c min_precursor_charge attribute schema type.
        std::string getMinPrecursorChargeSchemaType() const;


        //! Indicates presence of the \c isolated_precursor attribute.
        bool haveIsolatedPrecursor() const;

        //! Returns the value of the \c isolated_precursor attribute.
        bool isIsolatedPrecursor() const;

        //! Set a custom value for the \c isolated_precursor attribute.
        void setIsolatedPrecursor(const bool value);

        //! Delete the \c isolated_precursor attribute.
        void dropIsolatedPrecursor();

        //! Obtain a symbolic name for the \c isolated_precursor attribute schema type.
        std::string getIsolatedPrecursorSchemaType() const;


        //! Indicates presence of the \c minimum_a1 attribute.
        bool haveMinimumA1() const;

        //! Returns the value of the \c minimum_a1 attribute.
        std::string getMinimumA1() const;

        //! Set a custom value for the \c minimum_a1 attribute.
        void setMinimumA1(const char* value);

        //! Delete the \c minimum_a1 attribute.
        void dropMinimumA1();

        //! Obtain a symbolic name for the \c minimum_a1 attribute schema type.
        std::string getMinimumA1SchemaType() const;


        //! Indicates presence of the \c pep_threshold_type attribute.
        bool havePepThresholdType() const;

        //! Returns the value of the \c pep_threshold_type attribute.
        std::string getPepThresholdType() const;

        //! Set a custom value for the \c pep_threshold_type attribute.
        void setPepThresholdType(const char* value);

        //! Delete the \c pep_threshold_type attribute.
        void dropPepThresholdType();

        //! Obtain a symbolic name for the \c pep_threshold_type attribute schema type.
        std::string getPepThresholdTypeSchemaType() const;


        //! Indicates presence of the \c pep_threshold_value attribute.
        bool havePepThresholdValue() const;

        //! Returns the value of the \c pep_threshold_value attribute.
        std::string getPepThresholdValue() const;

        //! Set a custom value for the \c pep_threshold_value attribute.
        void setPepThresholdValue(const char* value);

        //! Delete the \c pep_threshold_value attribute.
        void dropPepThresholdValue();

        //! Obtain a symbolic name for the "pep_threshold_value" schema type.
        std::string getPepThresholdValueSchemaType() const;


        //! Indicates presence of the \c unique_pepseq attribute.
        bool haveUniquePepseq() const;

        //! Returns the value of the \c unique_pepseq attribute.
        bool isUniquePepseq() const;

        //! Set a custom value for the \c unique_pepseq attribute.
        void setUniquePepseq(bool value);

        //! Delete the \c unique_pepseq attribute.
        void dropUniquePepseq();

        //! Obtain a symbolic name for the \c unique_pepseq attribute schema type.
        std::string getUniquePepseqSchemaType() const;


        //! Indicates presence of the \c isolated_precursor_threshold attribute.
        bool haveIsolatedPrecursorThreshold() const;

        //! Returns the value of the \c isolated_precursor_threshold attribute.
        std::string getIsolatedPrecursorThreshold() const;

        //! Set a custom value for the \c isolated_precursor_threshold attribute.
        void setIsolatedPrecursorThreshold(const char * value);

        //! Delete the \c isolated_precursor_threshold attribute.
        void dropIsolatedPrecursorThreshold();

        //! Obtain a symbolic name for the \c isolated_precursor_threshold attribute schema type.
        std::string getIsolatedPrecursorThresholdSchemaType() const;


    private:

        int _minPrecursorCharge;
        bool _minPrecursorCharge_set;

        bool _isolatedPrecursor;
        bool _isolatedPrecursor_set;

        std::string _minimumA1;
        bool _minimumA1_set;

        std::string _pepThresholdType;
        bool _pepThresholdType_set;

        std::string _pepThresholdValue;
        bool _pepThresholdValue_set;

        bool _uniquePepseq;
        bool _uniquePepseq_set;

        std::string _isolatedPrecursorThreshold;
        bool _isolatedPrecursorThreshold_set;
    }; // class ms_quant_quality

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_QUALITY_HPP

/*------------------------------- End of File -------------------------------*/

