using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace elios.Persist
{
    public sealed class JsonArchive : TreeArchive
    {
        public JsonArchive(Type type, Type[] polymorphicTypes = null) : base(type, polymorphicTypes)
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
                var yamlReader = new YamlStream();
                yamlReader.Load(reader);
                doc = yamlReader.Documents.Single();
            }

            var mainNode = new Node(string.Empty);
            YamlArchive.ParseNode(doc.RootNode, mainNode);

            return mainNode;
        }
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
                    new JsonSerializer { Formatting = Formatting.Indented }.Serialize(jsonWriter, yamlDynamicObj);
            }
        }
    }
}