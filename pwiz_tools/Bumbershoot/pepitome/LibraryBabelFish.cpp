
#include "LibraryBabelFish.h"

namespace freicore
{
namespace pepitome
{

void LibraryBabelFish::initializeDatabase()
{
    // optimized for bulk insertion
    libraryIndex.execute("PRAGMA journal_mode=OFF;"
                      "PRAGMA synchronous=OFF;"
                      "PRAGMA automatic_indexing=OFF;"
                      "PRAGMA default_cache_size=500000;"
                      "PRAGMA temp_store=MEMORY"
                     );
    // Create tables
    sqlite::transaction transaction(libraryIndex);
    libraryIndex.execute("CREATE TABLE LibMetaData ( Id INTEGER PRIMARY KEY, Peptide TEXT, LibraryMass REAL, MonoMass REAL, AvgMass REAL, Charge INTEGER);"
                         "CREATE TABLE LibSpectrumData (Id INTEGER PRIMARY KEY, NumPeaks INTEGER, SpectrumData TEXT, FOREIGN KEY (Id) REFERENCES LibMetaData(Id));"
                        );
    transaction.commit();
}

void LibraryBabelFish::indexLibrary()
{
    // Get insertion commands
    sqlite::command insertMetaData(libraryIndex, "INSERT INTO LibMetaData ( Id, Peptide, LibraryMass, MonoMass, AvgMass, Charge) VALUES (?,?,?,?,?,?)");
    sqlite::command insertSpectrumData(libraryIndex, "INSERT INTO LibSpectrumData ( Id, NumPeaks, SpectrumData) VALUES (?,?,?)");
    
    library.loadLibrary(libraryName);
    size_t totalSpectra = library.size();
    cout << "Indexing  " << totalSpectra << " found in the library." << endl;
    for(sqlite3_int64 index = 0; index < totalSpectra; ++index)
    {
        if(!(index % 1000)) 
            cout << totalSpectra << ": " << index << '\r' << flush;
        // Read the library spectrum metadata and insert it into the DB
        library[index]->readSpectrumForIndexing();
        insertMetaData.binder() << index 
                                << library[index]->matchedPeptide->sequence()
                                << library[index]->libraryMass
                                << library[index]->monoisotopicMass
                                << library[index]->averageMass
                                << library[index]->id.charge;
        insertMetaData.execute();
        insertMetaData.reset();

        // Archive the peptide, peaks, and proteins data.
        stringstream packStream;
        text_oarchive packArchive( packStream );
        packArchive  & *library[index];
        // Compress the data before putting it in the DB
        stringstream encoded;
        bio::copy(packStream,
                  bio::compose(bio::zlib_compressor(bio::zlib_params::zlib_params(bio::zlib::best_compression)), 
                  bio::compose(bio::base64_encoder(),encoded)));
        
        sqlite3_int64 numPeaks = library[index]->peakPreData.size();
        insertSpectrumData.binder() << index 
                                << numPeaks
                                << encoded.str();

        // Insert the compressed spectrum data into library
        //insertSpectrumData.bind(1, index);
        //insertSpectrumData.bind(2, numPeaks);
        //insertSpectrumData.bind(3, static_cast<const void*>(compressedStream.str().data()), compressedStream.str().length());
        insertSpectrumData.execute();
        insertSpectrumData.reset();
        library[index]->clearSpectrum();
    }   
}
}
}

int main( int argc, char* argv[] )
{
    // Get the command line arguments and process them
    vector< string > args;
    for( int i=0; i < argc; ++i )
        args.push_back( argv[i] );
    if(args.size() < 2)
    {
        cout << "LibraryBabelFish <library file path>" << endl;
        exit(1);
    }

    freicore::pepitome::LibraryBabelFish converter(args[1]);
    converter.initializeDatabase();
    converter.indexLibrary();
}

