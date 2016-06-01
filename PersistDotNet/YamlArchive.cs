using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using YamlDotNet.RepresentationModel;

namespace elios.Persist
{

    /// <summary>
    /// Yaml Serializer
    /// </summary>
    /// <remarks>this class is thread safe</remarks>
    /// <seealso cref="elios.Persist.TreeArchive" />
    public sealed class YamlArchive : TreeArchive
    {
        /// <summary>
        /// Initializes <see cref="YamlArchive"/> that reads archives of the specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="polymorphicTypes"></param>
        public YamlArchive(Type type, IEnumerable<Type> polymorphicTypes = null) : base(type, polymorphicTypes)
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="YamlArchive"/> class.
        /// </summary>
        /// <param name="archive">The archive.</param>
        public YamlArchive(Archive archive) : base(archive)
        {
        }


        /// <summary>
        /// Writes a node to the <see cref="Stream" />
        /// </summary>
        /// <param name="target"></param>
        /// <param name="root"></param>
        protected override void WriteNode(Stream target, Node root)
        {
            SaveNode(target, root);
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
        /// Parses a node from a yaml <see cref="Stream"/>
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static Node LoadNode(Stream source)
        {
            YamlDocument doc;

            using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
            {
                var stream = new YamlStream();
                stream.Load(reader);

                doc = stream.Documents.Single();
            }

            var mainNode = new Node {Name = string.Empty };

            try
            {
                ParseNode(doc.RootNode, mainNode);
            }
            catch (Exception)
            {
                throw new SerializationException("invalid json/yaml document");
            }
            return mainNode;
        }
        /// <summary>
        /// writes a <see cref="Node"/> to a yaml stream
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="node">The node.</param>
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

        private static void ParseNode(YamlNode curYamlNode, Node curNode)
        {
            if (curYamlNode is YamlMappingNode)
            {
                foreach (var pair in ((YamlMappingNode)curYamlNode).Children)
                {
                    if (pair.Value is YamlScalarNode)
                        curNode.Attributes.Add(new NodeAttribute(( (YamlScalarNode) pair.Key ).Value, ( (YamlScalarNode) pair.Value ).Value));
                    else
                    {
                        var childNode = new Node {Name = pair.Key.ToString() };

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
                    var childNode = new Node {Name = string.Empty };

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