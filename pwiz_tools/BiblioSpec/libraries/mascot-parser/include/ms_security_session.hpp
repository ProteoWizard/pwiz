/*
##############################################################################
# file: ms_security_session.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates a mascot session as used in authentication                    #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2011-03-30 13:41:36 $ #
#   $Revision: 1.23 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#if !defined(mascot_session_DA3EC794_90F8_4c4f_AD5D_13C3B6971A51__INCLUDED_)
#define mascot_session_DA3EC794_90F8_4c4f_AD5D_13C3B6971A51__INCLUDED_

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

// Includes from the standard template library
#include <string>
#include <set>
#include <vector>
#include <map>
#include <time.h>

namespace matrix_science {
/** @addtogroup security_group
 *  @{
 **/

    class ms_security;
    class ms_security_options;

    //! Use this class to determine if a user is logged in and if they have 'permission' to perform tasks.
    /*! 
     * This is the <i>only</i> security class that most applications will need
     * to use. This class should be used even if security is disabled because
     * the underlying structure deals with this.  If security is disabled, then
     * isValid() and isPermitted() will both return true. 
     *
     * Session information is saved in files in the <tt>../sessions</tt>
     * directory. The session files have the same name as the session id
     * which will be of the general form 'username_uniquenumber'. The
     * unique number is assigned by the system and the username is
     * the login name. 
     *
     * Special cases are as follows:
     *
     * <table>
     * <tr><th>Session Name</th><th>Description</th></tr>
     *   <tr>
     *     <td>[username]_[random_number]</td>
     *     <td>Session name for a normal user.</td>
     *   </tr>
     *   <tr>
     *     <td>[username]_webserverauth</td>
     *     <td>Session name for a user logged in using web authentication. This 
     *         session file is created/modified when the user is modified. It is 
     *         not possible to use this session id unless the user is 'logged into' 
     *         the web server using the name 'username'
     *     </td>
     *  </tr>
     *   <tr>
     *     <td>all_secdisabledsession</td>
     *     <td>The Mascot security system is not enabled. This session file 
     *         will only exist if security is disabled. Attempts to use it when 
     *         security is enabled will fail.
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>cmd_cmdlinesession</td>
     *     <td>Any applications run directly from a shell or command 
     *         prompt will use this session id. Any script run directly as a 
     *         cgi application (or chained from a cgi script) will not be able 
     *         to use this session id. 
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>guest_guestsession</td>
     *     <td>Any user who is logged in as 'guest'. If the guest account is not 
     *         enabled, this session will not be available
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>[name]_computername</td>
     *     <td>for 3rd party applications that have not implemented 
     *         Mascot security, it is possible to specify a computer name 
     *         rather than a login name and password.
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>[ipaddr]_computeripaddress</td>
     *     <td>For 3rd party applications that have not implemented
     *         Mascot security, it is possible to specify an ip address 
     *         rather than a login name and password.
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>[string]_agentstring</td>
     *     <td>Agent string such as 'Mozilla'. Will only be used a last resort
     *         by third party applications.
     *     </td>
     *   </tr>
     *   <tr>
     *     <td>integradb\@name_session</td>
     *     <td>Mascot integra user. The integradb is the value returned by: 
     *         ms_security_options::getIntegraDatabaseName(). Internally, 
     *         this session id will be translated into a Mascot Integra 
     *         session string by replacing the \@ with a pipe symbol, and 
     *         removing the underscore between the name and the session.
     *         The Mascot cookie will be this session id, and not the Integra 
     *         connection string. There is a special case of the user 
     *         being <tt>(system)</tt>.
     *     </td>
     *   </tr>
     * </table>
     */
    class MS_MASCOTRESFILE_API ms_session : public ms_errors
    {
    public:
        //! Use this constructor to open the current session.
        ms_session(const std::string session_id = "");

        //! Use this constructor to start a new secure session.
        ms_session(const std::string userName, 
                   const std::string userPassword);

        //! This constructor is used by Mascot integra to login to Mascot.
        ms_session(const std::string userName,
                   const std::string connectionID,
                   const std::string database);

        //! Use this constructor to start a new session independent of users and Mascot security.
        ms_session(const int timeout, const std::string prefix);


#if !defined(DOXYGEN_SHOULD_SKIP_THIS)
        ms_session(const ms_session& src);
#endif
        /* SWIG function dispatcher fails in Perl 5.12 for some reason, and
         * tries to use ms_session(const ms_security&, const std::string&) as
         * the constructor for cases such as ms_session(300, "score_gif")...
         * This constructor is supposed to be for internal use anyway, which
         * is why it is defined out.
         */
#if !defined(DOXYGEN_SHOULD_SKIP_THIS) && !defined(SWIG)
        ms_session(const ms_security &sec,
                   const std::string &session_id);
        ms_session(const ms_security &sec,
                   const ms_user & user);
        ms_session& operator =(const ms_session& right);
#endif 

        ~ms_session();

    public:
        //! Return the sessionID for this session file.
        std::string getID() const;

        //! Return the unique userid for the currently logged in user.
        int getUserID() const;

        //! Return the user 'login' name for the currently logged in user.
        std::string getUserName() const;

        //! Return the full name for the currently logged in user.
        std::string getFullUserName() const;

        //! Return the email address for the currently logged in user.
        std::string getEmailAddress() const;

        //! Return true if the security system is enabled.
        bool isSecurityEnabled() const;

        //! Returns the time that the session was last updated.
        time_t getLastAccessed() const;

        //! Returns the time when the session times out.
        time_t getTimeout() const;

        //! Return ip address of the currently logged in user.
        std::string getIPAddress() const;

        //! Returns the permitted tasks for the currently logged in user.
        matrix_science::ms_security_tasks getPermittedTasks() const;

        //! Sets the permitted tasks for the currently logged in user.
        bool setPermittedTasks(const matrix_science::ms_security_tasks & val);

        //! Returns a list of all the parameters saved in the session file.
        std::map<std::string, std::string> getParams() const;

        //! Check if the task is permitted.
        bool isPermitted(const int taskID) const;

        //! Check if the value is in the long integer parameter list.
        bool isPermitted_long(const int taskID, const long value) const;

        //! Check if the value is in the floating point parameter list.
        bool isPermitted_double(const int taskID, const double value) const;

        //! Check if the value is in the string parameter list.
        bool isPermitted_string(const int taskID, const std::string value) const;

        //! Check if a particular fasta file can be accessed.
        bool isFastaPermitted(const std::string database) const;

        //! Check if a results file with the passed user ID can be viewed.
        bool canResultsFileBeViewed(const int userID) const;

        //! Save an additional 'string' parameter in the session file.
        bool saveStringParam(const std::string name, const std::string param);

        //! Save an additional 'integer' parameter in the session file.
        bool saveIntParam(const std::string name, int param);

        //! Save an additional 'long integer' parameter in the session file.
        bool saveLongParam(const std::string name, long param);

        //! Save an additional 'time' parameter in the session file.
        bool saveTimeParam(const std::string name, time_t param);

        //! Save an additional 'double' parameter in the session file.
        bool saveDoubleParam(const std::string name, double param);

        //! Save an additional 'bool' parameter in the session file.
        bool saveBoolParam(const std::string name, bool param);

        //! Return an additional user 'string' parameter from the session file.
        bool getStringParam(const std::string name, std::string & param) const;

        //! Return an additional user 'string' parameter from the session file.
        std::string getStringParam(const std::string name) const;

        //! Return an additional user 'integer' parameter from the session file.
        bool getIntParam(const std::string name, int & param) const;

        //! Return an additional user 'integer' parameter from the session file.
        int getIntParam(const std::string name) const;

        //! Return an additional user 'long' parameter from the session file.
        bool getLongParam(const std::string name, long & param) const;

        //! Return an additional user 'long' parameter from the session file.
        long getLongParam(const std::string name) const;

        //! Return an additional user 'time' parameter from the session file.
        bool getTimeParam(const std::string name, time_t & param) const;

        //! Return an additional user 'time' parameter from the session file.
        time_t getTimeParam(const std::string name) const;

        //! Return an additional user 'double' parameter from the session file.
        bool getDoubleParam(const std::string name, double & param) const;

        //! Return an additional user 'double' parameter from the session file.
        double getDoubleParam(const std::string name) const;

        //! Return an additional user 'bool' parameter from the session file.
        bool getBoolParam(const std::string name, bool & param) const;

        //! Remove a user parameter from the session file.
        bool clearParam(const std::string name);

        //! Destroy the current session. Should be called when a user logs out.
        bool destroy();

        //! Returns true if the current session has timed out.
        bool isTimedOut() const;

        //! Returns the type of user.
        matrix_science::ms_user::usertype getUserType() const;

        //! Only needs to be called by administration applications after user rights have changed.
        bool update(const ms_security & sec);

        //! Returns a list of user IDs that can be spoofed.
        std::vector<int> getSpoofableUsers() const;
    
    private:
        typedef std::map<std::string, std::string> param_t;
        std::string sessionID_;
        param_t  params_;
        ms_security_tasks permittedTasks_;
        bool saveParamShouldSaveFile_;
        bool allowParamAndFileSaving_;

        bool loadSessionFromFile();
        bool saveSessionToFile();
        void setDisabledSession(const ms_security_options & opts);
        bool setCommandLineSession(const ms_security_options & secOpt);
        bool setGuestSession();
        bool doesSessionFileExist(const std::string sess);
        bool getAllParams(const ms_user & u, const ms_security & sec);
        std::string getSessionIDFromUser(const ms_user & user);
        std::string getWebSvrAuthSessionID(std::string userName = "");
        bool checkIPAddressOrComputerName(const std::string ipAddress,
                                          std::string & sessionID);
        bool checkAgentString(std::string & sessionID);
        bool validateIntegraPassword(const ms_user & u, 
                                     const ms_security_options & secOpt,
                                     const std::string & userPassword,
                                     std::string & session_id,
                                     int & errorFlag) const;
        bool verifyIntegraConnection() const;
        bool isSessionIdentical(const ms_session & sess) const;
        void copyFrom(const ms_session * src);
        void createSessionFromID(const std::string session_id);
    };
/** @} */ // end of security_group
}
#endif // !defined(mascot_session_DA3EC794_90F8_4c4f_AD5D_13C3B6971A51__INCLUDED_)
