# Status: mostly ported.
# TODO: carry over tests.
# Base revision: 56043
#
# Copyright 2001, 2002, 2003 Dave Abrahams 
# Copyright 2002, 2006 Rene Rivera 
# Copyright 2002, 2003, 2004, 2005, 2006 Vladimir Prus 
# Distributed under the Boost Software License, Version 1.0. 
# (See accompanying file LICENSE_1_0.txt or http://www.boost.org/LICENSE_1_0.txt) 

# TODO: stop using grists to identify the name of features?
#       create a class for Features and Properties?
#       represent conditions using object trees, composite pattern?

import re

from b2.util import set, utility
from b2.util.utility import add_grist, get_grist, ungrist, replace_grist, to_seq
from b2.exceptions import *

__re_split_subfeatures = re.compile ('<(.*):(.*)>')
__re_no_hyphen = re.compile ('^([^:]+)$')
__re_slash_or_backslash = re.compile (r'[\\/]')

def reset ():
    """ Clear the module state. This is mainly for testing purposes.
    """
    global __all_attributes, __all_features, __implicit_features, __composite_properties
    global __features_with_attributes, __subfeature_value_to_name, __all_top_features, __free_features
    global __all_subfeatures
        
    # The list with all attribute names.
    __all_attributes = [ 'implicit',
                        'executed',
                        'composite',
                        'optional',
                        'symmetric',
                        'free',
                        'incidental',
                        'path',
                        'dependency',
                        'propagated',
                        'link-incompatible',
                        'subfeature',
                        'order-sensitive'
                       ]
    
    # A map containing all features. The key is the gristed feature name. The value is a map with:
    #    'values': [],
    #    'attributes': [],
    #    'subfeatures': [],
    #    'default': None
    __all_features = {}
    
    # All non-subfeatures.
    __all_top_features = []
    
    # Maps valus to the corresponding implicit feature
    __implicit_features = {}
    
    # A map containing all composite properties. The key is the name of the property. The value is a map with:
    #    'components': []
    __composite_properties = {}
    
    __features_with_attributes = {}
    for attribute in __all_attributes:
        __features_with_attributes [attribute] = []
    
    # Maps a value to the corresponding subfeature name.
    __subfeature_value_to_name = {}
    
    # All free features
    __free_features = []

    __all_subfeatures = []

reset ()

def enumerate ():
    """ Returns an iterator to the features map.
    """
    return __all_features.iteritems ()

# FIXME: prepare-test/finish-test?

def feature (name, values, attributes = []):
    """ Declares a new feature with the given name, values, and attributes.
        name: the feature name
        values: a sequence of the allowable values - may be extended later with feature.extend
        attributes: a sequence of the feature's attributes (e.g. implicit, free, propagated, ...)
    """
    name = add_grist (name)

    __validate_feature_attributes (name, attributes)

    feature = { 
        'values': [],
        'attributes': attributes,
        'subfeatures': [],
        'default': None
        }
    __all_features [name] = feature
    
    feature ['attributes'] = attributes
    
    for attribute in attributes:
        __features_with_attributes [attribute].append (name)
        
    if 'subfeature' in attributes:
        __all_subfeatures.append(name)
    else:
        __all_top_features.append(name)

    extend (name, values)

    # FIXME: why his is needed.
    if 'free' in attributes:
        __free_features.append (name)

def set_default (feature, value):
    """ Sets the default value of the given feature, overriding any previous default.
        feature: the name of the feature
        value: the default value to assign
    """

    if isinstance(feature, list):
        feature = feature[0]

    feature = add_grist (feature)
    f = __all_features [feature]
    attributes = f['attributes']
    bad_attribute = None

    if "free" in attributes:
        bad_attribute = "free"
    elif "optional" in attributes:
        bad_attribute = "optional"
        
    if bad_attribute:
        raise InvalidValue ("%s property %s cannot have a default" % (bad_attribute, feature))
        
    if isinstance(value, list):
        value = value[0]

    values = f['values']
    if not value in values:
        raise InvalidValue ("The specified default value, '%s' is invalid.\n" % value + "allowed values are: %s" % values)

    f ['default'] = value

def defaults (features):
    """ Returns the default property values for the given features.
    """
    result = []
    for f in features:
        attributes = __all_features [f]['attributes']
        if not 'free' in attributes and not 'optional' in attributes:
            defaults = __all_features [f]['default']
            if defaults:
                result.append (replace_grist (defaults, f))

    return result

def valid (names):
    """ Returns true iff all elements of names are valid features.
    """
    def valid_one (name): return __all_features.has_key (name)
        
    if isinstance (names, str):
        return valid_one (names)
    else:
        return [ valid_one (name) for name in names ]

def attributes (feature):
    """ Returns the attributes of the given feature.
    """
    return __all_features [feature]['attributes']
        
def values (feature):
    """ Return the values of the given feature.
    """
    validate_feature (feature)
    return __all_features [feature]['values']

def is_implicit_value (value_string):
    """ Returns true iff 'value_string' is a value_string
    of an implicit feature.
    """
    v = value_string.split('-')
    
    if not __implicit_features.has_key(v[0]):
        return False

    feature = __implicit_features[v[0]]
    
    for subvalue in (v[1:]):
        if not __find_implied_subfeature(feature, subvalue, v[0]):
            return False
            
    return True

def implied_feature (implicit_value):
    """ Returns the implicit feature associated with the given implicit value.
    """
    components = implicit_value.split('-')
    
    if not __implicit_features.has_key(components[0]):
        raise InvalidValue ("'%s' is not a value of an implicit feature" % implicit_value)
        
    return __implicit_features[components[0]]

def __find_implied_subfeature (feature, subvalue, value_string):
    feature = add_grist (feature)
    if value_string == None: value_string = ''

    if not __subfeature_value_to_name.has_key (feature) \
        or not __subfeature_value_to_name [feature].has_key (value_string) \
        or not __subfeature_value_to_name [feature][value_string].has_key (subvalue):
        return None
        
    return __subfeature_value_to_name[feature][value_string][subvalue]

# Given a feature and a value of one of its subfeatures, find the name
# of the subfeature. If value-string is supplied, looks for implied
# subfeatures that are specific to that value of feature
#  feature             # The main feature name
#  subvalue            # The value of one of its subfeatures
#  value-string        # The value of the main feature

def implied_subfeature (feature, subvalue, value_string):
    result = __find_implied_subfeature (feature, subvalue, value_string)
    if not result:
        raise InvalidValue ("'%s' is not a known subfeature value of '%s%s'" % (subvalue, feature, value_string))

    return result

def validate_feature (name):
    """ Checks if all name is a valid feature. Otherwise, raises an exception.
    """
    x = valid (name)
    if not x:
        raise InvalidFeature ("'%s' is not a valid feature name" % name)

def valid (names):
    """ Returns true iff all elements of names are valid features.
    """
    def valid_one (name): return __all_features.has_key (name)
        
    if isinstance (names, str):
        return valid_one (names)
    else:
        return [ valid_one (name) for name in names ]

def __expand_subfeatures_aux (feature, value, dont_validate = False):
    """ Helper for expand_subfeatures.
        Given a feature and value, or just a value corresponding to an
        implicit feature, returns a property set consisting of all component
        subfeatures and their values. For example:
        
          expand_subfeatures <toolset>gcc-2.95.2-linux-x86
              -> <toolset>gcc <toolset-version>2.95.2 <toolset-os>linux <toolset-cpu>x86
          equivalent to:
              expand_subfeatures gcc-2.95.2-linux-x86

        feature:        The name of the feature, or empty if value corresponds to an implicit property
        value:          The value of the feature.
        dont_validate:  If True, no validation of value string will be done.
    """
    if not feature:
        feature = implied_feature(value)
    else:
        validate_feature(feature)

    if not dont_validate:
        validate_value_string(feature, value)
    
    components = value.split ("-")
    
    # get the top-level feature's value
    value = replace_grist(components[0], '')

    result = [ replace_grist(components[0], feature) ]
    
    subvalues = components[1:]

    while len(subvalues) > 0:
        subvalue = subvalues [0]    # pop the head off of subvalues
        subvalues = subvalues [1:]
        
        subfeature = __find_implied_subfeature (feature, subvalue, value)
        
        # If no subfeature was found, reconstitute the value string and use that
        if not subfeature:
            result = '-'.join(components)
            result = replace_grist (result, feature)
            return [result]
            
        f = ungrist (feature)
        # FIXME: why grist includes '<>'?
        result.append (replace_grist (subvalue, '<' + f + '-' + subfeature + '>'))
    
    return result

def expand_subfeatures (properties, dont_validate = False):
    """
    Make all elements of properties corresponding to implicit features
    explicit, and express all subfeature values as separate properties
    in their own right. For example, the property
    
       gcc-2.95.2-linux-x86
    
    might expand to
    
      <toolset>gcc <toolset-version>2.95.2 <toolset-os>linux <toolset-cpu>x86

    properties:     A sequence with elements of the form
                    <feature>value-string or just value-string in the
                    case of implicit features.
  : dont_validate:  If True, no validation of value string will be done.
    """
    result = []
    for p in properties:
        p_grist = get_grist (p)
        # Don't expand subfeatures in subfeatures
        if ':' in p_grist:
            result.append (p)
        else:
            result.extend (__expand_subfeatures_aux (p_grist, replace_grist (p, ''), dont_validate))

    return result



# rule extend was defined as below:
    # Can be called three ways:
    #
    #    1. extend feature : values *
    #    2. extend <feature> subfeature : values *
    #    3. extend <feature>value-string subfeature : values *
    #
    # * Form 1 adds the given values to the given feature
    # * Forms 2 and 3 add subfeature values to the given feature
    # * Form 3 adds the subfeature values as specific to the given
    #   property value-string.
    #
    #rule extend ( feature-or-property subfeature ? : values * )
#
# Now, the specific rule must be called, depending on the desired operation:
#   extend_feature
#   extend_subfeature

def extend (name, values):
    """ Adds the given values to the given feature.
    """
    name = add_grist (name)
    __validate_feature (name)
    feature = __all_features [name]
    
    if 'implicit' in feature ['attributes']:
        for v in values:
            if __implicit_features.has_key (v):
                raise BaseException ("'%s' is already associated with the feature '%s'" % (v, __implicit_features [v]))

            __implicit_features[v] = name

    if len (feature ['values']) == 0 and len (values) > 0:
        # This is the first value specified for this feature,
        # take it as default value
        feature ['default'] = values[0]

    feature['values'].extend (values)

def validate_value_string (feature, value_string):
    """ Checks that value-string is a valid value-string for the given feature.
    """
    f = __all_features [feature]
    if 'free' in f ['attributes'] or value_string in f ['values']:
        return

    values = [value_string]

    if f['subfeatures']:
        if not value_string in f['subfeatures']:
            values = value_string.split('-')

    # An empty value is allowed for optional features
    if not values[0] in f['values'] and \
           (values[0] or not 'optional' in f['attributes']):
        raise InvalidValue ("'%s' is not a known value of feature '%s'\nlegal values: '%s'" % (values [0], feature, f ['values']))

    for v in values [1:]:
        # this will validate any subfeature values in value-string
        implied_subfeature(feature, v, values[0])


""" Extends the given subfeature with the subvalues.  If the optional
    value-string is provided, the subvalues are only valid for the given
    value of the feature. Thus, you could say that
    <target-platform>mingw is specifc to <toolset>gcc-2.95.2 as follows:
    
          extend-subfeature toolset gcc-2.95.2 : target-platform : mingw ;

    feature:        The feature whose subfeature is being extended.
    
    value-string:   If supplied, specifies a specific value of the
                    main feature for which the new subfeature values
                    are valid.
    
    subfeature:     The name of the subfeature.
    
    subvalues:      The additional values of the subfeature being defined.
"""
def extend_subfeature (feature, value_string, subfeature, subvalues):
    feature = add_grist (feature)
    validate_feature (feature)

    if value_string:
        validate_value_string (feature, value_string)

    subfeature_name = __get_subfeature_name (subfeature, value_string)
    
    f = ungrist (feature)
    extend (f + '-' + subfeature_name, subvalues) ;
    
    __add_to_subfeature_value_to_name_map (feature, value_string, subfeature_name, subvalues)

def subfeature (feature_name, value_string, subfeature, subvalues, attributes = []):
    """ Declares a subfeature.
        feature_name:   Root feature that is not a subfeature.
        value_string:   An optional value-string specifying which feature or
                        subfeature values this subfeature is specific to,
                        if any.                
        subfeature:     The name of the subfeature being declared.
        subvalues:      The allowed values of this subfeature.
        attributes:     The attributes of the subfeature.
    """
    feature_name = add_grist (feature_name)
    validate_feature (feature_name)
    
    # Add grist to the subfeature name if a value-string was supplied
    subfeature_name = __get_subfeature_name (subfeature, value_string)
    
    if subfeature_name in __all_features [feature_name]['subfeatures']:
        message = "'%s' already declared as a subfeature of '%s'" % (subfeature, feature_name)
        message += " specific to '%s'" % value_string
        raise BaseException (message)

    __all_features [feature_name]['subfeatures'].append (subfeature_name)

    # First declare the subfeature as a feature in its own right
    f = ungrist (feature_name)
    feature (f + '-' + subfeature_name, subvalues, attributes + ['subfeature'])
    
    # Now make sure the subfeature values are known.
    extend_subfeature (feature_name, value_string, subfeature, subvalues)

def compose (composite_property, component_properties):
    """ Sets the components of the given composite property.
    """
    component_properties = to_seq (component_properties)

    feature = get_grist (composite_property)
    if not 'composite' in attributes (feature):
        raise BaseException ("'%s' is not a composite feature" % feature)

    if __composite_properties.has_key (composite_property):
        raise BaseException ('components of "%s" already set: %s' % (composite_property, str (__composite_properties [composite_property]['components'])))

    if composite_property in component_properties:
        raise BaseException ('composite property "%s" cannot have itself as a component' % composite_property)

    entry = { 'components': component_properties }
    __composite_properties [composite_property] = entry


def expand_composite (property):
    result = [ property ]
    if __composite_properties.has_key (property):
        for p in __composite_properties [property]['components']:
            result.extend (expand_composite (p))
    return result


def get_values (feature, properties):
    """ Returns all values of the given feature specified by the given property set.
    """
    result = []
    for p in properties:
        if get_grist (p) == feature:
            result.append (replace_grist (p, ''))
    
    return result

def free_features ():
    """ Returns all free features.
    """
    return __free_features

def expand_composites (properties):
    """ Expand all composite properties in the set so that all components
        are explicitly expressed.
    """
    explicit_features = get_grist (properties)

    result = []

    # now expand composite features
    for p in properties:
        expanded = expand_composite (p)

        for x in expanded:
            if not x in result:
                f = get_grist (x)

                if f in __free_features:
                    result.append (x)
                elif not x in properties:  # x is the result of expansion
                    if not f in explicit_features:  # not explicitly-specified
                        if f in get_grist (result):
                            raise FeatureConflict ("expansions of composite features result in "
                            "conflicting values for '%s'\nvalues: '%s'\none contributing composite property was '%s'" % (f, 
                            get_values (f, result) + [replace_grist (x, '')], p))
                        else:
                            result.append (x)
                elif f in get_grist (result):
                    raise FeatureConflict ("explicitly-specified values of non-free feature '%s' conflict\n"
                    "existing values: '%s'\nvalue from expanding '%s': '%s'" % (f, 
                    get_values (f, properties), p, replace_grist (x, '')))
                else:
                    result.append (x)

    return result

def is_subfeature_of (parent_property, f):
    """ Return true iff f is an ordinary subfeature of the parent_property's
        feature, or if f is a subfeature of the parent_property's feature
        specific to the parent_property's value.
    """
    if not valid (f) or not 'subfeature' in __all_features [f]['attributes']:
        return False

    specific_subfeature = __re_split_subfeatures.match (f)

    if specific_subfeature:
        # The feature has the form
        # <topfeature-topvalue:subfeature>,
        # e.g. <toolset-msvc:version>
        feature_value = split_top_feature(specific_subfeature.group(1))
        if replace_grist (feature_value [1], '<' + feature_value [0] + '>') == parent_property:
            return True
    else:
        # The feature has the form <topfeature-subfeature>,
        # e.g. <toolset-version>
        top_sub = split_top_feature (ungrist (f))

        if top_sub [1] and add_grist (top_sub [0]) == get_grist (parent_property):
            return True

    return False

def __is_subproperty_of (parent_property, p):
    """ As is_subfeature_of, for subproperties.
    """
    return is_subfeature_of (parent_property, get_grist (p))

    
# Returns true iff the subvalue is valid for the feature.  When the
# optional value-string is provided, returns true iff the subvalues
# are valid for the given value of the feature.
def is_subvalue(feature, value_string, subfeature, subvalue):

    if not value_string:
        value_string = ''

    if not __subfeature_value_to_name.has_key(feature):
        return False
        
    if not __subfeature_value_to_name[feature].has_key(value_string):
        return False
        
    if not __subfeature_value_to_name[feature][value_string].has_key(subvalue):
        return False

    if __subfeature_value_to_name[feature][value_string][subvalue]\
           != subfeature:
        return False

    return True




def implied_subfeature (feature, subvalue, value_string):
    result = __find_implied_subfeature (feature, subvalue, value_string)
    if not result:
        raise InvalidValue ("'%s' is not a known subfeature value of '%s%s'" % (subvalue, feature, value_string))

    return result


def expand (properties):
    """ Given a property set which may consist of composite and implicit
        properties and combined subfeature values, returns an expanded,
        normalized property set with all implicit features expressed
        explicitly, all subfeature values individually expressed, and all
        components of composite properties expanded. Non-free features
        directly expressed in the input properties cause any values of
        those features due to composite feature expansion to be dropped. If
        two values of a given non-free feature are directly expressed in the
        input, an error is issued.
    """
    expanded = expand_subfeatures (properties)
    return expand_composites (expanded)
    

def split_top_feature (feature_plus):
    """ Given an ungristed string, finds the longest prefix which is a
        top-level feature name followed by a dash, and return a pair
        consisting of the parts before and after that dash.  More
        interesting than a simple split because feature names can contain
        dashes.
    """
    e = feature_plus.split ('-')
    f = e [0]

    v = None
    while e:
        if add_grist (f) in __all_top_features:
            if len (e) > 1:
                after = '-'.join (e [1:])
            else:
                after = ''
                
            v = (f, after)

        e = e [1:]
        f = f + '-'
        if len (e): f += e [0]

    return v

def add_defaults (properties):
    """ Given a set of properties, add default values for features not
        represented in the set. 
        Note: if there's there's ordinary feature F1 and composite feature
        F2, which includes some value for F1, and both feature have default values,
        then the default value of F1 will be added, not the value in F2. This might
        not be right idea: consider
        
          feature variant : debug ... ;
               <variant>debug : .... <runtime-debugging>on
          feature <runtime-debugging> : off on ;
          
          Here, when adding default for an empty property set, we'll get
        
            <variant>debug <runtime_debugging>off
         
          and that's kind of strange.        
    """
    result = [ x for x in properties ]
    
    for v in replace_grist (properties, ''):
        if v in properties:
            raise BaseException ("'add_defaults' requires explicitly specified features, but '%s' appears to be the value of an un-expanded implicit feature" % v)

    # We don't add default for elements with ":" inside. This catches:
    # 1. Conditional properties --- we don't want <variant>debug:<define>DEBUG
    #    to be takes as specified value for <variant>
    # 2. Free properties with ":" in values. We don't care, since free properties
    #    don't have defaults.
    xproperties = [ property for property in properties if __re_no_hyphen.match (property) ]
    missing_top = set.difference (__all_top_features, get_grist (xproperties))
    more =  defaults (missing_top)
    result += more
    xproperties += more
    
    # Add defaults for subfeatures of features which are present
    for p in xproperties:
        gp = get_grist (p)
        s = []
        if __all_features.has_key (gp):
            s = __all_features [gp]['subfeatures']
        f = ungrist (gp)
        
        xbase = ['<%s-%s>' % (f, xs) for xs in s]
            
        missing_subs = set.difference (xbase, get_grist (result))
        result += defaults (__select_subfeatures (p, missing_subs))
    
    return result

def minimize (properties):
    """ Given an expanded property set, eliminate all redundancy: properties
        which are elements of other (composite) properties in the set will
        be eliminated. Non-symmetric properties equal to default values will be
        eliminated, unless the override a value from some composite property.
        Implicit properties will be expressed without feature
        grist, and sub-property values will be expressed as elements joined
        to the corresponding main property.
    """    
# FXIME: the code below was in the original feature.jam file, however 'p' is not defined.
#       # Precondition checking
#       local implicits = [ set.intersection $(p:G=) : $(p:G) ] ;
#       if $(implicits)
#       {
#           error minimize requires an expanded property set, but \"$(implicits[1])\"
#             appears to be the value of an un-expanded implicit feature ;
#       }
           
    # remove properties implied by composite features
    components = []
    for property in properties:
        if __composite_properties.has_key (property):
            components.extend (__composite_properties [property]['components'])
    
    x = set.difference (properties, components)
    
    # handle subfeatures and implicit features
    x = __move_subfeatures_to_the_end (x)
    
    result = []
    while x:
        fullp = x [0]
        p = fullp
        f = get_grist (p)
        v = replace_grist (p, '')
        
        # eliminate features in implicit properties.
        if 'implicit' in __all_features [f]['attributes']:
            p = v

        # locate all subproperties of $(x[1]) in the property set
        subproperties = __select_subproperties (fullp, x)
        
        if subproperties:
            # reconstitute the joined property name
            subproperties.sort ()
            joined = p + '-' + '-'.join (replace_grist (subproperties, ''))
            result.append (joined)

            x = set.difference (x [1:], subproperties)

        else:
            # eliminate properties whose value is equal to feature's
            # default and which are not symmetric and which do not
            # contradict values implied by composite properties.
            
            # since all component properties of composites in the set
            # have been eliminated, any remaining property whose
            # feature is the same as a component of a composite in the
            # set must have a non-redundant value.
            if [fullp] != defaults ([f]) or 'symmetric' in attributes (f)\
                   or get_grist (fullp) in get_grist (components):
                result.append (p)

            x = x [1:]

    return result


def split (properties):
    """ Given a property-set of the form
        v1/v2/...vN-1/<fN>vN/<fN+1>vN+1/...<fM>vM

    Returns
        v1 v2 ... vN-1 <fN>vN <fN+1>vN+1 ... <fM>vM

    Note that vN...vM may contain slashes. This is resilient to the
    substitution of backslashes for slashes, since Jam, unbidden,
    sometimes swaps slash direction on NT.
    """

    def split_one (properties):
        pieces = re.split (__re_slash_or_backslash, properties)
        result = []
        
        for x in pieces:
            if not get_grist (x) and len (result) > 0 and get_grist (result [-1]):
                result = result [0:-1] + [ result [-1] + '/' + x ]
            else:
                result.append (x)
        
        return result

    if isinstance (properties, str):
        return split_one (properties)

    result = []
    for p in properties:
        result += split_one (p)
    return result
    

def compress_subproperties (properties):
    """ Combine all subproperties into their parent properties

        Requires: for every subproperty, there is a parent property.  All
        features are explicitly expressed.
        
        This rule probably shouldn't be needed, but
        build-request.expand-no-defaults is being abused for unintended
        purposes and it needs help
    """
    result = []
    matched_subs = []
    for p in properties:
        pg = get_grist (p)
        if not pg:
            raise BaseException ("Gristed variable exppected. Got '%s'." % p)
        
        if not 'subfeature' in __all_features [pg]['attributes']:
            subs = __select_subproperties (p, properties)
            
            matched_subs.extend (subs)

            subvalues = '-'.join (get_value (subs))
            if subvalues: subvalues = '-' + subvalues

            result.append (p + subvalues)

        else:
            all_subs.append (p)

    # TODO: this variables are used just for debugging. What's the overhead?
    assert (set.equal (all_subs, matched_subs))

    return result

######################################################################################
# Private methods

def __select_subproperties (parent_property, properties):
    return [ x for x in properties if __is_subproperty_of (parent_property, x) ]

def __move_subfeatures_to_the_end (properties):
    """ Helper for minimize, below - returns the list with
        the same properties, but where all subfeatures
        are in the end of the list
    """
    x1 = []
    x2 = []
    for p in properties:
        if 'subfeature' in __all_features [get_grist (p)]['attributes']:
            x2.append (p)

        else:
            x1.append (p)
            
    return x1 + x2

def __get_subfeature_name (subfeature, value_string):
    if value_string == None: 
        prefix = ''
    else:
        prefix = value_string + ':'

    return prefix + subfeature


def __validate_feature_attributes (name, attributes):
    for attribute in attributes:
        if not attribute in __all_attributes:
            raise InvalidAttribute ("unknown attributes: '%s' in feature declaration: '%s'" % (str (set.difference (attributes, __all_attributes)), name))
    
    if name in __all_features:
            raise AlreadyDefined ("feature '%s' already defined" % name)
    elif 'implicit' in attributes and 'free' in attributes:
        raise InvalidAttribute ("free features cannot also be implicit (in declaration of feature '%s')" % name)
    elif 'free' in attributes and 'propagated' in attributes:
        raise InvalidAttribute ("free features cannot also be propagated (in declaration of feature '%s')" % name)

    
def __validate_feature (feature):
    """ Generates an error if the feature is unknown.
    """
    if not __all_features.has_key (feature):
        raise BaseException ('unknown feature "%s"' % feature)

def __add_to_subfeature_value_to_name_map (feature, value_string, subfeature_name, subvalues):
    # provide a way to get from the given feature or property and
    # subfeature value to the subfeature name.
    if value_string == None: value_string = ''
    
    if not __subfeature_value_to_name.has_key (feature):
        __subfeature_value_to_name [feature] = {}
        
    if not __subfeature_value_to_name [feature].has_key (value_string):
        __subfeature_value_to_name [feature][value_string] = {}
        
    for subvalue in subvalues:
        __subfeature_value_to_name [feature][value_string][subvalue] = subfeature_name
    

def __select_subfeatures (parent_property, features):
    """ Given a property, return the subset of features consisting of all
        ordinary subfeatures of the property's feature, and all specific
        subfeatures of the property's feature which are conditional on the
        property's value.
    """
    return [f for f in features if is_subfeature_of (parent_property, f)]
  
# FIXME: copy over tests.
