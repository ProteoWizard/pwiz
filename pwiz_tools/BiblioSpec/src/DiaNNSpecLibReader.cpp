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
#include "libraries/csv.h"
#ifdef USE_PARQUET_READER
#include "arrow/io/api.h"
#include "arrow/api.h"
#include "parquet/arrow/reader.h"
#endif
#include <boost/type_index.hpp>
#include <pwiz/data/proteome/AminoAcid.hpp>
#include <boost/range/algorithm/find.hpp>
//#include "pwiz/data/msdata/MSDataFile.hpp"

namespace BiblioSpec
{
const int LATEST_SUPPORTED_VERSION = -3;

// code adapted from CC4-by-licensed diann.cpp by Vadim Demichev (https://github.com/vdemichev/DiaNN)
namespace {

// PSM with retention time info
struct RtPSM
{
    float rt, rtStart, rtEnd;
    int fileId;
    float score;
    float ionMobility;
};

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
    Verbosity::debug("array of %d %s", size, boost::typeindex::type_index(typeid(T)).pretty_name().c_str());
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
            int position = max(1, static_cast<int>(j));

            if (name.find("(UniMod:", i, modPrefixLength) != i)
            {
                string potentialMass = name.substr(i + 1, end - i - 1);
                if (potentialMass.find_first_not_of("01234567890.") != string::npos)
                    throw std::runtime_error("unable to handle mod in library entry as either a UniMod id or delta mass: " + potentialMass + " in " + name);
                double modMass = lexical_cast<double>(potentialMass);

                if (!mods.empty() && mods.back().position == position)
                    mods.back().deltaMass += modMass;
                else
                    mods.emplace_back(position, modMass);
                continue;
            }

            i += modPrefixLength;
            mod = name.substr(i, end - i);
            CVID unimodCvid = (CVID) (UNIMOD_unimod_root_node + lexical_cast<int>(mod));
            if (!mods.empty() && mods.back().position == position)
                mods.back().deltaMass += unimod::modification(unimodCvid).deltaMonoisotopicMass();
            else 
                mods.emplace_back(position, unimod::modification(unimodCvid).deltaMonoisotopicMass());
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
        float qvalue = 0.0, pg_qvalue = 0.0, best_fr_mz = 0.0, ptm_qvalue, site_conf;

        // temporary variables used while processing rows from a single run in the report file
        float bestQValue;
        RtPSM* bestPSM;

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
            if (v <= -3) {
                in.read((char*)&pg_qvalue, sizeof(float));
                in.read((char*)&ptm_qvalue, sizeof(float));
                in.read((char*)&site_conf, sizeof(float));
            }
        }
    };

    std::vector<Entry> entries;
    std::map<string, std::reference_wrapper<Entry>> entryByModPeptideAndCharge;

    template<class F> void read(F &in) {
        int gd = 0, gc = 0, ip = 0, version = 0;

        in.read((char*)&version, sizeof(int));
        if (version >= 0) { gd = version; version = 0; }
        else in.read((char*)&gd, sizeof(int));

        if (version < LATEST_SUPPORTED_VERSION)
            Verbosity::error("speclib file has version %d, but BiblioSpec only supports up to version %d", -1*version, -1*LATEST_SUPPORTED_VERSION);
        else
            Verbosity::debug("speclib file has version %d",  -1*version);

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
        auto precursorItr = precursors.begin();
        for (auto &e : entries)
        {
            e.lib = this;
            if (e.name != *precursorItr)
            {
                Verbosity::error("Precursor mismatch between %s and %s in speclib file", e.name.c_str(), precursorItr->c_str());
            }
            ++precursorItr;
            entryByModPeptideAndCharge.emplace(e.name, std::ref(e));
        }
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
        int egt = 0;
        size_t i, peg;
        for (i = 0; i < elution_groups.size(); i++) { if (elution_groups[i] > egt) { egt = elution_groups[i]; } egt++; }
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
    Impl(const char* specLibFile, BlibBuilder& blibMaker) : specLibFile_(specLibFile), blibMaker_(blibMaker)
    {
        if (blibMaker.isScoreLookupMode())
            return;

        // the DIANN reader directly creates a non-redundant library
        string createRetentionTimes =
            "CREATE TABLE RetentionTimes (RefSpectraID INTEGER, "
            "RedundantRefSpectraID INTEGER, "
            "SpectrumSourceID INTEGER, "
            "ionMobility REAL, "
            "collisionalCrossSectionSqA REAL, "
            "ionMobilityHighEnergyOffset REAL, "
            "ionMobilityType TINYINT, "
            "retentionTime REAL, "
            "startTime REAL, "
            "endTime REAL, "
            "score REAL, "
            "bestSpectrum INTEGER, " // boolean
            "FOREIGN KEY(RefSpectraID) REFERENCES RefSpectra(id) )";
        blibMaker_.sql_stmt(createRetentionTimes.c_str());

        if (sqlite3_prepare(blibMaker_.getDb(),
            "INSERT INTO RetentionTimes (RefSpectraID, RedundantRefSpectraID, "
            "SpectrumSourceID, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, "
            "retentionTime, startTime, endTime, score, bestSpectrum) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            -1, &insertRetentionTimesStmt_, NULL) != SQLITE_OK)
        {
            throw BlibException(false, "Error preparing insert statement: %s", sqlite3_errmsg(blibMaker_.getDb()));
        }
    }

    ~Impl()
    {
        if (blibMaker_.isScoreLookupMode())
            return;

        sqlite3_finalize(insertRetentionTimesStmt_);
    }

    Library specLib;
    const char* specLibFile_;
    sqlite3_stmt* insertRetentionTimesStmt_;
    BlibBuilder& blibMaker_;
    IONMOBILITY_TYPE ionMobilityType_ = IONMOBILITY_NONE;

    template<int column_count>
    class Reader
    {
        typedef io::CSVReader<column_count, io::trim_chars<' ', ' '>, io::no_quote_escape<'\t'>> CsvReader;
        public:
        class ParquetReader;

        private:
        unique_ptr<CsvReader> csvReader_;
        unique_ptr<ParquetReader> parquetReader_;
        string filepath_;

        public:

        void open_file(const string& filepath)
        {
            filepath_ = filepath;

            if (ParquetReader::is_parquet(filepath))
                parquetReader_.reset(new ParquetReader(filepath_));
            else
                csvReader_.reset(new CsvReader(filepath_));
        }

        void close()
        {
            if (parquetReader_)
                parquetReader_->close();
        }

        // a wrapper for reading Parquet files that provides the same interface as CSVReader
        class ParquetReader
        {
            const string& filepath_;

#ifdef USE_PARQUET_READER
            unique_ptr<parquet::arrow::FileReader> reader_;
            vector<string> columnNames_; // and order
            vector<int> columnIndices_;
            int64_t numRowGroups_;
            int64_t rowIndex_ = 0;
            int64_t rowGroup_ = -1;

            // an enum of supported types for columns
            enum ColumnType { int64, fp, str };

            std::shared_ptr<arrow::Table> table_;
            vector<std::shared_ptr<arrow::Array>> columnArrays_;
            vector<ColumnType> columnTypes_;

            inline std::shared_ptr<arrow::Array> get_arrow_column_chunk(std::shared_ptr<arrow::Table> table, string column_name)
            {
                return table->GetColumnByName(column_name)->chunk(0);
            }
#endif

        public:

            ParquetReader(const string& filepath) : filepath_(filepath)
            {
#ifndef USE_PARQUET_READER
#ifdef WIN32
                Verbosity::error("parquet files are not supported in current build (runtime debugging on)");
#else
                Verbosity::error("parquet files are not supported in current build (Linux)");
#endif
#else
                auto input = arrow::io::ReadableFile::Open(filepath);
                if (!input.ok())
                    Verbosity::error("cannot open %s: %s", filepath.c_str(), input.status().message().c_str());

                auto got_reader = parquet::arrow::OpenFile(input.ValueOrDie(), arrow::default_memory_pool(), &reader_);
                if (!got_reader.ok())
                    Verbosity::error("cannot initialise arrow reader for %s: %s", filepath.c_str(), got_reader.message().c_str());

                numRowGroups_ = reader_->num_row_groups();
                if (!numRowGroups_)
                    Verbosity::error("parquet library contains zero row groups for %s", filepath.c_str());
#endif
            }

            static bool is_parquet(const string& filepath)
            {
#ifndef USE_PARQUET_READER
                return bal::iends_with(filepath, ".parquet");
#else
                auto input = arrow::io::ReadableFile::Open(filepath);
                if (!input.ok())
                    return false;

                unique_ptr<parquet::arrow::FileReader> reader;
                auto got_reader = parquet::arrow::OpenFile(input.ValueOrDie(), arrow::default_memory_pool(), &reader);
                return got_reader.ok();
#endif
            }

#ifndef USE_PARQUET_READER
            void close() {}
            template<class ...ColNames> void read_header(io::ignore_column ignore_policy, ColNames...cols) {}
            bool has_column(const std::string& name) const { return false; }
            template<class ...ColType> bool read_row(ColType& ...cols) { return false; }
            void seek_begin() {}
            uint64_t num_rows() const { return 0; }
#else
            void close()
            {
                table_.reset();
                columnArrays_.clear();
                reader_.release();
            }

            void read_header_helper(int fieldIndex, const char* columnName)
            {
                columnIndices_.emplace_back(fieldIndex);
                columnNames_.emplace_back(columnName);
            }

            void read_header_helper(const std::shared_ptr<arrow::Schema>& schema, const char* columnName)
            {
                int fieldIndex = schema->GetFieldIndex(columnName);
                if (fieldIndex < 0)
                    Verbosity::error("file %s does not have a column called %s", filepath_.c_str(), columnName);

                read_header_helper(fieldIndex, columnName);
            }

            template<class ...ColNames>
            void read_header_helper(const std::shared_ptr<arrow::Schema>& schema, const char* columnName, ColNames...cols)
            {
                read_header_helper(schema, columnName);
                read_header_helper(schema, cols...);
            }

            template<class ...ColNames>
            void read_header(io::ignore_column ignore_policy, ColNames...cols)
            {
                std::shared_ptr<arrow::Schema> schema;
                auto got_schema = reader_->GetSchema(&schema);
                if (!got_schema.ok())
                    Verbosity::error("cannot read schema from %s: %s", filepath_.c_str(), got_schema.message().c_str());

                read_header_helper(schema, cols...);

                auto got_table = reader_->ReadTable(columnIndices_ , &table_);
                if (!got_table.ok())
                    Verbosity::error("cannot read table from %s: %s", filepath_.c_str(), got_table.message().c_str());
            }

            bool has_column(const std::string& name) const
            {
                auto column_names = table_->ColumnNames();
                return boost::range::find(column_names, name) != column_names.end();
            }

            void read_column(std::size_t i, int& t) const
            {
                const auto& intArray = std::static_pointer_cast<arrow::Int64Array>(columnArrays_[i]);
                t = intArray->Value(rowIndex_);
            }

            void read_column(std::size_t i, int64_t& t) const
            {
                const auto& intArray = std::static_pointer_cast<arrow::Int64Array>(columnArrays_[i]);
                t = intArray->Value(rowIndex_);
            }

            void read_column(std::size_t i, float& t) const
            {
                const auto& fpArray = std::static_pointer_cast<arrow::FloatArray>(columnArrays_[i]);
                t = fpArray->Value(rowIndex_);
            }

            void read_column(std::size_t i, double& t) const
            {
                const auto& fpArray = std::static_pointer_cast<arrow::FloatArray>(columnArrays_[i]);
                t = fpArray->Value(rowIndex_);
            }

            void read_column(std::size_t i, std::string_view& t) const
            {
                const auto& strArray = std::static_pointer_cast<arrow::StringArray>(columnArrays_[i]);
                t = strArray->Value(rowIndex_);
            }

            void read_column(std::size_t i, std::string& t) const
            {
                const auto& strArray = std::static_pointer_cast<arrow::StringArray>(columnArrays_[i]);
                t = strArray->Value(rowIndex_);
            }

            template<class... ColType>
            void read_row_helper(std::size_t i, ColType&... cols) const
            {
                std::size_t col = i;
                ((read_column(col++, cols)), ...);
            }

            template<class ...ColType>
            bool read_row(ColType& ...cols)
            {
                constexpr auto numColumns = sizeof...(ColType);
                static_assert(numColumns >= column_count, "not enough columns specified");
                static_assert(numColumns <= column_count, "too many columns specified");

                // go to first row group, or next group if rowIndex has reached num_rows()
                if (rowGroup_ == -1 || rowIndex_ >= table_->num_rows())
                {
                    ++rowGroup_;
                    if (rowGroup_ >= numRowGroups_)
                        return false;

                    rowIndex_ = 0;
                    auto got_row_group = reader_->ReadRowGroup(rowGroup_, columnIndices_, &table_);
                    if (!got_row_group.ok())
                        Verbosity::error("cannot read row group %d from %s", rowGroup_, filepath_.c_str());

                    if (table_->num_rows() == 0)
                        return false;

                    columnArrays_.clear();
                    for (size_t i = 0; i < columnNames_.size(); ++i)
                        columnArrays_.emplace_back(get_arrow_column_chunk(table_, columnNames_[i]));
                }

                read_row_helper(0, cols...);
                ++rowIndex_;

                return true;
            }

            void seek_begin()
            {
                rowIndex_ = 0;
                rowGroup_ = -1;
                columnArrays_.clear();
            }

            mutable std::optional<uint64_t> num_rows_;

            uint64_t num_rows() const
            {
                if (num_rows_)
                    return num_rows_.value();

                uint64_t num_rows = 0;
                for (int i=0; i < reader_->num_row_groups(); ++i)
                {
                    std::shared_ptr<arrow::Table> table;
                    auto got_row_group = reader_->ReadRowGroup(i, vector {0}, &table);
                    if (!got_row_group.ok())
                        Verbosity::error("cannot read row group %d from %s", i, filepath_.c_str());
                    num_rows += table->num_rows();
                }
                Verbosity::debug("Found %d rows in parquet file.", num_rows);
                num_rows_ = num_rows;
                return num_rows;
            }
#endif
        };

        static bool is_parquet(const string& filepath) { return ParquetReader::is_parquet(filepath); }

        template<class ...ColNames>
        void read_header(io::ignore_column ignore_policy, ColNames...cols)
        {
            if (csvReader_)
                csvReader_->read_header(ignore_policy, std::forward<ColNames>(cols)...);
            else
                parquetReader_->read_header(ignore_policy, std::forward<ColNames>(cols)...);
        }

        bool has_column(const std::string& name) const
        {
            if (csvReader_)
                return csvReader_->has_column(name);
            return parquetReader_->has_column(name);
        }

        template<class ...ColType>
        bool read_row(ColType& ...cols)
        {
            if (csvReader_)
                return csvReader_->read_row(std::forward<ColType&>(cols)...);
            return parquetReader_->read_row(std::forward<ColType&>(cols)...);
        }

        void seek_begin()
        {
            if (csvReader_)
                csvReader_.reset(new CsvReader(filepath_));
            else
                parquetReader_->seek_begin();
        }

        uint64_t num_rows()
        {
            if (csvReader_)
            {
                ifstream fin(filepath_, ios::binary);
                return std::count(std::istream_iterator<char>(fin >> std::noskipws), {}, '\n');
            }
            return parquetReader_->num_rows();
        }
    };
};

DiaNNSpecLibReader::DiaNNSpecLibReader(BlibBuilder& maker, const char* specLibFile, const ProgressIndicator* parent_progress)
    : BuildParser(maker, specLibFile, parent_progress),
    impl_(new Impl(specLibFile, blibMaker_) )
{
    setSpecFileName(specLibFile, false);
    lookUpBy_ = INDEX_ID;
    // point to self as spec reader
    delete specReader_;
    specReader_ = this;

    if (!bfs::exists(specLibFile))
        Verbosity::error("speclib %s does not exist", specLibFile);

    prepareInsertSpectrumStatement(); // must reprepare after adding RetentionTimes table
}

DiaNNSpecLibReader::~DiaNNSpecLibReader()
{
    specReader_ = NULL; // so the parent class doesn't try to delete itself
}

bool DiaNNSpecLibReader::parseFile()
{
    {
        ifstream specLibStream(impl_->specLibFile_, ios::binary);
        if (!specLibStream)
            Verbosity::error("failed to open stream for speclib %s", impl_->specLibFile_);
        impl_->specLib.read(specLibStream);
        Verbosity::status("Read %d entries from speclib.", impl_->specLib.entries.size());
    }
    string specLibFile = impl_->specLibFile_;

    // crude output for viewing speclib files
    /*MSData msd;
    msd.id = msd.run.id = specLibFile;
    auto sl = new SpectrumListSimple;
    msd.run.spectrumListPtr.reset(sl);
    for (const auto& speclibEntry : impl_->specLib.entries)
    {
        SpectrumPtr s(new pwiz::msdata::Spectrum);
        s->index = sl->size();
        s->id = "merged=" + pwiz::util::toString(s->index) + " target=" + speclibEntry.name;
        s->set(MS_MSn_spectrum);
        s->set(MS_ms_level, 2);
        s->setMZIntensityArrays(vector<double>(), vector<double>(), MS_number_of_detector_counts);
        s->precursors.emplace_back(speclibEntry.target.mz, speclibEntry.target.charge);
        auto& mz = s->getMZArray()->data;
        auto& inten = s->getIntensityArray()->data;
        for (auto fragment : speclibEntry.target.fragments)
        {
            mz.push_back(fragment.mz);
            inten.push_back(fragment.height);
        }
        s->defaultArrayLength = mz.size();
        sl->spectra.emplace_back(s);
    }
    MSDataFile::write(msd, specLibFile + ".mzML");*/

    bfs::path specLibFilePath(specLibFile);

    auto diannReportFilepath = bal::replace_last_copy(specLibFile, "-lib.tsv.speclib", "-report.tsv");
    if (diannReportFilepath == specLibFile)
        diannReportFilepath = bal::replace_last_copy(specLibFile, "-lib.parquet.skyline.speclib", ".parquet");
    if (diannReportFilepath == specLibFile)
        diannReportFilepath = bal::replace_last_copy(specLibFile, "-lib.skyline.speclib", ".parquet");

    // special case for FragPipe
    if (specLibFilePath.filename().string() == "library.tsv.speclib" ||
        specLibFilePath.filename().string() == "library.tsv.skyline.speclib" ||
        specLibFilePath.filename().string() == "lib.predicted.speclib")
    {
        for (auto filename : vector<string>{ "diann-output.parquet", "report.parquet", "diann-output.tsv", "report.tsv" })
        {
            auto fragpipeDiannReport = specLibFilePath.parent_path() / "diann-output" / filename;
            if (bfs::exists(fragpipeDiannReport))
            {
                Verbosity::debug("Found DIA-NN tsv/speclib from FragPipe.");
                diannReportFilepath = fragpipeDiannReport.string();
                break;
            }
        }
    }

    string diannStatsFilepath;
    map<string, string> diannFilenameByRun;

    /*if (diannReportFilepath == impl_->specLibFile_)
        throw BlibException(true, "unable to determine DIA-NN report filename for '%s': speclib must end in -lib.tsv.speclib and report must end in -report.tsv", impl_->specLibFile_);
    if (!bfs::exists(diannReportFilepath))
        throw BlibException(true, "could not find DIA-NN report file '%s' for '%s'", bfs::path(diannReportFilepath).filename().string().c_str(), impl_->specLibFile_);*/
    if (diannReportFilepath == specLibFile || !bfs::exists(diannReportFilepath))
    {
        // iterate all TSV files in the same directory as the speclib, check the ones sharing the most leading characters, and look for the report headers in them
        vector<bfs::path> siblingTsvFiles, siblingParquetFiles;
        pwiz::util::expand_pathmask(bfs::path(impl_->specLibFile_).parent_path() / "*.tsv", siblingTsvFiles);
        pwiz::util::expand_pathmask(bfs::path(impl_->specLibFile_).parent_path() / "*.parquet", siblingParquetFiles);
        vector<bfs::path> siblingFiles;
        siblingFiles.insert(siblingFiles.end(), siblingParquetFiles.begin(), siblingParquetFiles.end());
        siblingFiles.insert(siblingFiles.end(), siblingTsvFiles.begin(), siblingTsvFiles.end());
        map<size_t, vector<bfs::path>, std::greater<size_t>> tsvFilepathBySharedPrefixLength;
        auto speclibFilename = bfs::path(impl_->specLibFile_).filename().string();
        for (const auto& tsvFilepath : siblingFiles)
        {
            if (!bfs::is_regular_file(tsvFilepath))
                continue;
            if (bal::contains(tsvFilepath.filename().string(), "first-pass") && !bal::contains(speclibFilename, "first-pass"))
                continue;
            if (bal::ends_with(tsvFilepath.string(), ".stats.tsv"))
            {
                diannStatsFilepath = tsvFilepath.string();
                continue;
            }
            size_t sharedPrefixLength = 0, i;
            auto tsvFilename = tsvFilepath.filename().string();
            for (i = 0; i < tsvFilename.length() && i < speclibFilename.length(); ++i)
                if (tsvFilename[i] != speclibFilename[i])
                    break;
            sharedPrefixLength = i;
            tsvFilepathBySharedPrefixLength[sharedPrefixLength].emplace_back(tsvFilepath);
        }

        diannReportFilepath.clear();
        for (const auto& sharedPrefixLengthAndFilepathsPair : tsvFilepathBySharedPrefixLength)
        {
            for (const auto& tsvFilepath : sharedPrefixLengthAndFilepathsPair.second)
            {
                Impl::Reader<3> reader;
                reader.open_file(tsvFilepath.string());
                reader.read_header(io::ignore_extra_column | io::ignore_missing_column, "Precursor.Id", "Global.Q.Value", "RT");
                if (reader.has_column("Precursor.Id") && reader.has_column("Global.Q.Value") && reader.has_column("RT"))
                {
                    diannReportFilepath = tsvFilepath.string();
                    break;
                }
            }
            if (!diannReportFilepath.empty())
                break;
        }

        if (diannReportFilepath.empty())
            throw BlibException(true, "unable to determine DIA-NN report filename for '%s': the Parquet or TSV report is required to read speclib files and must be in the same directory as the speclib and share some leading characters (e.g. somedata-tsv.speclib and somedata-report.parquet)",
                impl_->specLibFile_);
    }
    else
    {
        // iterate all TSV files in the same directory as the speclib, looking for stats.tsv
        vector<bfs::path> siblingTsvFiles;
        pwiz::util::expand_pathmask(bfs::path(impl_->specLibFile_).parent_path() / "*.stats.tsv", siblingTsvFiles);
        for (const auto& tsvFilepath : siblingTsvFiles)
        {
            if (!bfs::is_regular_file(tsvFilepath))
                continue;
            if (bal::contains(tsvFilepath.filename().string(), "first-pass") && !bal::contains(diannReportFilepath, "first-pass"))
                continue;
            if (bal::ends_with(tsvFilepath.string(), ".stats.tsv"))
            {
                diannStatsFilepath = tsvFilepath.string();
                break;
            }
        }
    }

    if (!diannStatsFilepath.empty())
    {
        try
        {
            Verbosity::debug("Reading filenames from stats.tsv: %s", diannStatsFilepath.c_str());
            Impl::Reader<3> reader;
            reader.open_file(diannStatsFilepath);
            reader.read_header(io::ignore_extra_column, "File.Name", "Precursors.Identified", "Proteins.Identified");
            string fileName, id1, id2;
            while (reader.read_row(fileName, id1, id2))
            {
                string runName = bfs::path(fileName).filename().replace_extension("").string();
                fileName = bfs::path(fileName).filename().string();
                diannFilenameByRun[runName] = fileName;
                Verbosity::debug("%s -> %s", runName.c_str(), fileName.c_str());
            }
        }
        catch (std::exception& e)
        {
            Verbosity::warn("error reading filepaths from stats report \"%s\": %s", diannStatsFilepath.c_str(), e.what());
        }
    }

    auto& speclib = impl_->specLib;
    Impl::Reader<10> reader;
    reader.open_file(diannReportFilepath);
    Verbosity::debug("Opened report file %s.", diannReportFilepath.c_str());
    const char* fileNameColumn = reader.is_parquet(diannReportFilepath) ? "Run" : "File.Name"; // DIANN v2 doesn't provide File.Name column
    Verbosity::status("Reading report headers.");
    reader.read_header(io::ignore_extra_column, "Run", fileNameColumn, "Protein.Group", "Precursor.Id", "Global.Q.Value", "Q.Value", "RT", "RT.Start", "RT.Stop", "IM");
    Verbosity::debug("Read report headers.");

    std::string_view run, fileName, proteinGrp, precursorId;
    float globalQValue;
    RtPSM redundantPSM;
    string firstRun; // used as a placeholder for setSpecFileName; SpectrumSourceFile/fileId is actually managed while reading rows
    int64_t redundantPsmCount = 0;

    // build PSM list from speclib; but filename and retention time info will be missing
    // iterate through report rows, keep track of best PSM for each precursorId
    // after reading entire report, we can build RefSpectra, rows will have rt[start/end] from the best PSM
    // then populate RetentionTimes table from redundant PSMs for each precursorId
    map<string, list<RtPSM>> retentionTimesByPrecursorId;
    map<string, NonRedundantPSM*> psmByPrecursorId;
    Verbosity::debug("Creating PSMs for %d speclib entries.", speclib.entries.size());
    for (const auto& speclibEntry : speclib.entries)
    {
        auto psm = new NonRedundantPSM;
        psm->charge = speclibEntry.target.charge;
        psm->unmodSeq = get_aas(speclibEntry.name, psm->mods);
        psm->specIndex = speclibEntry.target.index;
        psm->score = 2; // not a valid q-value; indicates the PSM hasn't been added to psm list yet
        psmByPrecursorId[speclibEntry.name] = psm;
    }
    Verbosity::debug("Finished creating PSMs.");

    Verbosity::status("Reading %d rows from report.", reader.num_rows());
    readAddProgress_ = parentProgress_->newNestedIndicator(reader.num_rows());
    while (reader.read_row(run, fileName, proteinGrp, precursorId, globalQValue, redundantPSM.score, redundantPSM.rt, redundantPSM.rtStart, redundantPSM.rtEnd, redundantPSM.ionMobility))
    {
        string precursorIdStr(precursorId);
        auto findItr = psmByPrecursorId.find(precursorIdStr);
        if (findItr == psmByPrecursorId.end())
        {
            // skip contaminant proteins if they are not included in the speclib file
            if (bal::starts_with(proteinGrp, "contaminant_"))
                continue;

            throw BlibException(false, "could not find precursorId '%s' in speclib; is '%s' the correct report TSV file?", precursorId, bfs::path(diannReportFilepath).filename().string().c_str());
        }

        bfs::path currentRunFilepath = bal::replace_all_copy(string(fileName), "\\", "/"); // backslash to slash should work on both Linux and Windows
        string currentRunFilename = currentRunFilepath.filename().string();
        if (bal::iends_with(currentRunFilename, ".dia")) // trim DIA extension if present
            currentRunFilename = currentRunFilename.substr(0, currentRunFilename.length() - 4);
        if (firstRun.empty())
            firstRun = currentRunFilename;

        bool rowPassesFilter = globalQValue <= getScoreThreshold(GENERIC_QVALUE_INPUT);

        auto& retentionTimes = retentionTimesByPrecursorId[precursorIdStr];
        if (rowPassesFilter)
        {
            string spectrumFilepath = currentRunFilename;
            auto findItr2 = diannFilenameByRun.find(currentRunFilename);
            if (findItr2 != diannFilenameByRun.end())
                spectrumFilepath = findItr2->second;

            retentionTimes.emplace_back(redundantPSM);
            retentionTimes.back().fileId = insertSpectrumFilename(spectrumFilepath, true, DIA);
            ++redundantPsmCount;
        }

        auto psm = findItr->second;

        // if this is the first row for the precursorId, add it to psm list
        if (rowPassesFilter && psm->score == 2)
        {
            psms_.emplace_back(psm);
            if (redundantPSM.ionMobility > 0)
                impl_->ionMobilityType_ = IONMOBILITY_INVERSEREDUCED_VSECPERCM2;
        }

        // update bestQValue and bestPSM if this row is better than any previous one
        if (rowPassesFilter && globalQValue < psm->score)
        {
            auto findItr2 = speclib.entryByModPeptideAndCharge.find(precursorIdStr);
            if (findItr2 == speclib.entryByModPeptideAndCharge.end())
                throw BlibException(false, "could not find precursorId '%s' in speclib; is '%s' the correct report TSV file?", precursorId, bfs::path(diannReportFilepath).filename().string().c_str());

            psm->score = globalQValue;
            psm->fileId = retentionTimes.back().fileId;

            auto& speclibEntry = findItr2->second.get();
            speclibEntry.bestQValue = globalQValue;
            speclibEntry.bestPSM = &retentionTimes.back();
        }

        readAddProgress_->increment();
    }
    reader.close();

    filteredOutPsmCount_ = speclib.entries.size() - psms_.size();


    // CONSIDER: when/if another input type needs to build a non-redundant library directly, move this code to a common location
    Verbosity::status("Building retention time table with %ld entries.", redundantPsmCount);
    blibMaker_.beginTransaction();
    for (const auto& kvp : retentionTimesByPrecursorId)
    {
        const auto& precursorIdStr = kvp.first;
        const auto& redundantPsms = kvp.second;

        auto findItr = speclib.entryByModPeptideAndCharge.find(precursorIdStr);
        const auto& speclibEntry = findItr->second.get();

        for (const auto& psm : redundantPsms)
        {
            // RefSpectraID, RedundantRefSpectraID, SpectrumSourceID, ionMobility, collisionalCrossSectionSqA, ionMobilityHighEnergyOffset, ionMobilityType, retentionTime, startTime, endTime, score, bestSpectrum
            int bestSpectrum = speclibEntry.bestPSM->fileId == psm.fileId ? 1 : 0;

            auto& insertStmt_ = impl_->insertRetentionTimesStmt_;
            sqlite3_bind_int(insertStmt_, 1, speclibEntry.target.index);
            sqlite3_bind_int(insertStmt_, 2, 0);
            sqlite3_bind_int(insertStmt_, 3, psm.fileId);
            sqlite3_bind_double(insertStmt_, 4, psm.ionMobility);
            sqlite3_bind_double(insertStmt_, 5, 0);
            sqlite3_bind_double(insertStmt_, 6, 0);
            sqlite3_bind_int(insertStmt_, 7, psm.ionMobility > 0 ? impl_->ionMobilityType_ : 0);
            sqlite3_bind_double(insertStmt_, 8, psm.rt);
            sqlite3_bind_double(insertStmt_, 9, psm.rtStart);
            sqlite3_bind_double(insertStmt_, 10, psm.rtEnd);
            sqlite3_bind_double(insertStmt_, 11, psm.score);
            sqlite3_bind_int(insertStmt_, 12, bestSpectrum);
            if (sqlite3_step(insertStmt_) != SQLITE_DONE) {
                throw BlibException(false, "Error inserting row into RetentionTimes: %s", sqlite3_errmsg(blibMaker_.getDb()));
            }
            else if (sqlite3_reset(insertStmt_) != SQLITE_OK) {
                throw BlibException(false, "Error resetting insert statement: %s", sqlite3_errmsg(blibMaker_.getDb()));
            }
        }

        // update copies field for PSM
        auto findItr2 = psmByPrecursorId.find(precursorIdStr);
        findItr2->second->copies = redundantPsms.size();
    }
    blibMaker_.endTransaction();

    Verbosity::status("Reading %d spectra from speclib.", psms_.size());
    setSpecFileName(firstRun, false);
    buildTables(GENERIC_QVALUE);

    // update RetentionTimes.RefSpectraID field after filtering
    Verbosity::status("Updating RetentionTimes table after filtering.");
    blibMaker_.sql_stmt("UPDATE RetentionTimes SET RefSpectraID = newId "
                        "FROM (SELECT DISTINCT t.[RefSpectraID] as oldId, s.[id] as newId FROM RefSpectra s, RetentionTimes t WHERE s.[SpecIdInFile] = t.[RefSpectraID]) AS IdMapping "
                        "WHERE RefSpectraID == IdMapping.oldId");

    return true;
}

vector<PSM_SCORE_TYPE> DiaNNSpecLibReader::getScoreTypes() {
    return vector<PSM_SCORE_TYPE>(1, GENERIC_QVALUE);
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
    returnData.retentionTime = entry.bestPSM->rt;
    returnData.startTime = entry.bestPSM->rtStart;
    returnData.endTime = entry.bestPSM->rtEnd;
    returnData.ionMobility = entry.bestPSM->ionMobility;
    if (returnData.ionMobility > 0)
        returnData.ionMobilityType = impl_->ionMobilityType_;

    if (!getPeaks)
        return true;

    returnData.mzs = new double[returnData.numPeaks];
    returnData.intensities = new float[returnData.numPeaks];
    for (int i=0; i < returnData.numPeaks; ++i)
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
