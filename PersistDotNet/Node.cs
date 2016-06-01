using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace elios.Persist
{
    /// <summary>
    /// Node used to represent Archives
    /// </summary>
    public class Node
    {
        internal virtual long Id { get; set; }

        /// <summary>
        /// name of the node
        /// </summary>
        public virtual string Name { get; set; }
        /// <summary>
        /// is the node a container
        /// </summary>
        public virtual bool IsContainer { get; set; }

        /// <summary>
        /// attributes of the node
        /// </summary>
        public virtual List<NodeAttribute> Attributes { get; }

        /// <summary>
        /// children nodes for this node
        /// </summary>
        public virtual List<Node> Nodes { get; }
       
        /// <summary>
        /// Creates a node
        /// </summary>
        public Node()
        {
            Attributes = new List<NodeAttribute>();
            Nodes = new List<Node>();
        }
        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copyNode"></param>
        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        public Node(Node copyNode)
        {
            Id = copyNode.Id;
            Name = copyNode.Name;
            IsContainer = copyNode.IsContainer;
            Attributes = copyNode.Attributes.ToList();
            Nodes = copyNode.Nodes.Select(element => new Node(element)).ToList();
        }
        
        /// <summary>
        /// Shows the node name and its attributes names and values
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Name} =>  {string.Join(" | ", Attributes.Select(attribute => attribute.ToString()))}";
        }

        /// <summary>
        /// Creates a dynamic proxy to access all the attributes and Nodes, also you can invoke all of the IDictionary&lt;string,object&gt; methods
        /// like Add(string,object) , Clear , ContainsKey, Remove, etc...
        /// </summary>
        /// <returns>a dynamic object that is also an IDictionary&lt;string,object&gt; </returns>
        public dynamic AsDynamic()
        {
            return new DynamicNode(this);
        }

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        protected bool Equals(Node other)
        {
            return string.Equals(Name, other.Name)
                   && IsContainer == other.IsContainer
                   && Attributes.Zip(other.Attributes, Tuple.Create).All(t => t.Item1.Equals(t.Item2))
                   && Nodes.Zip(other.Nodes, Tuple.Create).All(t => t.Item1.Equals(t.Item2));
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
            if (obj.GetType() != GetType())
                return false;
            return Equals((Node)obj);
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
                var hashCode = Name?.GetHashCode() ?? 0;
                hashCode = ( hashCode * 397 ) ^ IsContainer.GetHashCode();
                hashCode = ( hashCode * 397 ) ^ ( Attributes?.GetHashCode() ?? 0 );
                hashCode = ( hashCode * 397 ) ^ ( Nodes?.GetHashCode() ?? 0 );
                return hashCode;
            }
        }
    }

}