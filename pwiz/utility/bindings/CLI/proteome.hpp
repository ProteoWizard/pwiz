#ifndef _PROTEOME_HPP_CLI_
#define _PROTEOME_HPP_CLI_


namespace pwiz {
namespace CLI {
namespace proteome {


public ref class Chemistry
{
    public:

    /// <summary>
	/// the mass of a proton in unified atomic mass units
	/// </summary>
    static property double Proton { double get(); }

    /// <summary>
	/// the mass of a neutron in unified atomic mass units
	/// </summary>
    static property double Neutron { double get(); }

    /// <summary>
	/// the mass of an electron in unified atomic mass units
	/// </summary>
    static property double Electron { double get(); }
};

ref class Fragmentation;

/// <summary>
/// represents a peptide (sequence of amino acids)
/// </summary>
public ref class Peptide
{
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

    internal:
    ref class Impl;
    Impl^ impl_;
};


/// <summary>
/// provides fragment ion masses for a peptide
/// </summary>
public ref class Fragmentation
{
    public:

    Fragmentation(Peptide^ peptide,
                  bool monoisotopic,
                  bool modified);

    /// <summary>
    /// returns the a ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double a(int length, int charge);

    /// <summary>
    /// returns the b ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double b(int length, int charge);

    /// <summary>
    /// returns the c ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double c(int length, int charge);

    /// <summary>
    /// returns the x ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double x(int length, int charge);

    /// <summary>
    /// returns the y ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double y(int length, int charge);

    /// <summary>
    /// returns the z ion of length &lt;length&gt;
    /// if &lt;charge&gt; = 0: returns neutral mass
    /// if &lt;charge&gt; > 0: returns charged m/z
    /// </summary>
    double z(int length, int charge);

    internal:
    ref class Impl;
    Impl^ impl_;
};

} // namespace proteome
} // namespace CLI
} // namespace pwiz

#endif // _PROTEOME_HPP_CLI_
