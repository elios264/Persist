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
        public virtual string Name { get; }
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
        /// Creates a node with the specified name
        /// </summary>
        /// <param name="name"></param>
        public Node(string name)
        {
            Name = name;
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
    }
}