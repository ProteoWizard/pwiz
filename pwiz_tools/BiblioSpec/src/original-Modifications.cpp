/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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
