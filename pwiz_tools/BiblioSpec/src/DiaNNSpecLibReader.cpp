//
// Original author: Matt Chambers <matt.chambers42@gmail.com>
//
// Copyright 2020 Matt Chambers
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

#include "DiaNNSpecLibReader.h"
#include "pwiz/data/common/Unimod.hpp"

namespace BiblioSpec
{
const int LATEST_SUPPORTED_VERSION = -2;

// code adapted from CC4-by-licensed diann.cpp by Vadim Demichev (https://github.com/vdemichev/DiaNN)
namespace {
        
template <class F, class T> void read_vector(F &in, std::vector<T> &v) {
    int size = 0; in.read((char*)&size, sizeof(int));
    if (size) {
        v.resize(size);
        in.read((char*)&(v[0]), size * sizeof(T));
    }
}

template<class F> void read_string(F &in, std::string &s) {
    int size = 0; in.read((char*)&size, sizeof(int));
    if (size) {
        s.resize(size);
        in.read((char*)&(s[0]), size);
    }
}

template <class F, class T> void read_array(F &in, std::vector<T> &a, int v = 0) {
    int size = 0; in.read((char*)&size, sizeof(int)); 
    if (size) {
        a.resize(size);
        for (int i = 0; i < size; i++) a[i].read(in, v);
    }
}

template<class F> void read_strings(F &in, std::vector<std::string> &strs) {
    int size = 0; in.read((char*)&size, sizeof(int));
    if (size) {
        strs.resize(size);
        for (int i = 0; i < size; i++) read_string(in, strs[i]);
    }
}


std::vector<std::pair<std::string, std::string> > UniMod;
std::vector<std::pair<std::string, int> > UniModIndex;
std::vector<int> UniModIndices;

struct MOD {
    std::string name;
    std::string aas;
    float mass = 0.0;
    int label = 0;

    MOD() {};
    MOD(const std::string &_name, float _mass, int _label = 0) { name = _name, mass = _mass; label = _label; }
    void init(const std::string &_name, const std::string &_aas, float _mass, int _label = 0) { name = _name, aas = _aas, mass = _mass; label = _label; }
    friend bool operator < (const MOD &left, const MOD &right) { return left.name < right.name || (left.name == right.name && left.mass < right.mass); }
};
std::vector<MOD> FixedMods, VarMods;

std::vector<MOD> Modifications = {
    MOD("UniMod:4", (float)57.021464),
    MOD("Carbamidomethyl (C)", (float)57.021464),
    MOD("Carbamidomethyl", (float)57.021464),
    MOD("CAM", (float)57.021464),
    MOD("+57", (float)57.021464),
    MOD("+57.0", (float)57.021464),
    MOD("UniMod:26", (float)39.994915),
    MOD("PCm", (float)39.994915),
    MOD("UniMod:5", (float)43.005814),
    MOD("Carbamylation (KR)", (float)43.005814),
    MOD("+43", (float)43.005814),
    MOD("+43.0", (float)43.005814),
    MOD("CRM", (float)43.005814),
    MOD("UniMod:7", (float)0.984016),
    MOD("Deamidation (NQ)", (float)0.984016),
    MOD("Deamidation", (float)0.984016),
    MOD("Dea", (float)0.984016),
    MOD("+1", (float)0.984016),
    MOD("+1.0", (float)0.984016),
    MOD("UniMod:35", (float)15.994915),
    MOD("Oxidation (M)", (float)15.994915),
    MOD("Oxidation", (float)15.994915),
    MOD("Oxi", (float)15.994915),
    MOD("+16", (float)15.994915),
    MOD("+16.0", (float)15.994915),
    MOD("Oxi", (float)15.994915),
    MOD("UniMod:1", (float)42.010565),
    MOD("Acetyl (Protein N-term)", (float)42.010565),
    MOD("+42", (float)42.010565),
    MOD("+42.0", (float)42.010565),
    MOD("UniMod:255", (float)28.0313),
    MOD("AAR", (float)28.0313),
    MOD("UniMod:254", (float)26.01565),
    MOD("AAS", (float)26.01565),
    MOD("UniMod:122", (float)27.994915),
    MOD("Frm", (float)27.994915),
    MOD("UniMod:1301", (float)128.094963),
    MOD("+1K", (float)128.094963),
    MOD("UniMod:1288", (float)156.101111),
    MOD("+1R", (float)156.101111),
    MOD("UniMod:27", (float)-18.010565),
    MOD("PGE", (float)-18.010565),
    MOD("UniMod:28", (float)-17.026549),
    MOD("PGQ", (float)-17.026549),
    MOD("UniMod:526", (float)-48.003371),
    MOD("DTM", (float)-48.003371),
    MOD("UniMod:325", (float)31.989829),
    MOD("2Ox", (float)31.989829),
    MOD("UniMod:342", (float)15.010899),
    MOD("Amn", (float)15.010899),
    MOD("UniMod:1290", (float)114.042927),
    MOD("2CM", (float)114.042927),
    MOD("UniMod:359", (float)13.979265),
    MOD("PGP", (float)13.979265),
    MOD("UniMod:30", (float)21.981943),
    MOD("NaX", (float)21.981943),
    MOD("UniMod:401", (float)-2.015650),
    MOD("-2H", (float)-2.015650),
    MOD("UniMod:528", (float)14.999666),
    MOD("MDe", (float)14.999666),
    MOD("UniMod:385", (float)-17.026549),
    MOD("dAm", (float)-17.026549),
    MOD("UniMod:23", (float)-18.010565),
    MOD("Dhy", (float)-18.010565),
    MOD("UniMod:129", (float)125.896648),
    MOD("Iod", (float)125.896648),
    MOD("Phosphorylation (ST)", (float)79.966331),
    MOD("UniMod:21", (float)79.966331),
    MOD("+80", (float)79.966331),
    MOD("+80.0", (float)79.966331),
    MOD("UniMod:259", (float)8.014199, 1),
    MOD("Lys8", (float)8.014199, 1),
    MOD("UniMod:267", (float)10.008269, 1),
    MOD("Arg10", (float)10.008269, 1),
    MOD("UniMod:268", (float)6.013809, 1),
    MOD("UniMod:269", (float)10.027228, 1)
};

inline size_t closing_bracket(const std::string &name, char symbol, size_t pos) {
    size_t end;
    int par;
    char close = (symbol == '(' ? ')' : ']');
    for (end = pos + 1, par = 1; end < name.size(); end++) {
        char s = name[end];
        if (s == close) {
            par--;
            if (!par) break;
        }
        else if (s == symbol) par++;
    }
    return end;
}

inline std::string get_aas(const std::string &name, vector<SeqMod>& mods)
{
    size_t i, j, end;
    std::string result, mod;
    int modPrefixLength = 8; // length of "(UniMod:"

    for (i = j = 0; i < name.size(); i++) {
        char symbol = name[i];
        if (symbol < 'A' || symbol > 'Z') {
            if (symbol != '(' && symbol != '[') continue;
            end = closing_bracket(name, symbol, i);

            if (name.find("(UniMod:", i, modPrefixLength) != i)
                throw std::runtime_error("unable to handle mod in library entry: " + name);

            i += modPrefixLength;
            mod = name.substr(i, end-i);
            CVID unimodCvid = (CVID) (UNIMOD_unimod_root_node + lexical_cast<int>(mod));
            mods.emplace_back(SeqMod((int) j, pwiz::data::unimod::modification(unimodCvid).deltaMonoisotopicMass()));
            continue;
        }
        ++j;
        result.push_back(symbol);
    }
    return result;
}

inline void to_eg(std::string &eg, const std::string &name) {
    size_t i, j;

    eg.clear();
    for (i = 0; i < name.size(); i++) {
        char symbol = name[i];
        if (symbol < 'A' || symbol > 'Z') {
            if (symbol != '(' && symbol != '[') continue;
            i++;

            size_t end = closing_bracket(name, symbol, i);
            if (end == std::string::npos) throw std::runtime_error(std::string("incorrect peptide name format: ") + name);

            std::string mod = name.substr(i, end - i);
            for (j = 0; j < Modifications.size(); j++) if (Modifications[j].name == mod) {
                if (!Modifications[j].label) eg += std::string("(") + UniMod[j].second + std::string(")");
                break;
            }
            if (j == Modifications.size()) eg += std::string("(") + mod + std::string(")"); // no warning here
            i = end;
            continue;
        }
        else eg.push_back(symbol);
    }
}

inline std::string to_eg(const std::string &name) {
    std::string eg;
    to_eg(eg, name);
    return eg;
}

inline std::string to_charged_eg(const std::string &name, int charge) { return to_eg(name) + std::to_string(charge); }

enum {
loss_none, loss_H2O, loss_NH3, loss_CO,
loss_N, loss_other
};

const int fFromFasta = 1 << 0;
const int fPredictedSpectrum = 1 << 1;
const int fPredictedRT = 1 << 2;

class Product {
public:
float mz = 0.0;
float height = 0.0;
char charge = 0, type = 0, index = 0, loss = 0;

Product() {}
Product(float _mz, float _height, int _charge) {
    mz = _mz;
    height = _height;
    charge = _charge;
}
Product(float _mz, float _height, int _charge, int _type, int _index, int _loss) {
    mz = _mz;
    height = _height;
    charge = _charge;
    type = _type;
    index = _index;
    loss = _loss;
}
void init(float _mz, float _height, int _charge) {
    mz = _mz;
    height = _height;
    charge = _charge;
}
friend inline bool operator < (const Product &left, const Product &right) { return left.mz < right.mz; }

inline int ion_code() { return (((((int)type) * 20 + (int)charge)) * (loss_other + 1) + (int)loss) * 100 + (int)index + 1; }
};

class Peptide {
    public:
    int index = 0, charge = 0, length = 0, no_cal = 0;
    float mz = 0.0, iRT = 0.0, sRT = 0.0, lib_qvalue = 0.0;
    float iIM = 0.0, sIM = 0.0;
    std::vector<Product> fragments;

    void init(float _mz, float _iRT, int _charge, int _index) {
        mz = _mz;
        iRT = _iRT;
        sRT = 0.0;
        charge = _charge;
        index = _index;
    }

    inline void free() {
        std::vector<Product>().swap(fragments);
    }

    template <class F> void read(F &in, int v) {
        in.read((char*)&index, sizeof(int));
        in.read((char*)&charge, sizeof(int));
        in.read((char*)&length, sizeof(int));

        in.read((char*)&mz, sizeof(float));
        in.read((char*)&iRT, sizeof(float));
        in.read((char*)&sRT, sizeof(float));

        if (v <= -2) {
            in.read((char*)&lib_qvalue, sizeof(float));
            in.read((char*)&iIM, sizeof(float));
            in.read((char*)&sIM, sizeof(float));
        }

        read_vector(in, fragments);
    }
};


class Isoform {
public:
    std::string id;
    mutable std::string name, gene, description;
    mutable std::set<int> precursors; // precursor indices in the library
    int name_index = 0, gene_index = 0;
    bool swissprot = true;

    Isoform() {}
    Isoform(const std::string &_id) { id = _id; }
    Isoform(const std::string &_id, const std::string &_name, const std::string &_gene, const std::string &_description, bool _swissprot) {
        id = _id;
        name = _name;
        gene = _gene;
        description = _description;
        swissprot = _swissprot;
    }

    friend inline bool operator < (const Isoform &left, const Isoform &right) { return left.id < right.id; }

    template <class F> void read(F &in, int v = 0) {
        int sp = 0, size = 0;
        in.read((char*)&sp, sizeof(int));
        in.read((char*)&size, sizeof(int));
        swissprot = sp;

        read_string(in, id);
        read_string(in, name);
        read_string(in, gene);
        in.read((char*)&name_index, sizeof(int));
        in.read((char*)&gene_index, sizeof(int));
        precursors.clear();
        for (int i = 0; i < size; i++) {
            int pr = -1;
            in.read((char*)&pr, sizeof(int));
            if (pr >= 0) precursors.insert(pr);
        }
    }
};

class PG {
public:
    std::string ids;
    mutable std::string names, genes;
    mutable std::vector<int> precursors; // precursor indices in the library
    mutable std::set<int> proteins;
    std::vector<int> name_indices, gene_indices;

    PG() {}

    PG(std::string _ids) {
        ids = _ids;
    }

    friend inline bool operator < (const PG &left, const PG &right) { return left.ids < right.ids; }

    template <class F> void read(F &in, int v = 0) {
        int size_p = 0;
        in.read((char*)&size_p, sizeof(int));

        read_string(in, ids);
        read_string(in, names);
        read_string(in, genes);
        read_vector(in, name_indices);
        read_vector(in, gene_indices);
        read_vector(in, precursors);
        for (int i = 0; i < size_p; i++) {
            int p = -1;
            in.read((char*)&p, sizeof(int));
            if (p >= 0) proteins.insert(p);
        }
    }
};


class Library {
    public:
    std::string name, fasta_names;
    std::vector<Isoform> proteins;
    std::vector<PG> protein_ids;
    std::vector<PG> protein_groups;
    std::vector<PG> gene_groups;
    std::vector<int> gg_index;
    std::vector<std::string> precursors; // precursor IDs in canonical format; library-based entries only
    std::vector<std::string> names;
    std::vector<std::string> genes;

    int skipped = 0;
    double iRT_min = 0.0, iRT_max = 0.0;
    bool gen_decoys = true, gen_charges = true, infer_proteotypicity = true, from_speclib = false;

    std::map<std::string, int> eg;
    std::vector<int> elution_groups; // same length as entries, indices of the elution groups
    std::vector<int> co_elution;
    std::vector<std::pair<int, int> > co_elution_index;

    class Entry {
        public:
        Library * lib;
        Peptide target, decoy;
        int entry_flags = 0, proteotypic = 0;
        std::string name; // precursor id
        std::set<PG>::iterator prot;
        int pid_index = 0, pg_index = 0, eg_id, best_run = -1, peak = 0, apex = 0, window = 0;
        float qvalue = 0.0, pg_qvalue = 0.0, best_fr_mz = 0.0;

        friend bool operator < (const Entry &left, const Entry &right) { return left.name < right.name; }

        inline void free() {
            target.free();
            decoy.free();
        }

        template <class F> void read(F &in, int v) {
            target.read(in, v);
            int dc = 0; in.read((char*)&dc, sizeof(int));
            if (dc) decoy.read(in, v);

            int ff = 0, prt = 0;
            in.read((char*)&ff, sizeof(int));
            in.read((char*)&prt, sizeof(int));
            entry_flags = ff, proteotypic = prt;
            in.read((char*)&pid_index, sizeof(int));
            read_string(in, name);
        }
    };

    std::vector<Entry> entries;

    template<class F> void read(F &in) {
        int gd = 0, gc = 0, ip = 0, version = 0;

        in.read((char*)&version, sizeof(int));
        if (version >= 0) { gd = version; version = 0; }
        else in.read((char*)&gd, sizeof(int));

        if (version < LATEST_SUPPORTED_VERSION)
            Verbosity::error("speclib file has version %d, but BiblioSpec only supports up to version %d", -1*version, -1*LATEST_SUPPORTED_VERSION);

        in.read((char*)&gc, sizeof(int));
        in.read((char*)&ip, sizeof(int));
        gen_decoys = gd, gen_charges = gc, infer_proteotypicity = ip;

        read_string(in, name);
        read_string(in, fasta_names);
        read_array(in, proteins);
        read_array(in, protein_ids);
        read_strings(in, precursors);
        read_strings(in, names);
        read_strings(in, genes);
        in.read((char*)&iRT_min, sizeof(double));
        in.read((char*)&iRT_max, sizeof(double));
        read_array(in, entries, version);
        for (auto &e : entries) e.lib = this;
        if (version <= -1 && in.peek() != std::char_traits<char>::eof()) read_vector(in, elution_groups);
    }

    void assemble_elution_groups() {
        Verbosity::debug("Assembling elution groups");
        eg.clear(), elution_groups.clear(), elution_groups.reserve(entries.size());
        for (auto &e : entries) {
            auto name = to_eg(e.name);
            auto egp = eg.insert(std::pair<std::string, int>(name, (int) eg.size()));
            elution_groups.push_back(e.eg_id = egp.first->second);
        }
        eg.clear();
    }

    void elution_group_index() {
        size_t i, egt = 0, peg;
        for (i = 0; i < elution_groups.size(); i++) if (elution_groups[i] > egt) egt = elution_groups[i]; egt++;
        co_elution_index.resize(egt, std::pair<int, int>(0, 1));
        std::set<std::pair<int, int> > ce;
        for (i = 0; i < elution_groups.size(); i++) ce.insert(std::pair<int, int>(elution_groups[i], (int) i));
        co_elution.resize(ce.size()); i = peg = 0;
        if (co_elution_index.size()) co_elution_index[0].second = 0;
        for (auto it = ce.begin(); it != ce.end(); it++, i++) {
            co_elution[i] = it->second;
            if (it->first != (int) peg) co_elution_index[it->first].first = (int) i;
            else co_elution_index[it->first].second++;
            peg = it->first;
        }
        ce.clear();
    }

    bool load(const char * file_name) {

        name = std::string(file_name);
        Verbosity::debug("Loading spectral library %s", name.c_str());

        std::ifstream speclib(file_name, std::ifstream::binary);
        if (speclib.fail()) {
            Verbosity::error("cannot read the file");
            return false;
        }
        read(speclib);
        speclib.close();

        from_speclib = true;
        /*if (PGLevel != PGLevelSet) {
            if (PGLevelSet == 2 && genes.size() >= 2) PGLevel = 2;
            else if (PGLevelSet == 1 && names.size() >= 2) PGLevel = 1;
        }*/
        if (fasta_names.size()) Verbosity::debug("Library annotated with sequence database(s): %s", fasta_names.c_str());
        //if (!fasta_files.size() && (genes.size() >= 2 || names.size() >= 2)) library_protein_stats();
        if (!elution_groups.size()) assemble_elution_groups();

        int gc = gen_charges;
        gen_charges = false;
        for (auto &e : entries) {
            e.entry_flags &= ~fFromFasta;
            if (gc) for (auto &f : e.target.fragments) if (!(int)f.charge) gen_charges = true, gc = 0;
        }

        int ps = (int) proteins.size(), pis = (int) protein_ids.size();
        if (ps) if (!proteins[0].id.size()) ps--;
        if (pis) if (!protein_ids[0].ids.size()) pis--;

        elution_group_index();

        Verbosity::status("Spectral library loaded: %d protein isoforms, %d protein groups and %d precursors in %d elution groups.", ps, pis, entries.size(), co_elution_index.size());

        return true;
    }
};

} // end code adapted from diann.cpp

class DiaNNSpecLibReader::Impl
{
    public:
    Impl(const char* specLibFile) : specLibFile_(specLibFile) {}

    Library specLib;
    const char* specLibFile_;
};

DiaNNSpecLibReader::DiaNNSpecLibReader(BlibBuilder& maker, const char* specLibFile, const ProgressIndicator* parent_progress)
    : BuildParser(maker, specLibFile, parent_progress),
    impl_(new Impl(specLibFile))
{
    setSpecFileName(specLibFile, false);
    lookUpBy_ = INDEX_ID;
    // point to self as spec reader
    delete specReader_;
    specReader_ = this;
}

DiaNNSpecLibReader::~DiaNNSpecLibReader()
{
    specReader_ = NULL; // so the parent class doesn't try to delete itself
}

bool DiaNNSpecLibReader::parseFile()
{
    {
        ifstream specLibStream(impl_->specLibFile_, ios::binary);
        impl_->specLib.read(specLibStream);
    }

    for (const auto& entry : impl_->specLib.entries)
    {
        PSM* psm = new PSM;
        psm->charge = entry.target.charge;
        psm->unmodSeq = get_aas(entry.name, psm->mods);
        psm->specIndex = entry.target.index;
        psms_.emplace_back(psm);
    }

    buildTables(UNKNOWN_SCORE_TYPE, impl_->specLibFile_);

    return true;
}

// SpecFileReader methods
/**
    * Implemented to satisfy SpecFileReader interface.  Since spec and
    * results files are the same, no need to open a new one.
    */
void DiaNNSpecLibReader::openFile(const char* filename, bool mzSort) {}

void DiaNNSpecLibReader::setIdType(SPEC_ID_TYPE type) {}

/**
    * Return a spectrum via the returnData argument.  If not found in the
    * spectra map, return false and leave returnData unchanged.
    */
bool DiaNNSpecLibReader::getSpectrum(int identifier, SpecData& returnData, SPEC_ID_TYPE findBy, bool getPeaks)
{
    const auto& entry = impl_->specLib.entries.at(identifier);

    returnData.charge = entry.target.charge;
    returnData.id = entry.target.index;
    returnData.numPeaks = entry.target.fragments.size();
    returnData.mz = entry.target.mz;
    returnData.retentionTime = entry.target.iRT;

    if (!getPeaks)
        return true;

    returnData.mzs = new double[returnData.numPeaks];
    returnData.intensities = new float[returnData.numPeaks];
    for (size_t i=0; i < returnData.numPeaks; ++i)
    {
        const auto& product = entry.target.fragments[i];
        returnData.mzs[i] = product.mz;
        returnData.intensities[i] = product.height;
    }
    return true;
}

/**
    * Only specific spectra can be accessed from the DiaNNSpecLibReader.
    */
bool DiaNNSpecLibReader::getSpectrum(string identifier, SpecData& returnData, bool getPeaks)
{
    Verbosity::warn("DiaNNSpecLibReader cannot fetch spectra by string identifier, "
        "only by spectrum index.");
    return false;
}

/**
    * Only specific spectra can be accessed from the DiaNNSpecLibReader.
    */
bool DiaNNSpecLibReader::getNextSpectrum(SpecData& returnData, bool getPeaks)
{
    Verbosity::warn("DiaNNSpecLibReader does not support sequential file reading.");
    return false;
}

} // namespace BiblioSpec
