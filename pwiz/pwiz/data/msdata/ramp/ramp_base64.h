// $Id$
// base64 decode optimized using table lookup 
// Copyright (C) 2005 Insilicos LLC All Rights Reserved
// bpratt 10-5-05

/*

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

*/

#ifndef _RAMPBASE64_H
#define _RAMPBASE64_H

void b64_decode (char *dest, const char *src, int destlen);
void b64_encode (char *dest, const char *src, int destlen);


#endif /* BASE64_H */
