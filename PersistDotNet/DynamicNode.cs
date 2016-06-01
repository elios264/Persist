using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace elios.Persist
{
    internal class DynamicNode : DynamicObject, IDictionary<string,object> 
    {
        private readonly Node m_node;
        private static readonly MethodInfo[] DictionaryMethods = typeof(DynamicNode).GetInterfaceMap(typeof(IDictionary<string, object>)).TargetMethods.Concat(typeof(DynamicNode).GetInterfaceMap(typeof(ICollection<KeyValuePair<string, object>>)).TargetMethods).ToArray();

        internal DynamicNode(Node node)
        {
            m_node = node;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return TryGetValue(binder.Name, out result);
        }
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this[binder.Name] = value;
            return true;
        }
        public override bool TryDeleteMember(DeleteMemberBinder binder)
        {
            return Remove(binder.Name);
        }

        public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes)
        {
            if (indexes.Length > 1)
                throw new NotSupportedException("Rank of 1 is the max");

            var key = indexes[0];

            if (key is int)
                return Remove(this.ElementAt((int)key));

            return Remove(key.ToString());
        }
        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (indexes.Length > 1)
                throw new NotSupportedException("Rank of 1 is the max");

            var key = indexes[0];

            if (key is int)
            {
                result = this.ElementAtOrDefault((int)key).Value;
                return result != null;
            }
            
            return TryGetValue(key.ToString(), out result);
        }
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (indexes.Length > 1)
                throw new NotSupportedException("Rank of 1 is the max");

            var key = indexes[0];

            if (key is int)
                this[(int)key] = value;
            else
                this[key.ToString()] = value;

            return true;
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            return ( result = binder.Type == typeof(Node)
                ? (object)m_node
                : ( binder.Type == typeof(IDictionary<string, object>)
                    ? this
                    : null ) ) != null;
        }
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return Keys;
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            foreach (var method in DictionaryMethods)
            {
                result = null;

                if (method.Name != binder.Name)
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length != args.Length)
                    continue;

                if (!parameters.Zip(args, Tuple.Create).All(t => t.Item1.ParameterType.IsAssignableFrom(t.Item1.ParameterType)))
                    continue;

                result = method.Invoke(this, args);

                return true;
            }

            return base.TryInvokeMember(binder, args, out result);
        }

        public override string ToString()
        {
            return m_node.ToString();
        }

        #region DicionaryImplementation


        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (var attribute in m_node.Attributes)
                yield return new KeyValuePair<string, object>(attribute.Name, attribute.Value);

            foreach (var node in m_node.Nodes)
                yield return new KeyValuePair<string, object>(node.Name, node.AsDynamic());
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<string, object> item)
        {
            if (item.Value is IConvertible)
                m_node.Attributes.Add(new NodeAttribute(item.Key, (IConvertible)item.Value));
            else
                m_node.Nodes.Add(new XmlArchive(item.Value.GetType()).Write(item.Value, item.Key));
        }
        public void Clear()
        {
            m_node.Attributes.Clear();
            m_node.Nodes.Clear();
        }
        public bool Contains(KeyValuePair<string, object> item)
        {
            var idx = m_node.Attributes.FindIndex(a => a.Name == item.Key);
            if (idx > -1 && m_node.Attributes[idx].Value == item.Value.ToString())
                return true;

            idx = m_node.Nodes.FindIndex(n => n.Name == item.Key);
            if (idx > -1 && new XmlArchive(item.Value.GetType()).Write(item.Value, item.Key).Equals(m_node.Nodes[idx]))
                return true;

            return false;
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            foreach (var element in this)
                array[arrayIndex++] = element;
        }
        public bool Remove(KeyValuePair<string, object> item)
        {
            var idx = m_node.Attributes.FindIndex(a => a.Name == item.Key);
            if (idx > -1 && m_node.Attributes[idx].Value == item.Value.ToString())
            {
                m_node.Attributes.RemoveAt(idx);
                return true;
            }

            idx = m_node.Nodes.FindIndex(n => n.Name == item.Key);
            if (idx > -1 && new XmlArchive(item.Value.GetType()).Write(item.Value, item.Key).Equals(m_node.Nodes[idx]))
            {
                m_node.Attributes.RemoveAt(idx);
                return true;
            }

            return false;
        }
        public int Count
        {
            get { return m_node.Attributes.Count + m_node.Nodes.Count; }
        }
        public bool IsReadOnly
        {
            get { return false; }
        }
        public bool ContainsKey(string key)
        {
            return m_node.Attributes.Any(a => a.Name == key) || m_node.Nodes.Any(n => n.Name == key);
        }
        public void Add(string key, object value)
        {
            Add(new KeyValuePair<string, object>(key,value));
        }
        public bool Remove(string key)
        {
            var idx = m_node.Attributes.FindIndex(a => a.Name == key);
            if (idx > -1)
            {
                m_node.Attributes.RemoveAt(idx);
                return true;
            }


            idx = m_node.Nodes.FindIndex(n => n.Name == key);
            if (idx > -1)
            {
                m_node.Nodes.RemoveAt(idx);
                return true;
            }

            return false;
        }
        public bool TryGetValue(string key, out object value)
        {
            var idx = m_node.Attributes.FindIndex(a => a.Name == key);
            if (idx > -1)
            {
                value = m_node.Attributes[idx].Value;
                return true;
            }


            idx = m_node.Nodes.FindIndex(n => n.Name == key);
            if (idx > -1)
            {
                value = m_node.Nodes[idx].AsDynamic();
                return true;
            }

            value = null;
            return false;
        }
        public object this[string key]
        {
            get
            {
                object value;
                if (TryGetValue(key, out value))
                    return value;

                throw new KeyNotFoundException(key);
            }
            set
            {
                var idx = m_node.Attributes.FindIndex(a => a.Name == key);
                if (idx > -1)
                {
                    if (value is IConvertible)
                        m_node.Attributes[idx].Value = ( (IConvertible)value ).ToString(Archive.Provider);
                    else
                    {
                        m_node.Attributes.RemoveAt(idx);
                        m_node.Nodes.Add(new XmlArchive(value.GetType()).Write(key, key));
                    }

                    return;
                }

                idx = m_node.Nodes.FindIndex(n => n.Name == key);
                if (idx > -1)
                {
                    if (value is IConvertible)
                    {
                        m_node.Nodes.RemoveAt(idx);
                        m_node.Attributes.Add(new NodeAttribute(key, (IConvertible)value));
                    }
                    else
                        m_node.Nodes[idx] = new XmlArchive(value.GetType()).Write(key, key);

                    return;
                }

                if (value is IConvertible)
                    m_node.Attributes.Add(new NodeAttribute(key, (IConvertible)value));
                else
                    m_node.Nodes.Add(new XmlArchive(value.GetType()).Write(value, key));
            }
        }
        public object this[int idx]
        {
            get { return this.ElementAt(idx).Value; }
            set
            {
                if (idx < m_node.Attributes.Count)
                {
                    if (value is IConvertible)
                        m_node.Attributes[idx].Value = ( (IConvertible)value ).ToString(Archive.Provider);
                    else
                        throw new InvalidOperationException("object is not IConvertible");

                    return;
                }

                if (idx < Count)
                {
                    if (value is IConvertible)
                    {
                        throw new InvalidOperationException("object is IConvertible");
                    }

                    var node = new XmlArchive(value.GetType()).Write(value);
                    node.Name = null;
                    m_node.Nodes[idx - m_node.Attributes.Count] = node;

                    return;
                }

                if (value is IConvertible)
                    throw new InvalidOperationException("object is IConvertible");

                if (idx == Count)
                {
                    var n = new XmlArchive(value.GetType()).Write(value);
                    n.Name = null;
                    m_node.Nodes.Add(n);
                }
            }
        }
        public ICollection<string> Keys
        {
            get { return new Collection<string>(m_node); }
        }
        public ICollection<object> Values
        {
            get { return new Collection<object>(m_node); }
        }

        private class Collection<T> : ICollection<T>, IReadOnlyCollection<T> where T : class
        {
            private readonly Node m_node;
            private static readonly bool IsKeys = typeof(T) == typeof(string);

            public Collection(Node node)
            {
                m_node = node;
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (var attribute in m_node.Attributes)
                    yield return ((IsKeys ?  attribute.Name : attribute.Value) as T );

                foreach (var node in m_node.Nodes)
                    yield return ((IsKeys ? node.Name : node.AsDynamic()) as T);
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            public void Add(T item)
            {
                throw new ReadOnlyException();
            }
            public void Clear()
            {
                throw new ReadOnlyException();
            }
            public bool Contains(T item)
            {
                if (IsKeys)
                {
                    return m_node.Attributes.Any(a => a.Name == (item as string)) || m_node.Nodes.Any(n => n.Name == (item as string));
                }

                return m_node.Attributes.Any(a => a.Value == (item as string)) || m_node.Nodes.Any(n =>
                {
                    var archive = new XmlArchive(item.GetType());

                    return n == archive.Write(item, n.Name);
                });
            }
            public void CopyTo(T[] array, int arrayIndex)
            {
                foreach (var item in this)
                    array[arrayIndex++] = item;
            }
            public bool Remove(T item)
            {
                throw new ReadOnlyException();
            }
            public int Count
            {
                get { return m_node.Attributes.Count + m_node.Nodes.Count; }

            }
            public bool IsReadOnly
            {
                get { return true; }
            }
        }

        #endregion

    }
}