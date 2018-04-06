//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#define PWIZ_SOURCE


#include "Unimod.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Singleton.hpp"


using namespace pwiz::cv;
using namespace pwiz::chemistry;
using namespace boost::logic;


namespace pwiz {
namespace data {
namespace unimod {


struct UnimodData : public boost::singleton<UnimodData>
{
    vector<Modification> modifications;
    map<CVID, size_t> indexByCVID;
    map<string, size_t> indexByTitle;
    multimap<double, size_t> indexByMonoisotopicMass;
    multimap<double, size_t> indexByAverageMass;

    UnimodData(boost::restricted)
    {
        // dictionary of chemical "bricks" used in unimod formulas
        map<string, Formula> brickFormulaByTitle;

        brickFormulaByTitle["Hex"] = Formula("H10 C6 O5");
        brickFormulaByTitle["HexN"] = Formula("H11 C6 O4 N1");
        brickFormulaByTitle["HexNAc"] = Formula("H13 C8 N1 O5");
        brickFormulaByTitle["dHex"] = Formula("C6 H10 O4");
        brickFormulaByTitle["HexA"] = Formula("C6 H8 O6");
        brickFormulaByTitle["Kdn"] = Formula("C9 H14 O8");
        brickFormulaByTitle["Kdo"] = Formula("C8 H12 O7");
        brickFormulaByTitle["NeuAc"] = Formula("C11 H17 N1 O8");
        brickFormulaByTitle["NeuGc"] = Formula("C11 H17 N1 O9");
        brickFormulaByTitle["Pent"] = Formula("C5 H8 O4");
        brickFormulaByTitle["Water"] = Formula("H2 O1");
        brickFormulaByTitle["Phos"] = Formula("H1 P1 O3");
        brickFormulaByTitle["Sulf"] = Formula("S1 O3");
        brickFormulaByTitle["Hep"] = Formula("C7 H12 O6");
        brickFormulaByTitle["Me"] = Formula("C1 H2");

        brickFormulaByTitle["C"] = Formula("C1");
        brickFormulaByTitle["H"] = Formula("H1");
        brickFormulaByTitle["O"] = Formula("O1");
        brickFormulaByTitle["N"] = Formula("N1");
        brickFormulaByTitle["S"] = Formula("S1");
        brickFormulaByTitle["P"] = Formula("P1");

        brickFormulaByTitle["13C"] = Formula("_13C1");
        brickFormulaByTitle["2H"] = Formula("_2H1");
        brickFormulaByTitle["18O"] = Formula("_18O1");
        brickFormulaByTitle["15N"] = Formula("_15N1");

        map<string, Site> siteMap;
        siteMap["N-term"] = Site::NTerminus;
        siteMap["C-term"] = Site::CTerminus;
        for(char aa : string("ABCDEFGHIJKLMNPQRSTUVWXYZ"))
            siteMap[string(1, aa)] = site(aa);

        map<string, Position> positionMap;
        positionMap["Anywhere"] = Position::Anywhere;
        positionMap["Any N-term"] = Position::AnyNTerminus;
        positionMap["Any C-term"] = Position::AnyCTerminus;
        positionMap["Protein N-term"] = Position::ProteinNTerminus;
        positionMap["Protein C-term"] = Position::ProteinCTerminus;

        map<string, Classification> classificationMap;
        classificationMap["Artefact"] = Classification::Artifact;
        classificationMap["Chemical derivative"] = Classification::ChemicalDerivative;
        classificationMap["Co-translational"] = Classification::CoTranslational;
        classificationMap["Isotopic label"] = Classification::IsotopicLabel;
        classificationMap["Multiple"] = Classification::Multiple;
        classificationMap["N-linked glycosylation"] = Classification::NLinkedGlycosylation;
        classificationMap["Non-standard residue"] = Classification::NonStandardResidue;
        classificationMap["O-linked glycosylation"] = Classification::OLinkedGlycosylation;
        classificationMap["Other glycosylation"] = Classification::OtherGlycosylation;
        classificationMap["Other"] = Classification::Other;
        classificationMap["Post-translational"] = Classification::PostTranslational;
        classificationMap["Pre-translational"] = Classification::PreTranslational;
        classificationMap["AA substitution"] = Classification::Substitution;
        classificationMap["Synth. pep. protect. gp."] = Classification::SynthPepProtectGP;

        vector<string> formulaTokens;

        for(CVID cvid : cvids())
        {
            const CVTermInfo& term = cvTermInfo(cvid);
            if (!bal::starts_with(term.id, "UNIMOD") || bal::ends_with(term.id, ":0"))
                continue;

            try
            {
                Modification mod;
                mod.cvid = term.cvid;
                mod.name = term.name;

                multimap<string, string>::const_iterator itr;

                itr = term.propertyValues.find("delta_composition");
                if (itr == term.propertyValues.end())
                    throw runtime_error("no delta_composition property for term \"" + term.id + "\"");

                formulaTokens.clear();
                bal::split(formulaTokens, itr->second, bal::is_space());

                // <brick>(<quantity>) if quantity>1 or just <brick> for quantity=1
                for(string& token : formulaTokens)
                {
                    Formula brickFormula;

                    size_t openParenthesis = token.find_first_of('(');
                    if (openParenthesis != string::npos)
                    {
                        brickFormula = getBrickFormula(token.substr(0, openParenthesis), brickFormulaByTitle);

                        size_t closeParenthesis = token.find_first_of(')', openParenthesis+1);
                        if (closeParenthesis == string::npos)
                            throw runtime_error("unmatched opening parenthesis in \"" + token + "\"");

                        int brickQuantity = lexical_cast<int>(token.substr(openParenthesis+1, closeParenthesis-openParenthesis-1));
                        brickFormula *= brickQuantity;
                    }
                    else
                        brickFormula = getBrickFormula(token, brickFormulaByTitle);

                    mod.deltaComposition += brickFormula;
                }

                itr = term.propertyValues.find("approved");
                if (itr == term.propertyValues.end())
                    throw runtime_error("no approved property for term \"" + term.id + "\"");
                mod.approved = itr->second == "1";

                multimap<string, string>::const_iterator end = term.propertyValues.end();

                // properties are ordered asciibetically:
                // spec_1_classification
                // spec_1_hidden
                // spec_1_position
                // spec_1_site
                itr = term.propertyValues.lower_bound("spec_");
                while (true)
                {
                    Modification::Specificity spec;

                    map<string, Classification>::const_iterator itr2 = classificationMap.find(itr->second);
                    if (itr2 == classificationMap.end())
                        throw runtime_error("unknown classification \"" + itr->second + "\" for term \"" + term.id + "\"");
                    spec.classification = itr2->second;

                    // skip redundant classification properties for the current site
                    do {++itr;} while (itr != end && bal::ends_with(itr->first, "classification"));
                    assert(itr != end && bal::starts_with(itr->first, "spec"));

                    spec.hidden = itr->second == "1";

                    // skip redundant hidden properties for the current site
                    do {++itr;} while (itr != end && bal::ends_with(itr->first, "hidden"));
                    assert(itr != end && bal::starts_with(itr->first, "spec"));

                    map<string, Position>::const_iterator itr3 = positionMap.find(itr->second);
                    if (itr3 == positionMap.end())
                        throw runtime_error("unknown position \"" + itr->second + "\" for term \"" + term.id + "\"");
                    spec.position = itr3->second;

                    // skip redundant position properties for the current site
                    do {++itr;} while (itr != end && bal::ends_with(itr->first, "position"));
                    assert(itr != end && bal::starts_with(itr->first, "spec"));

                    map<string, Site>::const_iterator itr4 = siteMap.find(itr->second);
                    if (itr4 == siteMap.end())
                        throw runtime_error("unknown site \"" + itr->second + "\" for term \"" + term.id + "\"");
                    spec.site = itr4->second;

                    mod.specificities.push_back(spec);

                    // add copies of the currently specificity for each site, e.g.
                    // spec_1_site = S
                    // spec_1_site = T
                    ++itr;
                    while (itr != end && bal::ends_with(itr->first, "site"))
                    {
                        itr4 = siteMap.find(itr->second);
                        if (itr4 == siteMap.end())
                            throw runtime_error("unknown site \"" + itr->second + "\" for term \"" + term.id + "\"");
                        spec.site = itr4->second;
                        mod.specificities.push_back(spec);
                        ++itr;
                    }

                    if (itr == end || !bal::starts_with(itr->first, "spec_"))
                        break;
                }

                modifications.push_back(mod);
                size_t modIndex = modifications.size() - 1;
                indexByCVID[mod.cvid] = modIndex;
                indexByTitle[mod.name] = modIndex;
                indexByMonoisotopicMass.insert(make_pair(mod.deltaMonoisotopicMass(), modIndex));
                indexByAverageMass.insert(make_pair(mod.deltaAverageMass(), modIndex));
            }
            catch (exception& e)
            {
                // TODO: log this error
                cerr << "[UnimodData::ctor] error parsing term \"" << term.id << "\": " << e.what() << "\n";
                //throw runtime_error("[UnimodData::ctor] error parsing mod \"" + title + "\"");
            }
        }
    }

    Formula getBrickFormula(const string& brick, const map<string, Formula>& brickFormulaByTitle) const
    {
        map<string, Formula>::const_iterator itr = brickFormulaByTitle.find(brick);
        if (itr != brickFormulaByTitle.end())
            return itr->second;

        try
        {
            // handle less common elements like Cl, Cu, Se, etc.
            return Formula(brick + "1");
        }
        catch (runtime_error&)
        {
            throw invalid_argument("[UnimodData::getBrickFormula] unknown element or brick \"" + brick + "\"");
        }
    }
};


PWIZ_API_DECL Site site(char symbol)
{
    // maps character to a corresponding Site bitmask
    const static size_t nil = Site(Site::not_mask).value();
    const static size_t symbolMap[] =
    {
        nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil, // 0-19
        nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil, // 20-39
        nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil,nil, // 40-59
        nil,nil,nil,nil,nil, // 60-64
        Site(Site::Alanine).value(), // A (65)
        Site(Site::Asparagine | Site::AsparticAcid).value(), // B
        Site(Site::Cysteine).value(),
        Site(Site::AsparticAcid).value(),
        Site(Site::GlutamicAcid).value(),
        Site(Site::Phenylalanine).value(),
        Site(Site::Glycine).value(),
        Site(Site::Histidine).value(),
        Site(Site::Isoleucine).value(),
        Site(Site::Leucine | Site::Isoleucine).value(), // J
        Site(Site::Lysine).value(),
        Site(Site::Leucine).value(),
        Site(Site::Methionine).value(),
        Site(Site::Asparagine).value(),
        nil, // O
        Site(Site::Proline).value(),
        Site(Site::Glutamine).value(),
        Site(Site::Arginine).value(),
        Site(Site::Serine).value(),
        Site(Site::Threonine).value(),
        Site(Site::Selenocysteine).value(), // U
        Site(Site::Valine).value(),
        Site(Site::Tryptophan).value(),
        Site(Site::Any).value(), // X
        Site(Site::Tyrosine).value(),
        Site(Site::Glutamine | Site::GlutamicAcid).value(), // Z
        nil,nil,nil,nil,nil,nil,nil,nil, // [ \ ] ^ _ ` a b
        Site(Site::CTerminus).value(), // c
        nil,nil,nil,nil,nil,nil,nil,nil,nil,nil, // d e f g h i j k l m
        Site(Site::NTerminus).value(), // n
        nil,nil,nil,nil,nil,nil,nil,nil,nil, // o p q r s t u v w
        Site(Site::Any).value() // x
    };

    if (symbol > 'x' || (symbol != 'x' && symbolMap[(size_t) symbol] == nil))
        throw invalid_argument("[unimod::site] invalid symbol \"" + string(1, symbol) + "\"");

    return Site::get_by_value(symbolMap[(size_t) symbol]).get();
}


PWIZ_API_DECL Position position(CVID cvid)
{
    switch (cvid)
    {
        case CVID_Unknown: return Position::Anywhere;
        case MS_modification_specificity_peptide_N_term: return Position::AnyNTerminus;
        case MS_modification_specificity_peptide_C_term: return Position::AnyCTerminus;
        case MS_modification_specificity_protein_N_term: return Position::ProteinNTerminus;
        case MS_modification_specificity_protein_C_term: return Position::ProteinCTerminus;
        default: throw invalid_argument("[unimod::position] invalid cvid \"" + cvTermInfo(cvid).shortName() + "\" (" + lexical_cast<string>(cvid) + ")");
    }
}


PWIZ_API_DECL double Modification::deltaMonoisotopicMass() const {return deltaComposition.monoisotopicMass();}
PWIZ_API_DECL double Modification::deltaAverageMass() const {return deltaComposition.molecularWeight();}


PWIZ_API_DECL const std::vector<Modification>& modifications() {return UnimodData::instance->modifications;}

PWIZ_API_DECL
vector<Modification> modifications(double mass,
                                   double tolerance,
                                   tribool monoisotopic /*= true*/,
                                   tribool approved /*= true*/,
                                   Site site /*= Site::Any*/,
                                   Position position /*= Position::Anywhere*/,
                                   Classification classification /*= Classification::Any*/,
                                   tribool hidden /*= tribool::indeterminate*/)
{
    UnimodData::lease unimodData;
    vector<Modification> result;
    multimap<double, size_t>::const_iterator itr, end;

    // assume monoisotopic
    if (monoisotopic || indeterminate(monoisotopic))
    {
        itr = unimodData->indexByMonoisotopicMass.lower_bound(mass - tolerance);
        end = unimodData->indexByMonoisotopicMass.upper_bound(mass + tolerance);
    }
    else
    {
        itr = unimodData->indexByAverageMass.lower_bound(mass - tolerance);
        end = unimodData->indexByAverageMass.upper_bound(mass + tolerance);
    }

    for (; itr != end; ++itr)
    {
        const Modification& mod = unimodData->modifications[itr->second];
        if (!indeterminate(approved) && approved != mod.approved)
            continue;

        for(const Modification::Specificity& specificity : mod.specificities)
        {
            if ((site == Site::Any || site[specificity.site]) &&
                (position == Position::Anywhere || position == specificity.position) &&
                (classification == Classification::Any || classification[specificity.classification]) &&
                (indeterminate(hidden) || hidden == specificity.hidden))
            {
                result.push_back(mod);
                break;
            }
        }
    }

    // for indeterminate mass type, append the results of the equivalent average mass call
    if (indeterminate(monoisotopic))
    {
        vector<Modification> avgResults = modifications(mass, tolerance, false, approved,
                                                       site, position, classification, hidden);
        set<CVID> existingResults;
        for(const Modification& mod : result)
            existingResults.insert(mod.cvid);

        for(const Modification& mod : avgResults)
            if (!existingResults.count(mod.cvid))
                result.push_back(mod);
    }

    return result;
}

PWIZ_API_DECL const Modification& modification(CVID cvid)
{
    UnimodData::lease unimodData;
    map<CVID, size_t>::const_iterator itr = unimodData->indexByCVID.find(cvid);
    if (itr == unimodData->indexByCVID.end())
        throw runtime_error("[unimod::modification] invalid cvid \"" + cvTermInfo(cvid).shortName() + "\"");

    return unimodData->modifications[itr->second];
}

PWIZ_API_DECL const Modification& modification(const std::string& title)
{
    UnimodData::lease unimodData;
    map<string, size_t>::const_iterator itr = unimodData->indexByTitle.find(title);
    if (itr == unimodData->indexByTitle.end())
        throw runtime_error("[unimod::modification] invalid title \"" + title + "\"");

    return unimodData->modifications[itr->second];
}


} // namespace unimod
} // namespace data
} // namespace pwiz
