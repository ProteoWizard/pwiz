# Status: ported.
# Base revision: 40480

#  Copyright (C) Vladimir Prus 2002. Permission to copy, use, modify, sell and
#  distribute this software is granted provided this copyright notice appears in
#  all copies. This software is provided "as is" without express or implied
#  warranty, and with no claim as to its suitability for any purpose.

from b2.util.utility import *
import property, feature, string
from b2.exceptions import *
from b2.util.sequence import unique
from b2.util.set import difference

def reset ():
    """ Clear the module state. This is mainly for testing purposes.
    """
    global __cache

    # A cache of property sets
    # TODO: use a map of weak refs?
    __cache = {}
    
reset ()


def create (raw_properties = []):
    """ Creates a new 'PropertySet' instance for the given raw properties,
        or returns an already existing one.
    """
    raw_properties.sort ()
    raw_properties = unique (raw_properties)
 
    key = '-'.join (raw_properties)

    if not __cache.has_key (key):
        __cache [key] = PropertySet (raw_properties)

    return __cache [key]

def create_with_validation (raw_properties):
    """ Creates new 'PropertySet' instances after checking
        that all properties are valid and converting incidental
        properties into gristed form.
    """
    property.validate (raw_properties)
    
    return create (property.make (raw_properties))

def empty ():
    """ Returns PropertySet with empty set of properties.
    """
    return create ()

def create_from_user_input(raw_properties, jamfile_module, location):
    """Creates a property-set from the input given by the user, in the
    context of 'jamfile-module' at 'location'"""

    property.validate(raw_properties)

    specification = property.translate_paths(raw_properties, location)
    specification = property.translate_indirect(specification, jamfile_module)
    specification = property.expand_subfeatures_in_conditions(specification)
    specification = property.make(specification)
    return create(specification)


def refine_from_user_input(parent_requirements, specification, jamfile_module,
                           location):
    """Refines requirements with requirements provided by the user.
    Specially handles "-<property>value" syntax in specification
     to remove given requirements.
     - parent-requirements -- property-set object with requirements
       to refine
     - specification -- string list of requirements provided by the use
     - project-module -- the module to which context indirect features
       will be bound.
     - location -- the path to which path features are relative."""


    if not specification:
        return parent_requirements

    
    add_requirements = []
    remove_requirements = []

    for r in specification:
        if r[0] == '-':
            remove_requirements.append(r[1:])
        else:
            add_requirements.append(r)
        
        if remove_requirements:
            # Need to create property set, so that path features
            # and indirect features are translated just like they
            # are in project requirements.
            ps = create_from_user_input(remove_requirements,
                                        jamfile_module, location)
            
            parent_requirements = create(difference(parent_requirements.raw(),
                                                    ps.raw()))
            specification = add_requirements

        requirements = create_from_user_input(specification,
                                              jamfile_module, location)
        
        return parent_requirements.refine(requirements)
    
class PropertySet:
    """ Class for storing a set of properties.
        - there's 1<->1 correspondence between identity and value. No
          two instances of the class are equal. To maintain this property,
          the 'PropertySet.create' rule should be used to create new instances.
          Instances are immutable.

        - each property is classified with regard to it's effect on build
          results. Incidental properties have no effect on build results, from
          Boost.Build point of view. Others are either free, or non-free, which we
          call 'base'. Each property belong to exactly one of those categories and
          it's possible to get list of properties in each category.

          In addition, it's possible to get list of properties with specific
          attribute.

        - several operations, like and refine and as_path are provided. They all use
          caching whenever possible.
    """
    def __init__ (self, raw_properties = []):

        self.raw_ = raw_properties
        
        self.incidental_ = []
        self.free_ = []
        self.base_ = []
        self.dependency_ = []
        self.non_dependency_ = []
        self.conditional_ = []
        self.non_conditional_ = []
        self.propagated_ = []
        self.link_incompatible = []
        
        # A cache of refined properties.
        self.refined_ = {}
        
        # A cache of property sets created by adding properties to this one.
        self.added_ = {}

        # Cache for the default properties.
        self.defaults_ = None

        # Cache for the expanded properties.
        self.expanded_ = None

        # Cache for the expanded composite properties
        self.composites_ = None

        # Cache for the property set containing propagated properties.
        self.propagated_ps_ = None
        
        # A map of features to its values.
        self.feature_map_ = None
        
        # A tuple (target path, is relative to build directory)
        self.target_path_ = None
        
        self.as_path_ = None
        
        # A cache for already evaluated sets.
        self.evaluated_ = {}
        
        for p in raw_properties:
            if not get_grist (p):
                raise BaseException ("Invalid property: '%s'" % p)
            
            att = feature.attributes (get_grist (p))
            
            # A feature can be both incidental and free,
            # in which case we add it to incidental.
            if 'incidental' in att:
                self.incidental_.append (p)
            elif 'free' in att:
                self.free_.append (p)
            else:
                self.base_.append (p)
        
            if 'dependency' in att:
                self.dependency_.append (p)
            else:
                self.non_dependency_.append (p)
            
            if property.is_conditional (p):
                self.conditional_.append (p)
            else:
                self.non_conditional_.append (p)
                                    
            if 'propagated' in att:
                self.propagated_.append (p)

            if 'link_incompatible' in att:
                self.link_incompatible.append (p)
    
    def raw (self):
        """ Returns the list of stored properties.
        """
        return self.raw_

    def __str__(self):
        return string.join(self.raw_)
    
    def base (self):
        """ Returns properties that are neither incidental nor free.
        """
        return self.base_
    
    def free (self):
        """ Returns free properties which are not dependency properties.
        """
        return self.free_

    def dependency (self):
        """ Returns dependency properties.
        """
        return self.dependency_
    
    def non_dependency (self):
        """ Returns properties that are not dependencies.
        """
        return self.non_dependency_
    
    def conditional (self):
        """ Returns conditional properties.
        """
        return self.conditional_
        
    def non_conditional (self):
        """ Returns properties that are not conditional.
        """
        return self.non_conditional_
              
    def incidental (self):
        """ Returns incidental properties.
        """
        return self.incidental_
    
    def refine (self, requirements):
        """ Refines this set's properties using the requirements passed as an argument.
        """
        str_req = str (requirements)
        if not self.refined_.has_key (str_req):
            r = property.refine (self.raw (), requirements.raw ())

            self.refined_ [str_req] = create (r)

        return self.refined_ [str_req]

    def expand (self):
        if not self.expanded_:
            expanded = feature.expand (self.raw_)
            self.expanded_ = create (expanded)
        return self.expanded_

    def expand_componsite(self):
        if not self.componsites_:
            self.composites_ = create(feature.expand_composires(self.raw_))
        return self.composites_

    def evaluate_conditionals(self, context=None):
        if not context:
            context = self

        if not self.evaluated_.has_key(context):
            self.evaluated_[context] = create(
                property.evaluate_conditionals_in_context(self.raw_,
                                                          context.raw()))

        return self.evaluated_[context]

    def propagated (self):
        if not self.propagated_ps_:
            self.propagated_ps_ = create (self.propagated_)
        return self.propagated_ps_

    def add_defaults (self):
        if not self.defaults_:
            expanded = feature.add_defaults(self.raw_)
            self.defaults_ = create(expanded)
        return self.defaults_

    def as_path (self):
        if not self.as_path_:
            self.as_path_ = property.as_path(self.base_)

        return self.as_path_

    def target_path (self):
        """ Computes the target path that should be used for 
            target with these properties.
            Returns a tuple of
              - the computed path
              - if the path is relative to build directory, a value of
                'true'. 
        """
        if not self.target_path_:
            # The <location> feature can be used to explicitly
            # change the location of generated targets
            l = self.get ('<location>')
            if l:
                computed = l
                is_relative = False

            else:
                p = self.as_path ()
                
                # Really, an ugly hack. Boost regression test system requires
                # specific target paths, and it seems that changing it to handle
                # other directory layout is really hard. For that reason,
                # we teach V2 to do the things regression system requires.
                # The value o '<location-prefix>' is predended to the path.
                prefix = self.get ('<location-prefix>')
                
                if prefix:
                    if len (prefix) > 1:
                        raise AlreadyDefined ("Two <location-prefix> properties specified: '%s'" % prefix)
                        
                    computed = os.path.join(prefix[0], p)

                else:
                    computed = p

                if not computed:
                    computed = "."

                is_relative = True

            self.target_path_ = (computed, is_relative)
            
        return self.target_path_
                    
    def add (self, ps):
        """ Creates a new property set containing the properties in this one,
            plus the ones of the property set passed as argument.
        """
        if not self.added_.has_key (str (ps)):
            self.added_ [str (ps)] = create (self.raw_ + ps.raw ())
        return self.added_ [str (ps)]
    
    def add_raw (self, properties):
        """ Creates a new property set containing the properties in this one,
            plus the ones passed as argument.
        """
        return self.add (create (properties))

    
    def get (self, feature):
        """ Returns all values of 'feature'.
        """
        if not self.feature_map_:
            self.feature_map_ = {}

            for v in self.raw_:
                key = get_grist (v)
                if not self.feature_map_.has_key (key):
                    self.feature_map_ [key] = []
                self.feature_map_ [get_grist (v)].append (replace_grist (v, ''))
        
        return self.feature_map_.get (feature, [])
    
