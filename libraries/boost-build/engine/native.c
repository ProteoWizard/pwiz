/* Copyright Vladimir Prus 2003. Distributed under the Boost */
/* Software License, Version 1.0. (See accompanying */
/* file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt) */

#include "native.h"
#include "hash.h"
#include "object.h"

void declare_native_rule( const char * module, const char * rule, const char * * args,
                          LIST * (*f)( FRAME *, int ), int version )
{
    OBJECT * module_obj = 0;
    module_t * m;
    if ( module )
    {
        module_obj = object_new( module );
    }
    m = bindmodule( module_obj );
    if ( module_obj )
    {
        object_free( module_obj );
    }
    if (m->native_rules == 0) {
        m->native_rules = hashinit( sizeof( native_rule_t ), "native rules");
    }

    {
        native_rule_t n, *np = &n;
        n.name = object_new( rule );
        if (args)
        {
            n.arguments = args_new();
            lol_build( n.arguments->data, args );
        }
        else
        {
            n.arguments = 0;
        }
        n.procedure = function_builtin( f, 0 );
        n.version = version;
        hashenter(m->native_rules, (HASHDATA**)&np);
    }
}
