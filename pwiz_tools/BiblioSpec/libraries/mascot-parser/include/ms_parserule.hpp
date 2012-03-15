/*
##############################################################################
# file: ms_parserule.hpp                                                     #
# 'msparser' toolkit                                                         #
# Represents PARSE section of mascot.dat file                                #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_parserule.hpp           $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.8 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_PARSERULE_HPP
#define MS_PARSERULE_HPP

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

// for the sake of #include <string>
#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif
#include <string>


namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represents a single regular expression parsing rule.
    /*!
     * Instances of this class can be stored in ms_parseoptions as a list of
     * parse rules specified in <tt>PARSE</tt> section of <tt>mascot.dat</tt>.
     */
    class MS_MASCOTRESFILE_API ms_parserule
    {
        friend class ms_datfile;
        friend class ms_parseoptions;
    public:
        //! Default constructor.
        ms_parserule();

        //! Copying constructor.
        ms_parserule(const ms_parserule& src);

        //! Destructor.
        ~ms_parserule();

        //! Initialises the instance.
        void defaultValues();

        //! Copies all contents from another instance.
        void copyFrom(const ms_parserule* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_parserule& operator=(const ms_parserule& right);
#endif
        //! Call this member to check if this rule has been specified in the file or just a placeholder.
        /*!
         *  Parse rules in the <tt>PARSE</tt> section are specified in
         *  arbitrary order with their unique numbers. There might be gaps and
         *  unspecified rules.  Therefore, it is recommended to check if the
         *  rule is actually available in the file before retrieving its
         *  properties.
         *
         *  By default this returns <b>FALSE</b>.
         *  
         */
        bool isAvailable() const;

        //! Change the availability of the rule.
        /*!
         *  See #isAvailable() for more information.
         */
        void setAvailable(const bool value);

        //! Returns a string representing the rule.
        std::string getRuleStr() const;

        //! Change the rule string.
        void setRuleStr(const char* str);

    private:
        bool    available_;
        std::string szRule_;

        void *compiledExp;
        void compileAccessionRegex(ms_errs* pErr);
    };// class ms_parserule

    //! Represents the <tt>PARSE</tt> section of <tt>mascot.dat</tt>.
    /*!
     *  Also get yourselves acquainted with the base class ms_customproperty. 
     *  It facilitates the following tasks:
     *
     *  <ul>
     *  <li>Retrieving an unsupported property.</li>
     *  <li>Retrieving a raw/text/XML property representation.</li>
     *  <li>Checking for existence of a certain property rather than 
     *  dealing with its default value.</li>
     *  <li>Accessing commented lines in a section.</li>
     *  </ul>
     *
     *  More functionality is described in the documentation for
     *  ms_customproperty.
     */
    class MS_MASCOTRESFILE_API ms_parseoptions : public ms_customproperty
    {
        friend class ms_datfile;
    public:
        //! Default constructor.
        ms_parseoptions();

        //! Copying constructor.
        ms_parseoptions(const ms_parseoptions& src);

        //! Destructor.
        ~ms_parseoptions();
        
        //! Initializes the instance.
        void defaultValues();

        //! Can be used to copy content from another instance.
        void copyFrom(const ms_parseoptions* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_parseoptions& operator=(const ms_parseoptions& right);
#endif
        //! Check whether the section has been actually read from the file.
        /*!
         *  By default the <tt>PARSE</tt> section is unavailable until it has 
         *  been set to a different state.
         */
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. whether it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Can be used to get upper limit for a parse rule number.
        /*!
         *  The limit is currently hard-coded. Use this function to retrieve
         *  its value.
         */
        int getNumberOfParseRules() const;

        //! Makes all rules unavailable.
        void clearParseRules();

        //! Returns a parse rule by number.
        /*!
         *  Parse rules are not not stored consecutively. They are stored
         *  according to their numbers explicitly specified in the file. So,
         *  not every number will yield an available rule. See
         *  ms_parserule#isAvailable().
         *
         *  \param index a number from 0 to (getNumberOfParseRules()-1).
         *  \return an instance of ms_parserule class representing a rule.
         *  See \ref DynLangRulesOfThumb.
         */
        const ms_parserule* getParseRule(const int index) const;

        //! Set a new parse rule for a given number.
        void setParseRule(const int index, const ms_parserule* rule);

        //! Makes a parse rule with the specified index unavailable.
        /*!
         *  Unavailable rules are not shown in the file any more and are not
         *  valid entries.  In order to make it available again, create a new
         *  rule and put it in using #setParseRule() member.
         */
        void dropParseRule(const int index);

        int findOrAddParseRule(const char * rule, bool & added);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool                sectionAvailable_;
        ms_parserule        *parseRules_;
    };
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_PARSERULE_HPP

/*------------------------------- End of File -------------------------------*/
