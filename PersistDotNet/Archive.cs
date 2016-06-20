using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using static elios.Persist.Utils;

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
        /// Default serialization Culture
        /// </summary>
        public static CultureInfo Culture = CultureInfo.CurrentCulture;

        private readonly bool m_isDynamic;
        private ObjectIDGenerator m_generator;
        private readonly PersistInfo m_mainInfo;
        /// <summary>
        /// Metatypes mapping
        /// </summary>
        protected static readonly IReadOnlyDictionary<Type,Type> MetaTypes;
        private readonly Dictionary<Type,PersistInfo> m_additionalTypes;

        /// <summary>
        /// Gets the <see cref="Type"/> used to create this archive
        /// </summary>
        public Type CreationType => m_isDynamic ? null : m_mainInfo.Type;

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
                m_additionalTypes = additionalTypes.ToDictionary(type => type, PersistInfoFor);
                m_mainInfo = PersistInfoFor(mainType);
                m_isDynamic = false;
            }
            else
            {
                m_additionalTypes = new Dictionary<Type, PersistInfo>();
                m_mainInfo = new PersistInfo(typeof(object));
                m_isDynamic = true;
            }
        }
        //Methods called by TreeSerializer & BinarySerializer
        /// <summary>
        /// Call this method on a derived type to begin the serialization of the archive, this will start a subsecuent call of Being/End Write object/value abstract methods
        /// </summary>
        /// <param name="data">the object you want to serialize</param>
        /// <param name="rootName">the root name of the document (usually used only for xml archives) </param>
        protected void WriteMain(object data, string rootName)
        {
            Assert(m_mainInfo.Type.IsInstanceOfType(data), $"the type {GetFriendlyName(data.GetType())} does not match the constructed type of this serializer ({m_mainInfo.Type})");

            m_generator = new ObjectIDGenerator();
            m_mainInfo.Name = rootName ??  (data.GetType().IsGenericType ? null : data.GetType().Name);
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
        private void Write(PersistInfo persistInfo, object persistValue)
        {
            if (persistValue == null) return;

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
                    PersistInfo persistTypeInfo;

                    if (!m_additionalTypes.TryGetValue(valueType, out persistTypeInfo))
                    {
                        if (m_isDynamic)
                            m_additionalTypes.Add(valueType, persistTypeInfo = PersistInfoFor(valueType));
                        else
                            throw new InvalidOperationException($"The type {valueType} was not expected.create a Archive(null) to use runtime discovery or use PersistIncludeAttribute or the parameter additional types to specify types that are not know statically");
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
                    using (new OnDispose(() => EndWriteObject(UidOf(persistValue))))
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
        private void Resolve(PersistInfo persistInfo, object owner)
        {   //Handle polymorphic
            Type valueType;
            if (( valueType = owner.GetType() ) != persistInfo.Type)
            {
                PersistInfo persistTypeInfo = m_additionalTypes[valueType];

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
                using (new OnDispose(() => EndReadObject(null) ))
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
        private object Read(PersistInfo persistInfo, object owner = null)
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
                PersistInfo persistTypeInfo = m_additionalTypes[valueType];

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

        private long UidOf(object o)
        {
            bool firstTime;
            return m_generator.GetId(o, out firstTime);
        }
        private PersistInfo PersistInfoFor(Type type)
        {
            var persistInfo = new PersistInfo(type);
            PersistInfoFor(persistInfo,null);

            Assert(!HasCircularDependency(new[] { persistInfo }, member => new []{ member.ValueItemInfo, member.KeyItemInfo}.Where(n => n != null).Concat(member.Children).Where(m => !m.IsReference)), 
                   "Could not initialize serializer because a circular dependency has been detected please use [Persist(IsReference = true)] to avoid this exception");

            return persistInfo;
        }
        private PersistInfo PersistInfoFor(MemberMetadata memberMetadata, Dictionary<Type, PersistInfo> cache)
        {
            var persistInfo = new PersistInfo(memberMetadata.Info)
            {
                Name = memberMetadata.Attribute.Name ?? memberMetadata.Info.Name,
                IsReference = memberMetadata.Attribute.IsReference,
                RunConstructor = memberMetadata.Attribute.RunConstructor,
                ChildName = memberMetadata.Attribute.ChildName
            };

            Assert(!persistInfo.IsReference || persistInfo.PersistType != PersistType.Convertible,
                $"Cannot have a reference [Persist(IsReference=true)] on the simple type: {persistInfo.Type} in property {memberMetadata.Info.Name}!");

            if (persistInfo.PersistType == PersistType.Convertible)
                return persistInfo;

            if (cache.ContainsKey(persistInfo.Type))
            {
                var info = cache[persistInfo.Type];
                persistInfo.Children = info.Children;
                return persistInfo;
            }

            if (persistInfo.PersistType == PersistType.Complex)
                cache.Add(persistInfo.Type, persistInfo);

            switch (persistInfo.PersistType)
            {
            case PersistType.Complex:
                foreach (var metadata in MetadataFor(persistInfo.Type))
                    persistInfo.Children.Add(PersistInfoFor(metadata, cache));
                break;
            case PersistType.List:
                var valueType = persistInfo.Type.GetEnumeratedType();

                persistInfo.IsReference = false;
                persistInfo.RunConstructor = true;
                persistInfo.ValueItemInfo = new PersistInfo(valueType)
                {
                    IsReference = memberMetadata.Attribute.IsReference,
                    RunConstructor = memberMetadata.Attribute.RunConstructor,
                    Name = memberMetadata.Attribute.ChildName ?? (valueType.IsGenericType ? ItemKwd : valueType.Name)
                };
                PersistInfoFor(persistInfo.ValueItemInfo, cache);
                break;
            case PersistType.Dictionary:
                var valueTypes = persistInfo.Type.GetDictionaryTypes();

                persistInfo.IsReference = false;
                persistInfo.RunConstructor = true;
                persistInfo.ChildName = memberMetadata.Attribute.ChildName ?? ItemKwd;
                persistInfo.KeyItemInfo = new PersistInfo(valueTypes.Item1)
                {
                    Name = memberMetadata.Attribute.KeyName ?? (valueTypes.Item1.IsGenericType ? KeyKwd : valueTypes.Item1.Name)
                };
                persistInfo.ValueItemInfo = new PersistInfo(valueTypes.Item2)
                {
                    IsReference = memberMetadata.Attribute.IsReference,
                    RunConstructor = memberMetadata.Attribute.RunConstructor,
                    Name = memberMetadata.Attribute.ValueName ?? (valueTypes.Item2.IsGenericType ? ValueKwd : valueTypes.Item2.Name)
                };
                PersistInfoFor(persistInfo.KeyItemInfo, cache);
                PersistInfoFor(persistInfo.ValueItemInfo, cache);
                break;
            }

            Assert(!(persistInfo.ValueItemInfo?.IsReference ?? false) || persistInfo.ValueItemInfo.PersistType != PersistType.Convertible, 
                $"Cannot have a reference [Persist(IsReference=true)] on the simple type: {persistInfo.Type} in property {memberMetadata.Info.Name}!");

            return persistInfo;
        }
        private void PersistInfoFor(PersistInfo persistInfo, Dictionary<Type, PersistInfo> cache)
        {
            if (persistInfo.PersistType == PersistType.Convertible)
                return;

            cache = cache ?? new Dictionary<Type, PersistInfo>();
            if (cache.ContainsKey(persistInfo.Type))
            {
                persistInfo.Children = cache[persistInfo.Type].Children;
                return;
            }

            if (persistInfo.PersistType == PersistType.Complex)
                cache.Add(persistInfo.Type, persistInfo);

            switch (persistInfo.PersistType)
            {
            case PersistType.Complex:
                foreach (var metadata in MetadataFor(persistInfo.Type))
                    persistInfo.Children.Add(PersistInfoFor(metadata,cache));
                break;
            case PersistType.List:
                var valueType = persistInfo.Type.GetEnumeratedType();

                persistInfo.IsReference = false;
                persistInfo.RunConstructor = true;
                persistInfo.ValueItemInfo = new PersistInfo(valueType) { Name = valueType.IsGenericType ? ItemKwd : valueType.Name };
                PersistInfoFor(persistInfo.ValueItemInfo, cache);
                break;
            case PersistType.Dictionary:
                var valueTypes = persistInfo.Type.GetDictionaryTypes();

                persistInfo.IsReference = false;
                persistInfo.RunConstructor = true;
                persistInfo.KeyItemInfo = new PersistInfo(valueTypes.Item1)
                {
                    Name = valueTypes.Item1.IsGenericType ? KeyKwd : valueTypes.Item1.Name
                };
                persistInfo.ValueItemInfo = new PersistInfo(valueTypes.Item2)
                {
                    Name = valueTypes.Item2.IsGenericType ? ValueKwd : valueTypes.Item2.Name
                };
                PersistInfoFor(persistInfo.KeyItemInfo,cache);
                PersistInfoFor(persistInfo.ValueItemInfo, cache);
                break;
            }
        }
        private IEnumerable<MemberMetadata> MetadataFor(Type mainType)
        {
            const BindingFlags searchMode = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            if (!mainType.IsSealed)
            {
                PersistIncludeAttribute additional;
                if (m_additionalTypes != null && (additional = mainType.GetCustomAttribute<PersistIncludeAttribute>()) != null)
                    foreach (var additionalType in additional.AdditionalTypes.Where(t => !m_additionalTypes.ContainsKey(t)))
                    {
                        m_additionalTypes.Add(additionalType, null); //To avoid recursion
                        m_additionalTypes[additionalType] = PersistInfoFor(additionalType);
                    }
            }

            Type metaType;
            var isMetaType = MetaTypes.TryGetValue(mainType, out metaType);
            var isAnonymous = mainType.IsAnonymousType();
            var realFields = new FieldInfo[0];
            var realProperties = new PropertyInfo[0];


            if (isMetaType)
            {
                realFields = mainType.GetFields(searchMode);
                realProperties = mainType.GetProperties(searchMode);
                mainType = metaType;
            }
            else if (isAnonymous)
                realFields = mainType.GetFields(searchMode);

            var propertyMembers = mainType.GetProperties(searchMode)
                .Select(info => new { p = info, attr = info.GetCustomAttribute<PersistAttribute>() })
                .Where(_ => !isAnonymous)
                .Where(info =>
                {
                    return GetPersistType(PersistInfo.GetMemberType(info.p)) == PersistType.Convertible
                        ? info.p.SetMethod != null && info.p.SetMethod.IsPublic && (info.attr == null || info.attr.Ignore == false)
                        : (info.p.GetMethod.IsPublic
                            ? info.attr == null || info.attr.Ignore == false
                            : info.attr != null && info.attr.Ignore == false);
                })
                .Select(info => new MemberMetadata(isMetaType
                        ? realProperties.Single(p => p.Name == info.p.Name)
                        : info.p, info.attr ?? new PersistAttribute(info.p.Name)));

            IEnumerable<MemberMetadata> fieldMembers;
            if (isAnonymous)
            {
                fieldMembers = mainType.GetProperties(searchMode)
                    .Select(info => new MemberMetadata(realFields.First(fi => fi.Name.Contains($"<{info.Name}>")), new PersistAttribute(info.Name)));
            }
            else
            {
                fieldMembers = mainType.GetFields(searchMode).Select(info => new
                {
                    f = info,
                    attr = info.GetCustomAttribute<PersistAttribute>()
                }).Where(info => info.attr != null && info.attr.Ignore == false).Select(info => new MemberMetadata(isMetaType
                    ? realFields.Single(p => p.Name == info.f.Name)
                    : info.f, info.attr));
            }

            return propertyMembers.Concat(fieldMembers);
        }
        private static PersistType GetPersistType(Type type)
        {
            Type metaType;

            return typeof(IConvertible).IsAssignableFrom(type) || (MetaTypes.TryGetValue(type, out metaType) ? Attribute.IsDefined(metaType,typeof(TypeConverterAttribute)) : Attribute.IsDefined(type, typeof(TypeConverterAttribute)))
                ? PersistType.Convertible
                : (typeof(IDictionary).IsAssignableFrom(type) // || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    ? PersistType.Dictionary
                    : (typeof(IEnumerable).IsAssignableFrom(type)
                        ? PersistType.List
                        : PersistType.Complex));
        }
        private object CreateInstanceForCurrentObject(PersistInfo info)
        {
            if (info.IsAnonymousType)
                return FormatterServices.GetUninitializedObject(info.Type);

            string typeName = (string)ReadValue(ClassKwd, typeof(string));
            Type typeToCreate = typeName == null
                ? info.Type
                : m_additionalTypes.Keys.FirstOrDefault(t => GetFriendlyName(t) == typeName || GetFriendlyName(t, true) == typeName);

            if (m_isDynamic && typeToCreate == null)
            {
                typeToCreate = GetFriendlyType(typeName);
                m_additionalTypes.Add(typeToCreate, PersistInfoFor(typeToCreate));
            }

            if (typeToCreate == null || !info.Type.IsAssignableFrom(typeToCreate ?? info.Type))
                typeToCreate = info.Type;

            typeToCreate = typeToCreate ?? info.Type;

            return info.RunConstructor ? Activator.CreateInstance(typeToCreate) : FormatterServices.GetUninitializedObject(typeToCreate);
        }
        private string GetFriendlyName(Type type, bool forceFullName = false)
        {
            var friendlyName = m_isDynamic || forceFullName
                ? type.FullName
                : type.Name;
            if (!type.IsGenericType)
                return friendlyName;

            var iBacktick = friendlyName.IndexOf('`');
            if (iBacktick > 0)
                friendlyName = friendlyName.Remove(iBacktick);

            var genericParameters = type.GetGenericArguments().Select(t => GetFriendlyName(t, forceFullName));
            friendlyName += "<" + string.Join(", ", genericParameters) + ">";

            return friendlyName;
        }
        private static Type GetFriendlyType(string typeName)
        {
            int index = typeName.IndexOf("<");
            if (index == -1)
            {
                return Type.GetType(typeName) 
                       ?? Assembly.GetEntryAssembly().GetType(typeName) 
                       ?? AppDomain.CurrentDomain.GetAssemblies()
                                   .Except(new[] {Assembly.GetEntryAssembly(), Assembly.GetAssembly(typeof(int)),})
                                   .Select(a => a.GetType(typeName)).First(t => t != null);
            }

            string genericClassType = typeName.Substring(0, index);
            string genericParameters = typeName.Substring(index + 1);
            genericParameters = genericParameters.Substring(0, genericParameters.Length - 1);

            int open = 0;
            int close = 0;
            int i = 0;
            bool found = false;
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

            var type1 = genericParameters.Substring(0, i).Trim();
            var type2 = found ? genericParameters.Substring(i + 1).Trim() : null;
            var numberArgs = found ? 2 : 1;
            var genericClassTypeComplete = $"{genericClassType}`{numberArgs}";

            var type = GetFriendlyType(genericClassTypeComplete);

            return numberArgs == 1
                ? type.MakeGenericType(GetFriendlyType(type1))
                : type.MakeGenericType(GetFriendlyType(type1), GetFriendlyType(type2));
        }

        //Helper Classes
        private enum PersistType
        {
            Complex,
            List,
            Dictionary,
            Convertible
        }
        private class PersistInfo
        {
            private readonly Lazy<bool> m_isAnonymousType;

            public string Name { get; set; }

            public Type Type { get; }
            public PersistType PersistType { get; }

            public bool IsAnonymousType => m_isAnonymousType.Value;
            public bool IsReference { get; set; }
            public bool RunConstructor { get; set; } = true;

            public PersistInfo KeyItemInfo { get; set; }
            public PersistInfo ValueItemInfo { get; set; }
            public string ChildName { get; set; } = ItemKwd;

            public List<PersistInfo> Children { get; set; } = new List<PersistInfo>();

            public Func<object, object> GetValue { get; }
            public Action<object, object> SetValue { get; }

            public PersistInfo(Type type)
            {
                Type = type;
                PersistType = GetPersistType(type);

                m_isAnonymousType = new Lazy<bool>(() => Type.IsAnonymousType());
            }
            public PersistInfo(MemberInfo info)
            {
                PropertyInfo prp = info as PropertyInfo;
                FieldInfo fld = info as FieldInfo;

                Type = prp?.PropertyType ?? fld?.FieldType;
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
        private struct MemberMetadata
        {
            public readonly MemberInfo Info;
            public readonly PersistAttribute Attribute;

            public MemberMetadata(MemberInfo m, PersistAttribute a)
            {
                Info = m;
                Attribute = a;
            }
        }
    }
}