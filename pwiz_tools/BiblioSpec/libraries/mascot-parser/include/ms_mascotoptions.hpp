/*
##############################################################################
# file: ms_mascotoptions.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates "Options" section of "mascot.dat" file that describes most    #
# important parameters                                                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_mascotoptions.hpp       $ #
#     $Author: villek $ #
#       $Date: 2011-06-02 15:27:44 $ #
#   $Revision: 1.43 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTOPTIONS_HPP
#define MS_MASCOTOPTIONS_HPP

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
#ifndef DOXYGEN_SHOULD_SKIP_THIS
    class MS_MASCOTRESFILE_API ms_blastaccession
    {
        friend class ms_datfile;

    public:
        ms_blastaccession();
        ms_blastaccession(const ms_blastaccession& src);
        ~ms_blastaccession();

        void defaultValues();
        void copyFrom(const ms_blastaccession* right);
#ifndef SWIG
        ms_blastaccession& operator=(const ms_blastaccession& right);
#endif
        bool isUseRegex() const;
        void setUseRegex(const bool flag);

        int getMinLength() const;
        void setMinLength(const int value);

        const char* getStart() const;
        const char* getStop() const;

    private:

        bool useRegex_;
        int  minLen_;
        char start_[255];
        char stop_[255];
    }; // class ms_blastaccession
#endif

    //! An instance of this class represents all the parameters specified in the <b>Options</b> section of <tt>mascot.dat</tt>.
    /*!
     *  An instance of this class is created and populated in ms_datfile. 
     * 
     *  It can also be created separately and initialized with default values.
     *  One can create an instance of the class or copy from another instance
     *  in order to pass it then as an options containing object.
     *
     *  For detailed information on any of the options please consult Mascot
     *  manual.
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
    class MS_MASCOTRESFILE_API ms_mascotoptions: public ms_customproperty
    {
        friend class ms_datfile;

    public:
        //! Definitions for columns in <tt>ms-review.exe</tt> for use with getReviewColWidth().
        /*!
         * See \ref DynLangEnums.
         */
        enum MS_REVIEW_COL
        {
            MS_REVIEW_COL_JOB           = 0x0000, //!< The job number column.
            MS_REVIEW_COL_PID           = 0x0001, //!< The process ID column.
            MS_REVIEW_COL_DATABASE      = 0x0002, //!< The database name column.
            MS_REVIEW_COL_USERNAME      = 0x0003, //!< The USERNAME column.
            MS_REVIEW_COL_USEREMAIL     = 0x0004, //!< The USEREMAIL column.
            MS_REVIEW_COL_TITLE         = 0x0005, //!< The search title (COM) column.
            MS_REVIEW_COL_RESULTS_FILE  = 0x0006, //!< The results file name column.
            MS_REVIEW_COL_START_TIME    = 0x0007, //!< The job start time and date column.
            MS_REVIEW_COL_DURATION      = 0x0008, //!< The job duration column (seconds).
            MS_REVIEW_COL_STATUS        = 0x0009, //!< The job status column - normally 'User read res'.
            MS_REVIEW_COL_PRIORITY      = 0x000A, //!< The job priority column (0 = normal priority).
            MS_REVIEW_COL_SEARCHTYPE    = 0x000B, //!< The job search type column (PMF, MIS or SQ).
            MS_REVIEW_COL_ENZYMEUSED    = 0x000C, //!< The job enzyme used column (Yes or No).
            MS_REVIEW_COL_IPADDRESS     = 0x000D, //!< The IP address from where the search was submitted.
            MS_REVIEW_COL_USERID        = 0x000E, //!< The USERID of the user who submitted the search.
            MS_REVIEW_COL____LAST___    = 0x000F  //!< Placeholder.
        };

        //! Definitions for retention time override when calling getPercolatorExeFlags().
        /*!
         * See \ref DynLangEnums.
         */
        enum PERC_EXE_RT {
            PERC_EXE_RT_USE_DEFAULT = 1, //!< Use the default value in <tt>mascot.dat</tt>.
            PERC_EXE_RT_FORCE_ON    = 2, //!< Override the value in <tt>mascot.dat</tt>, forcing the use of retention times.
            PERC_EXE_RT_FORCE_OFF   = 3  //!< Override the value in <tt>mascot.dat</tt>, turning off the use of retention times.
        };

        // Can't use a const int with VC6
        enum EXEC_AFTER_SEARCH
        {
            MAX_EXEC_AFTER_SEARCH = 10
        };

        //! Default constructor
        ms_mascotoptions();

        //! Copying constructor.
        ms_mascotoptions(const ms_mascotoptions& src);

        //! Destructor.
        ~ms_mascotoptions();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_mascotoptions* right);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_mascotoptions& operator=(const ms_mascotoptions& right);
#endif
        //! Initialises the instance with default values.
        void defaultValues();

        //! Indicates whether the section has been actually read from the file.
        bool isSectionAvailable() const;

        //! Changes availability of the section, i.e. whether it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Returns the value of <b>SaveLastQueryAsc</b>.
        bool isSaveLastQueryAsc() const;

        //! Sets <b>SaveLastQueryAsc</b> to <b>1</b> or <b>0</b>. 
        void setSaveLastQueryAsc(const bool flag);

        //! Returns the value of <b>SaveEveryLastQueryAsc</b>.
        bool isSaveEveryLastQueryAsc() const;

        //! Sets <b>SaveEveryLastQueryAsc</b> to <b>1</b> or <b>0</b>.
        void setSaveEveryLastQueryAsc(const bool flag);

        //! Returns the value of <b>LastQueryAscFile</b>.
        std::string getLastQueryAscFile() const;

        //! Sets <b>LastQueryAscFile</b> to the passed value.
        void setLastQueryAscFile(const char* str);

        //! Returns the value of <b>ErrorLogFile</b>.
        std::string getErrorLogFile() const;

        //! Sets <b>ErrorLogFile</b> path.
        void setErrorLogFile(const char* str);

        //! Returns the value of <b>SearchLogFile</b>.
        std::string getSearchLogFile() const;

        //! Sets <b>SearchLogFile</b>.
        void setSearchLogFile(const char* str);

        //! Returns the value of <b>MonitorLogFile</b>.
        std::string getMonitorLogFile() const;

        //! Sets <b>MonitorLogFile</b>.
        void setMonitorLogFile(const char* str);

        //! Returns the value of <b>InterFileBasePath</b>.
        std::string getInterFileBasePath() const;

        //! Sets <b>InterFileBasePath</b>.
        void setInterFileBasePath(const char* str);

        //! Returns the value of <b>InterFileRelPath</b>.
        std::string getInterFileRelPath() const;

        //! Sets <b>InterFileRelPath</b>.
        void setInterFileRelPath(const char* str);

        //! Returns the value of <b>MascotCmdLine</b>.
        std::string getMascotCmdLine() const;

        //! Sets <b>MascotCmdLine</b>.
        void setMascotCmdLine(const char* str);

        //! Returns the value of <b>TestDirectory</b>.
        std::string getTestDirectory() const;

        //! Sets <b>TestDirectory</b>.
        void setTestDirectory(const char* str);

        //! Returns the value of <b>MascotControlFile</b>.
        std::string getMascotControlFile() const;

        //! Sets <b>MascotControlFile</b>.
        void setMascotControlFile(const char* str);

        //! Returns the value of <b>MascotNodeControlFile</b>.
        std::string getMascNodeCtrlFile() const;

        //! Sets <b>MascNodeCtrlFile</b>.
        void setMascNodeCtrlFile(const char* str);

        //! Returns the value of <b>MascotJobIdFile</b>.
        std::string getMascotJobIdFile() const;

        //! Sets <b>MascotJobIdFile</b>.
        void setMascotJobIdFile(const char* str);

        //! Returns the value of <b>GetSeqJobIdFile</b>.
        std::string getGetSeqJobIdFile() const;

        //! Sets <b>GetSeqJobIdFile</b>.
        void setGetSeqJobIdFile(const char* str);

        //! Returns the value of <b>UniqueJobStartNumber</b>.
        int getUniqueJobStartNumber() const;

        //! Sets <b>UniqueJobStartNumber</b>.
        void setUniqueJobStartNumber(const int value);

        //! Returns the value of <b>ResultsPerlScript</b>.
        std::string getResultsPerlScript() const;

        //! Sets <b>ResultsPerlScript</b>.
        void setResultsPerlScript(const char* str);

        //! Returns the value of <b>NoResultsScript</b>.
        std::string getNoResultsScript() const;

        //! Sets <b>NoResultsScript</b>.
        void setNoResultsScript(const char* str);

        //! Returns the value of <b>ResultsFullURL</b>.
        std::string getResultsFullURL() const;

        //! Sets <b>ResultsFullURL</b>.
        void setResultsFullURL(const char* str);

        //! Returns the value of <b>LogoImageFile</b>.
        std::string getLogoImageFile() const;

        //! Sets <b>LogoImageFile</b>.
        void setLogoImageFile(const char* str);

        //! Returns the value of <b>MassDecimalPlaces</b>.
        int getMassDecimalPlaces() const;

        //! Sets <b>MassDecimalPlaces</b>.
        void setMassDecimalPlaces(const int value);

        //! Returns the value of <b>IonsDecimalPlaces</b>.
        int getIonsDecimalPlaces() const;

        //! Sets <b>IonsDecimalPlaces</b>.
        void setIonsDecimalPlaces(const int value);

        //! Returns the value of <b>IntensitySigFigs</b>.
        int getIntensitySigFigs() const;

        //! Sets <b>IntensitySigFigs</b>.
        void setIntensitySigFigs(const int value);

        //! Returns the value of <b>EmailUsersEnabled</b>.
        bool isEmailUsersEnabled() const;

        //! Sets <b>EmailUsersEnabled</b>.
        void setEmailUsersEnabled(const bool value);

        //! Returns the value of <b>EmailErrorsEnabled</b>.
        bool isEmailErrorsEnabled() const;

        //! Sets <b>EmailErrorsEnabled</b>.
        void setEmailErrorsEnabled(const bool value);

        //! Returns the value of <b>MailTransport</b>.
        int getMailTransport() const;

        //! Sets <b>MailTransport</b>.
        void setMailTransport(const int value);

        //! Returns the value of <b>EmailService</b>.
        std::string getEmailService() const;

        //! Sets <b>EmailService</b>.
        void setEmailService(const char* str);

        //! Returns the value of <b>EmailPassword</b>.
        std::string getEmailPassword() const;

        //! Sets <b>EmailPassword</b>.
        void setEmailPassword(const char* str);

        //! Returns the value of <b>EmailProfile</b>.
        std::string getEmailProfile() const;

        //! Sets <b>EmailPassword</b>.
        void setEmailProfile(const char* str);

        //! Returns the value of <b>sendmailPath</b>.
        std::string getSendmailPath() const;

        //! Sets <b>sendmailPath</b>.
        void setSendmailPath(const char* str);

        //! Returns the value of <b>EmailFromUser</b>.
        std::string getEmailFromUser() const;

        //! Sets <b>EmailFromUser</b>.
        void setEmailFromUser(const char* str);

        //! Returns the value of <b>EmailFromTextName</b>.
        std::string getEmailFromTextName() const;

        //! Sets <b>EmailFromTextName</b>.
        void setEmailFromTextName(const char* str);

        //! Returns the value of <b>EmailTimeOutPeriod</b>.
        int getEmailTimeOutPeriod() const;

        //! Sets <b>EmailTimeOutPeriod</b>.
        void setEmailTimeOutPeriod(const int value);

        //! Returns the value of <b>MonitorEmailCheckFreq</b>.
        int getMonitorEmailCheckFreq() const;

        //! Sets <b>MonitorEmailCheckFreq</b>.
        void setMonitorEmailCheckFreq(const int value);

        //! Returns the value of <b>MailTempFile</b>.
        std::string getMailTempFile() const;

        //! Sets <b>MailTempFile</b>.
        void setMailTempFile(const char* str);

        //! Returns the value of <b>ErrMessageEmailTo</b>.
        std::string getErrMessageEmailTo() const;

        //! Sets <b>ErrMessageEmailTo</b>.
        void setErrMessageEmailTo(const char* str);

        //! Returns the value of <b>MonitorTestTimeout</b>.
        int getMonitorTestTimeout() const;

        //! Sets <b>MonitorTestTimeout</b>.
        void setMonitorTestTimeout(const int value);

        //! Returns the value of <b>NTMonitorGroup</b>.
        std::string getNTMonitorGroup() const;

        //! Sets <b>NTMonitorGroup</b>.
        void setNTMonitorGroup(const char* str);

        //! Returns the value of <b>NTIUserGroup</b>.
        std::string getNTIUserGroup() const;

        //! Sets <b>NTIUserGroup</b>.
        void setNTIUserGroup(const char* str);

        //! Returns the value of <b>UnixWebUserGroup</b>.
        int getUnixWebUserGroup() const;

        //! Sets <b>UnixWebUserGroup</b>.
        void setUnixWebUserGroup(const int value);

        //! Returns the value of <b>ForkForUnixApache</b>.
        bool isForkForUnixApache() const;

        //! Sets <b>ForkForUnixApache</b>.
        void setForkForUnixApache(const bool value);

        //! Returns the value of <b>SeparateLockMem</b>.
        int getSeparateLockMem() const;

        //! Sets <b>SeparateLockMem</b>.
        void setSeparateLockMem(const int value);

        //! Returns the value of <b>FormVersion</b>.
        std::string getFormVersion() const;

        //! Sets <b>FormVersion</b>.
        void setFormVersion(const char* str);

        //! Returns the value of <b>MaxSequenceLen</b>.
        int getMaxSequenceLen() const;

        //! Sets <b>MaxSequenceLen</b>.
        void setMaxSequenceLen(const int value);

        //! Returns the value of <b>MaxConcurrentSearches</b>.
        int getMaxConcurrentSearches() const;

        //! Sets <b>MaxConcurrentSearches</b>.
        void setMaxConcurrentSearches(const int value);

        //! Returns the value of <b>MaxSearchesPerUser</b>.
        int getMaxSearchesPerUser() const;

        //! Sets <b>MaxSearchesPerUser</b>.
        void setMaxSearchesPerUser(const int value);

        //! Returns the value of <b>CentroidWidth</b>.
        double getCentroidWidth() const;

        //! Sets <b>CentroidWidth</b>.
        void setCentroidWidth(const double value);

        //! Returns the value of <b>CentroidWidthCount</b>.
        int getCentroidWidthCount() const;

        //! Sets <b>CentroidWidthCount</b>.
        void setCentroidWidthCount(const int value);

        //! Returns the value of <b>MaxDescriptionLen</b>.
        int getMaxDescriptionLen() const;

        //! Sets <b>MaxDescriptionLen</b>.
        void setMaxDescriptionLen(const int value);

        //! Returns the value of <b>MaxNumPeptides</b>.
        int getMaxNumPeptides() const;

        //! Sets <b>MaxNumPeptides</b>.
        void setMaxNumPeptides(const int value);

        //! Returns the value of <b>Vmemory</b>.
        INT64 getVmemory() const;

        //! Sets <b>Vmemory</b>.
        void setVmemory(const INT64 value);

        //! Returns the values of <b>ReportNumberChoices</b>.
        int getNumberOfReportNumberChoices() const;

        //! Deletes all <b>ReportNumberChoices</b> values from the list.
        void clearReportNumberChoices();

        //! Returns an element of the <b>ReportNumberChoices</b> list by its number.
        int getReportNumberChoice(const int index) const;

        //! Sets an element of the <b>ReportNumberChoices</b> list by its index.
        void setReportNumberChoice(const int index, const int value);

        //! Append a value to the <b>ReportNumberChoices</b> list.
        void appendReportNumberChoice(const int value);

        //! Returns the values of <b>TargetFDRPercentages</b>.
        int getNumberOfTargetFDRPercentages() const;

        //! Deletes all <b>TargetFDRPercentages</b> values from the list.
        void clearTargetFDRPercentages();

        //! Returns an element of the <b>TargetFDRPercentages</b> list by its number.
        double getTargetFDRPercentage(const int index) const;

        //! Sets an element of the <b>TargetFDRPercentages</b> list by its index.
        void setTargetFDRPercentage(const int index, const double value, const bool makeDefault = false);

        //! Append a value to the <b>TargetFDRPercentages</b> list.
        void appendTargetFDRPercentage(const double value, const bool makeDefault = false);

        //! Returns true if the element of <b>TargetFDRPercentages</b> is a default value.
        bool isDefaultTargetFDRPercentage(const int index) const;

        //! Makes an element of <b>TargetFDRPercentages</b> at the given index the default value.
        void setDefaultTargetFDRPercentage(const int index);

        //! Returns <b>ReviewColWidths</b> by number.
        int getReviewColWidth(const MS_REVIEW_COL index) const;

        //! Sets <b>ReviewColWidths</b>.
        void setReviewColWidth(const MS_REVIEW_COL index, const int value);

        //! Returns the value of <b>proxy_server</b>.
        std::string getProxyServer() const;

        //! Sets <b>proxy_server</b>.
        void setProxyServer(const char* str);

        //! Returns the value of <b>proxy_username</b>.
        std::string getProxyUsername() const;

        //! Sets <b>proxy_username</b>.
        void setProxyUsername(const char* str);

        //! Returns the value of <b>proxy_password</b>.
        std::string getProxyPassword() const;

        //! Sets <b>proxy_password</b>.
        void setProxyPassword(const char* str);

        //! Returns the value of <b>MinPepLenInPepSummary</b>.
        int getMinPepLenInPepSummary() const;

        //! Sets <b>MinPepLenInPepSummary</b>.
        void setMinPepLenInPepSummary(const int value);

        //! Returns the value of <b>MaxQueries</b>.
        int getMaxQueries() const;

        //! Sets <b>MaxQueries</b>.
        void setMaxQueries(const int value);

        //! Returns the value of <b>ShowSubSets</b>.
        bool isShowSubsets() const;

        //! Sets <b>ShowSubSets</b>.
        void setShowSubsets(const bool value);

        //! Returns the value of <b>ShowSubSets</b>.
        double getShowSubsets() const;

        //! Sets <b>ShowSubSets</b>.
        void setShowSubsets(const double value);

        //! Returns the value of <b>RequireBoldRed</b>.
        bool isRequireBoldRed() const;

        //! Sets <b>RequireBoldRed</b>.
        void setRequireBoldRed(const bool value);

        //! Returns the value of <b>SigThreshold</b>.
        double getSigThreshold() const;

        //! Set the <b>SigThreshold</b>.
        void setSigThreshold(const double value);

        //! Returns the value of <b>SiteAnalysisMD10Prob</b>.
        double getSiteAnalysisMD10Prob() const;

        //! Set the <b>SiteAnalysisMD10Prob</b>.
        void setSiteAnalysisMD10Prob(const double value);

        //! Returns the value of <b>MaxVarMods</b>.
        int getMaxVarMods() const;

        //! Sets <b>MaxVarMods</b>.
        void setMaxVarMods(const int value);

        //! Returns the value of <b>MaxEtVarMods</b>.
        int getMaxEtVarMods() const;

        //! Sets <b>MaxEtVarMods</b>.
        void setMaxEtVarMods(const int value);

        //! Returns the value of <b>ErrTolMaxAccessions</b>.
        int getErrTolMaxAccessions() const;

        //! Sets <b>ErrTolMaxAccessions</b>.
        void setErrTolMaxAccessions(const int value);

        //! Returns the value of <b>LabelAll</b>.
        bool isLabelAll() const;

        //! Sets <b>LabelAll</b>.
        void setLabelAll(const bool value);

        //! Returns the value of <b>ShowAllFromErrorTolerant</b>.
        bool isShowAllFromErrorTolerant() const;

        //! Sets <b>ShowAllFromErrorTolerant</b>.
        void setShowAllFromErrorTolerant(const bool value);

        //! Returns the value of <b>IgnoreIonsScoreBelow</b>.
        double getIgnoreIonsScoreBelow() const;

        //! Sets <b>IgnoreIonsScoreBelow</b>.
        void setIgnoreIonsScoreBelow(const double value);

        //! Returns the value of <b>MonitorPidFile</b>.
        std::string getMonitorPidFile() const;

        //! Sets <b>MonitorPidFile</b>.
        void setMonitorPidFile(const char* str);

        //! Returns the value of <b>StoreModPermutations</b>.
        bool isStoreModPermutations() const;

        //! Sets <b>StoreModPermutations</b>.
        void setStoreModPermutations(const bool value);

        //! Returns the value of <b>ProteinsInResultsFile</b>.
        int getProteinsInResultsFile() const;

        //! Sets <b>ProteinsInResultsFile</b>.
        void setProteinsInResultsFile(const int value);

        //! Returns the value of <b>MascotMessage</b>.
        std::string getMascotMessage() const;

        //! Sets <b>MascotMessage</b>.
        void setMascotMessage(const char* str);

        //! Returns the value of <b>SplitNumberOfQueries</b>.
        int getSplitNumberOfQueries() const;

        //! Sets <b>SplitNumberOfQueries</b>.
        void setSplitNumberOfQueries(const int value);

        //! Returns the value of <b>SplitDataFileSize</b>.
        int getSplitDataFileSize() const;

        //! Sets <b>SplitDataFileSize</b>.
        void setSplitDataFileSize(const int value);

        //! Returns the value of <b>MoveOldDbToOldDir</b>.
        bool isMoveOldDbToOldDir() const;

        //! Sets <b>MoveOldDbToOldDir</b>.
        void setMoveOldDbToOldDir(const bool value);

        //! Returns the value of <b>RemoveOldIndexFiles</b>.
        bool isRemoveOldIndexFiles() const;

        //! Sets <b>RemoveOldIndexFiles</b>.
        void setRemoveOldIndexFiles(const bool value);

        //! Returns the value of <b>FeatureTableLength</b>.
        int getFeatureTableLength() const;

        //! Sets <b>FeatureTableLength</b>.
        void setFeatureTableLength(const int value);

        //! Returns the value of <b>FeatureTableMinScore</b>.
        double getFeatureTableMinScore() const;

        //! Sets <b>FeatureTableMinScore</b>.
        void setFeatureTableMinScore(const double value);

        //! Returns the value of <b>ICATLight</b>.
        std::string getICATLight() const;

        //! Sets <b>ICATLight</b>.
        void setICATLight(const char* modName);

        //! Returns the value of <b>ICATHeavy</b>.
        std::string getICATHeavy() const;

        //! Sets <b>ICATHeavy</b>.
        void setICATHeavy(const char* modName);

        //! Returns the value of <b>ICATFilter</b>.
        std::string getICATFilter() const;

        //! Sets <b>ICATFilter</b>.
        void setICATFilter(const char* filterString);

        //! Returns the value of name of the <b>ICATQuantitationMethod</b>.
        std::string getICATQuantitationMethod() const;

        //! Sets <b>ICATQuantitationMethod</b>.
        void setICATQuantitationMethod(const char* methodName);

        //! Returns the number of databases that shouldn't be checked for duplicate accessions.
        unsigned int getNumberOfIgnoreDupeAccessions() const;

        //! Clears the list of databases that shouldn't be checked for duplicate accessions.
        void clearIgnoreDupeAccessions();

        //! Returns one of the list databases that shouldn't be checked for duplicate accessions.
        std::string getIgnoreDupeAccession(const unsigned int index) const;

        //! Sets an item in the <b>IgnoreDupeAccessions</b> list by its index.
        void setIgnoreDupeAccession(const unsigned int index, const std::string value);

        //! Appends a database to the list databases that shouldn't be checked for duplicate accessions.
        void appendIgnoreDupeAccession(const std::string value);

        //! Checks to see if the passed database name is in the list databases that shouldn't be checked for duplicate accessions.
        bool isInIgnoreDupeAccessionList(const std::string value) const;

        //! Returns the value of <b>UnixDirPerm</b>.
        int getUnixDirPerm() const;

        //! Sets <b>UnixDirPerm</b>.
        void setUnixDirPerm(const int value);

        //! Returns true if the UnixDirPerm value is defined.
        bool isUnixDirPermDefined() const;

        //! Returns the value of <b>Mudpit</b>.
        int getMudpit() const;

        //! Sets <b>Mudpit</b>.
        void setMudpit(const int value);

        //! Gets the first <b>PrecursorCutOut</b>.
        double getPrecursorCutOutLowerLimit() const;

        //! Sets the first <b>PrecursorCutOut</b>.
        void setPrecursorCutOutLowerLimit(const double value);

        //! Gets the second <b>PrecursorCutOut</b>.
        double getPrecursorCutOutUpperLimit() const;

        //! Sets the second <b>PrecursorCutOut</b>.
        void setPrecursorCutOutUpperLimit(const double value);

        //! Returns the value of <b>AutoSelectCharge</b>.
        bool isAutoSelectCharge() const;

        //! Sets <b>AutoSelectCharge</b>.
        void setAutoSelectCharge(const bool value);

        //! Returns value of the <b>TaxBrowserUrl</b>.
        std::string getTaxBrowserUrl() const;

        //! Sets <b>TaxBrowserURL</b>.
        void setTaxBrowserUrl(const std::string value);

        //! Returns value of the <b>MinPepLenInSearch</b>.
        int getMinPepLenInSearch() const;

        //! Sets <b>MinPepLenInSearch</b>.
        void setMinPepLenInSearch(const int value);

        //! Returns value of the <b>MaxPepNumVarMods</b>.
        int getMaxPepNumVarMods() const;

        //! Sets <b>MaxPepNumVarMods</b>.
        void setMaxPepNumVarMods(const int value);

        //! Returns value of the <b>IteratePMFIntensities</b>.
        bool isIteratePMFIntensities() const;

        //! Sets <b>IteratePMFIntensities</b>.
        void setIteratePMFIntensities(const bool value);

        //! Returns the value of <b>MinEtagMassDelta</b>.
        double getMinEtagMassDelta(void) const;

        //! Sets <b>MinEtagMassDelta</b>.
        void setMinEtagMassDelta(const double value);

        //! Returns the value of <b>MaxEtagMassDelta</b>.
        double getMaxEtagMassDelta(void) const;

        //! Sets <b>MaxEtagMassDelta</b>.
        void setMaxEtagMassDelta(const double value);

        //! Returns the value of <b>ResultsFileFormatVersion</b>.
        std::string getResultsFileFormatVersion() const;

        //! Sets <b>ResultsFileFormatVersion</b>.
        void setResultsFileFormatVersion(const char *value);

        //! Returns the value of <b>SortUnassigned</b>.
        std::string getSortUnassigned() const;

        //! Sets <b>SortUnassigned</b>.
        void setSortUnassigned(const std::string newValue);

        //! Returns the value of <b>SelectSwitch</b>.
        int getSelectSwitch() const;

        //! Sets <b>SelectSwitch</b>.
        void setSelectSwitch(const int value);

        //! Returns the value of <b>MudpitSwitch</b>.
        double getMudpitSwitch() const;

        //! Sets <b>MudpitSwitch</b>.
        void setMudpitSwitch(const double value);

        //! Returns the value of <b>MaxDatabases</b>.
        int getMaxDatabases() const;

        //! Sets <b>MaxDatabases</b>.
        void setMaxDatabases(const int value);

        //! Returns the value of <b>CacheDirectory</b>.
        std::string getCacheDirectory() const;

        //! Sets <b>CacheDirectory</b>.
        void setCacheDirectory(const char * value);

        //! Returns the value of <b>ResfileCache</b>.
        std::string getResfileCache() const;

        //! Sets <b>ResfileCache</b>.
        void setResfileCache(const char * value);

        //! Returns the value of <b>ResultsCache</b>.
        std::string getResultsCache() const;

        //! Sets <b>ResultsCache</b>.
        void setResultsCache(const char * value);

        //! Returns the value of <b>Percolator</b>.
        bool isPercolator() const;

        //! Sets <b>Percolator</b>.
        void setPercolator(const bool value);

        //! Returns the value of <b>PercolatorFeatures</b>.
        std::string getPercolatorFeatures() const;

        //! Sets <b>PercolatorFeatures</b>.
        void setPercolatorFeatures(std::string value);

        //! Returns the value of <b>PercolatorMinQueries</b>.
        int getPercolatorMinQueries() const;

        //! Sets <b>PercolatorMinQueries</b>.
        void setPercolatorMinQueries(const int value);

        //! Returns the value of <b>PercolatorMinSequences</b>.
        int getPercolatorMinSequences() const;

        //! Sets <b>PercolatorMinSequences</b>.
        void setPercolatorMinSequences(const int value);

        //! Returns the value of <b>PercolatorUseProteins</b>.
        bool isPercolatorUseProteins() const;

        //! Sets <b>PercolatorUseProteins</b>.
        void setPercolatorUseProteins(const bool value);

        //! Returns the value of <b>PercolatorUseRT</b>.
        bool isPercolatorUseRT() const;

        //! Sets <b>PercolatorUseRT</b>.
        void setPercolatorUseRT(const bool value);

        //! Returns the value of <b>PercolatorExeFlags</b>.
        std::string getPercolatorExeFlags(bool anyRetentionTimes,
                                          const std::vector<std::string> percolatorFiles,
                                          const PERC_EXE_RT rt = PERC_EXE_RT_USE_DEFAULT) const;

        //! Sets <b>PercolatorExeFlags</b>.
        void setPercolatorExeFlags(const char * value);

        //! Returns the values of the <b>ExecAfterSearch_<i>XXX</i></b> parameters.
        int getExecAfterSearch(const char               * szResultsFilePath,
                               std::vector<std::string> & commands,
                               std::vector<std::string> & titles,
                               std::vector<int>         & waitfor,
                               std::vector<int>         & logging,
                               std::vector<std::string> & additionalFlags,
                               const char               * sessionID = 0,
                               const char               * taskID = 0) const;

        //! Sets <b>ExecAfterSearch_<i>XXX</i></b>.
        void setExecAfterSearch(const int num, std::string value);

        //! Returns the value of <b>ResultsPerlScript_2</b>.
        std::string getResultsPerlScript_2() const;

        //! Sets <b>ResultsPerlScript_2</b>.
        void setResultsPerlScript_2(std::string value);

        //! Returns the value of <b>ResultsFullURL_2</b>.
        std::string getResultsFullURL_2() const;

        //! Sets <b>ResultsFullURL_2</b>.
        void setResultsFullURL_2(std::string value);

        //! Returns the value of <b>ProteinFamilySwitch</b>.
        int getProteinFamilySwitch() const;

        //! Sets <b>ProteinFamilySwitch</b>.
        void setProteinFamilySwitch(const int value);

        //! Returns the value of <b>DecoyTypeSpecific</b>.
        int getDecoyTypeSpecific() const;

        //! Sets <b>DecoyTypeSpecific</b>.
        void setDecoyTypeSpecific(const int value);

        //! Returns the value of <b>DecoyTypeNoEnzyme</b>.
        int getDecoyTypeNoEnzyme() const;

        //! Sets <b>DecoyTypeNoEnzyme</b>.
        void setDecoyTypeNoEnzyme(const int value);

        //////////////// Undocumented members go here //////////////////

        //! Returns <b>GetSeqBlastAccession</b>.
        const ms_blastaccession* getGetSeqBlastAccession() const;

        //! Sets <b>GetSeqBlastAccession</b>.
        void setGetSeqBlastAccession(const ms_blastaccession* value);

        // MatrixScience internal usage only
        int getMinPeakIteration() const;

        // MatrixScience internal usage only
        void setMinPeakIteration(const int value);

        // MatrixScience internal usage only
        bool isEncryptURL() const;

        // MatrixScience internal usage only
        void setEncryptURL(const bool value);

        // MatrixScience internal usage only
        std::string getPercolatorExeFlags() const;

        // MatrixScience internal usage only
        std::string getExecAfterSearch(const int num) const;

        // MatrixScience internal usage only
        void checkExecAfterSearch(ms_errs & err);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        void        synchronizeReportNumberChoices_();
        void        synchronizeIgnoreDupeAccession_();
        void        synchronizeTargetFDRPercentages_();

        bool        sectionAvailable_;

        bool        saveLastQueryAsc_;
        bool        saveEveryLastQueryAsc_;
        std::string lastQueryAscFile_;

        std::string errorLogFile_;
        std::string searchLogFile_;
        std::string monitorLogFile_;
        std::string interFileBasePath_;
        std::string interFileRelPath_;

        std::string mascotCmdLine_;
        std::string testDirectory_;
        std::string mascotControlFile_;
        std::string mascNodeCtrlFile_;
        std::string mascotJobIdFile_;
        std::string getSeqJobIdFile_;
        int         uniqueJobStartNumber_;

        std::string resultsPerlScript_;
        std::string noResultsScript_;
        std::string resultsFullURL_;
        std::string logoImageFile_;

        int         massDecimalPlaces_;
        int         ionsDecimalPlaces_;
        int         intensitySigFigs_;

        bool        emailUsersEnabled_;
        bool        emailErrorsEnabled_;
        int         mailTransport_;
        std::string emailService_;
        std::string emailPassword_;
        std::string emailProfile_;
        std::string sendmailPath_;
        std::string emailFromUser_;
        std::string emailFromTextName_;
        int         emailTimeOutPeriod_;
        int         monitorEmailCheckFreq_;
        std::string mailTempFile_;
        std::string errMessageEmailTo_;

        int         monitorTestTimeout_;
        std::string NTMonitorGroup_;
        std::string NTIUserGroup_;
        int         unixWebUserGroup_;
        bool        forkForUnixApache_;
        int         separateLockMem_;

        std::string formVersion_;
        int         maxSequenceLen_;
        int         maxConcurrentSearches_;
        int         maxSearchesPerUser_;
        double      centroidWidth_;
        int         centroidWidthCount_;
        int         maxDescriptionLen_;
        int         maxNumPeptides_;
        INT64       vmemory_;

        std::vector< int > reportNumberChoices_;
        std::vector< double > targetFDRPercentages_;
        int         defaultTargetFDRPercentagesIx_;

        int         reviewColWidths_[MS_REVIEW_COL____LAST___];

        std::string proxy_server_;
        std::string proxy_username_;
        std::string proxy_password_;

        int         maxVarMods_;
        int         maxEtVarMods_;
        int         errTolMaxAccessions_;
        bool        labelAll_;
        bool        showAllFromErrorTolerant_;

        int         minPepLenInPepSummary_;
        int         maxPepNumVarMods_;
        int         maxQueries_;
        double      showSubsets_;
        bool        requireBoldRed_;
        double      sigThreshold_;
        double      siteAnalysisMD10Prob_;
        double      ignoreIonsScoreBelow_;

        std::string monitorPidFile_;
        bool        storeModPermutations_;
        int         proteinsInResultsFile_;
        std::string mascotMessage_;
        int         splitNumberOfQueries_;
        int         splitDataFileSize_;
        bool        moveOldDbToOldDir_;
        bool        removeOldIndexFiles_;

        int         featureTableLength_;
        double      featureTableMinScore_;

        std::string ICATLight_;
        std::string ICATHeavy_;
        std::string ICATFilter_;
        std::string ICATQuantitationMethod_;

        int         UnixDirPerm_;
        bool        UnixDirPermDefined_;
        int         mudpit_;

        typedef std::vector<std::string> t_dupeAccs;
        t_dupeAccs ignoreDupeAccessions_;

        double      precursorCutOutLowerLimit_;
        double      precursorCutOutUpperLimit_;

        bool        autoSelectCharge_;
        std::string taxBrowserUrl_;
        int         minPepLenInSearch_;
        bool        iteratePMFIntensities_;

        double      minEtagMassDelta_;
        double      maxEtagMassDelta_;

        std::string resultsFileFormatVersion_;
        std::string sortUnassigned_;
        int         selectSwitch_;
        double      mudpitSwitch_;

        int         maxDatabases_;
        std::string cacheDirectory_;

        std::string resfileCache_;
        std::string resultsCache_;

        bool        percolator_;
        std::string percolatorFeatures_;
        int         percolatorMinQueries_;
        int         percolatorMinSequences_;
        bool        percolatorUseProteins_;
        bool        percolatorUseRT_;
        std::string percolatorExeFlags_;
        std::vector<std::string> execAfterSearch_;
        std::vector<std::string> execAfterSearchCommands_;
        std::vector<std::string> execAfterSearchTitles_;
        std::vector<int>         execAfterSearchWaitFor_;
        std::vector<int>         execAfterSearchLogging_;
        std::vector<std::string> execAfterSearchAdditionalFlags_;
        std::string resultsPerlScript_2_;
        std::string resultsFullURL_2_;
        int         proteinFamilySwitch_;

        int         decoyTypeSpecific_;
        int         decoyTypeNoEnzyme_;

        ms_blastaccession   getSeqBlastAccession_;
        int         minPeakIteration_;
        bool        encryptURL_;
    }; // class ms_mascotoptions
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_MASCOTOPTIONS_HPP

/*------------------------------- End of File -------------------------------*/
