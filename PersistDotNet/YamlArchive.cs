using System;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace elios.Persist
{

    public sealed class YamlArchive : TreeArchive
    {
        public YamlArchive(Type type, Type[] polymorphicTypes = null) : base(type, polymorphicTypes)
        {
        }

        protected override void WriteNode(Stream target, Node root)
        {
            SaveNode(target, root);
        }
        protected override Node ParseNode(Stream source)
        {
            return LoadNode(source);
        }

        public static Node LoadNode(Stream source)
        {
            YamlDocument doc;

            using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
            {
                var stream = new YamlStream();
                stream.Load(reader);

                doc = stream.Documents.Single();
            }

            var mainNode = new Node(string.Empty);

            ParseNode(doc.RootNode, mainNode);

            return mainNode;
        }
        public static void SaveNode(Stream target, Node node)
        {
            YamlDocument doc = new YamlDocument(new YamlMappingNode());
            WriteNode((YamlMappingNode)doc.RootNode, node);

            using (var writer = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
            {
                var stream = new YamlStream(doc);
                stream.Save(writer, false);
            }
        }

        internal static void ParseNode(YamlNode curYamlNode, Node curNode)
        {
            if (curYamlNode is YamlMappingNode)
            {
                foreach (var pair in ((YamlMappingNode)curYamlNode).Children)
                {
                    if (pair.Value is YamlScalarNode)
                        curNode.Attributes.Add(new NodeAttribute(( (YamlScalarNode) pair.Key ).Value, ( (YamlScalarNode) pair.Value ).Value));
                    else
                    {
                        var childNode = new Node(pair.Key.ToString());

                        curNode.Nodes.Add(childNode);
                        ParseNode(pair.Value,childNode);
                    }
                }
            }
            else
            {
                curNode.IsContainer = true;

                foreach (var node in ((YamlSequenceNode)curYamlNode).Children)
                {
                    var childNode = new Node(string.Empty);

                    curNode.Nodes.Add(childNode);
                    ParseNode(node, childNode);
                }
            }
        }
        internal static void WriteNode(YamlNode yamlNode, Node node)
        {
            if (yamlNode is YamlMappingNode)
                foreach (var attribute in node.Attributes)
                {
                    ((YamlMappingNode)yamlNode).Add(new YamlScalarNode(attribute.Name), new YamlScalarNode(attribute.Value));
                }
            else if (node.Attributes.Count > 0)
                throw new InvalidOperationException("arrays cannot contain attributes");

            foreach (var e in node.Nodes)
            {
                YamlNode childNode;

                if (e.IsContainer)
                {
                    childNode = new YamlSequenceNode();
                    WriteNode(childNode, e);
                }
                else
                {
                    childNode = new YamlMappingNode();
                    WriteNode(childNode, e);
                }

                if (yamlNode is YamlMappingNode)
                {
                    var mNode = ((YamlMappingNode)yamlNode);

                    if (mNode.Children.ContainsKey(new YamlScalarNode(e.Name)))
                        throw new InvalidOperationException(" YamlArchive/JsonArchive cannot serialize anonymous containers aka [Persist(\"\")]");
                    mNode.Add(new YamlScalarNode(e.Name), childNode);
                }
                else
                    ((YamlSequenceNode)yamlNode).Add(childNode);
            }
        }

    }
}