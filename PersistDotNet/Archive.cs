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
        /// <summary>
        /// Looks for derived types in the current domain so you do not have to deal with manually setting them in the constructor
        /// </summary>
        public static bool LookForDerivedTypes = false;


        private static readonly Lazy<List<Type>> DomainTypes;

        private ObjectIDGenerator m_generator;
        private readonly PersistMember m_mainInfo;
        private readonly List<Type> m_polymorphicTypes;
        private readonly Dictionary<Type,Type> m_metaTypes;

        /// <summary>
        /// Gets the type of the <see cref="Type"/> used to create this archive
        /// </summary>
        public Type CreationType => m_mainInfo.Type;

        static Archive()
        {
            DomainTypes = new Lazy<List<Type>>(() =>
            {
                var d = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a => a.GetExportedTypes()).Where(type => !type.IsAbstract).ToList();
                d.TrimExcess();
                return d;
            });
        }

        /// <summary>
        /// Base class used to serialize and deserialize Archives
        /// </summary>
        /// <param name="mainType">type of the object you are going to read or write</param>
        /// <param name="polymorphicTypes">polymorphicTypes that have to be considered when writing or reading</param>
        protected Archive(Type mainType, IEnumerable<Type> polymorphicTypes)
        {
            m_polymorphicTypes = new List<Type>(polymorphicTypes ?? Enumerable.Empty<Type>());
            m_metaTypes = Assembly.GetAssembly(mainType)
                                  .GetTypes()
                                  .Where(type => Attribute.IsDefined(type, typeof(MetadataTypeAttribute)))
                                  .ToDictionary(type => type.GetCustomAttribute<MetadataTypeAttribute>().MetadataClassType, type => type);

            switch (GetPersistType(mainType))
            {
            case PersistType.Complex:
                m_mainInfo = GeneratePersistMember(mainType);
                break;
            case PersistType.List:
            case PersistType.Dictionary:
                var info = GeneratePersistMember(typeof(Container<>).MakeGenericType(mainType));
                m_mainInfo = info.Children.Single();
                break;
            case PersistType.Convertible:
                throw new SerializationException("the root of the data to serialize can't implement IConvertible");
            }
         }
        /// <summary>
        /// Initializes a new instance of the <see cref="Archive"/> class using the metadata from other serializer.
        /// </summary>
        /// <param name="archive">The archive.</param>
        protected Archive(Archive archive)
        {
            m_mainInfo = archive.m_mainInfo;
            m_polymorphicTypes = archive.m_polymorphicTypes;
            m_metaTypes = archive.m_metaTypes;
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
            object result = null;
            Read(m_mainInfo, ref result);

            return result;
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
        /// <param name="isContainer">is the object an array or list or dictionary</param>
        protected abstract void BeginWriteObject(string name, bool isContainer = false);

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

        //Helper Methods
        private void Resolve(PersistMember persistInfo, object owner)
        {
            switch (persistInfo.PersistType)
            {
            case PersistType.Convertible:
                break;
            case PersistType.List:
                if (BeginReadObject(persistInfo.Name))
                {
                    var count = GetObjectChildrenCount(persistInfo.ValueItemInfo.Name);

                    for (int i = 0; i < count; i++)
                    {
                        if (persistInfo.ValueItemInfo.IsReference)
                        {
                            BeginReadObject(persistInfo.ValueItemInfo.Name);
                            ( (IList)owner ).Add(ReadReference(AddressKwd));
                            EndReadObject(null);
                        }
                        else
                        {
                            Resolve(persistInfo.ValueItemInfo, ( (IList)owner )[i]);
                        }
                    }
                    EndReadObject(null);
                }
                break;
            case PersistType.Dictionary:
                if (BeginReadObject(persistInfo.Name))
                {
                    if (persistInfo.ValueItemInfo.IsReference)
                    {
                        var count = GetObjectChildrenCount(persistInfo.ChildName);
                        for (int i = 0; i < count; i++)
                        {
                            BeginReadObject(persistInfo.ChildName);
                            object keyValue = null;
                            Read(persistInfo.KeyItemInfo, ref keyValue);
                            object childValue = ReadReference(persistInfo.ValueItemInfo.Name);
                            EndReadObject(null);

                            ( (IDictionary)owner ).Add(keyValue, childValue);
                        }
                    }
                    else
                    {
                        foreach (DictionaryEntry subItem in (IDictionary)owner)
                        {
                            BeginReadObject(persistInfo.ChildName);
                            Resolve(persistInfo.KeyItemInfo, subItem.Key);
                            Resolve(persistInfo.ValueItemInfo, subItem.Value);
                            EndReadObject(null);
                        }
                    }
                    EndReadObject(null);
                }
                break;
            case PersistType.Complex:
                if (BeginReadObject(persistInfo.Name))
                {
                    foreach (var child in persistInfo.Children.Where(child => child.DeclaringType.IsInstanceOfType(owner)))
                    {
                        if (child.IsReference)
                            child.SetValue(owner, ReadReference(child.Name));
                        else
                            Resolve(child, child.GetValue(owner));
                    }

                    EndReadObject(null);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }
        }
        private void Read(PersistMember persistInfo, ref object owner)
        {
            if (persistInfo.PersistType == PersistType.Convertible || BeginReadObject(persistInfo.Name))
            {
                if (owner == null && persistInfo.PersistType != PersistType.Convertible)
                {
                    if (persistInfo.IsAnonymousType)
                        owner = FormatterServices.GetUninitializedObject(persistInfo.Type);
                    else
                    {
                        var classType = (string)ReadValue(ClassKwd, typeof(string));
                        var typeToCreate = classType == null ? persistInfo.Type : m_polymorphicTypes.First(type => type.Name == classType);

                        owner = persistInfo.RunConstructor ? Activator.CreateInstance(typeToCreate) : FormatterServices.GetUninitializedObject(typeToCreate);
                    }
                }


                int count;
                switch (persistInfo.PersistType)
                {
                case PersistType.Convertible:
                    owner = ReadValue(persistInfo.Name, persistInfo.Type);
                    return;
                case PersistType.List:
                    if (persistInfo.ValueItemInfo.IsReference)
                        break;

                    count = GetObjectChildrenCount(persistInfo.ValueItemInfo.Name);
                    for (int i = 0; i < count; i++)
                    {
                        object childValue = null;

                        if (persistInfo.ValueItemInfo.PersistType == PersistType.Convertible)
                        {
                            BeginReadObject(persistInfo.ValueItemInfo.Name);
                            childValue = ReadValue(ValueKwd, persistInfo.ValueItemInfo.Type);
                            EndReadObject(null);
                        }
                        else
                        {
                            Read(persistInfo.ValueItemInfo, ref childValue);
                        }

                        ( (IList)owner ).Add(childValue);
                    }
                    break;
                case PersistType.Dictionary:
                    if (persistInfo.ValueItemInfo.IsReference)
                        break;

                    count = GetObjectChildrenCount(persistInfo.ChildName);
                    for (int i = 0; i < count; i++)
                    {
                        object keyValue = null;
                        object childValue = null;

                        BeginReadObject(persistInfo.ChildName);
                        Read(persistInfo.KeyItemInfo, ref keyValue);
                        Read(persistInfo.ValueItemInfo, ref childValue);
                        EndReadObject(null);

                        ( (IDictionary)owner ).Add(keyValue, childValue);
                    }
                    break;
                case PersistType.Complex:
                    if (persistInfo.IsReference)
                        break;

                    Type ownerType = owner.GetType();
                    foreach (var child in persistInfo.Children.Where(child => child.DeclaringType.IsAssignableFrom(ownerType)))
                    {
                        object childValue = child.GetValue(owner);

                        Read(child, ref childValue);
                        child.SetValue?.Invoke(owner, childValue);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
                }


                EndReadObject(owner);
            }
        }
        private void Write(PersistMember persistInfo, object persistValue)
        {
            if (persistValue == null)
                return;

            if (persistInfo.IsReference)
            {
                WriteReference(persistInfo.Name, UidOf(persistValue));
                return;
            }

            switch (persistInfo.PersistType)
            {
            case PersistType.Convertible:
                WriteValue(persistInfo.Name, persistValue);
                break;
            case PersistType.List:
                BeginWriteObject(persistInfo.Name, true);

                foreach (var subItem in (IEnumerable)persistValue)
                {
                    if (persistInfo.ValueItemInfo.PersistType == PersistType.Convertible)
                    {
                        BeginWriteObject(persistInfo.ValueItemInfo.Name);
                        WriteValue(ValueKwd, (IConvertible)subItem);
                        EndWriteObject(-1);
                    }
                    else if (persistInfo.ValueItemInfo.IsReference)
                    {
                        BeginWriteObject(persistInfo.ValueItemInfo.Name);
                        WriteReference(AddressKwd, UidOf(subItem));
                        EndWriteObject(-1);
                    }
                    else
                        Write(persistInfo.ValueItemInfo, subItem);
                }

                EndWriteObject(UidOf(persistValue));
                break;
            case PersistType.Dictionary:
                BeginWriteObject(persistInfo.Name, true);

                    foreach (DictionaryEntry subItem in (IDictionary)persistValue)
                    {
                        BeginWriteObject(persistInfo.ChildName);
                        Write(persistInfo.KeyItemInfo, subItem.Key);
                        Write(persistInfo.ValueItemInfo, subItem.Value);
                        EndWriteObject(-1);
                    }

                    EndWriteObject(UidOf(persistValue));
                break;
            case PersistType.Complex:
                BeginWriteObject(persistInfo.Name);

                if (persistValue.GetType() != persistInfo.Type)
                    WriteValue(ClassKwd, persistValue.GetType().Name);

                foreach (var child in persistInfo.Children.Where(child => child.DeclaringType.IsInstanceOfType(persistValue)))
                    Write(child, child.GetValue(persistValue));

                EndWriteObject(UidOf(persistValue));
                break;
            default:
                throw new ArgumentOutOfRangeException();
            }
        }
        private long UidOf(object o)
        {
            bool firstTime;
            return m_generator.GetId(o, out firstTime);
        }
        private PersistMember GeneratePersistMember(Type type)
        {
            var persistMember = new PersistMember(type);
            var context = new Stack<PersistMember>(new[] { persistMember });
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
                        RunConstructor = memberInfo.Attribute.RunConstructor
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
                        case PersistType.Convertible: //Handle convertible cases
                            if (memberInfo.Attribute.IsReference)
                                throw new SerializationException($"Cannot have a reference [Persist(IsReference=true)] on the simple type: {childInfo.Type} in property {memberInfo.Member.Name}!");
                            break;
                        case PersistType.List: //Handle list cases
                            {
                                childInfo.IsReference = false;
                                childInfo.RunConstructor = true;

                                var valueType = childInfo.Type.GetEnumeratedType();
                                var valueItemInfo = new PersistMember(valueType)
                                {
                                    IsReference = memberInfo.Attribute.IsReference,
                                    RunConstructor = memberInfo.Attribute.RunConstructor,
                                    Name = memberInfo.Attribute.ChildName ?? (valueType.IsGenericType ? ItemKwd : valueType.Name)
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
                                    Name = memberInfo.Attribute.KeyName ?? (dictypes.Item1.IsGenericType ? KeyKwd : dictypes.Item1.Name)
                                };
                                var valueItemInfo = new PersistMember(dictypes.Item2)
                                {
                                    IsReference = memberInfo.Attribute.IsReference,
                                    RunConstructor = memberInfo.Attribute.RunConstructor,
                                    Name = memberInfo.Attribute.ValueName ?? (dictypes.Item2.IsGenericType ? ValueKwd : dictypes.Item2.Name)
                                };

                                childInfo.ChildName = memberInfo.Attribute.ChildName ?? ItemKwd;
                                childInfo.KeyItemInfo = keyItemInfo;
                                childInfo.ValueItemInfo = valueItemInfo;

                                List<PersistMember> typeMembers;
                                if (definedMembers.TryGetValue(dictypes.Item2, out typeMembers))
                                {
                                    valueItemInfo.Children = typeMembers;
                                }
                                else if (GetPersistType(valueItemInfo.Type) == PersistType.Complex)
                                {
                                    context.Push(valueItemInfo);
                                    definedMembers.Add(dictypes.Item2, valueItemInfo.Children);
                                }

                                if (definedMembers.TryGetValue(dictypes.Item1, out typeMembers))
                                {
                                    keyItemInfo.Children = typeMembers;
                                }
                                else if (GetPersistType(keyItemInfo.Type) == PersistType.Complex)
                                {
                                    context.Push(keyItemInfo);
                                    definedMembers.Add(dictypes.Item1, keyItemInfo.Children);
                                }
                            }
                            break;
                    }
                }
            }

            if (Utils.HasCircularDependency(new[] { persistMember }, member => member.Children.Where(m => !m.IsReference)))
            {
                throw new SerializationException("Could not initialize serializer because a circular dependency has been detected please use [Persist(IsReference = true)] to avoid this exception");
            }

            return persistMember;
        }
        private IEnumerable<MemberAttrib> GetElegibleMembers(Type mainType)
        {
            const BindingFlags searchMode = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            if (!mainType.IsSealed && LookForDerivedTypes)
            {
                foreach (var derived in DomainTypes.Value.Concat(Assembly.GetAssembly(mainType).GetTypes().Where(type => !type.IsPublic && !type.IsAbstract)).Where(type => type != mainType && mainType.IsAssignableFrom(type)))
                    m_polymorphicTypes.Add(derived);
            }

            var elegibleMembers = Enumerable.Empty<MemberAttrib>();
            var allDerivedTypes = mainType.IsSealed
                ? Enumerable.Empty<Type>()
                : m_polymorphicTypes.Where(mainType.IsAssignableFrom).SelectMany(dT =>
                {
                    var typesTillMain = new List<Type>();

                    while (dT != mainType)
                    {
                        typesTillMain.Add(dT);
                        dT = dT.BaseType;
                    }

                    return typesTillMain;
                });

            foreach (var memberType in new[] {mainType}.Concat(allDerivedTypes))
            {
                var currentMemberType = memberType;
                var isAnonymous = currentMemberType.IsAnonymousType();

                var searchFlags = currentMemberType == mainType ? searchMode : searchMode | BindingFlags.DeclaredOnly;

                Type metaType;
                bool isMetaType = m_metaTypes.TryGetValue(currentMemberType, out metaType);

                var realFields = new FieldInfo[0];
                var realProperties = new PropertyInfo[0];


                if (isMetaType)
                {
                    realFields = currentMemberType.GetFields(searchFlags);
                    realProperties = currentMemberType.GetProperties(searchFlags);

                    currentMemberType = metaType;
                }
                else if (isAnonymous)
                {
                    realFields = currentMemberType.GetFields(searchFlags);
                }

                var propertyMembers = currentMemberType.GetProperties(searchFlags).Select(info => new
                {
                    p = info,
                    attr = info.GetCustomAttribute<PersistAttribute>()
                }).Where(_ => !isAnonymous).Where(info => { return GetPersistType(PersistMember.GetMemberType(info.p)) == PersistType.Convertible ? info.p.SetMethod != null && info.p.SetMethod.IsPublic && ( info.attr == null || info.attr.Ignore == false ) : ( info.p.GetMethod.IsPublic ? info.attr == null || info.attr.Ignore == false : info.attr != null && info.attr.Ignore == false ); }).Select(info => new MemberAttrib(isMetaType ? realProperties.Single(p => p.Name == info.p.Name) : info.p, info.attr ?? new PersistAttribute(info.p.Name)));

                IEnumerable<MemberAttrib> fieldMembers;
                if (isAnonymous)
                {
                    fieldMembers = currentMemberType.GetProperties(searchFlags).Select(info => new MemberAttrib(realFields.First(fi => fi.Name.Contains($"<{info.Name}>")), new PersistAttribute(info.Name)));
                }
                else
                {
                    fieldMembers = currentMemberType.GetFields(searchFlags).Select(info => new
                    {
                        f = info,
                        attr = info.GetCustomAttribute<PersistAttribute>()
                    }).Where(info => info.attr != null && info.attr.Ignore == false).Select(info => new MemberAttrib(isMetaType ? realFields.Single(p => p.Name == info.f.Name) : info.f, info.attr));
                }
                elegibleMembers = elegibleMembers.Concat(propertyMembers.Concat(fieldMembers));
            }

            return elegibleMembers;
        }
        private static PersistType GetPersistType(Type type)
        {
            return typeof(IConvertible).IsAssignableFrom(type)
                ? PersistType.Convertible
                : ( typeof(IDictionary).IsAssignableFrom(type)
                    ? PersistType.Dictionary
                    : ( typeof(IEnumerable).IsAssignableFrom(type)
                        ? PersistType.List
                        : PersistType.Complex ) );
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
            public Type DeclaringType { get; }
            public PersistType PersistType { get; }

            public bool IsAnonymousType => m_isAnonymousType.Value;
            public bool IsReference { get; set; }
            public bool RunConstructor { get; set; } = true;

            public PersistMember KeyItemInfo { get; set; }
            public PersistMember ValueItemInfo { get; set; }
            public string ChildName { get; set; }

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
                DeclaringType = info.DeclaringType;
                PersistType = GetPersistType(Type);

                m_isAnonymousType = new Lazy<bool>(() => Type.IsAnonymousType());

                GetValue = owner => prp != null ? prp.GetValue(owner) : fld.GetValue(owner);

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