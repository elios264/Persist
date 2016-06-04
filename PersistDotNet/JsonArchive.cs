using System;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace elios.Persist
{
    /// <summary>
    /// Json Serializer
    /// <remarks>this class is thread safe</remarks>
    /// </summary>
    public sealed class JsonArchive : TreeArchive
    {
        /// <summary>
        /// Initializes <see cref="JsonArchive"/> that reads archives of the specified type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="additionalTypes"></param>
        public JsonArchive(Type type, params Type[] additionalTypes) : base(type, additionalTypes)
        {
        }

        /// <summary>
        /// Writes a node to a json <see cref="Stream"/>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="root"></param>
        protected override void WriteNode(Stream target, Node root)
        {
            SaveNode(target, root);
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
            return YamlArchive.LoadNode(source);
        }

        /// <summary>
        /// writes a <see cref="Node"/> to a json stream
        /// </summary>
        /// <param name="target"></param>
        /// <param name="node"></param>
        public static void SaveNode(Stream target, Node node)
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
                    new Serializer(SerializationOptions.JsonCompatible).Serialize(jsonWriter,yamlDynamicObj);
            }
        }
    }
}