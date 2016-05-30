using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace elios.Persist
{
    /// <summary>
    /// Json Serializer
    /// </summary>
    public sealed class JsonArchive : TreeArchive
    {
        /// <summary>
        /// Gets or sets a value indicating whether to pretty print the json
        /// </summary>
        /// <value>
        ///   <c>true</c> if [pretty print]; otherwise, false<c>false</c>.
        /// </value>
        public bool PrettyPrint { get; set; } = true;

        /// <summary>
        /// Initializes <see cref="JsonArchive"/> that reads archives of the specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="polymorphicTypes"></param>
        public JsonArchive(Type type, Type[] polymorphicTypes = null) : base(type, polymorphicTypes)
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArchive"/> class class using the metadata from other serializer.
        /// </summary>
        /// <param name="archive">The archive.</param>
        public JsonArchive(Archive archive) : base(archive)
        {        
        }

        /// <summary>
        /// Writes a node to a json <see cref="Stream"/>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="root"></param>
        protected override void WriteNode(Stream target, Node root)
        {
            SaveNode(target, root,PrettyPrint);
        }
        /// <summary>
        /// Parses a node form the <see cref="Stream"/>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        protected override Node ParseNode(Stream source)
        {
            return LoadNode(source);
        }

        /// <summary>
        /// Parses a node from a json <see cref="Stream"/>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Node LoadNode(Stream source)
        {
            YamlDocument doc;
            using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
            {
                var yamlReader = new YamlStream();
                yamlReader.Load(reader);
                doc = yamlReader.Documents.Single();
            }

            var mainNode = new Node(string.Empty);
            YamlArchive.ParseNode(doc.RootNode, mainNode);

            return mainNode;
        }

        /// <summary>
        /// writes a <see cref="Node"/> to a json stream
        /// </summary>
        /// <param name="target"></param>
        /// <param name="node"></param>
        /// <param name="prettyPrint">pretty json?</param>
        public static void SaveNode(Stream target, Node node, bool prettyPrint = true)
        {
            YamlDocument doc = new YamlDocument(new YamlMappingNode());
            YamlArchive.WriteNode((YamlMappingNode)doc.RootNode, node);

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    new YamlStream(doc).Save(writer);

                stream.Seek(0, SeekOrigin.Begin);

                object yamlDynamicObj;
                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                    yamlDynamicObj = new Deserializer().Deserialize(reader);

                using (var jsonWriter = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                    new JsonSerializer { Formatting = prettyPrint ? Formatting.Indented : Formatting.None }.Serialize(jsonWriter, yamlDynamicObj);
            }
        }
    }
}