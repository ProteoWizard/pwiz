
#ifndef _LIBRARYBABELFISH_
#define _LIBRARYBABELFISH_

#include "pepitome.h"

namespace freicore {
namespace pepitome {
struct LibraryBabelFish
{
    SpectraStore        library;
    string              libraryName;
    string              libraryIndexName;
    sqlite::database    libraryIndex;

    LibraryBabelFish(string name) : libraryName (name), 
                                    libraryIndexName(name + ".index"),
                                    libraryIndex(libraryIndexName)
    {}
    
    void initializeDatabase();
    void indexLibrary();
};

}
}

#endif
