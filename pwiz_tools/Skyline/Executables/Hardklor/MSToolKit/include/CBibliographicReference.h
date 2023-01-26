/*
Copyright 2020, Michael R. Hoopmann, Institute for Systems Biology
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

#ifndef _CBIBLIOGRAPHICREFERENCE_H
#define _CBIBLIOGRAPHICREFERENCE_H

#include <cstdio>
#include <string>

class CBibliographicReference {
public:

  //Data Members
  std::string authors;
  std::string doi;
  std::string editor;
  std::string id;
  std::string issue;
  std::string name;
  std::string pages;
  std::string publication;
  std::string publisher;
  std::string title;
  std::string volume;
  std::string year;

  //Functions
  void writeOut(FILE* f, int tabs = -1);

private:
};

#endif
