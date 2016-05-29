using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace elios.Persist
{
    public enum ArchiveFormat
    {
        Guess,
        Xml,
        Json,
        Yaml
    }

    public abstract class ArchiveUtils
    {
        public static void Write(Stream target, object data, ArchiveFormat format = ArchiveFormat.Xml, string rootName = null, Type[] polymorphicTypes = null)
        {
            Archive serializer;

            switch (format)
            {
            case ArchiveFormat.Xml: 
                serializer = new XmlArchive(data.GetType(),polymorphicTypes);
                break;
            case ArchiveFormat.Json:
                serializer = new JsonArchive(data.GetType(), polymorphicTypes);
                break;
            case ArchiveFormat.Yaml:
                serializer = new YamlArchive(data.GetType(), polymorphicTypes);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            serializer.Write(target, data, rootName);
        }
        public static void Write(string filePath, object data, ArchiveFormat format = ArchiveFormat.Guess, string rootName = null, Type[] polymorphicTypes = null)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var writeStream = new FileStream(filePath, FileMode.Create))
                Write(writeStream, data, format, rootName, polymorphicTypes);
        }

        public static object Read(Stream source, Type type, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            Archive serializer;

            if (format == ArchiveFormat.Guess)
                format = GuessFormat(source);

            switch (format)
            {
                case ArchiveFormat.Xml:
                    serializer = new XmlArchive(type, polymorphicTypes);
                    break;
                case ArchiveFormat.Json:
                    serializer = new JsonArchive(type, polymorphicTypes);
                    break;
                case ArchiveFormat.Yaml:
                    serializer = new YamlArchive(type, polymorphicTypes);
                    break;
                case ArchiveFormat.Guess:
                    throw new FormatException(nameof(source));
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            return serializer.Read(source);
        }
        public static object Read(string filePath, Type type, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return Read(readStream, type, format, polymorphicTypes);
        }

        public static T Read<T>(Stream source, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            return (T)Read(source, typeof(T), format, polymorphicTypes);
        }
        public static T Read<T>(string filePath, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            return (T)Read(filePath, typeof(T), format, polymorphicTypes);
        }

        public static T ReadAnonymous<T>(Stream source, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(source);

            switch (format)
            {
            case ArchiveFormat.Xml:
                var doc = new XmlDocument();
                doc.Load(source);
                doc.ChildNodes.OfType<XmlNode>().Where(x => x.NodeType == XmlNodeType.XmlDeclaration).ToList().ForEach(node => doc.RemoveChild(node));
                doc.ElementifyAllAttributes();
                doc.RemoveAllAttributes();
                return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeXmlNode(doc));
            case ArchiveFormat.Json:
                using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                    return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            case ArchiveFormat.Yaml:
                using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                    return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(new Deserializer().Deserialize(reader)));
            case ArchiveFormat.Guess:
                throw new FormatException(nameof(source));
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        public static T ReadAnonymous<T>(string filePath, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return ReadAnonymous<T>(readStream, format);
        }

        public static object ReadAnonymous(Stream source, ArchiveFormat format = ArchiveFormat.Guess)
        {
            return ReadAnonymous<object>(source, format);
        }
        public static object ReadAnonymous(string filePath, ArchiveFormat format = ArchiveFormat.Guess)
        {
            return ReadAnonymous<object>(filePath, format);
        }

        public static void WriteAnonymous(Stream target, object data, ArchiveFormat format = ArchiveFormat.Xml, string rootName = "Root")
        {
            switch (format)
            {
            case ArchiveFormat.Xml:
                JsonConvert.DeserializeXNode(JsonConvert.SerializeObject(data), rootName).Save(target);
                break;
            case ArchiveFormat.Json:
                using (var writer = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                    JsonSerializer.Create().Serialize(writer, data);
                break;
            case ArchiveFormat.Yaml:
                using (var writer = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                    new Serializer().Serialize(writer,data);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        public static void WriteAnonymous(string filePath, object data, ArchiveFormat format = ArchiveFormat.Guess, string rootName = "Root")
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var writeStream = new FileStream(filePath, FileMode.Create))
                WriteAnonymous(writeStream, data,format,rootName);
        }

        public static string Convert(Stream source, ArchiveFormat newFormat = ArchiveFormat.Xml, ArchiveFormat sourceFormat = ArchiveFormat.Guess, string rootName = "Root")
        {
            var obj = ReadAnonymous(source,sourceFormat);
            using (var targetStream = new MemoryStream())
            {
                WriteAnonymous(targetStream, obj, newFormat, rootName);
                return Encoding.UTF8.GetString(targetStream.ToArray());
            }
        }
        public static string Convert(string filePath, ArchiveFormat newFormat = ArchiveFormat.Xml, ArchiveFormat sourceFormat = ArchiveFormat.Guess, string rootName = "Root")
        {
            using (var readStream = new FileStream(filePath, FileMode.Open))
                return Convert(readStream, newFormat, sourceFormat, rootName);
        }

        public static void SaveNode(Stream target, Node node, ArchiveFormat format = ArchiveFormat.Xml)
        {
            switch (format)
            {
            case ArchiveFormat.Xml:
                XmlArchive.SaveNode(target,node);
                break;
            case ArchiveFormat.Json:
                JsonArchive.SaveNode(target, node);
                break;
            case ArchiveFormat.Yaml:
                YamlArchive.SaveNode(target, node);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        public static void SaveNode(string filePath, Node node, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var writeStream = new FileStream(filePath, FileMode.Create))
                SaveNode(writeStream,node,format);
        }

        public static Node LoadNode(Stream source, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(source);

            switch (format)
            {
                case ArchiveFormat.Xml: return XmlArchive.LoadNode(source);
                case ArchiveFormat.Json: return JsonArchive.LoadNode(source);
                case ArchiveFormat.Yaml: return YamlArchive.LoadNode(source);
                default: throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        public static Node LoadNode(string filePath, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return LoadNode(readStream, format);
        }

        public static ArchiveFormat GuessFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath);

            switch (extension.ToLower())
            {
            case ".xml":
                return ArchiveFormat.Xml;
            case ".yaml":
                return ArchiveFormat.Yaml;
            case ".json":
                return ArchiveFormat.Json;
            default:
                return ArchiveFormat.Guess;
            }
        }
        public static ArchiveFormat GuessFormat(Stream source)
        {
            using (new Utils.OnDispose(() => source.Seek(0, SeekOrigin.Begin)))
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(source);
                    return ArchiveFormat.Xml;
                }
                catch (Exception)
                {
                }

                source.Seek(0, SeekOrigin.Begin);
                try
                {
                    using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                        JObject.Parse(reader.ReadToEnd());

                    return ArchiveFormat.Json;
                }
                catch (Exception)
                {
                }


                source.Seek(0, SeekOrigin.Begin);
                try
                {
                    using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                        new YamlStream().Load(reader);

                    return ArchiveFormat.Yaml;
                }
                catch (Exception)
                {
                }


                return ArchiveFormat.Guess;
            }
        }
    }
}