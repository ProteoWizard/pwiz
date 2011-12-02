/*
 * Copyright 1993, 1995 Christopher Seiwald.
 *
 * This file is part of Jam - see jam.c for Copyright information.
 */

/*
 * expand.h - expand a buffer, given variable values
 */

#ifndef EXPAND_SW20111118_H
#define EXPAND_SW20111118_H

#include "lists.h"
#include "object.h"

LIST * var_expand( LIST  * l, const char * in, const char *end, LOL * lol, OBJECT * cancopyin );
void var_expand_unit_test();

#endif
