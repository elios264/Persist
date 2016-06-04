using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using YamlDotNet.RepresentationModel;
using static elios.Persist.Utils;

namespace elios.Persist
{

    /// <summary>
    /// Yaml Serializer
    /// </summary>
    /// <remarks>this class is thread safe</remarks>
    /// <seealso cref="elios.Persist.TreeArchive" />
    public sealed class YamlArchive : TreeArchive
    {
        private const string PolymorphicContainerKeyword = "items";
        private const string ErrorAnonymous = "YamlArchive/JsonArchive cannot serialize anonymous containers aka [Persist(\"\")]";


        /// <summary>
        /// Initializes <see cref="YamlArchive"/> that reads archives of the specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="additionalTypes"></param>
        public YamlArchive(Type type, params Type[] additionalTypes ) : base(type, additionalTypes)
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
            try
            {
                YamlDocument yamlDoc;

                using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                {
                    var stream = new YamlStream();
                    stream.Load(reader);
                    yamlDoc = stream.Documents.Single();
                }

                return ParseNode((YamlMappingNode)yamlDoc.RootNode, new Node { Name = string.Empty });
            }
            catch (Exception)
            {
                throw new SerializationException("invalid json/yaml document");
            }
        }
        /// <summary>
        /// writes a <see cref="Node"/> to a yaml stream
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="node">The node.</param>
        public static void SaveNode(Stream target, Node node)
        {
            var mainYamlNode = new YamlMappingNode();
            var yamlDoc = new YamlDocument(mainYamlNode);

            WriteNode(mainYamlNode, node);

            using (var writer = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                new YamlStream(yamlDoc).Save(writer, false);
        }

        private static Node ParseNode(YamlMappingNode yamlNode, Node node)
        {
            if (yamlNode.Children.Count == 2)
            {
                var keys = new YamlNode[2];
                yamlNode.Children.Keys.CopyTo(keys,0);

                if (((YamlScalarNode)keys[0]).Value == ClassKwd && ((YamlScalarNode)keys[1]).Value == PolymorphicContainerKeyword)
                {
                    var values = new YamlNode[2];
                    yamlNode.Children.Values.CopyTo(values,0);

                    node.IsContainer = true;
                    node.Attributes.Add(new NodeAttribute(ClassKwd,((YamlScalarNode)values[0]).Value));

                    return ParseNode((YamlSequenceNode)values[1], node);
                }
            }

            foreach (var mapping in yamlNode.Children)
            {
                string key = ((YamlScalarNode)mapping.Key).Value;

                if (mapping.Value is YamlScalarNode)
                {
                    node.Attributes.Add(new NodeAttribute(key,mapping.Value.ToString()));
                }
                else if (mapping.Value is YamlMappingNode)
                {
                    node.Nodes.Add(ParseNode((YamlMappingNode)mapping.Value, new Node { Name = key }));
                }
                else if (mapping.Value is YamlSequenceNode)
                {
                    node.Nodes.Add(ParseNode((YamlSequenceNode)mapping.Value, new Node { Name = key, IsContainer = true }));
                }
            }

            return node;
        }
        private static Node ParseNode(YamlSequenceNode yamlNode, Node node)
        {
            foreach (var child in yamlNode)
            {
                if (child is YamlMappingNode)
                {
                    node.Nodes.Add(ParseNode((YamlMappingNode)child,new Node {Name = string.Empty} ));
                }
                else
                {
                    node.Nodes.Add(ParseNode((YamlSequenceNode)child, new Node { Name = string.Empty, IsContainer = true }));
                }
            }
            return node;
        }
        internal static void WriteNode(YamlMappingNode yamlNode, Node node)
        {
            if (node.IsContainer)
            {
                if (node.Attributes.Count == 1)
                    yamlNode.Add(node.Attributes[0].Name, node.Attributes[0].Value);

                var sequence = new YamlSequenceNode();

                yamlNode.Add(PolymorphicContainerKeyword, sequence);
                WriteNode(sequence, node);
            }
            else
            {
                node.Attributes.ForEach(a => yamlNode.Add(a.Name,a.Value));

                foreach (var childNode in node.Nodes)
                {
                    if (!childNode.IsContainer || (childNode.Attributes.Count == 1 && childNode.Attributes[0].Name == ClassKwd))
                    {
                        var n = new YamlMappingNode();
                        WriteNode(n, childNode);
                        yamlNode.Add(childNode.Name,n);
                    }
                    else
                    {
                        var n = new YamlSequenceNode();
                        WriteNode(n, childNode);
                        yamlNode.Add(childNode.Name,n);
                    }
                }
            }
        }
        private static void WriteNode(YamlSequenceNode yamlNode, Node node)
        {
            Assert((node.Attributes.Count == 1 && node.Attributes[0].Name == ClassKwd) || node.Attributes.Count == 0, ErrorAnonymous);

            string name = null;
            foreach (var childNode in node.Nodes)
            {
                Assert((name = name ?? childNode.Name) == childNode.Name, ErrorAnonymous);

                if (!childNode.IsContainer || (childNode.Attributes.Count == 1 && childNode.Attributes[0].Name == ClassKwd))
                {
                    var n = new YamlMappingNode();
                    WriteNode(n, childNode);
                    yamlNode.Add(n);
                }
                else
                {
                    var n = new YamlSequenceNode();
                    WriteNode(n,childNode);
                    yamlNode.Add(n);
                }
            }
        }
    }
}