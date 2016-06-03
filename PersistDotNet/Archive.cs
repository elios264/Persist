using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace elios.Persist
{

    /// <summary>
    /// Main Archive class, derive from it if you want to implement your own serializer ( BinaryArchive is missing)
    /// </summary>
    /// <remarks>this class is not thread safe</remarks>
    public abstract class Archive
    {
        /// <summary>
        /// Attribute value name used when working polymorphic types to specify the object type name
        /// </summary>
        public static string ClassKwd = "class";
        /// <summary>
        /// Fallback attribute value name used when working with list or dictionaries with no <see cref="T:PersistAttribute.ChildName"/> or <see cref="T:PersistAttribute.ValueName"/> specified
        /// </summary>
        public static string ValueKwd = "value";
        /// <summary>
        /// Fallback attribute value name used when working with dictionaries with no <see cref="T:PersistAttribute.KeyName"/> specified
        /// </summary>
        public static string KeyKwd = "key";
        /// <summary>
        /// Fallback attribute value name used when working with lists with no <see cref="T:PersistAttribute.ChildName"/> specified
        /// </summary>
        public static string ItemKwd = "item";
        /// <summary>
        /// Attribute value name used when working with referenced objects using <see cref="T:PersistAttribute.IsReference"/>
        /// </summary>
        public static string AddressKwd = "id";
        /// <summary>
        /// Default serialization provider
        /// </summary>
        public static IFormatProvider Provider = CultureInfo.CurrentCulture;

        private readonly bool m_isDynamic;
        private ObjectIDGenerator m_generator;
        private readonly PersistMember m_mainInfo;
        private static readonly Dictionary<Type,Type> MetaTypes;
        private readonly Dictionary<Type,PersistMember> m_additionalTypes;

        /// <summary>
        /// Gets the <see cref="Type"/> used to create this archive
        /// </summary>
        public Type CreationType => m_isDynamic ? null : m_mainInfo.Type;
        /// <summary>
                                                                                 /// Gets the additional types used to create this archive.
                                                                                 /// </summary>
                                                                                 /// <value>
                                                                                 /// The additional types.
                                                                                 /// </value>
        public IReadOnlyCollection<Type> AdditionalTypes
        {
            get { return (IReadOnlyCollection<Type>)( (object)m_additionalTypes.Keys ); }
        }

        static Archive()
        {
            MetaTypes = AppDomain.CurrentDomain.GetAssemblies()
                                  .Where(a => !a.IsDynamic)
                                  .SelectMany(a => a.GetExportedTypes())
                                  .Where(type => Attribute.IsDefined(type, typeof(MetadataTypeAttribute)))
                                  .ToDictionary(type => type.GetCustomAttribute<MetadataTypeAttribute>().MetadataClassType, type => type);
        }

        /// <summary>
        /// Base class used to serialize and deserialize Archives
        /// </summary>
        /// <param name="mainType">type of the object you are going to read or write</param>
        /// <param name="additionalTypes">additionalTypes that have to be considered when writing or reading</param>
        protected Archive(Type mainType, params Type[] additionalTypes)
        {
            if (mainType != null)
            {
                m_additionalTypes = additionalTypes.ToDictionary(type => type, GeneratePersistMember);
                m_mainInfo = GeneratePersistMember(mainType);
                m_isDynamic = false;
            }
            else
            {
                m_additionalTypes = new Dictionary<Type, PersistMember>();
                m_mainInfo = new PersistMember(typeof(object));
                m_isDynamic = true;
            }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Archive"/> class using the metadata from other serializer.
        /// </summary>
        /// <param name="archive">The archive.</param>
        protected Archive(Archive archive)
        {
            m_mainInfo = archive.m_mainInfo;
            m_additionalTypes = archive.m_additionalTypes;
        }

        //Methods called by TreeSerializer & BinarySerializer
        /// <summary>
        /// Call this method on a derived type to begin the serialization of the archive, this will start a subsecuent call of Being/End Write object/value abstract methods
        /// </summary>
        /// <param name="data">the object you want to serialize</param>
        /// <param name="rootName">the root name of the document (usually used only for xml archives) </param>
        protected void WriteMain(object data, string rootName)
        {
            if (!m_mainInfo.Type.IsInstanceOfType(data))
                throw new SerializationException($"the type {data.GetType().Name} does not match the constructed type of this serializer ({m_mainInfo.Type})");

            m_generator = new ObjectIDGenerator();
            m_mainInfo.Name = rootName ?? data.GetType().Name;
            Write(m_mainInfo, data);
            m_generator = null;
        }
        /// <summary>
        /// Call this method on a derived type to begin the deserialization of the archive, this will start a subsecuent call of Being/End Read object/value abstract methods
        /// </summary>
        /// <returns>returns the deserialized object</returns>
        protected object ReadMain()
        {
            return Read(m_mainInfo);
        }
        /// <summary>
        /// Call this method on a derived type to perform the second step on the deserialization of an archive that uses references to solve them
        /// </summary>
        /// <param name="obj"></param>
        protected void ResolveMain(object obj)
        {
            Resolve(m_mainInfo, obj);
        }

        //Abstract Methods
        /// <summary>
        /// Serializes the specified data into the stream
        /// </summary>
        /// <param name="target">target serialization stream</param>
        /// <param name="data">data to serialize</param>
        /// <param name="rootName">root name of the document (eg. xml doc rootname)</param>
        public abstract void Write(Stream target, object data, string rootName = null);
        /// <summary>
        /// Serializes the specified data into a file
        /// </summary>
        /// <param name="path">target serialization file</param>
        /// <param name="data">data to serialize</param>
        /// <param name="rootName">root name of the document (eg. xml doc rootname)</param>
        public abstract void Write(string path, object data, string rootName = null);

        /// <summary>
        /// Deserializes the archive contained by the specified <see cref="Stream"/>
        /// </summary>
        /// <param name="source">the stream that contains the <see cref="Archive"/> to deserialize</param>
        /// <returns></returns>
        public abstract object Read(Stream source);
        /// <summary>
        /// Deserializes the archive contained in the specified filePath
        /// </summary>
        /// <param name="filePath">the stream that contains the <see cref="Archive"/> to deserialize</param>
        /// <returns></returns>
        public abstract object Read(string filePath);

        /// <summary>
        /// A nested object begins to be read
        /// </summary>
        /// <param name="name">object name</param>
        /// <returns></returns>
        protected abstract bool BeginReadObject(string name);
        /// <summary>
        /// A nested object begins to be written
        /// </summary>
        /// <param name="name">object name</param>
        protected abstract void BeginWriteObject(string name);

        /// <summary>
        /// Ends reading a nested object
        /// </summary>
        /// <param name="obj">object just read for bookeeping</param>
        protected abstract void EndReadObject(object obj);
        /// <summary>
        /// Ends writing a nested object
        /// </summary>
        /// <param name="id">an unique id for the object for bookeeping</param>
        protected abstract void EndWriteObject(long id);

        /// <summary>
        /// Reads a value for the current nested object and cast it to the specified type
        /// </summary>
        /// <param name="name">value name</param>
        /// <param name="type">value type</param>
        /// <returns></returns>
        protected abstract object ReadValue(string name, Type type);
        /// <summary>
        /// writes a value for the current nested object
        /// </summary>
        /// <param name="name">value name</param>
        /// <param name="data">value</param>
        protected abstract void WriteValue(string name, object data);

        /// <summary>
        /// Writes a reference of an object instead of its value
        /// </summary>
        /// <param name="name">name for the reference</param>
        /// <param name="id">id of the reference</param>
        protected abstract void WriteReference(string name, long id);
        /// <summary>
        /// reads a reference
        /// </summary>
        /// <param name="name">name of the reference</param>
        /// <returns></returns>
        protected abstract object ReadReference(string name);

        /// <summary>
        /// when reading or resolving queries for the number of children of the current object
        /// </summary>
        /// <param name="name">filter string children name</param>
        /// <returns></returns>
        protected abstract int GetObjectChildrenCount(string name);
        /// <summary>
        /// Gets or sets a value indicating whether the current object needs to be a container
        /// </summary>
        /// <value>
        /// <c>true</c> if the current object needs to be a container otherwise, <c>false</c>.
        /// </value>
        protected abstract bool IsCurrentObjectContainer { get; set; }


        //Helper Methods
        private void Resolve(PersistMember persistInfo, object owner)
        {   //Handle polymorphic
            Type valueType;
            if (( valueType = owner.GetType() ) != persistInfo.Type)
            {
                PersistMember persistTypeInfo = m_additionalTypes[valueType];

                persistTypeInfo.Name = persistInfo.Name;
                persistTypeInfo.ChildName = persistInfo.ChildName;
                persistTypeInfo.IsReference = persistInfo.IsReference;
                persistInfo = persistTypeInfo;
            }

            //Handle convertbile
            if (persistInfo.PersistType == PersistType.Convertible)
                return;

            //handle List, Dictionary, Complex
            if (BeginReadObject(persistInfo.Name))
                using (new Utils.OnDispose(() => EndReadObject(null) ))
                {
                    switch (persistInfo.PersistType)
                    {
                    case PersistType.List:
                        int count = GetObjectChildrenCount(persistInfo.ValueItemInfo.Name);
                        for (int i = 0; i < count; i++)
                            if (persistInfo.ValueItemInfo.IsReference)
                            {
                                BeginReadObject(persistInfo.ValueItemInfo.Name);
                                ((IList)owner).Add(ReadReference(AddressKwd));
                                EndReadObject(null);
                            }
                            else
                                Resolve(persistInfo.ValueItemInfo, ( (IList)owner )[i]);
                        break;
                    case PersistType.Dictionary:
                        if (persistInfo.ValueItemInfo.IsReference)
                        {
                            count = GetObjectChildrenCount(persistInfo.ChildName);
                            for (int i = 0; i < count; i++)
                            {
                                BeginReadObject(persistInfo.ChildName);
                                ( (IDictionary)owner ).Add(Read(persistInfo.KeyItemInfo), ReadReference(persistInfo.ValueItemInfo.Name));
                                EndReadObject(null);
                            }
                        }
                        else
                            foreach (DictionaryEntry subItem in (IDictionary)owner)
                            {
                                BeginReadObject(persistInfo.ChildName);
                                Resolve(persistInfo.KeyItemInfo, subItem.Key);
                                Resolve(persistInfo.ValueItemInfo, subItem.Value);
                                EndReadObject(null);
                            }
                        break;
                    case PersistType.Complex:
                        foreach (var child in persistInfo.Children)
                        {
                            if (child.IsReference)
                                child.SetValue(owner, ReadReference(child.Name));
                            else
                                Resolve(child,child.GetValue(owner));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                    }
                }
        }
        private object Read(PersistMember persistInfo, object owner = null)
        {
            if (persistInfo.PersistType != PersistType.Convertible && !BeginReadObject(persistInfo.Name))
                return owner;

            //Create object
            if (owner == null && persistInfo.PersistType != PersistType.Convertible)
                owner = CreateInstanceForCurrentObject(persistInfo);

            //Handle Polymorphic
            Type valueType = owner?.GetType() ?? persistInfo.Type;
            bool isPolymorphic = false;

            if (!persistInfo.IsReference && valueType != persistInfo.Type)
            {
                PersistMember persistTypeInfo = m_additionalTypes[valueType];

                persistTypeInfo.Name = persistInfo.Name;
                persistTypeInfo.ChildName = persistInfo.ChildName;
                persistTypeInfo.IsReference = persistInfo.IsReference;
                persistInfo = persistTypeInfo;
                isPolymorphic = true;
            }

            ////Handle Convertible, Complex, List, Dictionary
            if (!persistInfo.IsReference && ( !( persistInfo.ValueItemInfo?.IsReference ?? false ) ))
            {
                switch (persistInfo.PersistType)
                {
                case PersistType.Convertible:
                    if (isPolymorphic || ( ( CreationType == persistInfo.Type || IsCurrentObjectContainer ) && BeginReadObject(persistInfo.Name) ))
                    {
                        owner = ReadValue(ValueKwd, persistInfo.Type);
                        break;
                    }
                    return ReadValue(persistInfo.Name, persistInfo.Type);
                case PersistType.List:
                    int count = GetObjectChildrenCount(persistInfo.ValueItemInfo.Name);
                    for (int i = 0; i < count; i++)
                        ( (IList)owner ).Add(Read(persistInfo.ValueItemInfo));
                    break;
                case PersistType.Dictionary:
                    count = GetObjectChildrenCount(persistInfo.ChildName);
                    for (int i = 0; i < count; i++)
                    {
                        BeginReadObject(persistInfo.ChildName);
                        ( (IDictionary)owner ).Add(Read(persistInfo.KeyItemInfo), Read(persistInfo.ValueItemInfo));
                        EndReadObject(null);
                    }
                    break;
                case PersistType.Complex:
                    foreach (var childInfo in persistInfo.Children)
                    {
                        var childValue = Read(childInfo, childInfo.GetValue(owner));
                        childInfo.SetValue?.Invoke(owner, childValue);
                    }
                    break;
                }
            }

            EndReadObject(owner);
            return owner;
        }
        private void Write(PersistMember persistInfo, object persistValue)
        {
            if (persistInfo.IsReference) //Handle references
                if (IsCurrentObjectContainer)
                {
                    BeginWriteObject(persistInfo.Name);
                    WriteReference(AddressKwd, UidOf(persistValue));
                    EndWriteObject(-1);
                }
                else
                    WriteReference(persistInfo.Name, UidOf(persistValue));
            else
            { //Handle Polymorphic
                Type valueType;
                bool isPolymorphic = false;

                if (( valueType = persistValue.GetType() ) != persistInfo.Type)
                {
                    PersistMember persistTypeInfo;

                    if (!m_additionalTypes.TryGetValue(valueType, out persistTypeInfo))
                    {
                        if (m_isDynamic)
                            m_additionalTypes.Add(valueType, persistTypeInfo = GeneratePersistMember(valueType));
                        else
                            throw new InvalidOperationException($"the type {valueType} was not expected. Use PersistInclude or the parameter additional types to specify types that are not know statically");
                    }

                    persistTypeInfo.Name = persistInfo.Name;
                    persistTypeInfo.ChildName = persistInfo.ChildName;
                    persistTypeInfo.IsReference = persistInfo.IsReference;
                    persistInfo = persistTypeInfo;
                    isPolymorphic = true;
                }

                //Handle Convertible
                if (persistInfo.PersistType == PersistType.Convertible)
                {
                    if (isPolymorphic || CreationType == persistInfo.Type || IsCurrentObjectContainer)
                    {
                        BeginWriteObject(persistInfo.Name);
                        if (isPolymorphic)
                            WriteValue(ClassKwd, GetFriendlyName(valueType));
                        WriteValue(ValueKwd, persistValue);
                        EndWriteObject(UidOf(persistValue));
                    }
                    else
                        WriteValue(persistInfo.Name, persistValue);
                }
                else //Handle Complex, List, Dictionary
                    using (new Utils.OnDispose(() => EndWriteObject(UidOf(persistValue))))
                    {
                        BeginWriteObject(persistInfo.Name);

                        if (isPolymorphic)
                            WriteValue(ClassKwd, GetFriendlyName(valueType));

                        switch (persistInfo.PersistType)
                        {
                        case PersistType.Complex:
                            foreach (var memberInfo in persistInfo.Children)
                                Write(memberInfo, memberInfo.GetValue(persistValue));
                            break;
                        case PersistType.List:
                            IsCurrentObjectContainer = true;
                            foreach (var elementValue in (IEnumerable)persistValue)
                                Write(persistInfo.ValueItemInfo, elementValue);
                            break;
                        case PersistType.Dictionary:
                            IsCurrentObjectContainer = true;
                            foreach (DictionaryEntry elementValue in (IDictionary)persistValue)
                                {
                                    BeginWriteObject(persistInfo.ChildName);
                                    Write(persistInfo.KeyItemInfo, elementValue.Key);
                                    Write(persistInfo.ValueItemInfo, elementValue.Value);
                                    EndWriteObject(-1);
                                }
                            break;
                        }
                    }
            }
        }

        private long UidOf(object o)
        {
            bool firstTime;
            return m_generator.GetId(o, out firstTime);
        }
        private PersistMember GeneratePersistMember(Type type)
        {
            PersistMember persistMember;
            switch (GetPersistType(type))
            {
            case PersistType.Convertible:
                return new PersistMember(type);
            case PersistType.Complex:
                persistMember = new PersistMember(type);
                break;
            case PersistType.List:
            case PersistType.Dictionary:
                persistMember = new PersistMember(typeof(Container<>).MakeGenericType(type));
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }

            var context = new Stack<PersistMember>(new[] {persistMember});
            var definedMembers = new Dictionary<Type, List<PersistMember>>();

            while (context.Count > 0)
            {
                PersistMember current = context.Pop();

                foreach (var memberInfo in GetElegibleMembers(current.Type))
                {
                    //Create childInfo & add it to its parent
                    PersistMember childInfo = new PersistMember(memberInfo.Member)
                    {
                        Name = memberInfo.Attribute.Name ?? memberInfo.Member.Name,
                        IsReference = memberInfo.Attribute.IsReference,
                        RunConstructor = memberInfo.Attribute.RunConstructor,
                        ChildName =  memberInfo.Attribute.ChildName
                    };
                    current.Children.Add(childInfo);

                    //Try to get its children or queue the creation of them
                    List<PersistMember> memberChildren;
                    if (definedMembers.TryGetValue(childInfo.Type, out memberChildren))
                    {
                        childInfo.Children = memberChildren;
                    }
                    else if (childInfo.PersistType == PersistType.Complex)
                    {
                        context.Push(childInfo);
                        definedMembers.Add(childInfo.Type, childInfo.Children);
                    }

                    switch (childInfo.PersistType)
                    {
                    case PersistType.List: //Handle list cases
                    {
                        childInfo.IsReference = false;
                        childInfo.RunConstructor = true;

                        var valueType = childInfo.Type.GetEnumeratedType();
                        var valueItemInfo = new PersistMember(valueType)
                        {
                            IsReference = memberInfo.Attribute.IsReference,
                            RunConstructor = memberInfo.Attribute.RunConstructor,
                            Name = memberInfo.Attribute.ChildName ?? ( valueType.IsGenericType
                                ? ItemKwd
                                : valueType.Name )
                        };
                        childInfo.ValueItemInfo = valueItemInfo;

                        List<PersistMember> typeMembers;
                        if (definedMembers.TryGetValue(valueType, out typeMembers))
                        {
                            valueItemInfo.Children = typeMembers;
                        }
                        else if (GetPersistType(valueItemInfo.Type) == PersistType.Complex)
                        {
                            context.Push(valueItemInfo);
                            definedMembers.Add(valueType, valueItemInfo.Children);
                        }
                    }
                        break;
                    case PersistType.Dictionary: //Handle dictionary cases
                    {
                        childInfo.IsReference = false;
                        childInfo.RunConstructor = true;

                        var dictypes = childInfo.Type.GetDictionaryTypes();

                        var keyItemInfo = new PersistMember(dictypes.Item1)
                        {
                            IsReference = false,
                            Name = memberInfo.Attribute.KeyName ?? ( dictypes.Item1.IsGenericType
                                ? KeyKwd
                                : dictypes.Item1.Name )
                        };
                        var valueItemInfo = new PersistMember(dictypes.Item2)
                        {
                            IsReference = memberInfo.Attribute.IsReference,
                            RunConstructor = memberInfo.Attribute.RunConstructor,
                            Name = memberInfo.Attribute.ValueName ?? ( dictypes.Item2.IsGenericType
                                ? ValueKwd
                                : dictypes.Item2.Name )
                        };

                        childInfo.ChildName = memberInfo.Attribute.ChildName ?? ItemKwd;
                        childInfo.KeyItemInfo = keyItemInfo;
                        childInfo.ValueItemInfo = valueItemInfo;

                        List<PersistMember> typeMembers;
                        if (definedMembers.TryGetValue(dictypes.Item2, out typeMembers))
                            valueItemInfo.Children = typeMembers;
                        else if (GetPersistType(valueItemInfo.Type) == PersistType.Complex)
                        {
                            context.Push(valueItemInfo);
                            definedMembers.Add(dictypes.Item2, valueItemInfo.Children);
                        }

                        if (definedMembers.TryGetValue(dictypes.Item1, out typeMembers))
                            keyItemInfo.Children = typeMembers;
                        else if (GetPersistType(keyItemInfo.Type) == PersistType.Complex)
                        {
                            context.Push(keyItemInfo);
                            definedMembers.Add(dictypes.Item1, keyItemInfo.Children);
                        }
                    }
                        break;
                    }

                    if (( childInfo.IsReference && childInfo.PersistType == PersistType.Convertible ) || ( ( childInfo.ValueItemInfo?.IsReference ?? false ) && childInfo.ValueItemInfo.PersistType == PersistType.Convertible ))
                    {
                        throw new SerializationException($"Cannot have a reference [Persist(IsReference=true)] on the simple type: {childInfo.Type} in property {memberInfo.Member.Name}!");
                    }
                }
            }

            if (Utils.HasCircularDependency(new[] {persistMember}, member => member.Children.Where(m => !m.IsReference)))
            {
                throw new SerializationException("Could not initialize serializer because a circular dependency has been detected please use [Persist(IsReference = true)] to avoid this exception");
            }

            switch (GetPersistType(type))
            {
            case PersistType.Complex:
                return persistMember;
            case PersistType.List:
            case PersistType.Dictionary:
                return persistMember.Children.Single();
            default:
                throw new ArgumentOutOfRangeException();
            }
        }
        private IEnumerable<MemberAttrib> GetElegibleMembers(Type mainType)
        {
            const BindingFlags searchMode = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            if (!mainType.IsSealed)
            {
                PersistIncludeAttribute additional;
                if (m_additionalTypes != null && ( additional = mainType.GetCustomAttribute<PersistIncludeAttribute>() ) != null)
                    foreach (var additionalType in additional.AdditionalTypes.Where(t => !m_additionalTypes.ContainsKey(t)))
                    {
                        m_additionalTypes.Add(additionalType, null); //To avoid recursion
                        m_additionalTypes[additionalType] = GeneratePersistMember(additionalType);
                    }
            }

            Type metaType;
            bool isMetaType = MetaTypes.TryGetValue(mainType, out metaType);
            bool isAnonymous = mainType.IsAnonymousType();
            var realFields = new FieldInfo[0];
            var realProperties = new PropertyInfo[0];


            if (isMetaType)
            {
                realFields = mainType.GetFields(searchMode);
                realProperties = mainType.GetProperties(searchMode);

                mainType = metaType;
            }
            else if (isAnonymous)
            {
                realFields = mainType.GetFields(searchMode);
            }

            var propertyMembers = mainType.GetProperties(searchMode).Select(info => new
            {
                p = info,
                attr = info.GetCustomAttribute<PersistAttribute>()
            }).Where(_ => !isAnonymous).Where(info => GetPersistType(PersistMember.GetMemberType(info.p)) == PersistType.Convertible
                ? info.p.SetMethod != null && info.p.SetMethod.IsPublic && ( info.attr == null || info.attr.Ignore == false )
                : ( info.p.GetMethod.IsPublic
                    ? info.attr == null || info.attr.Ignore == false
                    : info.attr != null && info.attr.Ignore == false )).Select(info => new MemberAttrib(isMetaType
                        ? realProperties.Single(p => p.Name == info.p.Name)
                        : info.p, info.attr ?? new PersistAttribute(info.p.Name)));

            IEnumerable<MemberAttrib> fieldMembers;
            if (isAnonymous)
            {
                fieldMembers = mainType.GetProperties(searchMode).Select(info => new MemberAttrib(realFields.First(fi => fi.Name.Contains($"<{info.Name}>")), new PersistAttribute(info.Name)));
            }
            else
            {
                fieldMembers = mainType.GetFields(searchMode).Select(info => new
                {
                    f = info,
                    attr = info.GetCustomAttribute<PersistAttribute>()
                }).Where(info => info.attr != null && info.attr.Ignore == false).Select(info => new MemberAttrib(isMetaType
                    ? realFields.Single(p => p.Name == info.f.Name)
                    : info.f, info.attr));
            }

            return propertyMembers.Concat(fieldMembers);
        }
        private static PersistType GetPersistType(Type type)
        {
            return typeof(IConvertible).IsAssignableFrom(type)
                ? PersistType.Convertible
                : ( typeof(IDictionary).IsAssignableFrom(type) // || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    ? PersistType.Dictionary
                    : ( typeof(IEnumerable).IsAssignableFrom(type)
                        ? PersistType.List
                        : PersistType.Complex ) );
        }
        private object CreateInstanceForCurrentObject(PersistMember info)
        {
            if (info.IsAnonymousType)
                return FormatterServices.GetUninitializedObject(info.Type);

            string typeName = (string)ReadValue(ClassKwd, typeof(string));
            Type typeToCreate = typeName == null
                ? info.Type
                : m_additionalTypes.Keys.FirstOrDefault(t => GetFriendlyName(t) == typeName || GetFriendlyName(t,true) == typeName);

            if (m_isDynamic && typeToCreate == null)
            {
                typeToCreate = GetFriendlyType(typeName);
                m_additionalTypes.Add(typeToCreate, GeneratePersistMember(typeToCreate));
            }

            if (typeToCreate == null || !info.Type.IsAssignableFrom(typeToCreate ?? info.Type))
                typeToCreate = info.Type;

            typeToCreate = typeToCreate ?? info.Type;

            return info.RunConstructor
                ? Activator.CreateInstance(typeToCreate)
                : FormatterServices.GetUninitializedObject(typeToCreate);

        }
        private string GetFriendlyName(Type type, bool forceFullName = false)
        {
            var friendlyName = m_isDynamic || forceFullName ? type.FullName : type.Name;
            if (!type.IsGenericType) return friendlyName;

            var iBacktick = friendlyName.IndexOf('`');
            if (iBacktick > 0) friendlyName = friendlyName.Remove(iBacktick);

            var genericParameters = type.GetGenericArguments().Select(t => GetFriendlyName(t,forceFullName));
            friendlyName += "<" + string.Join(", ", genericParameters) + ">";

            return friendlyName;
        }
        private static Type GetFriendlyType(string typeName)
        {
            int index = typeName.IndexOf("<");
            if (index == -1)
                return Type.GetType(typeName)
                    ?? Assembly.GetEntryAssembly().GetType(typeName)
                    ?? AppDomain.CurrentDomain.GetAssemblies().Except(new []{ Assembly.GetEntryAssembly(), Assembly.GetAssembly(typeof(int)), }).Select(a => a.GetType(typeName)).First(t => t != null);

            string genericClassType = typeName.Substring(0, index);
            string genericParameters = typeName.Substring(index + 1);
            genericParameters = genericParameters.Substring(0, genericParameters.Length - 1);

            int open = 0; int close = 0; int i = 0; bool found = false;
            for (var c = genericParameters[i]; i < genericParameters.Length; c = genericParameters[i], i++)
            {
                if (c == '<') open ++;
                if (c == '>') close ++;
                if (c == ',' && open == close)
                {
                    i--;
                    found = true;
                    break;
                }
            }

            var type1 = genericParameters.Substring(0,i).Trim();
            var type2 = found ? genericParameters.Substring(i+1).Trim() : null;
            var numberArgs = found ? 2 : 1;
            var genericClassTypeComplete = $"{genericClassType}`{numberArgs}";

            var type = GetFriendlyType(genericClassTypeComplete);

            return numberArgs == 1
                ? type.MakeGenericType(GetFriendlyType(type1))
                : type.MakeGenericType(GetFriendlyType(type1),GetFriendlyType(type2));
        }

        //Helper Classes
        private enum PersistType
        {
            Complex,
            List,
            Dictionary,
            Convertible
        }
        private class PersistMember
        {
            private readonly Lazy<bool> m_isAnonymousType;

            public string Name { get; set; }

            public Type Type { get; }
            public PersistType PersistType { get; }

            public bool IsAnonymousType => m_isAnonymousType.Value;
            public bool IsReference { get; set; }
            public bool RunConstructor { get; set; } = true;

            public PersistMember KeyItemInfo { get; set; }
            public PersistMember ValueItemInfo { get; set; }
            public string ChildName { get; set; } = ItemKwd;

            public List<PersistMember> Children { get; set; } = new List<PersistMember>();

            public Func<object, object> GetValue { get; }
            public Action<object, object> SetValue { get; }

            public PersistMember(Type type)
            {
                Type = type;
                PersistType = GetPersistType(type);

                m_isAnonymousType = new Lazy<bool>(() => Type.IsAnonymousType());
            }
            public PersistMember(MemberInfo info)
            {
                PropertyInfo prp = info as PropertyInfo;
                FieldInfo fld = info as FieldInfo;

                Type = prp?.PropertyType ?? fld?.FieldType;
                PersistType = GetPersistType(Type);

                m_isAnonymousType = new Lazy<bool>(() => Type.IsAnonymousType());

                GetValue = owner => prp != null
                    ? prp.GetValue(owner)
                    : fld.GetValue(owner);

                if (prp != null && prp.CanWrite == false)
                    return;

                SetValue = (owner, childValue) =>
                {
                    if (prp != null)
                        prp.SetValue(owner, childValue);
                    else
                        fld.SetValue(owner, childValue);
                };
            }

            public override string ToString()
            {
                return $"Name: {Name} \t Type:{Type}";
            }
            public static Type GetMemberType(MemberInfo info)
            {
                PropertyInfo prp = info as PropertyInfo;
                FieldInfo fld = info as FieldInfo;

                return prp?.PropertyType ?? fld?.FieldType;
            }
        }
        private struct MemberAttrib
        {
            public readonly MemberInfo Member;
            public readonly PersistAttribute Attribute;

            public MemberAttrib(MemberInfo m, PersistAttribute a)
            {
                Member = m;
                Attribute = a;
            }
        }
        private sealed class Container<T>
        {
            public T Member { get; set; }
        }
    }
}