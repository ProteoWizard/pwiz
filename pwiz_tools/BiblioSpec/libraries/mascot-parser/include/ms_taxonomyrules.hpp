/*
##############################################################################
# file: ms_taxonomyrules.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates "mascot.dat"-file that describes most important parameters    #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_taxonomyrules.hpp       $ #
#     $Author: davidc $ #
#       $Date: 2011-03-14 14:39:05 $ #
#   $Revision: 1.16 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_TAXONOMYRULES_HPP
#define MS_TAXONOMYRULES_HPP

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

    //! All possible taxonomy species file formats.
    /*!
     * See \ref DynLangEnums.
     */
    enum TAX_SPECIES_FORMAT
    {
        TAX_SPECIES_NCBI,         //!< NCBI web-site source for taxonomy species files.
        TAX_SPECIES_SWISSPROT,    //!< SwissProt.
        TAX_SPECIES_PDB,          //!< PDB.
        TAX_SPECIES_GI2TAXID,     //!< GI2TAXID.
        TAX_SPECIES_ACC2TAXID,    //!< Simple 'accession taxID' (any whitespace).
        TAX_SPECIES_EXPLICIT,     //!< No lookup required, because the ID is given in the description line. For example: >IPI:IPI00000001.2 Tax_Id=9606 Double-stranded RNA ...
        TAX_SPECIES_FORMAT_COUNT        /* Always leave this one last */
    };

    //! An instance of this class describes one entry of taxonomy species files.
    class MS_MASCOTRESFILE_API ms_taxspeciesfiles
    {
        friend class ms_datfile;
        friend class ms_taxonomyrules;

    public:
        //! Default constructor.
        ms_taxspeciesfiles();

        //! Copying constructor.
        ms_taxspeciesfiles(const ms_taxspeciesfiles& src);

        //! Destructor.
        ~ms_taxspeciesfiles();

        //! Initialises the instance.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_taxspeciesfiles* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_taxspeciesfiles& operator=(const ms_taxspeciesfiles& right);
#endif
        //! Returns a format identifier for the entry.
        TAX_SPECIES_FORMAT getFormat() const;

        //! Change the format of the entry.
        void setFormat(const TAX_SPECIES_FORMAT value);

        //! Returns the file name.
        std::string getFileName() const;

        //! Change the file name.
        void setFileName(const char* name);

    private:
        TAX_SPECIES_FORMAT  format_;
        std::string         filename_;

        std::string getStringValue() const;
    }; // class ms_taxspeciesfiles

    //! Possible formats for the <tt>nodes.dmp</tt> and <tt>gencode.dmp</tt> files.
    /*!
     * See \ref DynLangEnums.
     */
    enum TAX_NODE_FORMAT
    {
        TAX_NODE_NCBI,     //!< NCBI nodes.dmp format
        TAX_NODE_GENCODE   //!< NCBI gencode.dmp format
    };

    //! Filenames and formats for taxonomy nodes or genetic codes files
    /*! See also ms_taxonomytree which provides functionality for the taxonomy nodes files.
     */
    class MS_MASCOTRESFILE_API ms_taxnodesfiles
    {
        friend class ms_datfile;
        friend class ms_taxonomyrules;
    public:

        //! Default constructor.
        ms_taxnodesfiles();

        //! Copying constructor.
        ms_taxnodesfiles(const ms_taxnodesfiles& src);

        //! Destructor.
        ~ms_taxnodesfiles();

        //! Initialises the instance.
        void defaultValues();

        //! Can be used to create a copy of another instance.
        void copyFrom(const ms_taxnodesfiles* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_taxnodesfiles& operator=(const ms_taxnodesfiles& right);
#endif
        //! Returns format identifier of the file.
        TAX_NODE_FORMAT getFormat() const;

        //! Change the format property.
        void setFormat(const TAX_NODE_FORMAT value);

        //! Returns the file name.
        std::string getFileName() const;

        //! Change the file name.
        void setFileName(const char* name);

    private:
        TAX_NODE_FORMAT format_;
        std::string     filename_;
        std::string getStringValue() const;
    }; // class ms_taxnodesfiles

    //! Represents regular expression parse rule plus some additional parameters.
    class MS_MASCOTRESFILE_API ms_parserule_plus
    {
        friend class ms_datfile;
        friend class ms_taxonomyrules;

    public:
        //! Constants used for combining TAX_CHOP_SRC values.
        /*!
         * See \ref DynLangEnums.
         */
        enum TAX_CHOP_TYPES
        {
            TAX_CHOP_PREFIX = 0x0001, //!< Remove all words at the start of the text specified in the PrefixRemoves section. See ms_taxonomyrules::getPrefixRemove().
            TAX_CHOP_SUFFIX = 0x0002, //!< Remove all words at the end of the text   specified in the SuffixRemoves section. See ms_taxonomyrules::getSuffixRemove().
            TAX_CHOP_WORDS  = 0x0004  //!< Remove one word at a time from the end of the text and try to get a taxonomy match again.
        };

        //! Data type used for the parameter specifying how to chop a source line. This will be zero or more of the ms_parserule_plus::TAX_CHOP_TYPES values OR'ed together.
        typedef unsigned int TAX_CHOP_SRC;

        //! Default constructor.
        ms_parserule_plus();

        //! Copying constructor.
        ms_parserule_plus(const ms_parserule_plus& src);

        //! Destructor.
        ~ms_parserule_plus();

        //! Initialises the instance.
        void defaultValues();

        //! Can be used to create a copy of another instance.
        void copyFrom(const ms_parserule_plus* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_parserule_plus& operator=(const ms_parserule_plus& right);
#endif
        //! Returns the file format.
        TAX_SPECIES_FORMAT getFileTypeToSearch() const;

        //! Change the file format.
        void setFileTypeToSearch(const TAX_SPECIES_FORMAT value);

        //! Returns the regular expression-based parse rule.
        const ms_parserule* getRule() const;

        //! Set a new parse rule.
        void setRule(const ms_parserule* src);

        //! Returns additional parameter specifying how to chop a source line.
        TAX_CHOP_SRC getChopSource() const;

        //! Change the parameter specifying how to a chop source line.
        void setChopSource(const TAX_CHOP_SRC value);

        //! Returns the database name.
        std::string getNameOfDB() const;

        //! Change the database name.
        void setNameOfDB(const char* name);

    private:
        TAX_SPECIES_FORMAT  fileTypeToSearch_;
        ms_parserule        rule_;
        TAX_CHOP_SRC        chopSrc_;
        std::string         nameOfDb_;
        std::string getStringValue() const;
    }; // ms_parserule_plus

    //! This class represents a single <b>Taxonomy_XXX</b> section in <tt>mascot.dat</tt>.
    /*!
     *  The Taxonomy section defines a set of taxonomy rules that can be
     *  selected for a database.  Usage of taxonomy rules can be turned off by
     *  setting <b>Enabled</b> property to <b>0</b>.  See #isEnabled() for more
     *  information.
     *
     *  Instances of this class are created in ms_datfile.
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
    class MS_MASCOTRESFILE_API ms_taxonomyrules: public ms_customproperty
    {
        friend class ms_datfile;

    public:
        //! Default constructor.
        ms_taxonomyrules();

        //! Copying constructor.
        ms_taxonomyrules(const ms_taxonomyrules& src);

        //! Destructor.
        ~ms_taxonomyrules();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_taxonomyrules* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_taxonomyrules& operator=(const ms_taxonomyrules& right);
#endif
        //! Checks whether the section has been actually read from the file.
        /*!
         *  By default a <tt>Taxonomy</tt> section is unavailable until it has 
         *  been set to a different state.
         */
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. whether it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Returns <b>TRUE</b> if <b>Enabled</b> parameter is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         *  Set <b>Enabled</b> parameter to <b>0</b> to disable the taxonomy.
         *  Most of the other parameters will be ignored although the taxonomy
         *  will be available for a database to select in database maintenance
         *  utility.
         *
         *  Default is <b>0</b>.
         */
        bool isEnabled() const;

        //! Change the value of <b>Enabled</b>.
        /*!
         *  See #isEnabled() for more information.
         */
        void setEnabled(const bool flag);

        //! Returns the value of <b>Identifier</b>.
        /*!
         *  This parameter contains a symbolic name for the taxonomy
         *  specification as seen, for instance, in the database GUI utility.
         *
         *  By default this is empty.
         */
        std::string getIdentifier() const;

        //! Change the value of <b>Identifier</b>.
        /*!
         *  For more information see #getIdentifier().
         */
        void setIdentifier(const char* str);

        //! Returns the value of <b>ErrorLevel</b>.
        /*!
         *  <b>ErrorLevel</b> indicates the type of warnings or errors that are
         *  found when creating the taxonomy information. Possible values and
         *  their meanings:
         *
         *  <ul>
         *  <li><b>0</b> - an entry is put into the
         *  <tt>NoTaxonomyMatch.txt</tt> file for every sequence where no
         *  taxonomy information is found.</li>
         *  <li><b>1+</b> (severe) - an entry is put into the
         *  <tt>NoTaxonomyMatch.txt</tt> file for every sequence that had any
         *  accession string without a match.  Since some sequences in NCBI
         *  will have up to 200 <tt>gi</tt> numbers (sources), there is
         *  a reasonable chance that some of these entries will not have
         *  species information, and this would cause the errors files to
         *  become very large.</li>
         *  </ul>
         *
         *  Default is <b>1</b>.
         */
        int getErrorLevel() const;

        //! Change the value of <b>ErrorLevel</b>.
        /*!
         *  See #getErrorLevel() for more information.
         */
        void setErrorLevel(const int value);

        //! Returns <b>TRUE</b> if <b>FromRefFile</b> parameter is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         *  <b>FromRefFile</b> is set to <b>0</b> to indicate that the taxonomy
         *  should be found in the <tt>.fasta</tt> file rather than in
         *  a reference file.
         *
         *  Default is <b>0</b>.
         *
         *  \sa #isConcatRefFileLines(), #getDescriptionLineSep().
         */
        bool isFromRefFile() const;

        //! Change the value of <b>FromRefFile</b>.
        /*!
         *  See #isFromRefFile() for more information.
         */
        void setFromRefFile(const bool flag);

        //! Returns <b>TRUE</b> if <b>ConcatRefFileLines</b> parameter is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         *  A value of <b>1</b> (default) means that there might be multiple
         *  lines in reference file describing the same entry of the database. 
         *
         *  \sa #isFromRefFile(), #getDescriptionLineSep().
         */
        bool isConcatRefFileLines() const;

        //! Change the value of <b>ConcatRefFileLines</b>.
        /*!
         *  See #isConcatRefFileLines() for more information.
         */
        void setConcatRefFileLines(const bool flag);

        //! Returns the value of <b>DescriptionLineSep</b>.
        /*!
         *  The line that contains the species IDs has multiple IDs, separated
         *  by a character, whose ASCII code is specified by
         *  <b>DescriptionLineSep</b>.
         *
         *  There is no default value for this parameter.
         */
        char getDescriptionLineSep() const;

        //! Change the value of <b>DescriptionLineSep</b>.
        /*!
         *  See #getDescriptionLineSep() for more information.
         */
        void setDescriptionLineSep(const char value);

        //! Returns the number of <b>NoBreakDescLineIf</b> entries.
        /*!
         *  See #getNoBreakDescLineIf() for explanations.
         */
        int getNumberOfNoBreakDescLineIf() const;

        //! Returns a <b>NoBreakDescLineIf</b> entry by its number.
        /*!
         *  <b>NoBreakDescLineIf</b> specifies keywords that prevent
         *  description line from being broken into separate words. 
         *
         *  By default this is an empty list.
         *
         *  \param index number of entry from 0 to
         *  (#getNumberOfNoBreakDescLineIf()-1).
         *  \return a string value.
         */
        std::string getNoBreakDescLineIf(const int index) const;

        //! Deletes all <b>NoBreakDescLineIf</b> entries.
        /*!
         *  See #getNoBreakDescLineIf() for more information.
         */
        void clearNoBreakDescLineIf();

        //! Adds an entry into the <b>NoBreakDescLineIf</b> list.
        /*!
         *  See #getNoBreakDescLineIf() for more information.
         *  \param str an item to add a copy of into the list.
         */
        void appendNoBreakDescLineIf(const char* str);

        //! Returns the number of file names specified in <b>SpeciesFiles</b>.
        /*!
         *  See #getSpeciesFile() and documentation for ms_taxspeciesfiles.
         */
        int getNumberOfSpeciesFiles() const;

        //! Returns an instance of ms_taxspeciesfiles describing an entry in <b>SpeciesFiles</b>.
        /*!
         *  The <b>SpeciesFiles</b> file is used to convert a species name to
         *  a taxonomy ID.  For more detailed information on how to configure
         *  this parameter consult Mascot manual and documentation for
         *  ms_taxspeciesfiles. 
         *
         *  By default the list is empty.
         *
         *  \param index file number from 0 to (#getNumberOfSpeciesFiles()-1).
         *  \return instance of ms_taxspeciesfiles class describing one of the
         *  files specified in <b>SpeciesFiles</b> parameter of the
         *  Taxonomy section.
         */
        const ms_taxspeciesfiles * getSpeciesFile(const int index) const;

        //! Deletes all entries for <b>SpeciesFiles</b>.
        /*!
         *  See #getSpeciesFile() for more information.
         */
        void clearSpeciesFiles();

        //! Adds an entry into the <b>SpeciesFiles</b> list.
        /*!
         *  See #getSpeciesFile() for more information.
         *  \param item an item to add a copy of into the list.
         */
        void appendSpeciesFile(const ms_taxspeciesfiles * item);

        // no longer in use
        int getNumberOfStrFiles() const;
        // no longer in use
        const ms_taxspeciesfiles * getStrFile(const int index) const;
        // no longer in use
        void clearStrFiles();
        // no longer in use
        void appendStrFile(const ms_taxspeciesfiles * item);

        // no longer in use
        const ms_parserule_plus* getStrRule() const;
        // no longer in use
        void setStrRule(const ms_parserule_plus* src);

        //! Returns the number of file names specified in <b>NodesFiles</b>.
        /*!
         *  See #getNodesFile() and documentation for ms_taxnodesfiles.
         */
        int getNumberOfNodesFiles() const;

        //! Returns an instance of ms_taxnodesfiles describing an entry in <b>NodesFiles</b>.
        /*!
         *  The <b>NodesFiles</b> file is used to traverse taxonomy hierarchy.
         *  It contains taxonomy ID nodes together with their parents IDs.
         *  For more detailed information on how to configure this parameter 
         *  consult Mascot manual and documentation for ms_taxnodesfiles.
         *
         *  By default the list of files is empty.
         *
         *  \param index file number from 0 to (#getNumberOfNodesFiles()-1).
         *  \return instance of ms_taxnodesfiles class describing one of the
         *  files specified in <b>NodesFiles</b> parameter of the
         *  Taxonomy section.
         */
        const ms_taxnodesfiles * getNodesFile(const int index) const;

        //! Deletes all <b>NodesFiles</b> entries.
        /*!
         *  See #getNodesFile() for more information.
         */
        void clearNodesFiles();

        //! Adds an entry into the <b>NodesFiles</b> list.
        /*!
         *  See #getNodesFile() for more information.
         *  \param item an item to add a copy of into the list.
         */
        void appendNodesFile(const ms_taxnodesfiles * item);

        //! Returns the number of file names specified in <b>GencodeFiles</b>.
        /*!
         *  See #getGencodeFile() and documentation for ms_taxnodesfiles.
         */
        int getNumberOfGencodeFiles() const;

        //! Returns an instance of ms_taxnodesfiles describing an entry in <b>GencodeFiles</b>.
        /*!
         *  The <b>GencodeFiles</b> file is used to find a proper NA
         *  translation table for a given taxonomy.  For more detailed
         *  information on how to configure this parameter consult Mascot
         *  manual and documentation for ms_taxnodesfiles.
         *
         *  By default the list of files is empty.
         *
         *  \param index file number from 0 to (#getNumberOfGencodeFiles()-1).
         *  \return instance of ms_taxnodesfiles class describing one of the
         *  files specified in <b>GencodeFiles</b> parameter of the
         *  Taxonomy section.
         */
        const ms_taxnodesfiles * getGencodeFile(const int index) const;

        //! Deletes all <b>GencodeFiles</b> entries.
        /*!
         *  See #getGencodeFile() for more information.
         */
        void clearGencodeFiles();

        //! Adds an entry into the <b>GencodeFiles</b> list.
        /*!
         *  See #getGencodeFile() for more information.
         *  \param item an item to add a copy of into the list.
         */
        void appendGencodeFile(const ms_taxnodesfiles * item);

        //! Returns the value of <b>DefaultRule</b>.
        /*!
         *  The <b>DefaultRule</b> describes how to find the species name in
         *  the line of text in the reference file.  The string in quotes is
         *  a regular expression.  All words in the <b>PrefixRemoves</b> and
         *  <b>SuffixRemoves</b> keywords should be removed before trying to do
         *  a match. For more detailed information on how to specify this
         *  parameter consult Mascot manual. 
         *
         *  If specified, <b>DoThisRuleFirst</b> is applied first, and the
         *  default rule would only be used if this failed.
         *
         *  \sa #getQuickRefSearch(), #getDoThisRuleFirst(), #getAccFromSpeciesLine()
         */
        const ms_parserule_plus* getDefaultRule() const;

        //! Change the value of <b>DefaultRule</b>.
        /*!
         *  See #getDefaultRule().
         */
        void setDefaultRule(const ms_parserule_plus* src);

        //! Returns the number of <b>PrefixRemoves</b> entries.
        /*!
         *  See #getPrefixRemove().
         */
        int getNumberOfPrefixRemoves() const;

        //! Returns the <b>PrefixRemoves</b> string by number.
        /*!
         *  See #getDefaultRule() for information on this parameter.
         *
         *  By default the list of prefixes is empty.
         *
         *  \param index number of a string specified in the parameter from
         *  0 to (#getNumberOfPrefixRemoves()-1).
         *  \return one of the string specifed in the parameter.
         */
        std::string getPrefixRemove(const int index) const;

        //! Deletes all <b>PrefixRemoves</b> entries.
        /*!
         *  See #getPrefixRemove() for more information.
         */
        void clearPrefixRemoves();

        //! Adds an entry into the <b>PrefixRemoves</b> list.
        /*!
         *  See #getPrefixRemove() for more information.
         *  \param item an item to add a copy of into the list.
         */
        void appendPrefixRemove(const char * item);

        //! Returns the number of <b>SuffixRemoves</b> entries.
        /*!
         *  See #getSuffixRemove().
         */
        int getNumberOfSuffixRemoves() const;

        //! Returns a <b>SuffixRemoves</b> string by number.
        /*!
         *  See #getDefaultRule() for information on this parameter.
         *
         *  By default the list of suffixes is empty.
         *
         *  \param index number of a string specified in the parameter from
         *  0 to (#getNumberOfSuffixRemoves()-1).
         *  \return one of the string specifed in the parameter.
         */
        std::string getSuffixRemove(const int index) const;

        //! Deletes all <b>SuffixRemoves</b> entries.
        /*!
         *  See #getSuffixRemove() for more information.
         */
        void clearSuffixRemoves();

        //! Adds an entry into the <b>SuffixRemoves</b> list.
        /*!
         *  See #getSuffixRemove() for more information.
         *
         *  \param item an item to add a copy of into the list.
         */
        void appendSuffixRemove(const char * item);

        //! Returns the value of <b>SrcDatabaseRule</b>.
        /*!
         *  The parameter is used for finding database source string with
         *  regular expression.
         *
         *  \sa #getPerDbSrcRule()
         */
        const ms_parserule* getSrcDatabaseRule() const;

        //! Change the value of <b>SrcDatabaseRule</b>.
        /*!
         *  See #getSrcDatabaseRule().
         */
        void setSrcDatabaseRule(const ms_parserule* src);

        //! Returns the number of database source strings.
        /*!
         *  See #getPerDbSrcRule() and documentation for ms_parserule_plus.
         */
        int getNumberOfPerDbSrcRules() const;

        //! Returns a database source string by its number.
        /*!
         *  Database source strings contained in taxonomy section all look like
         *  RULE_XXX or OTHERRULE.
         *
         *  By default the list of rules is empty.
         *
         *  \param index database source string number from 0 to
         *  (#getNumberOfPerDbSrcRules()-1).
         */
        const ms_parserule_plus * getPerDbSrcRule(const int index) const;

        //! Deletes all database source strings.
        /*!
         *  See #getPerDbSrcRule() for more information.
         */
        void clearPerDbSrcRules();

        //! Adds a new database source string into the list.
        /*!
         *  See #getPerDbSrcRule() for more information.
         *  \param item an item to add a copy of into the list
         */
        void appendPerDbSrcRule(const ms_parserule_plus * item);

        //! Returns the value of <b>DoThisRuleFirst</b>.
        /*!
         *  See #getDefaultRule().
         */
        const ms_parserule* getDoThisRuleFirst() const;

        //! Change the value of <b>DoThisRuleFirst</b>.
        /*!
         *  See #getDoThisRuleFirst().
         */
        void setDoThisRuleFirst(const ms_parserule* src);

        //! Returns the value of <b>AccFromSpeciesLine</b>.
        /*!
         *  MSDB database explicitly associates each species line with the
         *  accession string of the primary database entry.  A further rule,
         *  <b>AccFromSpeciesLine</b>, is used to extract this accession
         *  string.
         */
        const ms_parserule* getAccFromSpeciesLine() const;

        //! Change the value of <b>AccFromSpeciesLine</b>.
        /*!
         *  See #getAccFromSpeciesLine() for more information.
         */
        void setAccFromSpeciesLine(const ms_parserule* src);

        //! Returns the value of <b>QuickRefSearch</b>.
        /*!
         *  The <b>QuickRefSearch</b> string is used to speed up the
         *  compressing of the database. Rather than use the regular expression
         *  for each line in the <tt>.ref</tt> file, this text is used for
         *  a fast compare to the string <tt>C;Species</tt>.  Other lines are
         *  ignored.
         *
         *  By default this is empty.
         */
        std::string getQuickRefSearch() const;

        //! Change the value of <b>QuickRefSearch</b>.
        /*!
         *  See #getQuickRefSearch().
         */
        void setQuickRefSearch(const char* str);

        //! Returns the value of <b>DBLevelTaxId</b>.
        /*!
         *  Database level taxonomy ID can be specified in order to find proper
         *  NA translation table without <tt>names.dmp</tt> and
         *  <tt>nodes.dmp</tt> files usage.
         *
         *  Default is <b>-1</b>, which is not a valid taxonomy ID.
         */
        int getDBLevelTaxId() const;

        //! Change the value of <b>DBLevelTaxId</b>.
        /*
         *  See #getDBLevelTaxId().
         */
        void setDBLevelTaxId(const int value);

        //! Returns <b>TRUE</b> if there is a database level taxonomy ID in the file.
        bool isDBLevelTaxId() const;

        //! Deletes <b>DBLevelTaxId</b>; it will not be saved in the file.
        void clearDBLevelTaxId();

        //! Returns <b>TRUE</b> if <b>MitochondrialTranslation</b> parameter is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         *  Two types of NA translation tables can be used: nuclear and
         *  mitochondrial.  Setting this parameter to <b>0</b> indicates the
         *  first type whereas <b>1</b> indicates the second one.
         *
         *  Default is <b>0</b>.
         */
        bool isMitochondrialTranslation() const;

        //! Set the value of <b>MitochondrialTranslation</b>.
        /*!
         *  See #isMitochondrialTranslation().
         */
        void setMitochondrialTranslation(const bool flag);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool                sectionAvailable_;
        bool                enabled_;
        std::string         identifier_;
        int                 errorLevel_;
        bool                fromRefFile_;
        bool                concatRefFileLines_;
        char                descriptionLineSep_;

        std::vector< std::string > noBreakDescLineIf_;
        std::vector< ms_taxspeciesfiles* > speciesFiles_;
        std::vector< ms_taxspeciesfiles* > strStrFiles_;
        std::vector< ms_taxnodesfiles* > nodesFiles_;
        std::vector< ms_taxnodesfiles* > gencodeFiles_;
        std::vector< ms_parserule_plus* > perDbSrcRules_;
        std::vector< std::string > prefixRemoves_;
        std::vector< std::string > suffixRemoves_;

        ms_parserule        srcDatabaseRule_;
        ms_parserule        doThisRuleFirst_;
        ms_parserule        accFromSpeciesLine_;
        ms_parserule_plus   defaultRule_;
        ms_parserule_plus   strStrRule_;

        std::string         quickRefSearch_;
        int                 dbLevelTaxId_;
        bool                mitochondrialTranslation_;
    }; // class ms_taxonomyrules

    class ms_tinycdb;
    class ms_taxonomychoice;

    //! The complete taxonomy tree as built from one or more files such as nodes.dmp
    /*!
     * Used for determining the taxonomy lineage. This class is used
     * to load all of the files specified in a specified taxonomy section in 
     * mascot.dat, for example: \verbatim
     NodesFiles          NCBI:nodes.dmp, NCBI:merged.dmp
     \endverbatim
     * Index files are created using ms_tinycdb to improve performance in cases
     * where a moderate number of parent id's are required. Index files are created
     * in the same directory as the text files, with an additional 'extension' of
     * ".cdb". Error or warning messages in ms_tinycdb are concatenated into the 
     * ms_taxonomytree object. If, for example, the process does not have write 
     * access to the directory with the taxonomy files , then an error
     * ms_errs::
     * is generated, but the object is still valid and will continue with just 
     * the text files.
     *
     * Other possible errors include:
     * ms_errs::ERR_MSP_TAXONOMY_CONFLICT_PARENTS
     * ms_errs::ERR_MSP_TAXONOMY_INVALID_NODE_FILE
     * ms_errs::ERR_MSP_TAXONOMY_MISSING_NODE_FILE
     *
     * The nodes.dmp file also contains the genetic code translation table id
     * to be used when translating from nucleic acid to protein.
     *
     * Example code to see if Homo Sapiens (9606) are mammals (40674): \verbatim
     my $datfile = new msparser::ms_datfile("../config/mascot.dat");
     my $taxonomy_rules = $datfile->getTaxonomyRules(3);
     my $tree = new msparser::ms_taxonomytree($taxonomy_rules);
     if ($tree->isValid()) {
       my $human = 9606;
       my $mammalia = 40674;
       if ($tree->isSpeciesDescendantOf($mammalia, $human)) {
         print "Indeed, we are mammals\n";
       }
     }
     \endverbatim
     * 
     */
    class MS_MASCOTRESFILE_API ms_taxonomytree : public ms_errors
    {
    public:
        //! Create a taxonomy tree based on the rules specified in mascot.dat
        ms_taxonomytree(const ms_taxonomyrules * taxonomyRules, 
                        const char * taxonomyDirectory = "../taxonomy",
                        const bool useIndex = true);

        //! Copying constructor.
        ms_taxonomytree(const ms_taxonomytree & src);
        
        //! Destructor
        ~ms_taxonomytree();

        //! Can be used to create a copy of another instance.
        void copyFrom(const ms_taxonomytree * right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_taxonomytree& operator=(const ms_taxonomytree & right);

        struct TAX_TREE_NODE {
            int parentId;
            int tableId;
        };
        typedef std::vector<TAX_TREE_NODE> TAX_TREE_NODES;

        //! Return a raw array of TAX_TREE_NODE values, with the array index being the id
        TAX_TREE_NODES * getTaxIDArray();
#endif

        //! Determine if a particular id is a child of the \a parentID
        bool isSpeciesDescendantOf(const int parentID, const int id) const;

        //! Return the parent id and translation table id for a species
        bool getParent(const int id, int & ttParent, int & ttGenTable) const;

        //! Return true if the taxonomy id is included in the choice
        bool isIncludedIn(const int id, const ms_taxonomychoice * choice) const;

    private:
        bool usingCDB_;
        std::vector<ms_taxnodesfiles> files_;
        TAX_TREE_NODES nodes_;
        std::vector<std::string> fileNames_;
        std::vector<ms_tinycdb *> cdbFiles_;

        bool readFile(const std::string & filename,
                      const bool isMitochondrialTranslation);

    };


    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_TAXONOMYRULES_HPP

/*------------------------------- End of File -------------------------------*/
