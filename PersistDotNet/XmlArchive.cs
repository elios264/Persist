using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace elios.Persist
{

    public sealed class XmlArchive : TreeArchive
    {
        public XmlArchive(Type type, Type[] polymorphicTypes = null) : base(type, polymorphicTypes)
        {
        }

        protected override void WriteNode(Stream target, Node root)
        {
           SaveNode(target,root);
        }
        protected override Node ParseNode(Stream source)
        {
            return LoadNode(source);
        }

        public static void SaveNode(Stream target, Node node)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode mainNode = doc.CreateElement(node.Name);
            doc.AppendChild(mainNode);

            WriteNode(doc, mainNode, node);
            doc.Save(target);
        }
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