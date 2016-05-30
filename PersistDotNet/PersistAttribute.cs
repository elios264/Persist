using System;

namespace elios.Persist
{
    /// <summary>
    /// Preferences used to personalize an object serialization and deserialization members
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PersistAttribute : Attribute
    {
        /// <summary>
        /// name of the member, if not set the property or field name will be used
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// For complex types treats it as a reference only storing its id to be serialized elsewhere in the tree.
        /// For lists and dictionaries does the same thing but just for their values.
        /// </summary>
        public bool IsReference { get; set; }
        /// <summary>
        /// Opcional name for items on a dictionary or list
        /// </summary>
        public string ChildName { get; set; }
        /// <summary>
        /// Opctional name for key on dictionaries only
        /// </summary>
        public string KeyName { get; set; }
        /// <summary>
        /// Opctional name for value on dictionaries only
        /// </summary>
        public string ValueName { get; set; }

        /// <summary>
        /// Ignores the specified field since public properties are serialized by default
        /// </summary>
        public bool Ignore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the parameterless constructor of the type. default value is true
        /// </summary>
        /// <value>
        ///  if true <see cref="T:Activator.CreateInstance"/>  will be used otherwise <see cref="T:FormatterServices.GetUninitializedObject"/>
        /// </value>
        public bool RunConstructor { get; set; } = true;

        /// <summary>
        /// Default constructor
        /// </summary>
        public PersistAttribute()
        {
        }
        /// <summary>
        /// Creates a persist attribute with the specified name
        /// </summary>
        /// <param name="name"></param>
        public PersistAttribute(string name)
        {
            Name = name;
        }
    }
}