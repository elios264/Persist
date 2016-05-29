using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace elios.Persist
{

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

            public ParentNode(Node realNode) : base(string.Empty)
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

        protected TreeArchive(Type type, Type[] polymorphicTypes) : base(type, polymorphicTypes)
        {
            m_context = new Stack<Node>();
            m_writeReferences = new HashSet<long>();
            m_readReferences = new Dictionary<string, object>();
        }

        protected abstract void WriteNode(Stream target, Node root);
        protected abstract Node ParseNode(Stream source);

        public override void Write(Stream target, object data, string rootName = null)
        {
            lock (this)
            {
                WriteMain(data, rootName);
                ResolveWriteReferences();
                WriteNode(target, m_root);
                m_root = null;
            }
        }
        public override void Write(string target, object data, string rootName = null)
        {
            using (var writeStream = new FileStream(target, FileMode.Create))
                Write(writeStream,data,rootName);
        }

        protected override void BeginWriteObject(string name, bool isContainer = false)
        {
            if (m_context.Count == 0)
            {
                m_root = new Node(name) { IsContainer = isContainer};
                m_context.Push(m_root);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                var element = new Node(name) { IsContainer = isContainer};
                Current.Nodes.Add(element);
                m_context.Push(element);
            }
            else
            {
                m_context.Push(new ParentNode(Current));
            }
        }
        protected override void EndWriteObject(long id)
        {
            m_context.Pop().Id = id;
        }
        protected override void WriteReference(string name, long id)
        {
            Current.Attributes.Add(new NodeAttribute(name, id));
            m_writeReferences.Add(id);
        }
        protected override void WriteValue(string name, object data)
        {
            Current.Attributes.Add(new NodeAttribute(name,(IConvertible)data));
        }


        public override object Read(Stream source)
        {
            var firstStep = ParseNode(source);
            var secondStep = new Node(firstStep);

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
        public override object Read(string source)
        {
            using (var readStream = new FileStream(source, FileMode.Open))
                return Read(readStream);
        }

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
        protected override void EndReadObject(object value)
        {
            var objId = Current.Attributes.FirstOrDefault(attr => attr.Name == AddressKwd)?.Value;

            if (!(Current is ParentNode) &&  value!= null && objId != null)
            {
                m_readReferences.Add(objId,value);
            }

            m_context.Pop();
        }
        protected override object ReadValue(string name, Type type)
        {
            var value = Current.Attributes.FirstOrDefault(attr => attr.Name == name)?.Value;

            if (value != null)
            {
                return typeof (Enum).IsAssignableFrom(type) 
                    ? Enum.Parse(type, value) 
                    : Convert.ChangeType(value, type);
            }

            return null;
        }
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

        protected override int GetObjectChildrenCount(string name)
        {
            return Current.IsContainer 
                ? Current.Nodes.Count
                : Current.Nodes.Count(e => e.Name == name);
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