//============================================================================
//ZedGraph Class Library - A Flexible Line Graph/Bar Graph Library in C#
//Copyright © 2005  John Champion
//
//This library is free software; you can redistribute it and/or
//modify it under the terms of the GNU Lesser General Public
//License as published by the Free Software Foundation; either
//version 2.1 of the License, or (at your option) any later version.
//
//This library is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//Lesser General Public License for more details.
//
//You should have received a copy of the GNU Lesser General Public
//License along with this library; if not, write to the Free Software
//Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Linq;
using System.Windows.Forms;

namespace ZedGraph
{
    /// <summary>
    /// The Dendrogram class inherits from the <see cref="Scale" /> class, and implements
    /// the features specific to <see cref="AxisType.Dendrogram" />.
    /// </summary>
    /// <remarks>
    /// Dendrogram is the normal, default cartesian axis.
    /// </remarks>
    /// 
    /// <author> John Champion  </author>
    /// <version> $Revision: 1.10 $ $Date: 2007-04-16 00:03:02 $ </version>
    [Serializable]
    public class UserDefinedScale : Scale, ISerializable //, ICloneable
    {
        private Axis _owner;

        #region constructors

        /// <summary>
        /// Default constructor that defines the owner <see cref="Axis" />
        /// (containing object) for this new object.
        /// </summary>
        /// <param name="owner">The owner, or containing object, of this instance</param>
        public UserDefinedScale(Axis owner)
            : base(owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// The Copy Constructor
        /// </summary>
        /// <param name="rhs">The <see cref="ZedGraph.DendrogramScale" /> object from which to copy</param>
        /// <param name="owner">The <see cref="Axis" /> object that will own the
        /// new instance of <see cref="ZedGraph.DendrogramScale" /></param>
        public UserDefinedScale(Scale rhs, Axis owner)
            : base(rhs, owner)
        {
        }


        /// <summary>
        /// Create a new clone of the current item, with a new owner assignment
        /// </summary>
        /// <param name="owner">The new <see cref="Axis" /> instance that will be
        /// the owner of the new Scale</param>
        /// <returns>A new <see cref="Scale" /> clone.</returns>
        public override Scale Clone(Axis owner)
        {
            return new UserDefinedScale(this, owner);
        }
        #endregion

        #region properties

        /// <summary>
        /// Return the <see cref="AxisType" /> for this <see cref="Scale" />, which is
        /// <see cref="AxisType.UserDefined" />.
        /// </summary>
        public override AxisType Type
        {
            get { return AxisType.UserDefined; }
        }

        #endregion
        

        #region Serialization
        /// <summary>
        /// Current schema value that defines the version of the serialized file
        /// </summary>
        public const int schema2 = 10;

        /// <summary>
        /// Constructor for deserializing objects
        /// </summary>
        /// <param name="info">A <see cref="SerializationInfo"/> instance that defines the serialized data
        /// </param>
        /// <param name="context">A <see cref="StreamingContext"/> instance that contains the serialized data
        /// </param>
        protected UserDefinedScale(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // The schema value is just a file version parameter.  You can use it to make future versions
            // backwards compatible as new member variables are added to classes
            int sch = info.GetInt32("schema2");

        }
        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> instance with the data needed to serialize the target object
        /// </summary>
        /// <param name="info">A <see cref="SerializationInfo"/> instance that defines the serialized data</param>
        /// <param name="context">A <see cref="StreamingContext"/> instance that contains the serialized data</param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("schema2", schema2);
        }
        #endregion

    }
}
