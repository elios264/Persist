using System;
using System.Globalization;

namespace elios.Persist
{
    public class NodeAttribute
    {
        public string Name { get; }
        public string Value { get; }

        public NodeAttribute(string name, IConvertible value)
        {
            Name = name;
            Value = value.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return $"{Name}, {Value}";
        }
    }
}