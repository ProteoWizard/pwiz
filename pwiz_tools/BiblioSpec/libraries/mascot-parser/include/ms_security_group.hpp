/*
##############################################################################
# file: ms_security_group.hpp                                                #
# 'msparser' toolkit                                                         #
# Encapsulates a mascot user as used in authentication                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2004 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotresfi $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.12 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/



#if !defined(ms_users_group_INCLUDED_)
#define ms_users_group_INCLUDED_

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

    //! This class will normally only be used by Mascot Security Administration applications.
    class MS_MASCOTRESFILE_API ms_group
    {

    public:
        //! Definitions for predefined groups.
        /*!
         * See \ref DynLangEnums.
         */
        enum systemids
        {
            GROUPID_GUESTS          = 0x0001,  //!< The guests group. By default, the matrix_science::ms_user::USERID_GUEST user belongs to this group.
            GROUPID_ADMINISTRATORS  = 0x0002,  //!< The Administrators group. By default, the matrix_science::ms_user::USERID_ADMINISTRATOR user belongs to this group.
            GROUPID_POWERUSERS      = 0x0003,  //!< An example group for users who need access to almost everything.
            GROUPID_DAEMONS         = 0x0004,  //!< Mascot Daemon needs to be set up with a user to run searches.  By default, the matrix_science::ms_user::USERID_DAEMON user belongs to this group.
            GROUPID_INTEGRA_SYSTEM  = 0x0005,  //!< The Mascot Integra system group, only member is normally USERID_INTEGRA_SYSTEM. Users will never be able to log in as this user - it is only used from the Integra server.
            GROUPID_LAST            = 0x0006   //!< Placeholder.
        };

        //! Create a new ms_group.
	    ms_group();

        //! Create a new ms_group. 
        ms_group(const int groupID, const std::string groupName);

        // Copying constructor for c++ programs - don't document
        ms_group(const ms_group &src);

#ifndef SWIG
        // Assignment operator for c++ programs - don't document
        ms_group & operator=(const ms_group & right);
#endif
	    ~ms_group();

        //! Each group has one or more users. Use this function to add a user.
	    bool addUser(const int userID);

        //! Each group has one or more users. Use this function to delete a user.
	    bool deleteUser(const int userID);

        //! Returns true if the user is a member of the group.
        bool isUserInGroup(const int userID) const;

        //! Return a list of user ids in the group.
        std::vector<int> getAllUserIDs() const;

        //! Return the name of the group.
        std::string getName() const;

        //! Set the name of the group.
	    void setName(const std::string newVal);

        //! Each group has one or more permitted tasks. Use this function to add to the list.
        void addPermittedTask(const matrix_science::ms_security_task & task);

        //! Each group has one or more permitted tasks. Use this function to remove one from the list.
        bool removePermittedTask(const int taskID);

        //! Return the list of permitted tasks.
        matrix_science::ms_security_tasks getPermittedTasks() const;

        //! Sets the list of permitted tasks.
        void setPermittedTasks(matrix_science::ms_security_tasks& tasks);

        //! Return the unique group ID.
        int  getID() const;

        //! Set the unique group ID.
        void setID(int newVal);

    private:
	    int groupID_;
        typedef std::set<int> users_t;
        ms_security_tasks tasks_;
	    std::string groupName_;
        users_t        users_;

        void copyFrom(const ms_group * src);
        void updateUsersArrayTasks();
    };
    /** @} */ // end of security_group 
}
#endif // !defined(ms_users_group_INCLUDED_)
