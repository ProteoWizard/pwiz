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

#ifndef MODIFICATIONS_H
#define MODIFICATIONS_H

struct modData{
  void Set(char c, char s, float d,  int n) {
    character = c;
    symbol = s;
    diff = d;
    sign = n;
  }

  char character;
  char symbol;
  float diff;
  int sign; //+1 or -1
};

class Modifications{

 public:
  const static int numMods = 8;
  static const char* getSymbol(char character);
  static float getMass(char character);
  static char getSign(char character);
  modData modsList[numMods];

  Modifications();
};
#endif //MODIFICATIONS_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
