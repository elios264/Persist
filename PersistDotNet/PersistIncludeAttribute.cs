using System;

namespace elios.Persist
{
    /// <summary>
    /// Allows the <see cref="Archive"/> to recognize a type when it serializes or deserializes an object
    /// </summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public class PersistIncludeAttribute : Attribute
    {
        /// <summary>
        /// Gets the additional types.
        /// </summary>
        /// <value>
        /// The additional types.
        /// </value>
        public Type[] AdditionalTypes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PersistIncludeAttribute"/> class.
        /// </summary>
        /// <param name="additionalTypes">The additional types.</param>
        public PersistIncludeAttribute(params Type[] additionalTypes)
        {
            AdditionalTypes = additionalTypes;
        }
    }
}