using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace elios.Persist
{
    public class Node
    {
        internal virtual long Id { get; set; }

        public virtual string Name { get; }
        public virtual bool IsContainer { get; set; }

        public virtual List<NodeAttribute> Attributes { get; }
        public virtual List<Node> Nodes { get; }
       

        public Node(string name)
        {
            Name = name;
            Attributes = new List<NodeAttribute>();
            Nodes = new List<Node>();
        }
        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        public Node(Node copyNode)
        {
            Id = copyNode.Id;
            Name = copyNode.Name;
            IsContainer = copyNode.IsContainer;
            Attributes = copyNode.Attributes.ToList();
            Nodes = copyNode.Nodes.Select(element => new Node(element)).ToList();
        }

        public override string ToString()
        {
            return $"{Name} =>  {string.Join(" | ", Attributes.Select(attribute => attribute.ToString()))}";
        }
    }
}