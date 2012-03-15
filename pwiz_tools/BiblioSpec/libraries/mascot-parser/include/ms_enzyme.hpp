/*
##############################################################################
# file: ms_enzyme.hpp                                                        #
# 'msparser' toolkit                                                         #
# Encapsulates "enzymes"-file that describes available enzymes               #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2005 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_enzyme.hpp              $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.18 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_ENZYME_HPP
#define MS_ENZYME_HPP

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
#include <vector>


namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Represent a single entry in the <tt>enzymes</tt> file.
    /*! 
     *  An instance of this class is normally be created by calling
     *  matrix_science::ms_enzymefile::getEnzymeByNumber() or
     *  matrix_science::ms_enzymefile::getEnzymeByName() and should then only
     *  be used in 'read-only' mode.
     *
     *  As well as 'simple' enzymes, the following cases are supported:
     *
     *  <UL>
     *  <LI>semi-specific enzymes (#isSemiSpecific()) </LI>
     *  <LI>mixed cutters (C-term and N-term)</LI>
     *  <LI>independent cutters (#isIndependent()) </LI>
     *  </UL>
     *
     *  In case of independent enzymes an instance contains several 
     *  enzyme definitions that can be iterated through. Otherwise several 
     *  enzymes with mixed-term cutting are combined together.
     *
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
    class MS_MASCOTRESFILE_API ms_enzyme : public ms_customproperty
    {
        friend class ms_enzymefile;

    public:
        //! Definitions for types of cutter.
        /*!
         * See \ref DynLangEnums.
         */
        enum cuttertype
        {
            UNDEFINED_CUTTER = 0x0000, //!< Undefined - normally due to an invalid parameter when calling a function.
            NTERM_CUTTER     = 0x0001, //!< N Terminus cutter - cuts <i>before</i> the specified residue(s).
            CTERM_CUTTER     = 0x0002  //!< C Terminus cutter - cuts <i>after</i> the specified residue(s).
        };

        //! Default constructor.
        ms_enzyme();

        //! Copying constructor.
        ms_enzyme(const ms_enzyme& src);

        //! Destructor.
        ~ms_enzyme();

        //! Use this member to re-initialise an instance to default values.
        void defaultValues();

        //! Can be used to create a clone.
        void copyFrom(const ms_enzyme* right);

#ifndef SWIG
        //! C++ style operator= for copying.
        ms_enzyme& operator=(const ms_enzyme& right);
#endif
        //! Use this method to do basic check on a newly created object.
        bool isValid() const;

        //! Tries to detect any inconsistencies in the enzyme definition.
        bool verifyEnzyme(ms_errs* errObj) const;

        //! Returns a name of the enzyme as appears in the file.
        std::string getTitle() const;

        //! Sets a new title for the enzyme.
        void setTitle(const char* str);

        //! Returns TRUE if the enzyme is semi-specific.
        bool isSemiSpecific() const;

        //! Sets a new value for the semi specific flag.
        void setSemiSpecific(const bool value);

        //! Check whether multiple enzymes have been applied independently.
        bool isIndependent() const;

        //! Sets the flag that specifies whether multiple enzymes have been applied independently.
        void setIndependent(const bool bit);

        //! Returns the number of cutters.
        int getNumberOfCutters() const;

        //! Deletes all cutters from the list.
        void clearAllCutters();

        //! Adds an cutter to the enzyme.
        void addCutter(const cuttertype type, const char* cleaveAtStr, const char* restrictStr);
        
        //! Return the number of cutters that make up the enzyme definition.
        ms_enzyme::cuttertype getCutterType(const int cutterNum) const;

        //! Returns the list of cleavage points for a cutter.
        std::string getCleave(const int cutterNum) const;

        //! Returns the list of restriction points for a cutter.
        std::string getRestrict(const int cutterNum) const;

        //! Returns TRUE if the cutter can cut between the two residues.
        bool canCleave(const int  cutterNum, 
                       const char leftResidue, 
                       const char rightResidue) const;

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        void setNTerm(std::string& cleaveStr, std::string& restrictStr);
        void setCTerm(std::string& cleaveStr, std::string& restrictStr);
        std::string::size_type prepareNextNTerm();
        std::string::size_type prepareNextCTerm();
        void setIndependentInternal(const bool bit);
        void setSemiSpecificInternal(const bool bit);
        void makeCustomPropIndexed();

    private:
        std::string     title_;
        bool            semiSpecific_;

        bool            independent_;
        
        typedef std::vector< bool* > residue_flags_vector;

        residue_flags_vector cleavageNTerm;
        residue_flags_vector cleavageCTerm;
        residue_flags_vector restrictNTerm;
        residue_flags_vector restrictCTerm;
    };// class ms_enzyme

    class ms_filesource;

    //! Reads and parses the <tt>enzymes</tt> file that contains multiple enzyme definitions.
    /*!
     *  Proteolytic enzymes and specific chemical cleavage agents 
     *  are defined in <tt>../config/enzymes</tt> .
     *
     *  Usage is simple: 
     *
     *  <UL>
     *  <LI>Create an instance of this class.</LI>
     *  <LI>Set a file name if needed, otherwise a default name will be used.</LI>
     *  <LI>Call #read_file().</LI>
     *  <LI>Check for errors with #isValid().</LI>
     *  <LI>Iterate thourgh enzyme list using #getNumberOfEnzymes() and #getEnzymeByNumber().</LI>
     *  <LI>Alternatively, one can find an enzyme by name (if known) with #getEnzymeByName().</LI>
     *  </UL>
     */
    class MS_MASCOTRESFILE_API ms_enzymefile : public ms_errors
    {
    public:
        //! Default constructor.
        ms_enzymefile();

        //! Copying constructor.
        ms_enzymefile(const ms_enzymefile& src);

        //! This constructor may be used in order to read the given file immediately on construction.
        ms_enzymefile(const char* filename, const matrix_science::ms_connection_settings * cs = 0);

        //! Destructor.
        ~ms_enzymefile();

        //! Use this member to re-initialise the instance.
        void defaultValues();

        //! Call this member to copy all the information from another instance.
        void copyFrom(const ms_enzymefile* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_enzymefile& operator=(const ms_enzymefile& right);
#endif
        //! Call this member before reading the file if you want to specify a non-default name.
        void setFileName(const char* filename);

        //! Returns a file name previously set or a default one used to read the file.
        std::string getFileName() const;

        //! Sets the sessionID and proxy server for use with an HTTP transfer.
        void setConnectionSettings(const matrix_science::ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        matrix_science::ms_connection_settings getConnectionSettings() const;

        //! Reads and parses the file.
        void read_file();

        //! Reads and parses an in-memory null-terminated buffer instead of a disk file.
        void read_buffer(const char* buffer);

        //! Stores enzyme definitions in a file.
        void save_file();

        //! Returns a number of enzymes successfully read from the file.
        int getNumberOfEnzymes() const;

        //! Deletes all enzymes from the list.
        void clearEnzymes();

        //! Adds a copy of given enzyme at the end of the list.
        void appendEnzyme(const ms_enzyme* item);

        //! Returns a pointer to an internally stored enzyme.
        const ms_enzyme* getEnzymeByNumber(const int num) const;

        //! Finds an enzyme with the specified name (case insensitive).
        const ms_enzyme* getEnzymeByName(const char* name) const;

        //! Returns TRUE if an entry with name "NONE" has been found in the file.
        bool isNoneFound() const;

        //! Update the information for a specific enzyme.
        bool updateEnzymeByNumber(const int num, const ms_enzyme enzyme);

        //! Update the information for a specific enzyme.
        bool updateEnzymeByName(const char* name, const ms_enzyme enzyme);

        //! Remove an enzyme from the list in memory.
        bool deleteEnzymeByNumber(const int num);

        //! Remove an enzyme from the list in memory.
        bool deleteEnzymeByName(const char* name);


    private:

        void read_internal(ms_filesource *pFSource);

        std::string filename_;
        std::vector<ms_enzyme*> entries_;
        std::vector< std::string > comments_;
        bool noneFound_;
        ms_connection_settings cs_;

    }; // class ms_enzymefile
    /** @} */ // end of config_group
} // matrix_science

#endif // MS_ENZYME_HPP

/*------------------------------- End of File -------------------------------*/
