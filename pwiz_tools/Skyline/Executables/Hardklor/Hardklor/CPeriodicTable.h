/*
Copyright 2007-2016, Michael R. Hoopmann, Institute for Systems Biology
Michael J. MacCoss, University of Washington

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
#ifndef _CPERIODICTABLE_H
#define _CPERIODICTABLE_H

#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <iostream>
#include <vector>

typedef struct {
  int atomicNum;
  char symbol[3];
  double mass;
} element;

class CPeriodicTable {
 public:
   //Constructors & Destructors
   //CPeriodicTable();
   CPeriodicTable(const char* c="Hardklor.dat");
   ~CPeriodicTable();

   //Methods:
   element& at(int);
   int size();

 protected:
 private:
   //Methods:
   void defaultValues();
   void loadTable(const char*);

   //Data Members:
   std::vector<element> table;

};

#endif
