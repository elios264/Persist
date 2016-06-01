using System;

namespace elios.Persist
{
    /// <summary>
    /// An attribute is composed by a <see cref="Name"/> and a <see cref="Value"/>
    /// </summary>
    public class NodeAttribute
    {
        /// <summary>
        /// Name of the attribute
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// value of the attribute
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// creates a <see cref="NodeAttribute"/> with the specified name and value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public NodeAttribute(string name, IConvertible value)
        {
            Name = name;
            Value = value.ToString(Archive.Provider);
        }

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        protected bool Equals(NodeAttribute other)
        {
            return string.Equals(Name, other.Name) && string.Equals(Value, other.Value);
        }
        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((NodeAttribute)obj);
        }
        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return ( ( Name?.GetHashCode() ?? 0 ) * 397 ) ^ ( Value?.GetHashCode() ?? 0 );
            }
        }

        /// <summary>
        /// looks like { Name , Value }
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{{ {Name} , {Value} }}";
        }
    }
}