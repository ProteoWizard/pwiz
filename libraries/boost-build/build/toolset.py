# Status: being ported by Vladimir Prus
# Base revision:  40958
#
# Copyright 2003 Dave Abrahams 
# Copyright 2005 Rene Rivera 
# Copyright 2002, 2003, 2004, 2005, 2006 Vladimir Prus 
# Distributed under the Boost Software License, Version 1.0. 
# (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt) 

""" Support for toolset definition.
"""

import feature, property, generators
from b2.util.utility import *
from b2.util import set

__re_split_last_segment = re.compile (r'^(.+)\.([^\.])*')
__re_two_ampersands = re.compile ('(&&)')
__re_first_segment = re.compile ('([^.]*).*')
__re_first_group = re.compile (r'[^.]*\.(.*)')

# Flag is a mechanism to set a value 
# A single toolset flag. Specifies that when certain
# properties are in build property set, certain values
# should be appended to some variable.
#
# A flag applies to a specific action in specific module.
# The list of all flags for a module is stored, and each
# flag further contains the name of the rule it applies
# for, 
class Flag:

    def __init__(self, variable_name, values, condition, rule = None):
        self.variable_name = variable_name
        self.values = values
        self.condition = condition        
        self.rule = rule

    def __str__(self):
        return("Flag(" + str(self.variable_name) + ", " + str(self.values) +\
               ", " + str(self.condition) + ", " + str(self.rule) + ")")

def reset ():
    """ Clear the module state. This is mainly for testing purposes.
    """
    global __module_flags, __flags, __stv
    
    # Mapping from module name to a list of all flags that apply
    # to either that module directly, or to any rule in that module.
    # Each element of the list is Flag instance.
    # So, for module named xxx this might contain flags for 'xxx',
    # for 'xxx.compile', for 'xxx.compile.c++', etc.
    __module_flags = {}

    # Mapping from specific rule or module name to a list of Flag instances
    # that apply to that name.
    # Say, it might contain flags for 'xxx.compile.c++'. If there are
    # entries for module name 'xxx', they are flags for 'xxx' itself,
    # not including any rules in that module.
    __flags = {}
    
    # A cache for varaible settings. The key is generated from the rule name and the properties.
    __stv = {}
    
reset ()

# FIXME: --ignore-toolset-requirements
# FIXME: using
    
def normalize_condition (property_sets):
    """ Expands subfeatures in each property set.
        e.g
            <toolset>gcc-3.2
        will be converted to
        <toolset>gcc/<toolset-version>3.2

        TODO: does this one belong here or in feature?
    """
    result = []
    for p in property_sets:
        split = feature.split (p)
        expanded = feature.expand_subfeatures (split)
        result.append ('/'.join (expanded))

    return result

# FIXME push-checking-for-flags-module ....
# FIXME: investigate existing uses of 'hack-hack' parameter
# in jam code.

def flags (rule_or_module, variable_name, condition, values = []):
    """ Specifies the flags (variables) that must be set on targets under certain
        conditions, described by arguments.
        rule_or_module:   If contains dot, should be a rule name.
                          The flags will be applied when that rule is
                          used to set up build actions.
                          
                          If does not contain dot, should be a module name.
                          The flags will be applied for all rules in that
                          module.
                          If module for rule is different from the calling
                          module, an error is issued.

         variable_name:   Variable that should be set on target
         
         condition        A condition when this flag should be applied.
                          Should be set of property sets. If one of
                          those property sets is contained in build
                          properties, the flag will be used.
                          Implied values are not allowed:
                          "<toolset>gcc" should be used, not just
                          "gcc". Subfeatures, like in "<toolset>gcc-3.2"
                          are allowed. If left empty, the flag will
                          always used.
                          
                          Propery sets may use value-less properties 
                          ('<a>'  vs. '<a>value') to match absent 
                          properties. This allows to separately match
                          
                             <architecture>/<address-model>64
                             <architecture>ia64/<address-model>
                          
                          Where both features are optional. Without this
                          syntax we'd be forced to define "default" value.

         values:          The value to add to variable. If <feature>
                          is specified, then the value of 'feature' 
                          will be added.
    """
    if condition and not replace_grist (condition, ''):
        # We have condition in the form '<feature>', that is, without
        # value. That's a previous syntax:
        #
        #   flags gcc.link RPATH <dll-path> ;
        # for compatibility, convert it to
        #   flags gcc.link RPATH : <dll-path> ;                
        values = [ condition ]
        condition = None
    
    if condition:
        property.validate_property_sets (condition)
        condition = normalize_condition ([condition])
    
    __add_flag (rule_or_module, variable_name, condition, values)

def set_target_variables (manager, rule_or_module, targets, properties):
    """
    """
    key = rule_or_module + '.' + str (properties)
    settings = __stv.get (key, None)
    if not settings:
        settings = __set_target_variables_aux  (manager, rule_or_module, properties)

        __stv [key] = settings
        
    if settings:
        for s in settings:
            for target in targets:
                manager.engine ().set_target_variable (target, s [0], s[1], True)

def find_property_subset (property_sets, properties):
    """Returns the first element of 'property-sets' which is a subset of
    'properties', or an empty list if no such element exists."""
    
    prop_keys = get_grist(properties)

    for s in property_sets:
        # Handle value-less properties like '<architecture>' (compare with 
        # '<architecture>x86').

        set = feature.split(s)

        # Find the set of features that
        # - have no property specified in required property set 
        # - are omitted in build property set
        default_props = []
        for i in set:       
            # If $(i) is a value-less property it should match default 
            # value of an optional property. See the first line in the 
            # example below:
            #
            #  property set     properties     result
            # <a> <b>foo      <b>foo           match
            # <a> <b>foo      <a>foo <b>foo    no match
            # <a>foo <b>foo   <b>foo           no match
            # <a>foo <b>foo   <a>foo <b>foo    match
            if not (get_value(i) or get_grist(i) in prop_keys):
                default_props.append(i)

        # FIXME: can this be expressed in a more pythonic way?
        has_all = 1
        for i in set:
            if i not in (properties + default_props):
                has_all = 0
                break
        if has_all:
            return s

    return None
    

def register (toolset):
    """ Registers a new toolset.
    """
    feature.extend('toolset', [toolset])

def inherit_generators (toolset, properties, base, generators_to_ignore = []):
    if not properties:
        properties = [replace_grist (toolset, '<toolset>')]
        
    base_generators = generators.generators_for_toolset(base)
    
    for g in base_generators:
        id = g.id()
        
        if not id in generators_to_ignore:
            # Some generator names have multiple periods in their name, so
            # $(id:B=$(toolset)) doesn't generate the right new_id name.
            # e.g. if id = gcc.compile.c++, $(id:B=darwin) = darwin.c++,
            # which is not what we want. Manually parse the base and suffix
            # (if there's a better way to do this, I'd love to see it.)
            # See also register in module generators.
            (base, suffix) = split_action_id(id)

            new_id = toolset + '.' + suffix

            generators.register(g.clone(new_id, properties))

def inherit_flags(toolset, base, prohibited_properties = []):
    """Brings all flag definitions from the 'base' toolset into the 'toolset'
    toolset. Flag definitions whose conditions make use of properties in
    'prohibited-properties' are ignored. Don't confuse property and feature, for
    example <debug-symbols>on and <debug-symbols>off, so blocking one of them does
    not block the other one.
    
    The flag conditions are not altered at all, so if a condition includes a name,
    or version of a base toolset, it won't ever match the inheriting toolset. When
    such flag settings must be inherited, define a rule in base toolset module and
    call it as needed."""
    for f in __module_flags.get(base, []):
        
        if not f.condition or set.difference(f.condition, prohibited_properties):
            match = __re_first_group.match(f.rule)
            rule_ = None
            if match:
                rule_ = match.group(1)

            new_rule_or_module = ''

            if rule_:
                new_rule_or_module = toolset + '.' + rule_
            else:
                new_rule_or_module = toolset

            __add_flag (new_rule_or_module, f.variable_name, f.condition, f.values)

def inherit_rules (toolset, base):
    pass
    # FIXME: do something about this.
#    base_generators = generators.generators_for_toolset (base)

#    import action

#    ids = []
#    for g in base_generators:
#        (old_toolset, id) = split_action_id (g.id ())
#        ids.append (id) ;

#    new_actions = []

#    engine = get_manager().engine()
    # FIXME: do this!
#    for action in engine.action.values():
#        pass
#        (old_toolset, id) = split_action_id(action.action_name)
#    
#        if old_toolset == base:
#            new_actions.append ((id, value [0], value [1]))
#
#    for a in new_actions:
#        action.register (toolset + '.' + a [0], a [1], a [2])
        
    # TODO: how to deal with this?
#       IMPORT $(base) : $(rules) : $(toolset) : $(rules) : localized ;
#       # Import the rules to the global scope
#       IMPORT $(toolset) : $(rules) : : $(toolset).$(rules) ;
#   }
#   

######################################################################################
# Private functions

def __set_target_variables_aux (manager, rule_or_module, properties):
    """ Given a rule name and a property set, returns a list of tuples of
        variables names and values, which must be set on targets for that
        rule/properties combination. 
    """
    result = []

    for f in __flags.get(rule_or_module, []):
           
        if not f.condition or find_property_subset (f.condition, properties):
            processed = []
            for v in f.values:
                # The value might be <feature-name> so needs special
                # treatment.
                processed += __handle_flag_value (manager, v, properties)

            for r in processed:
                result.append ((f.variable_name, r))
    
    # strip away last dot separated part and recurse.
    next = __re_split_last_segment.match(rule_or_module)
    
    if next:
        result.extend(__set_target_variables_aux(
            manager, next.group(1), properties))

    return result

def __handle_flag_value (manager, value, properties):
    result = []
    
    if get_grist (value):
        matches = property.select (value, properties)
        for p in matches:
            att = feature.attributes (get_grist (p))
            
            ungristed = replace_grist (p, '')

            if 'dependency' in att:
                # the value of a dependency feature is a target
                # and must be actualized
                # FIXME: verify that 'find' actually works, ick!
                result.append (manager.targets ().find (ungristed).actualize ())

            elif 'path' in att or 'free' in att:
                values = []
                
                # Treat features with && in the value
                # specially -- each &&-separated element is considered
                # separate value. This is needed to handle searched
                # libraries, which must be in specific order.
                if not __re_two_ampersands.search (ungristed):
                    values.append (ungristed)

                else:
                    values.extend(value.split ('&&'))

                result.extend(values)
            else:
                result.append (ungristed)
    else:
        result.append (value)

    return result

def __add_flag (rule_or_module, variable_name, condition, values):
    """ Adds a new flag setting with the specified values.
        Does no checking.
    """
    f = Flag(variable_name, values, condition, rule_or_module)
    
    # Grab the name of the module
    m = __re_first_segment.match (rule_or_module)
    assert m
    module = m.group(1)

    __module_flags.setdefault(m, []).append(f)
    __flags.setdefault(rule_or_module, []).append(f)

def requirements():
    """Return the list of global 'toolset requirements'.
    Those requirements will be automatically added to the requirements of any main target."""
    return __requirements

def add_requirements(requirements):
    """Adds elements to the list of global 'toolset requirements'. The requirements
    will be automatically added to the requirements for all main targets, as if
    they were specified literally. For best results, all requirements added should
    be conditional or indirect conditional."""
    
    # FIXME:
    #if ! $(.ignore-requirements)
    #{
    __requirements.extend(requirements)
    #}
         
# Make toolset 'toolset', defined in a module of the same name,
# inherit from 'base'
# 1. The 'init' rule from 'base' is imported into 'toolset' with full
#    name. Another 'init' is called, which forwards to the base one.
# 2. All generators from 'base' are cloned. The ids are adjusted and 
#    <toolset> property in requires is adjusted too
# 3. All flags are inherited
# 4. All rules are imported.
def inherit(toolset, base):
    get_manager().projects().load_module(base, []);

    inherit_generators(toolset, [], base)
    inherit_flags(toolset, base)
    inherit_rules(toolset, base)
