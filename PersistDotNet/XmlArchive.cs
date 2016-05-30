using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace elios.Persist
{

    /// <summary>
    /// Xml serializer
    /// </summary>
    /// <seealso cref="elios.Persist.TreeArchive" />
    public sealed class XmlArchive : TreeArchive
    {
        /// <summary>
        /// Initializes <see cref="XmlArchive"/> that reads archives of the specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="polymorphicTypes"></param>
        public XmlArchive(Type type, IEnumerable<Type> polymorphicTypes = null) : base(type, polymorphicTypes)
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="XmlArchive"/> class class using the metadata from other serializer.
        /// </summary>
        /// <param name="archive">The archive.</param>
        public XmlArchive(Archive archive) : base(archive)
        {
        }

        /// <summary>
        /// Writes a node to the <see cref="Stream" />
        /// </summary>
        /// <param name="target"></param>
        /// <param name="root"></param>
        protected override void WriteNode(Stream target, Node root)
        {
           SaveNode(target,root);
        }
        /// <summary>
        /// Parses a node form the <see cref="Stream" />
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected override Node ParseNode(Stream source)
        {
            return LoadNode(source);
        }

        /// <summary>
        /// Writes a node to a xml <see cref="Stream"/>
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="node">The node.</param>
        public static void SaveNode(Stream target, Node node)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode mainNode = doc.CreateElement(node.Name);
            doc.AppendChild(mainNode);

            WriteNode(doc, mainNode, node);
            doc.Save(target);
        }
        /// <summary>
        /// Parses a node from a xml <see cref="Stream"/>
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static Node LoadNode(Stream source)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(source);

            Node mainNode = new Node(doc.DocumentElement.Name);

            ParseNode(doc.DocumentElement, mainNode);

            return mainNode;
        }

        internal static void ParseNode(XmlNode curXmlNode, Node curNode)
        {
            curNode.Attributes.AddRange(curXmlNode.Attributes.Cast<XmlAttribute>().Select(attribute => new NodeAttribute(attribute.Name,attribute.Value)));

            foreach (XmlNode xmlNode in curXmlNode.ChildNodes)
            {
                Node childNode = new Node(xmlNode.Name);
                curNode.Nodes.Add(childNode);

                ParseNode(xmlNode, childNode);
            }

        }
        internal static void WriteNode(XmlDocument doc, XmlNode xmlNode, Node node)
        {
            foreach (var attribute in node.Attributes)
            {
                var attr = doc.CreateAttribute(attribute.Name);
                attr.Value = attribute.Value;
                xmlNode.Attributes.Append(attr);
            }

            foreach (var e in node.Nodes)
            {
                var childNode = doc.CreateElement(e.Name);
                xmlNode.AppendChild(childNode);

                WriteNode(doc, childNode, e);
            }
        }

    }

}