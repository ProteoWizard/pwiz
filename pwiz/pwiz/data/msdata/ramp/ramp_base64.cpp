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

#include "stdlib.h"
#include "string.h"
#include "ramp_base64.h"

static const unsigned int lookup[] = { // basic base64 charset table
0, //  NUL
0, //  SOH 
0, //  STX 
0, //  ETX 
0, //  EOT 
0, //  ENQ 
0, //  ACK 
0, //  BEL 
0, //   BS 
0, //   HT 
0, //   LF 
0, //   VT 
0, //   FF 
0, //   CR 
0, //   SO 
0, //   SI 
0, //  DLE 
0, //  DC1 
0, //  DC2 
0, //  DC3 
0, //  DC4 
0, //  NAK 
0, //  SYN 
0, //  ETB 
0, //  CAN 
0, //   EM 
0, //  SUB 
0, //  ESC 
0, //   FS 
0, //   GS 
0, //   RS 
0, //   US 
0, //   SP 
0, //    ! 
0, //    " 
0, //    # 
0, //    $ 
0, //    % 
0, //    & 
0, //    ' 
0, //    ( 
0, //    ) 
0, //    * 
62, //    +
0, //    , 
0, //    - 
0, //    . 
63, //    /
52, //    0,  
53, //    1
54, //    2
55, //    3
56, //    4
57, //    5
58, //    6
59, //    7
60, //    8
61, //    9
0, //    :  
0, //    ;  
0, //    <  
0, //    =
0, //    >  
0, //    ?  
0, //    @  
0, //    A
1, //    B
2, //    C
3, //    D
4, //    E
5, //    F
6, //    G
7, //    H
8, //    I
9, //    J
10, //    K
11, //    L
12, //    M
13, //    N
14, //    O
15, //    P
16, //    Q
17, //    R
18, //    S
19, //    T
20, //    U
21, //    V
22, //    W
23, //    X
24, //    Y
25, //    Z
0, //    [   
0, //    '\'   
0, //    ]   
0, //    ^   
0, //    _   
0, //    `
26, //    a
27, //    b
28, //    c
29, //    d
30, //    e
31, //    f
32, //    g
33, //    h
34, //    i
35, //    j
36, //    k
37, //    l
38, //    m
39, //    n
40, //    o
41, //    p
42, //    q
43, //    r
44, //    s
45, //    t
46, //    u
47, //    v
48, //    w
49, //    x
50, //    y
51, //    z
0, //    { 
0, //    | 
0, //    } 
0, //    ~ 
0 //  DEL  
};

//
// wacky stuff to try to condense multiple decode steps
// into a single lookup
static unsigned char *lookup1=NULL;
static unsigned char *lookup2=NULL;
static unsigned char *lookup3=NULL;
static unsigned char *lookup12=NULL;
static int bLittleEndian;

static void b64_cleanup(void) {
   free(lookup1);
   free(lookup2);
   free(lookup3);
   free(lookup12);
}

static void b64_init() {
   if (!lookup1) { // first time?

      // init tables for faster base64 decode
      int i,j,k;
      lookup1 =  (unsigned char *)calloc(1,0x7fff);
      lookup2 =  (unsigned char *)calloc(1,0x7fff);
      lookup3 =  (unsigned char *)calloc(1,0x7fff);
      lookup12 = (unsigned char *)calloc(2,0x7fffff);

      // check endianness
      i = 1;
      bLittleEndian = *((char *)&i);

      for (i='+';i<='z';i++) {
         for (j='+';j<='z';j++) {
            int index = (i<<8)|j;
            lookup1[index] = (lookup[i]<<2)|(lookup[j]>>4);
            lookup2[index] = (lookup[i]<<4)|(lookup[j]>>2);
            lookup3[index] = (lookup[i]<<6)|(lookup[j]);
         }
      }
      for (i='+';i<='z';i++) {
         for (j='+';j<='z';j++) {
            for (k='+';k<='z';k++) {
               int index4;
               if (bLittleEndian) {
                  char *c = (char *)&index4;
                  index4=0;
                  *c++ = i;
                  *c++ = j;
                  *c = k;
                  index4 *=2;
               } else {
                  index4 = 2*((i<<16)|(j<<8)|k);
               }
               lookup12[index4++] = lookup1[(i<<8)|j];
               lookup12[index4++] = lookup2[(j<<8)|k];
            }
         }
      }
      atexit(b64_cleanup);
   }
}

void b64_decode ( char *out,  const char *in , int outlen)
{
   unsigned char *dest = (unsigned char *)out;
   const char *src = in;
   int count = outlen;
#ifdef OLDSCHOOL
   unsigned char a;
   unsigned char b;

   while (count--) {
      a = lookup[*src++];
      b = lookup[*src++];

      *dest++ = ( a << 2) | ( b >> 4);
      
      if (!count--) {
         return;
      }
      a = lookup[*src++];
      *dest++ = ( b << 4) | ( a >> 2);
      
      if (!count--) {
         return;
      }
      b = lookup[*src++];
      *dest++ = ( a << 6) | ( b );
      
   }

#else
   unsigned short int f;
   b64_init(); // set up lookup tables if needed
   if (bLittleEndian) { // can populate index with memcpy
      int index = 0;
      while (count>=3) {
         memcpy((char *)&index,src,3);
         memcpy(dest,lookup12 + 2*index,2);
         *(dest+2)=lookup3[((*(src+2))<<8)|(*(src+3))];
         src+=4;
         dest+=3;
         count-=3;
      }
   } else {
      while (count>3) {
         memcpy(dest,lookup12 + 2*(((*src)<<16)|((*(src+1))<<8)|(*(src+2))),2);
         *(dest+2)=lookup3[((*(src+2))<<8)|(*(src+3))];
         src+=4;
         dest+=3;
         count-=3;
      }
   }

   // pick up the last bit of conversion
   while (count--) {
      *dest++ = lookup1[f=(((*src)<<8)|(*(src+1)))];
      if (count--) {
         *dest++ = lookup2[f=((f<<8)|(*(src+2)))];
         if (count--) {
            *dest++ = lookup3[(short int)((f<<8)|(*(src+3)))];
         } else {
            break;
         }
      } else {
         break;
      }
      src += 4;
   }
#endif
}


//
// encoding stuff
//
static const unsigned char *b64_tbl = (const unsigned char*) "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
static const unsigned char b64_pad = '=';

/* base64 encode a group of between 1 and 3 input chars into a group of  4 output chars */
static void encode_group (unsigned char output[],
                          const unsigned char input[],
                          int n)
{
   unsigned char ingrp[3];

   ingrp[0] = n > 0 ? input[0] : 0;
   ingrp[1] = n > 1 ? input[1] : 0;
   ingrp[2] = n > 2 ? input[2] : 0;

   /* upper 6 bits of ingrp[0] */
   output[0] = n > 0 ? b64_tbl[ingrp[0] >> 2] : b64_pad;

   /* lower 2 bits of ingrp[0] | upper 4 bits of ingrp[1] */
   output[1] = n > 0 ? b64_tbl[((ingrp[0] & 0x3) << 4) | (ingrp[1] >> 4)] : b64_pad;

   /* lower 4 bits of ingrp[1] | upper 2 bits of ingrp[2] */
   output[2] = n > 1 ? b64_tbl[((ingrp[1] & 0xf) << 2) | (ingrp[2] >> 6)] : b64_pad;

   /* lower 6 bits of ingrp[2] */
   output[3] = n > 2 ? b64_tbl[ingrp[2] & 0x3f] : b64_pad;

}


void b64_encode (char *dest,
                const char *src,
                int len)
{
   int outsz = 0;

   while (len > 0)
    {
      encode_group ( (unsigned char*) dest + outsz, (const unsigned char*) src, len > 3 ? 3 : len);
      len -= 3;
      src += 3;
      outsz += 4;
         }

}
