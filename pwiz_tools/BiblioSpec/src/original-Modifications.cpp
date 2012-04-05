//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#include "original-Modifications.h"

Modifications::Modifications() {

    modsList[0].Set('p', '*', 80.0f, 1); //phosphorylation
    modsList[1].Set('o', '@', 16.0f, 1); //oxidation
    modsList[2].Set('a', '#', 42.0f, 1); //acytylation
    modsList[3].Set('d', '^', 28.0f, 1); //dimethylation
    modsList[4].Set('m', '&', 14.0f, 1); //monomethylation
    modsList[5].Set('t', '!', 42.0f, 1); //trimethylation
    modsList[6].Set('s', '~', 79.9f, 1); //sulfation
    modsList[7].Set('c', '$', 43.0f, 1); //carbanlyation
 
}


const char* Modifications::getSymbol(char character) {
    switch( character ) {
    case 'p':
        return "*"; 
    case 'o':
        return "@";
    case 'a':
        return  "#";
    case 'd':
        return  "^";
    case 'm':
        return  "&";
    case 't': 
        return "!";
    case 's': 
        return "~";
    case 'c': 
        return "$";
    }
    return " ";
}

float Modifications::getMass(char character) {
    switch( character ) {
    case 'p':
        return 80.0f; 
    case 'o':
        return 16.0f;
    case 'a':
        return 42.0f;
    case 'd':
        return 28.0f;
    case 'm':
        return 14.0f;
    case 't': 
        return 42.0f;
    case 's': 
        return 79.9f;
    case 'c': 
        return 43.0f;
    }
    return 57.02f;
}

char Modifications::getSign(char character) {
    switch( character ) {
    case 'p':
        return '+'; 
    case 'o':
        return '+';
    case 'a':
        return '+';
    case 'd':
        return '+';
    case 'm':
        return '+';
    case 't': 
        return '+';
    case 's': 
        return '+';
    case 'c': 
        return '+';
    }
    return '+';
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
