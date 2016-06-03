using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace elios.Persist
{

    /// <summary>
    /// Creates Archives in Tree formats like (xml, json, yaml), derive from it if you want to support a new tree format
    /// </summary>
    /// <remarks>this class is thread safe</remarks>
    /// <seealso cref="elios.Persist.Archive" />
    public abstract class TreeArchive : Archive
    {
        private class ParentNode : Node
        {
            private readonly Node m_realNode;

            public override string Name
            {
                get { return m_realNode.Name; }
            }
            internal override long Id
            {
                get { return m_realNode.Id; }
                set { m_realNode.Id = value; }
            }

            public override bool IsContainer
            {
                get { return m_realNode.IsContainer; }
                set { m_realNode.IsContainer = value; }
            }

            public override List<NodeAttribute> Attributes
            {
                get { return m_realNode.Attributes; }
            }
            public override List<Node> Nodes
            {
                get { return m_realNode.Nodes; }
            }

            public ParentNode(Node realNode)
            {
                m_realNode = realNode;
            }
        }

        private Node m_root;
        private readonly Stack<Node> m_context;
        private readonly HashSet<long> m_writeReferences;
        private readonly Dictionary<string, object> m_readReferences;

        private Node Current
        {
            get { return m_context.Peek(); }
        }

        /// <summary>
        /// abstract class constructor
        /// </summary>
        /// <param name="type"></param>
        /// <param name="additionalTypes"></param>
        protected TreeArchive(Type type, params Type[] additionalTypes) : base(type, additionalTypes)
        {
            m_context = new Stack<Node>();
            m_writeReferences = new HashSet<long>();
            m_readReferences = new Dictionary<string, object>();
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="TreeArchive"/> class.
        /// </summary>
        /// <param name="archive">The archive.</param>
        protected TreeArchive(Archive archive) : base(archive)
        {
        }


        /// <summary>
        /// Writes a node to the <see cref="Stream"/>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="root"></param>
        protected abstract void WriteNode(Stream target, Node root);
        /// <summary>
        /// Parses a node form the <see cref="Stream"/>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected abstract Node ParseNode(Stream source);

        /// <summary>
        /// Writes the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="data">The data.</param>
        /// <param name="rootName">Name of the root.</param>
        public override void Write(string target, object data, string rootName = null)
        {
            using (var writeStream = new FileStream(target, FileMode.Create))
                Write(writeStream,data,rootName);
        }
        /// <summary>
        /// Serializes the specified data into the stream
        /// </summary>
        /// <param name="target">target serialization stream</param>
        /// <param name="data">data to serialize</param>
        /// <param name="rootName">root name of the document (eg. xml doc rootname)</param>
        public override void Write(Stream target, object data, string rootName = null)
        {
            WriteNode(target,Write(data, rootName));
        }
        /// <summary>
        /// Serializes the specified data into a Node.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="rootName">Name of the root (usually for xml archives)</param>
        /// <returns></returns>
        public Node Write(object data, string rootName = null)
        {
            lock (this)
            {
                WriteMain(data,rootName);
                ResolveWriteReferences();
                var node = m_root;
                m_root = null;
                return node;
            }
        }

        /// <summary>
        /// A nested object begins to be written
        /// </summary>
        /// <param name="name">object name</param>
        protected override void BeginWriteObject(string name)
        {
            if (m_context.Count == 0)
            {
                m_root = new Node { Name = name};
                m_context.Push(m_root);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                var element = new Node { Name = name};
                Current.Nodes.Add(element);
                m_context.Push(element);
            }
            else
            {
                m_context.Push(new ParentNode(Current));
            }
        }
        /// <summary>
        /// Ends writing a nested object
        /// </summary>
        /// <param name="id">an unique id for the object for bookeeping</param>
        protected override void EndWriteObject(long id)
        {
            m_context.Pop().Id = id;
        }
        /// <summary>
        /// Writes a reference of an object instead of its value
        /// </summary>
        /// <param name="name">name for the reference</param>
        /// <param name="id">id of the reference</param>
        protected override void WriteReference(string name, long id)
        {
            Current.Attributes.Add(new NodeAttribute(name, id));
            m_writeReferences.Add(id);
        }
        /// <summary>
        /// writes a value for the current nested object
        /// </summary>
        /// <param name="name">value name</param>
        /// <param name="data">value</param>
        protected override void WriteValue(string name, object data)
        {
            if (name != ClassKwd || !( Current is ParentNode ))
                Current.Attributes.Add(new NodeAttribute(name, (IConvertible)data));
            else
                throw new InvalidOperationException($"Inside Object: {{{Current.Name}}}. Anonymous containers ( aka [Persist(\"\")] ) are not allowed to be polymorphic");
        }

        /// <summary>
        /// Deserializes the archive contained in the specified filePath
        /// </summary>
        /// <param name="filePath">the stream that contains the <see cref="Archive" /> to deserialize</param>
        /// <returns></returns>
        public override object Read(string filePath)
        {
            using (var readStream = new FileStream(filePath, FileMode.Open))
                return Read(readStream);
        }
        /// <summary>
        /// Deserializes the archive contained by the specified <see cref="Stream" />
        /// </summary>
        /// <param name="source">the stream that contains the <see cref="Archive" /> to deserialize</param>
        /// <returns></returns>
        public override object Read(Stream source)
        {
            return Read(ParseNode(source));
        }
        /// <summary>
        /// Deserializes the arhive contained in the specified <see cref="Node"/>
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public object Read(Node node)
        {
            var firstStep = new Node(node);
            var secondStep = new Node(node);

            lock (this)
            {
                m_root = firstStep;
                var result = ReadMain();
                if (m_readReferences.Count > 0) //Resolve if there are pending references
                {
                    m_root = secondStep;
                    ResolveMain(result);
                    m_readReferences.Clear();
                }

                m_root = null;
                return result;
            }
        }


        /// <summary>
        /// A nested object begins to be read
        /// </summary>
        /// <param name="name">object name</param>
        /// <returns></returns>
        protected override bool BeginReadObject(string name)
        {
            if (m_context.Count == 0)
            {
                m_context.Push(m_root);
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                m_context.Push(new ParentNode(Current));
                return true;
            }

            var cur =  Current.IsContainer ? Current.Nodes.FirstOrDefault() : Current.Nodes.FirstOrDefault(element => element.Name == name);
            if (cur != null)
            {
                Current.Nodes.Remove(cur);
                m_context.Push(cur);
                return true;
            }

            return false;
        }
        /// <summary>
        /// Ends the read object.
        /// </summary>
        /// <param name="value">The value.</param>
        protected override void EndReadObject(object value)
        {
            var objId = Current.Attributes.FirstOrDefault(attr => attr.Name == AddressKwd)?.Value;

            if (!(Current is ParentNode) &&  value!= null && objId != null)
            {
                m_readReferences.Add(objId,value);
            }

            m_context.Pop();
        }
        /// <summary>
        /// Reads a value for the current nested object and cast it to the specified type
        /// </summary>
        /// <param name="name">value name</param>
        /// <param name="type">value type</param>
        /// <returns></returns>
        protected override object ReadValue(string name, Type type)
        {
            var value = Current.Attributes.FirstOrDefault(attr => attr.Name == name)?.Value;

            if (value != null)
            {
                return typeof (Enum).IsAssignableFrom(type) 
                    ? Enum.Parse(type, value) 
                    : Convert.ChangeType(value, type,Provider);
            }

            return null;
        }
        /// <summary>
        /// reads a reference
        /// </summary>
        /// <param name="name">name of the reference</param>
        /// <returns></returns>
        /// <exception cref="SerializationException">unresolved reference  + id</exception>
        protected override object ReadReference(string name)
        {
            var id = Current.Attributes.FirstOrDefault(attr => attr.Name == name)?.Value;

            object reference;

            if (m_readReferences.TryGetValue(id, out reference))
            {
                return reference;
            }

            throw new SerializationException("unresolved reference " + id);
        }

        /// <summary>
        /// when reading or resolving queries for the number of children of the current object
        /// </summary>
        /// <param name="name">filter string children name</param>
        /// <returns></returns>
        protected override int GetObjectChildrenCount(string name)
        {
            return Current.IsContainer 
                ? Current.Nodes.Count
                : Current.Nodes.Count(e => e.Name == name);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current object needs to be a container
        /// </summary>
        /// <value>
        /// <c>true</c> if the current object needs to be a container otherwise, <c>false</c>.
        /// </value>
        protected override bool IsCurrentObjectContainer
        {
            get { return Current.IsContainer; }
            set { Current.IsContainer = value; }
        }

        private void ResolveWriteReferences()
        {
            if (m_writeReferences.Count == 0)
                return;

            m_context.Push(m_root);

            while (m_context.Count > 0)
            {
                Node e = m_context.Pop();

                if (e.Id > 0 && m_writeReferences.Contains(e.Id))
                {
                    e.Attributes.Add(new NodeAttribute(AddressKwd, e.Id));
                    m_writeReferences.Remove(e.Id);
                }

                foreach (var element in e.Nodes)
                {
                    m_context.Push(element);
                }
            }

            if (m_writeReferences.Count > 0)
            {
                throw new SerializationException("unresolved reference");
            }
        }
    }
}