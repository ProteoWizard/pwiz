/*
##############################################################################
# file: ms_datfile.hpp                                                       #
# 'msparser' toolkit                                                         #
# Encapsulates "mascot.dat"-file that describes most important parameters    #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_datfile.hpp             $ #
#     $Author: villek $ #
#       $Date: 2011-06-02 08:33:25 $ #
#   $Revision: 1.25 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_DATFILE_HPP
#define MS_DATFILE_HPP

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

    class ms_filesource;

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! Encapsulates the <tt>mascot.dat</tt> file that contains the most important parameters.
    /*!
     *  The file is divided into several sections. Each section has
     *  corresponding accessor methods (getXXX/setXXX). 
     *
     *  The path to <tt>mascot.dat</tt> can be specified explicitly. However,
     *  several default locations will be tried if the object is used
     *  without setting a file name. Before obtaining any section content check
     *  the object for any errors that might occur using #isValid() method.
     */
    class MS_MASCOTRESFILE_API ms_datfile : public ms_errors
    {
    public:

        //! Web server type for the updateForInstaller() function.
        /*!
         * See \ref DynLangEnums.
        */
        enum WST
        {
            WST_APACHE=0, //!< Apache 
            WST_IIS=1     //!< Microsoft IIS (Internet Information Services)
        };

        //! Flags for checkSeqDBInstallRequest().
        /*!
         * See \ref DynLangEnums.
         *
         * These values may be bitwise 'ORd' together.
         */
        enum SEQDB_INSTALL
        {
            // Anything in the lower 16 bits (1..16) should result in asking the user to confirm that they want to update
            SEQDB_INSTALL_SHOULD_QUERY_USER = 0x0000FFFF, //!< Bitwise 'and' the return value from checkSeqDBInstallRequest() with this value to determine if an update of the fasta files may be unwise.
            SEQDB_INSTALL_CUSTOMISED        = 0x00000001, //!< User has an unrecognised configuration, possibly with a customised SwissProt, so installing an update may be unwise.
            SEQDB_INSTALL_INACTIVE          = 0x00000002, //!< User has an existing configuration, but it is disabled.
            SEQDB_INSTALL_INVALID_CONFIG    = 0x00000004, //!< User has an invalid configuration. For example, missing parse rules or specifying to use accession from the fasta and ID from the .dat.
            SEQDB_INSTALL_EXISTING_LARGER   = 0x00000010, //!< User has an existing fasta file larger (and presumably later) than the one on the installer CD.

            // Bit 32 for something really bad.
            SEQDB_INSTALL_INVALID_DBNAME    = 0x00000000, //!< An unsupported dbName was passed to the function.

            // Further information that may be useful in bits 16 to 31 
            SEQDB_INSTALL_MUST_UPDATE_CFG   = 0x000F000F, //!< The configuration must be updated if the newer fasta files are to be installed.
            SEQDB_INSTALL_MISSING_CONFIG    = 0x00010000, //!< There is no configuration for the database. Installing should not give any conflict.
            SEQDB_INSTALL_OLD_RULES         = 0x00020000, //!< For example, the rules for SwissProt are for version 51 to version 55.
            SEQDB_INSTALL_WRONG_WILDCARD    = 0x00040000, //!< For example, could be Sprot_*.fasta rather than SwissProt_*.fasta.

            // Additional information, but no need to update 
            SEQDB_INSTALL_CURRENT_RULES     = 0x01000000  //!< Currently set if the rules for SwissProt are for version 56 to version ...
        };

        //! Flags for the updateForInstaller() function.
        /*!
         * See \ref DynLangEnums.
         */
        enum INST_FLAGS
        {
            IFLG_UPDATETAXONOMY      = 0x00000001,   //!< Inserts missing taxonomy sections.
            IFLG_UPDATEPARSERULES    = 0x00000002,   //!< Inserts missing parse rules.
            IFLG_UPDATESWISSPROT     = 0x00000004,   //!< Add an entry for SwissProt if it doesn't exist, or update it if parse rules are wrong.
            IFLG_UPDATECLUSTER       = 0x00000008,   //!< Update the cluster section.
            IFLG_UPDATEESTTAX        = 0x00000010,   //!< Update EST_human, EST_mouse if very old taxonomy.
            IFLG_ADDMISSINGOPTIONS   = 0x00000020,   //!< Add entries missing options from the default values in not.mascot.dat.
            IFLG_ADDREPORTAUTO       = 0x00000040,   //!< Add AUTO to the list in ReportNumberChoices if it isn't already present.
            IFLG_UPDATEAPACHEFORKOPT = 0x00000080,   //!< Update ForkForUnixApache using the value specified by the wst parameter.


            IFLG_DEFAULTNEWINSTALL   = IFLG_UPDATECLUSTER     |
                                       IFLG_UPDATEAPACHEFORKOPT, //!< Default flags for a new installation. Just the IFLG_UPDATECLUSTER and IFLG_UPDATEAPACHEFORKOPT flag.
            IFLG_DEFAULTUPGRADE      = IFLG_UPDATETAXONOMY    |
                                       IFLG_UPDATEPARSERULES  |
                                       IFLG_UPDATEESTTAX      |
                                       IFLG_ADDMISSINGOPTIONS |
                                       IFLG_ADDREPORTAUTO     |
                                       IFLG_UPDATEAPACHEFORKOPT   //!< Default flags for an upgrade. All flags except IFLG_UPDATECLUSTER and IFLG_UPDATESWISSPROT.
        };

        //! Default constructor.
        ms_datfile();

        //! Copying constructor.
        ms_datfile(const ms_datfile& src);

        //! Immediate action constructor.
        ms_datfile(const char* filename, const int timeoutSec = 0,
                   const matrix_science::ms_connection_settings * cs = 0);

        //! Destructor.
        ~ms_datfile();

        //! Can be used for initialising an instance with default values.
        void defaultValues();

        //! Use this member to make a copy of another instance.
        void copyFrom(const ms_datfile* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_datfile& operator=(const ms_datfile& right);
#endif
        //! Returns a path to the file that was set explicitly or the default file name.
        std::string getFileName() const;

        //! Set the file name explicitly, before saving or loading.
        void setFileName(const char* name);

        //! Sets the sessionID and proxy server for use with an HTTP transfer.
        void setConnectionSettings(const matrix_science::ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        matrix_science::ms_connection_settings getConnectionSettings() const;

        //! Call this method in order to read the configuration information from the file.
        void read_file();

        //! Saves the content in the file.
        void save_file();

        //! Returns an instance of the class representing the <tt>Databases</tt> section.
        const ms_databases* getDatabases() const;

        //! Update the content of the <tt>Databases</tt> section.
        void setDatabases(const ms_databases* dbs);

        //! Returns an instance of the class representing the <tt>PARSE</tt> section.
        const ms_parseoptions* getParseOptions() const;

        //! Update the content of the <tt>PARSE</tt> section.
        void setParseOptions(const ms_parseoptions* rules);

        //! Returns an instance of the class representing the <tt>WWW</tt> section.
        const ms_wwwoptions* getWWWOptions() const;

        //! Update the content of the <tt>WWW</tt> section.
        void setWWWOptions(const ms_wwwoptions* www);

        //! Returns an instance of the class representing the <tt>Processors</tt> section.
        const ms_processoroptions* getProcessors() const;

        //! Update the content of the <tt>Processors</tt> section.
        void setProcessors(const ms_processoroptions* info);

        //! Returns an instance of the class representing one of the <tt>Taxonomy_XXX</tt> sections.
        const ms_taxonomyrules* getTaxonomyRules(const int idx) const;

        //! Update or create an existing the <tt>Taxonomy_XXX</tt> section.
        void setTaxonomyRules(const int idx, const ms_taxonomyrules* rules);

        //! Returns a maximum possible Taxonomy section number that can be used to retrieve/set the section content.
        int getMaxTaxonomyRules() const;

        //! Returns an instance of the class representing the <tt>Cluster</tt> section.
        const ms_clusterparams* getClusterParams() const;

        //! Update the content of the <tt>Cluster</tt> section.
        void setClusterParams(const ms_clusterparams* params);

        //! Returns an instance of the class representing the <tt>UniGene</tt> section.
        const ms_unigeneoptions* getUniGeneOptions() const;

        //! Update the content of the <tt>UniGene</tt> section.
        void setUniGeneOptions(const ms_unigeneoptions * uoptions);

        //! Returns an instance of the class representing the <tt>Options</tt> section.
        const ms_mascotoptions* getMascotOptions() const;

        //! Update the content of the <tt>Options</tt> section.
        void setMascotOptions(const ms_mascotoptions* opt);

        //! Returns an instance of the class representing the <tt>Cron</tt> section.
        const ms_cronoptions* getCronOptions() const;

        //! Update the content of the <tt>Cron</tt> section.
        void setCronOptions(const ms_cronoptions* options);

        //! Returns an instance of the class containing location of other configuration files.
        const ms_mascotfiles* getMascotFiles() const;

        //! Update the location of other configuration files.
        void setMascotFiles(const ms_mascotfiles* src);

        //! Sets the time out for local file search.
        void setFindFileTimeout(const int seconds);

        //! Returns current time out value in seconds for open file operation.
        int getFindFileTimeout() const;

        //! Utility function for the installation program.
        unsigned int checkSeqDBInstallRequest(const std::string & dbName,
                                              const double newFastaSize) const;

        //! Creates and updates <tt>mascot.dat</tt> for the Mascot Installation programs.
        bool updateForInstaller(ms_errs    & log,
                                const char         * not_mascot_dat_path, 
                                const char         * mascot_dat_path,
                                const char         * licenseFilePath,
                                const char         * pathToMascot,
                                const char         * mascotURL,
                                const char         * sequenceDatabases,
                                const bool           clusterEnable,
                                const WST            wst,
                                const char         * NTGuestName,
                                const char         * NTAdminName,
                                const unsigned int   flags);

    private:
        std::string         filename_;

        int                 findFileTimeout_;

        ms_databases        Databases;

        ms_parseoptions     parseOptions;

        ms_wwwoptions       wwwOptions;

        ms_processoroptions Processors;

        ms_taxonomyrules    **taxonomyRules;

        ms_clusterparams    clusterParams;

        ms_unigeneoptions   unigeneOptions;

        ms_mascotoptions    Options;

        ms_cronoptions      cronOptions;

        ms_mascotfiles      MascotFiles;

        std::vector< std::string > comments_;

        ms_connection_settings  cs_;

    private:
        void defaultFileName();

        void defaultTaxonomyRules();
        void copyTaxonomyRules(const ms_datfile* right);

        void save_comment(const int i, void *f);

        void get_db_info(ms_filesource *f);
        void save_db_info(void *f);

        void get_parse_rules(ms_filesource *f);
        void save_parse_rules(void *f);

        void get_www(ms_filesource *f);
        void save_www(void *f);

        void get_taxonomy_rules(ms_filesource *f, const char * sectionTitle, const std::string comment = "");
        void save_taxonomy(void *f, const int index);

        void get_cluster_params(ms_filesource *f);
        void save_cluster_params(void *f);

        void get_unigene_options(ms_filesource *f);
        void save_unigene_options(void *f);

        void get_processors(ms_filesource *f);
        void save_processors(void *f);

        void get_options(ms_filesource *f);
        void save_options(void *f);

        void get_cron_job_list(ms_filesource *f);
        void save_cron_job_list(void *f);

        bool get_parse_rule_plus(const char *lineForError, char *pSrc, ms_parserule_plus *pRulePlus);
        void setIamParameter();
        void checkAndCompileParseRules();
        ms_cronjob * parseStringIntoCron(const char *szBuf);

        int parseIntoArray(const char * szBufCopy, 
                           bool * array, 
                           int minVal, 
                           int maxVal, 
                           int starMeans, 
                           const char * wholeLine);

        bool skipToEndOfSection(ms_filesource *f);
        bool getStringExcludingQuotes(const char *line, char *pDst);
        void readToEndOfLine(const char * pSrc, char * pDst, int maximumLength);
        void replaceHashes(matrix_science::ms_customproperty & section,
                           const std::string & localPath,
                           const std::string & hostname,
                           const std::string & url);
    }; // class ms_datfile
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_DATFILE_HPP

/*------------------------------- End of File -------------------------------*/
