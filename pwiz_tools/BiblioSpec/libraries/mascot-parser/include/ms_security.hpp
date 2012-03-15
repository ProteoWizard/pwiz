/*
##############################################################################
# file: ms_security.hpp                                                      #
# 'msparser' toolkit                                                         #
# Encapsulates Mascot security as used in authentication                     #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.13 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_security_INCLUDED_)
#define ms_security_INCLUDED_

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


#ifdef _WIN32
#include <io.h>
#else
#endif

#include <time.h>

namespace matrix_science {
    class ms_security_options;
    class ms_security_task;
    class ms_security_tasks;

#ifndef SWIG
    class ms_userSortByID
    {
    public:
        bool operator() (const ms_user * t1, const ms_user * t2) const
        {
            return (t1->getID() < t2->getID());
        }
    };

    class ms_groupSortByID
    {
    public:
        bool operator() (const ms_group * t1, const ms_group * t2) const
        {
            return (t1->getID() < t2->getID());
        }
    };

#endif

/** @addtogroup security_group
 *  @{
 **/

    //! The main security class to be used by the administration application.
    /*! 
     * Other applications should just need to use ms_session.
     *
     * When an object of this class is created, it loads all the users, groups
     * tasks and options into memory.
     */
    class MS_MASCOTRESFILE_API ms_security : public ms_errors
    {

    public:
        //! The constructor loads all the groups and users information from the <tt>../config</tt> directory.
	    ms_security();
	    ~ms_security();

    public:
	    //! Add a new user to the current list of users.
        bool addNewUser(const std::string       sessionID,
                        const int               userID,
                        const std::string       userName,
                        const std::string       password,
                        const time_t            passwordExpiry,
                        const std::string       fullName,
                        const std::string       emailAddress,
                        const ms_user::usertype userType,
                        const bool              enabled);

        //! Return a user given the name.
        ms_user getUser(const std::string userName) const;

        //! Return a user given the ID.
        ms_user getUserFromID(const int userID) const;

        //! Return a list of all user IDs.
        std::vector<int> getAllUserIDs() const;

        //! Delete a user from the system.
        bool deleteUser(const std::string sessionID, const std::string userName);

        //! Update an existing user int the system.
        bool updateUser(const std::string sessionID, const ms_user user);

        //! Update the password for an existing user.
        bool updatePassword(const std::string sessionID, 
                            const std::string userName,
                            const std::string oldPassword,
                            const std::string newPassword);

        //! Returns a list of Integra users.
        std::vector<std::string> getIntegraUsers() const;

        //! Add a new group to the current list of groups.
	    bool addNewGroup(const std::string sessionID,
                         const int groupID,
                         const std::string groupName);

        //! Return a group given the name .
	    ms_group getGroup(const std::string groupName) const;

        //! Return a group given the ID.
	    ms_group getGroupFromID(const int groupID) const;

        //! Return a list of all group IDs.
        std::vector<int> getAllGroupIDs() const;

        //! Delete a group from the system.
        bool deleteGroup(const std::string sessionID, const std::string groupName);

        //! Call this after updating any details for a group.
        bool updateGroup(const std::string sessionID, const ms_group & group);

        //! Return a list of the tasks permitted for the user.
        ms_security_tasks getPermittedTasksForUser(const std::string name) const;

        //! Return the security options.
        ms_security_options getMascotSecurityOptions() const;

        //! Return the complete list of possible tasks available.
        ms_security_tasks getTasks() const;

        //! Create default users, groups and sessions.
        bool createDefaults(const std::string sessionID = "");

        //! Update all the session files after making a change to parameters.
        bool updateAllSessionFiles(bool deleteOnly = false);

    protected:
        bool loadFromFile();
        bool saveToFile();

    private:
        typedef std::set<ms_user  *, ms_userSortByID>  userlist_t;
	    typedef std::set<ms_group *, ms_groupSortByID> grouplist_t;

        const char * invalidChars_;

        userlist_t users_;
        grouplist_t groups_;
        ms_security_tasks * tasks_;
	    ms_security_options options_;
	    int nextFreeGroupID_;
	    int nextFreeUserID_;

        void removeAllUsersFromMemory();
        void removeAllGroupsFromMemory();
        bool doUpdateUser(const ms_user user);

    };
/** @} */ // end of security_group
}
#endif // !defined(ms_security_INCLUDED_)
