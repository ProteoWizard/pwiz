# Copyright David Abrahams 2004. Distributed under the Boost
# Software License, Version 1.0. (See accompanying
# file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt)

from b2.build import type

def register ():
    
    if not type.registered ('LIB'):
        type.register ('LIB')
    
    type.register_type ('STATIC_LIB', ['lib', 'a'], 'LIB', ['NT', 'CYGWIN'])
    type.register_type ('STATIC_LIB', ['a'], 'LIB')
    
    type.register_type ('IMPORT_LIB', [], 'STATIC_LIB')
    type.set_generated_target_suffix ('IMPORT_LIB', [], 'lib')
    
    type.register_type ('SHARED_LIB', ['dll'], 'LIB', ['NT', 'CYGWIN'])
    type.register_type ('SHARED_LIB', ['so'], 'LIB')
    
    type.register_type ('SEARCHED_LIB', [], 'LIB')

register ()
