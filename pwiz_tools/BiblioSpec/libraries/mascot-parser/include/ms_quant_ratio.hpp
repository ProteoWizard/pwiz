/*
##############################################################################
# file: ms_quant_ratio.hpp                                                   #
# 'msparser' toolkit                                                         #
# Encapsulates "ratioType" from "quantitation.xml"-file                      #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2006 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
 * @(#)$Source: /vol/cvsroot/parser/inc/ms_quant_ratio.hpp,v $
 * @(#)$Revision: 1.8 $
 * @(#)$Date: 2010-09-06 16:18:57 $
##############################################################################
 */

#ifndef MS_QUANT_RATIO_HPP
#define MS_QUANT_RATIO_HPP

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

    class ms_quant_numerator; // forward declaration
    class ms_xml_schema; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents the \c ratioType type for the \c report_ratio element in <tt>quantitation.xml</tt>.
    class MS_MASCOTRESFILE_API ms_quant_ratio: public ms_xml_IValidatable
    {
        friend class msparser_internal::ms_quant_xmlloader;

    public:
        //! Default constructor.
        ms_quant_ratio();

        //! Copying constructor.
        ms_quant_ratio(const ms_quant_ratio& src);

        //! Destructor.
        virtual ~ms_quant_ratio();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_quant_ratio* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_quant_ratio& operator=(const ms_quant_ratio& right);
#endif
        //! Returns name of the schema type that can be used to validate this element.
        virtual std::string getSchemaType() const;

        //! Performs simple validation of the top-level elements only.
        virtual std::string validateShallow(const ms_xml_schema *pSchemaFileObj) const;

        //! Performs validation of all child elements in addition to 'shallow' validation.
        virtual std::string validateDeep(const ms_xml_schema *pSchemaFileObj) const;


        //! Returns the number of nested numerators.
        int getNumberOfNumerators() const;

        //! Deletes all numerators from the list.
        void clearNumerators();

        //! Adds a new numerator at the end of the list.
        void appendNumerator(const ms_quant_numerator *numerator);

        //! Returns a numerator object by its number or a null value in case of not found.
        const ms_quant_numerator * getNumeratorByNumber(const int idx) const;

        //! Returns a numerator object by its name or a null value in case of not found.
        const ms_quant_numerator * getNumeratorByName(const char *name) const;

        //! Update the information for a specific numerator refering to it by its index.
        bool updateNumeratorByNumber(const int idx, const ms_quant_numerator* numerator);

        //! Update the information for a specific numerator refering to it by its unique name.
        bool updateNumeratorByName(const char *name, const ms_quant_numerator* numerator);

        //! Remove a numerator from the list in memory by its index.
        bool deleteNumeratorByNumber(const int idx);

        //! Remove a numerator from the list in memory by its unique name.
        bool deleteNumeratorByName(const char *name);

        //! Obtain a symbolic name for the numerator element schema type.
        std::string getNumeratorSchemaType() const;


        //! Returns a number of nested denominators.
        int getNumberOfDenominators() const;

        //! Deletes all denominators from the list.
        void clearDenominators();

        //! Adds a new denominator at the end of the list.
        void appendDenominator(const ms_quant_numerator *denominator);

        //! Returns a denominator object by its number or a null value in case of not found.
        const ms_quant_numerator * getDenominatorByNumber(const int idx) const;

        //! Returns a denominator object by its name or a null value in case of not found.
        const ms_quant_numerator * getDenominatorByName(const char *name) const;

        //! Update the information for a specific denominator refering to it by its index.
        bool updateDenominatorByNumber(const int idx, const ms_quant_numerator* denominator);

        //! Update the information for a specific denominator refering to it by its unique name.
        bool updateDenominatorByName(const char *name, const ms_quant_numerator* denominator);

        //! Remove a denominator from the list in memory by its index.
        bool deleteDenominatorByNumber(const int idx);

        //! Remove a denominator from the list in memory by its unique name.
        bool deleteDenominatorByName(const char *name);

        //! Obtain a symbolic name for the denominator element schema type.
        std::string getDenominatorSchemaType() const;


        //! Indicates presence of the \c name attribute.
        bool haveName() const;

        //! Returns the value of the \c name attribute.
        std::string getName() const;

        //! Set a custom value for the \c name attribute.
        void setName(const char* value);

        //! Delete the \c name attribute.
        void dropName();

        //! Obtain a symbolic name for the \c name attribute schema type.
        std::string getNameSchemaType() const;

    private:
        typedef std::vector< ms_quant_numerator* > numerator_vector;
        numerator_vector _numerators;

        numerator_vector _denominators;

        std::string _name;
        bool _name_set;

    }; // class ms_quant_ratio

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_QUANT_RATIO_HPP

/*------------------------------- End of File -------------------------------*/
