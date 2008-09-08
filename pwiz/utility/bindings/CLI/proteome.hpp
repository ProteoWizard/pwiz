#ifndef _PROTEOME_HPP_CLI_
#define _PROTEOME_HPP_CLI_

#include "SharedCLI.hpp"
#include "utility/proteome/Chemistry.hpp"
#include "utility/proteome/Peptide.hpp"

namespace pwiz {
namespace CLI {
namespace proteome {

public ref class Chemistry
{
    public:

    /// <summary>
	/// the mass of a proton in unified atomic mass units
	/// </summary>
    static const double Proton = pwiz::proteome::Chemistry::Proton;

    /// <summary>
	/// the mass of a neutron in unified atomic mass units
	/// </summary>
    static const double Neutron = pwiz::proteome::Chemistry::Proton;

    /// <summary>
	/// the mass of an electron in unified atomic mass units
	/// </summary>
    static const double Electron = pwiz::proteome::Chemistry::Proton;
};

ref class Fragmentation;

/// <summary>
/// represents a peptide (sequence of amino acids)
/// </summary>
public ref class Peptide
{
    DEFINE_INTERNAL_BASE_CODE(pwiz::proteome, Peptide);

    public:

    Peptide(System::String^ sequence);

    property System::String^ sequence {System::String^ get();}

    double monoisotopicMass();
    double monoisotopicMass(bool modified);
    double monoisotopicMass(int charge);
    double monoisotopicMass(bool modified, int charge);

    double molecularWeight();
    double molecularWeight(bool modified);
    double molecularWeight(int charge);
    double molecularWeight(bool modified, int charge);

    /// <summary>
    /// returns a fragmentation model for the peptide;
    /// fragment masses can calculated as mono/avg and as modified/unmodified
    /// </summary>
    Fragmentation^ fragmentation(bool monoisotopic, bool modified);
};


/// <summary>
/// provides fragment ion masses for a peptide
/// </summary>
public ref class Fragmentation
{
    DEFINE_INTERNAL_BASE_CODE(pwiz::proteome, Fragmentation);

    public:

    Fragmentation(Peptide^ peptide,
                  bool monoisotopic,
                  bool modified);

    /// <summary>
    /// returns the a ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double a(int length, int charge);

    /// <summary>
    /// returns the b ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double b(int length, int charge);

    /// <summary>
    /// returns the c ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double c(int length, int charge);

    /// <summary>
    /// returns the x ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double x(int length, int charge);

    /// <summary>
    /// returns the y ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double y(int length, int charge);

    /// <summary>
    /// returns the z ion of length <length>
    /// if <charge> = 0: returns neutral mass
    /// if <charge> > 0: returns charged m/z
    /// </summary>
    double z(int length, int charge);
};

} // namespace proteome
} // namespace CLI
} // namespace pwiz

#endif // _PROTEOME_HPP_CLI_
