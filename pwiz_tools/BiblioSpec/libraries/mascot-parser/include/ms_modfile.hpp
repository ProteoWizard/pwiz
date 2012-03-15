/*
##############################################################################
# file: ms_modfile.hpp                                                       #
# 'msparser' toolkit                                                         #
# Encapsulates "mod_file"-file that defines amino acid modifications         #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_modfile.hpp             $ #
#     $Author: villek $ #
#       $Date: 2011-06-03 08:48:21 $ #
#   $Revision: 1.39 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MODFILE_HPP
#define MS_MODFILE_HPP

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
#include <memory>

namespace msparser_internal {
    class ms_modification_impl;
}

namespace matrix_science {

    class ms_masses;
    class ms_umod_modification;

    /** @addtogroup config_group
     *  
     *  @{
     */

    //! All possible modification types.
    /*!
     * See \ref DynLangEnums.
     */
    enum MOD_TYPES
    {
        MOD_TYPE_RESIDUE        = 0, //!< Applies to specific residues, independent of position within the peptide.
        MOD_TYPE_N_TERM         = 1, //!< Applies to the peptide N-terminus, independent of residue identity.
        MOD_TYPE_C_TERM         = 2, //!< Applies to the peptide C-terminus, independent of residue identity.
        MOD_TYPE_N_PROTEIN      = 3, //!< Applies to the protein N-terminus, independent of residue identity.
        MOD_TYPE_C_PROTEIN      = 4, //!< Applies to the protein C-terminus, independent of residue identity.
        MOD_TYPE_N_TERM_RESIDUE = 5, //!< Applies to a specific residue only when that residue is at N-terminus.
        MOD_TYPE_C_TERM_RESIDUE = 6, //!< Applies to a specific residue only when that residue is at C-terminus.
        MOD_TYPE_N_PROTEIN_RESIDUE= 7, //!< Applies to a specific residue only when that residue is at N-terminus of a protein.
        MOD_TYPE_C_PROTEIN_RESIDUE= 8, //!< Applies to a specific residue only when that residue is at C-terminus of a protein.
        MOD_TYPE_______LAST     = 9     // This must always be last....
    };

    //! All possible mass types.
    /*! 
     * See \ref DynLangEnums.
     */
    enum MASS_TYPE
    {
        MASS_TYPE_MONO = 0x0000,  //!< monoisotopic mass index
        MASS_TYPE_AVE  = 0x0001   //!< average mass index
    };

    //! Maximum number of variable mods.
    /*! 
     * See \ref DynLangEnums.
     */
    enum 
    { 
        MAX_VAR_MODS = 'W' -'A' + 10  //!< The results file uses 1..9 and A..W (1..32).
    };

    typedef std::vector<double> ms_vectorDouble;
    typedef std::vector< bool > ms_vectorBool;

    //! The class represents a single modification-entry in <tt>mod_file</tt>.
    /*!
     *  Instances of this class are created in ms_modfile when parsing
     *  <tt>mod_file</tt>.  It should be used in read-only mode. But if you
     *  really want to create a custom modification from scratch then specify
     *  modification type and supply the mass file object first and set residue
     *  masses or deltas second.
     *
     *  Modifications that only apply to a specific residue at a specific 
     *  terminus need to be handled with care. An example is the conversion
     *  of Methionine to Homoserine lactone. This is defined in
     *  <tt>mod_file</tt> as follows:
\verbatim
   Title:Hse_lact (C-term M)
   ResiduesCterm:M -31.000631 -31.1002
\endverbatim
     *
     *  These mods are always processed as variable mods, even if they 
     *  were originally specified as fixed mods. Mascot Parser implicitly 
     *  treats them as terminal group mods, so that the return values 
     *  from Mascot Parser for this modification are as follows:
\verbatim
     ms_modification::getResidueMass(matrix_science::MASS_TYPE_MONO, "M") returns 0
     ms_modification::getDelta(matrix_science::MASS_TYPE_MONO) returns -48.003370665
     ms_modification::isResidueModified("M") returns TRUE
     ms_modification::getCTerminusMass(matrix_science::MASS_TYPE_MONO) returns -31.000631
     ms_masses::getResidueMass(matrix_science::MASS_TYPE_MONO, "M") returns 131.04048
     ms_masses::isResidueModified("M") returns FALSE
     ms_masses::getCtermDelta(matrix_science::MASS_TYPE_MONO) returns 0
     ms_masses::getCterminalMass(matrix_science::MASS_TYPE_MONO) returns 17.002739665
\endverbatim
     *
     *  When writing code, you may wish to categorise such cases as residue 
     *  mods. If so, in the case of Hse_lact, the mass of the modified 
     *  residue would be the sum of the return values from 
     *  ms_modification::getDelta() and ms_masses::getResidueMass().
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
    class MS_MASCOTRESFILE_API ms_modification: public ms_customproperty
    {
        friend class ms_modfile;
        friend class ms_modvector;
    public:

        //! Default constructor.
        ms_modification(const ms_masses* massFile = NULL);

        //! Copying constructor.
        ms_modification(const ms_modification& src);

        //! Destructor.
        ~ms_modification();

        //! Re-initialises the object.
        void defaultValues();

        //! Copies modification configuration from another instance.
        void copyFrom(const ms_modification* src);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_modification& operator=(const ms_modification& right);
#endif
        //! Retrieves modification from the Unimod file object by its mod_file-style name.
        bool getFromUnimod(const char* modName, const ms_umod_configfile *umod_file);

        //! Stores pointer to the external ms_masses object to refer later.
        void setMassFile(const ms_masses* massFile);

        //! Returns a pointer to ms_masses instance held internally.
        const ms_masses* getMassFile() const;

        //! \deprecated Do not use.
        void copyMassFile(const ms_masses* massFile);

        //! Returns the modification title.
        std::string getTitle() const;

        //! Change the title for the modification.
        void setTitle(const char* value);

        //! Returns one of the enumeration MOD_TYPES values.
        int getModificationType() const;

        //! Sets the modification type parameter into the supplied value.
        void setModificationType(const int type);

        //! Returns value of the <b>Hidden</b> parameter.
        bool isHidden() const;

        //! Sets the value of <b>Hidden</b> parameter.
        void setHidden(const bool value);

        //! Returns TRUE if the modification is derived from the <tt>substitutions</tt> file.
        bool isSubstitution() const;

        //! Returns delta (in Da) value for the given mass type.
        double getDelta(const MASS_TYPE massType) const;

        //! Changes delta value for the given mass type.
        void setDelta(const double massMono, const double massAve);

        //! Return neutral loss delta.
        ms_vectorDouble getNeutralLoss(const MASS_TYPE massType) const;

        //! Returns a vector of boolean values (required/not required) for each neutral loss value.
        ms_vectorBool getNeutralLossRequired() const;

        //! Returns a vector of corresponding frequencies for each neutral loss value.
        //std::vector< double > getNeutralLossFrequencies() const;

        //! Change neutral loss delta.
        void setNeutralLoss(const ms_vectorDouble massMono, const ms_vectorDouble massAve, const ms_vectorBool required);

        //! Returns a total number of modified residue for a residue-specific modification.
        int getNumberOfModifiedResidues() const;

        //! Traverse the modified residues list.
        char getModifiedResidue(const int n) const;

        //! Check a single residue.
        bool isResidueModified(const char residue) const;

        //! Returns a mass of modified residue.
        double getResidueMass(const MASS_TYPE massType, const char residue) const;

        //! Deletes all residues modified together with their masses and deltas.
        void clearModifiedResidues();

        //! Adds one more residue that can be modified together with its new mass.
        void appendModifiedResidue(const char residue, const double massMono, const double massAve);

        //! Returns the mass of modified N-terminus.
        double getNTerminusMass(const MASS_TYPE massType) const;

        //! Sets the mass of modified N-terminus.
        void setNTerminusMass(const double massMono, const double massAve);

        //! Returns the mass of modified C-terminus.
        double getCTerminusMass(const MASS_TYPE massType) const;

        //! Sets the mass of modified C-terminus.
        void setCTerminusMass(const double massMono, const double massAve);

        //! Returns the number of masses to be ignored.
        int getNumberOfIgnoreMasses() const;

        //! Returns the value of the i<sup>th</sup> ignore mass. See #getNumberOfIgnoreMasses() for more explanations.
        double getIgnoreMass(const MASS_TYPE massType, const int idx) const;

        //! Deletes all ignore-masses.
        void clearIgnoreMasses();

        //! Appends a new ignore mass pair.
        void appendIgnoreMass(const double massMono, const double massAve);

        //! Returns a vector of RepPepNeutralLoss-values.
        ms_vectorDouble getReqPepNeutralLoss(const MASS_TYPE massType) const;

        //! Set the vector of RepPepNeutralLoss-values.
        void setReqPepNeutralLoss(const ms_vectorDouble valuesMono, const ms_vectorDouble valuesAve);

        //! Returns a vector of PepNeutralLoss-values.
        ms_vectorDouble getPepNeutralLoss(const MASS_TYPE massType) const;

        //! Set the vector of PepNeutralLoss-values.
        void setPepNeutralLoss(const ms_vectorDouble valuesMono, const ms_vectorDouble valuesAve);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        void setCustomProperty();

        msparser_internal::ms_modification_impl * m_pImpl;
    }; // ms_modification

    //! General usage class for creating lists of modifications to be passed as parameters.
    class MS_MASCOTRESFILE_API ms_modvector
    {
        friend class ms_modfile;
    public:
        //! Default constructor.
        ms_modvector();

        //! Copying constructor.
        ms_modvector(const ms_modvector& src);

        //! Copying constructor that takes a Unimod file object as a source.
        ms_modvector(const ms_umod_modification &src, const ms_umod_configfile &umod_file);

        //! Destructor.
        ~ms_modvector();

        //! Copies all content from another instance.
        void copyFrom(const ms_modvector* right);

        //! Copies all content from a Unimod modification object.
        void copyFrom(const ms_umod_modification* right, const ms_umod_configfile *umod_file);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_modvector& operator=(const ms_modvector& right);
#endif

        //! Returns the number of modifications currently held in the vector.
        int getNumberOfModifications() const;

        //! Deletes all modifications from the vector.
        void clearModifications();

        //! Adds a new modification at the end of the vector.
        void appendModification(const ms_modification* item);

        //! Returns a modification object by its number.
        const ms_modification * getModificationByNumber(const int numMod) const;

    private:
        typedef std::vector<ms_modification* > mod_vector;

        mod_vector  entries_;
    }; // class ms_modvector

    //! Use this class in order to read in the amino acid modification file.
    /*! 
     *  Amino acid modifications are defined in two files: <tt>mod_file</tt>
     *  and <tt>substitutions</tt>.  An instance of this class can be used to
     *  read a file with any other name. Otherwise, the default name
     *  <tt>../config/mod_file</tt> is used. Just create an instance of the
     *  class and call #read_file()-member. 
     *
     *  Alternatively, you can call #setFileName() to supply a custom file
     *  name. It is also recommended to read in the atom masses configuration
     *  file with ms_masses and call #setMassFile() before calling
     *  #read_file().
     *
     *  After reading a file and before using the object, check its by calling
     *  #isValid() and retrieve error descriptions with #getLastErrorString()
     *  if not valid.
     */
    class MS_MASCOTRESFILE_API ms_modfile: public ms_errors
    {
    public:
        //! Default constructor.
        ms_modfile();

        //! Copying constructor.
        ms_modfile(const ms_modfile& src);


        //! Copying constructor that accepts unimod-file object.
        ms_modfile(const ms_umod_configfile& src,
            const unsigned int flags = ms_umod_configfile::MODFILE_FLAGS_ALL);

        //! Immediate-action constructor that reads the given file on construction.
        ms_modfile(const char* filename, 
                   const ms_masses* massFile, 
                   const bool fromSubstitutions = false,
                   const ms_connection_settings * cs = 0);

        //! Destructor.
        ~ms_modfile();

        //! Call this member if you want to start again.
        void defaultValues();

        //! Copies all content from another instance.
        void copyFrom(const ms_modfile* right);

        //! Copies all content from unimod-file object.
        void copyFrom(const ms_umod_configfile* right,
                      const unsigned int flags = ms_umod_configfile::MODFILE_FLAGS_ALL);

#ifndef SWIG
        //! C++ style assignment operator.
        ms_modfile& operator=(const ms_modfile& right);
#endif
        //! Call this member to set a custom file name to read from a different location.
        void setFileName(const char* name);

        //! Returns a file name that is used to read configuration information.
        std::string getFileName() const;

        //! Sets the sessionID and proxy server for use with an HTTP transfer.
        /*!
         * This value would normally be passed in the constructor.
         * \param cs is the new connection settings.
         */
        void setConnectionSettings(const ms_connection_settings & cs);

        //! Returns the sessionID and proxy server for use with an HTTP transfer.
        /*! See also the constructor documentation and setConnectionSettings().
         * \return The current connection settings.
         */
        ms_connection_settings getConnectionSettings() const;

        //! Set the atom masses to use when generating fragmentation mass deltas.
        void setMassFile(const ms_masses* massFile);

        //! Returns previously set ms_masses class instance.
        const ms_masses* getMassFile() const;

        //! Returns <b>TRUE</b> if reading is done from the <tt>substitutions</tt> file.
        bool isFromSubstitutions() const;

        //! Sets internal flag telling which file should be used for extracting modifications.
        void setFromSubstitutions(const bool value);

        //! Reads configuration information from the file.
        void read_file();

        //! Stores modification information in the file.
        void save_file();

        //! Re-orders mods by their names.
        void sortModifications();

        //! Returns a number of modifications currently held in memory.
        int getNumberOfModifications() const;

        //! Deletes all modifications from the list.
        void clearModifications();

        //! Adds a new modification at the end of the list.
        void appendModification(const ms_modification* item);

        //! Returns a modification object by its number.
        const ms_modification * getModificationByNumber(const int numMod) const;

        //! Returns a modification object by its name or NULL in case of not found.
        const ms_modification * getModificationByName(const char* nameMod) const;

        //! Update the information for a specific modification.
        bool updateModificationByNumber(const int num, const ms_modification mod);

        //! Update the information for a specific modification.
        bool updateModificationByName(const char* name, const ms_modification mod);

        //! Remove a modification from the list in memory.
        bool deleteModificationByNumber(const int num);

        //! Remove a modification from the list in memory.
        bool deleteModificationByName(const char* name);

    private:
        void read_text();
        void save_text();

        typedef std::vector<ms_modification* > entries_vector;
        entries_vector  entries;

        std::string     filename_;
        std::auto_ptr<ms_masses> m_massFile;
        std::vector< std::string > comments_;
        bool            bFromSubstitutions_;
        ms_connection_settings cs_;

    }; // ms_modfile
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_MODFILE_HPP

/*------------------------------- End of File -------------------------------*/
