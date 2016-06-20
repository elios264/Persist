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
        public NodeAttribute(string name, string value)
        {
            Name = name;
            Value = value;
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