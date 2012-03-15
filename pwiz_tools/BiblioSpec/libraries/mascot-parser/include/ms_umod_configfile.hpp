/*
##############################################################################
# file: ms_umod_configfile.hpp                                               #
# 'msparser' toolkit                                                         #
# Represents unimod.xml file                                                 #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
# $Archive:: /Mowse/ms_mascotresfile/include/ms_umod_configfile.hpp                 $ #
# $Author: villek $ #
# $Date: 2010-09-06 16:18:57 $ #
# $Revision: 1.16 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_UMOD_CONFIGFILE_HPP
#define MS_UMOD_CONFIGFILE_HPP

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

    class ms_umod_element; // forward declaration
    class ms_umod_modification; // forward declaration
    class ms_umod_aminoacid; // forward declaration
    class ms_umod_modbrick; // forward declaration

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! This class represents the file <tt>unimod.xml</tt>.
    /*!
     * The Unimod file comprises chemical elements, amino-acids and
     * modifications.  The file can be created from scratch, read from a disk
     * file or retrieved from a remote location (HTTP) and then saved as
     * a <tt>mod_file</tt> or another <tt>unimod.xml</tt>.
     *
     * After reading a file and before using the object check validity by
     * calling #isValid() and retrieve error descriptions with
     * #getLastErrorString() if not valid. Similarly, after writing out the
     * file, #isValid() can be used to check if the file has been created
     * correctly and is valid for further use.
     */
    class MS_MASCOTRESFILE_API ms_umod_configfile: public ms_errors
    {
    public:
            //! Flags for the type of modifications to retrieve.
            /*!
             * See \ref DynLangEnums.
             *
             * The result of combining these flags is a union of modifications
             * sets.
             *
             */
            enum MODFILE_FLAGS 
            { 
                MODFILE_FLAGS_EMPTY   = 0x00000000, //!< Default behaviour (regular modifications only).
                MODFILE_FLAGS_REGULAR = 0x00000001, //!< Non-hidden modifications which are not AA-substitutions.
                MODFILE_FLAGS_HIDDEN  = 0x00000002, //!< Hidden modifications which are not AA-substitutions.
                MODFILE_FLAGS_AASUBST = 0x00000004, //!< AA substitutions.
                MODFILE_FLAGS_ALL     = 0x7FFFFFFF  //!< Get all modifications.
            };

        //! Default constructor.
        ms_umod_configfile();

        //! Immediate action constructor that reads the XML file with the given name.
        ms_umod_configfile(const char* fileName, const char* schemaFileName,
            const ms_connection_settings * cs = 0);

        //! Copying constructor.
        ms_umod_configfile(const ms_umod_configfile& right);

        //! Desctructor.
        virtual ~ms_umod_configfile();

#ifndef SWIG
        //! Assignment operator for C++ applications.
        ms_umod_configfile& operator=(const ms_umod_configfile& right);
#endif
        //! Copies all the information from another instance.
        void copyFrom(const ms_umod_configfile* right);

        //! Re-initialises the instance.
        virtual void defaultValues();

        //! Recalculates all masses derived from elements (brick masses, modification deltas etc.).
        void updateMasses();


        //! Returns the current file name.
        std::string getFileName() const;

        //! Changes the file name for subsequent read/write operations (do not use it for saving in a <tt>mod_file</tt>!).
        void setFileName(const char* filename);


        //! Call this member to specify a custom schema file name for validating the XML file.
        void setSchemaFileName(const char* name);

        //! Returns the current schema file name that is used to validate the XML file.
        std::string getSchemaFileName() const;

        //! Sets the sessionID and proxy server for use with an HTTP transfer.
        void setConnectionSettings(const ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        ms_connection_settings getConnectionSettings() const;


        //! Reads configuration information from the XML file.
        void read_file();

        //! Saves configuration information in the XML file (not <tt>mod_file</tt>).
        void save_file();


        //! Reads configuration information from a string buffer.
        void read_buffer(const char* buffer);

        //! Creates a string representation of the XML document with all the content of the object.
        std::string save_buffer(bool validateAgainstSchema = true);


        //! Validates the whole document against schema and returns errors as a string.
        std::string validateDocument() const;


        //! Returns a number of elements currently held in memory.
        int getNumberOfElements() const;

        //! Deletes all elements from the list.
        void clearElements();

        //! Adds a new element at the end of the list.
        void appendElement(const ms_umod_element *elem);

        //! Returns an element object by its number.
        const ms_umod_element * getElementByNumber(const int idx) const;

        //! Returns an element object by its name or NULL in case of not found.
        const ms_umod_element * getElementByName(const char *name) const;

        //! Update the information for a specific element.
        bool updateElementByNumber(const int idx, const ms_umod_element* element);

        //! Update the information for a specific element.
        bool updateElementByName(const char *name, const ms_umod_element* element);

        //! Remove an element from the list in memory.
        bool deleteElementByNumber(const int idx);

        //! Remove an element from the list in memory.
        bool deleteElementByName(const char *name);


        //! Returns an ordered list of modification names in mod_file-style, i.e. "Acetyl (K)" instead of "Acetyl".
        std::vector< std::string > getModFileList(const unsigned int flags = MODFILE_FLAGS_ALL) const;

        //! Returns modification index by its <tt>mod_file</tt>-style name (like "Acetyl (K)").
        int findModFileName(const char * modFileName) const;

        //! Retrieves modification index by its plain name.
        int findModification(const char* modName) const;


        //! Returns a number of modifications currently held in memory.
        int getNumberOfModifications() const;

        //! Deletes all modifications from the list.
        void clearModifications();

        //! Adds a new modification at the end of the list.
        void appendModification(const ms_umod_modification *mod);

        //! Returns a modification object by its number.
        const ms_umod_modification * getModificationByNumber(const int idx) const;

        //! Returns a modification object by its name or NULL in case of not found.
        const ms_umod_modification * getModificationByName(const char *name) const;

        //! Update the information for a specific modification.
        bool updateModificationByNumber(const int idx, const ms_umod_modification *mod);

        //! Update the information for a specific modification.
        bool updateModificationByName(const char *name, const ms_umod_modification *mod);

        //! Remove a modification from the list in memory.
        bool deleteModificationByNumber(const int idx);

        //! Remove a modification from the list in memory.
        bool deleteModificationByName(const char *name);


        //! Returns a number of amino acids currently held in memory.
        int getNumberOfAminoAcids() const;

        //! Deletes all amino acids from the list.
        void clearAminoAcids();

        //! Adds a new amino acid at the end of the list.
        void appendAminoAcid(const ms_umod_aminoacid *aa);

        //! Returns an amino acid object by its number.
        const ms_umod_aminoacid * getAminoAcidByNumber(const int idx) const;

        //! Returns an amino acid object by its name or NULL in case of not found.
        const ms_umod_aminoacid * getAminoAcidByName(const char *name) const;

        //! Update the information for a specific amino acid.
        bool updateAminoAcidByNumber(const int idx, const ms_umod_aminoacid *aa);

        //! Update the information for a specific amino acid.
        bool updateAminoAcidByName(const char *name, const ms_umod_aminoacid *aa);

        //! Remove an amino acid from the list in memory.
        bool deleteAminoAcidByNumber(const int idx);

        //! Remove an amino acid from the list in memory.
        bool deleteAminoAcidByName(const char *name);


        //! Returns a number of modification bricks currently held in memory.
        int getNumberOfModBricks() const;

        //! Deletes all modification bricks from the list.
        void clearModBricks();

        //! Adds a new modification brick at the end of the list.
        void appendModBrick(const ms_umod_modbrick *brick);

        //! Returns an modification brick object by its number.
        const ms_umod_modbrick * getModBrickByNumber(const int idx) const;

        //! Returns a modification brick object by its name or NULL in case of not found.
        const ms_umod_modbrick * getModBrickByName(const char *name) const;

        //! Update the information for a specific modification brick.
        bool updateModBrickByNumber(const int idx, const ms_umod_modbrick *brick);

        //! Update the information for a specific modification brick.
        bool updateModBrickByName(const char *name, const ms_umod_modbrick *brick);

        //! Remove a modification brick from the list in memory.
        bool deleteModBrickByNumber(const int idx);

        //! Remove a modification brick from the list in memory.
        bool deleteModBrickByName(const char *name);

        //! Returns major document schema version as read from the file, otherwise the latest.
        std::string getMajorVersion() const;

        //! Returns minor document schema version as read from the file, otherwise the latest.
        std::string getMinorVersion() const;
    private:

        std::string             m_fileName;
        std::string             m_schemaFileName;
        ms_connection_settings  m_cs;

        typedef std::vector< ms_umod_element* > element_vector;
        element_vector          m_elements;

        typedef std::vector< ms_umod_modification* > modification_vector;
        modification_vector     m_modifications;

        typedef std::vector< ms_umod_aminoacid* > aminoacid_vector;
        aminoacid_vector        m_aminoacids;

        typedef std::vector< ms_umod_modbrick* > modbrick_vector;
        modbrick_vector         m_modbricks;

        std::string m_majorVersion;
        std::string m_minorVersion;
    }; // class ms_umod_configfile

    /** @} */ // end of config_group

} // namespace matrix_science

#endif // MS_UMOD_CONFIGFILE_HPP
/*------------------------------- End of File -------------------------------*/
