//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#ifndef PWIZ_API_DECL
#ifdef PWIZ_DYN_LINK
#ifdef PWIZ_SOURCE
#define PWIZ_API_DECL __declspec(dllexport)
#else
#define PWIZ_API_DECL __declspec(dllimport)
#endif  // PWIZ_SOURCE
#else
#define PWIZ_API_DECL
#endif  // PWIZ_DYN_LINK
#endif  // PWIZ_API_DECL
