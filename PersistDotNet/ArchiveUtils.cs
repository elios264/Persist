using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Xml;
using YamlDotNet.RepresentationModel;

namespace elios.Persist
{
    /// <summary>
    /// Supoorted archive formats
    /// </summary>
    public enum ArchiveFormat
    {
        /// <summary>
        /// Guesses the archive format
        /// </summary>
        Guess,
        /// <summary>
        /// Xml archive format
        /// </summary>
        Xml,
        /// <summary>
        /// Json archive format
        /// </summary>
        Json,
        /// <summary>
        /// Yaml archive format
        /// </summary>
        Yaml
    }

    /// <summary>
    /// Helper functions to read and write archives
    /// </summary>
    public static class ArchiveUtils
    {
        /// <summary>
        /// serializes an object to an archive
        /// </summary>
        /// <param name="target">target stream</param>
        /// <param name="data">object to serialize</param>
        /// <param name="format">format to use</param>
        /// <param name="rootName">xml rootname</param>
        /// <param name="polymorphicTypes">additional serialization types</param>
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
        /// <summary>
        /// serializes an object to an archive
        /// </summary>
        /// <param name="filePath">target file</param>
        /// <param name="data">object to serialize</param>
        /// <param name="format">format to use if guess guesses the format you want to use based on the file extension</param>
        /// <param name="rootName">xml rootname</param>
        /// <param name="polymorphicTypes">additional serialization types</param>
        public static void Write(string filePath, object data, ArchiveFormat format = ArchiveFormat.Guess, string rootName = null, Type[] polymorphicTypes = null)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var writeStream = new FileStream(filePath, FileMode.Create))
                Write(writeStream, data, format, rootName, polymorphicTypes);
        }
        /// <summary>
        /// serializes an object to a <see cref="Node"/>
        /// </summary>
        /// <param name="data">object to serialize</param>
        /// <param name="rootName">Name of the root (usually for xml archives)</param>
        /// <param name="polymorphicTypes">The polymorphic types.</param>
        /// <returns></returns>
        public static Node Write(object data, string rootName = null, Type[] polymorphicTypes = null)
        {
            return new JsonArchive(data.GetType(),polymorphicTypes).Write(data, rootName);
        }

        /// <summary>
        /// deserializes an archive
        /// </summary>
        /// <param name="source">source stream</param>
        /// <param name="type">object type</param>
        /// <param name="format">archive format</param>
        /// <param name="polymorphicTypes">additional deserialization types</param>
        /// <returns>the deserialized object</returns>
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
        /// <summary>
        /// deserializes an archive
        /// </summary>
        /// <param name="filePath">source file</param>
        /// <param name="type">object type</param>
        /// <param name="format">archive format</param>
        /// <param name="polymorphicTypes">additional deserialization types</param>
        /// <returns>the deserialized object</returns>
        public static object Read(string filePath, Type type, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return Read(readStream, type, format, polymorphicTypes);
        }
        /// <summary>
        /// Deserializes the arhive contained in the specified <see cref="Node"/>
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="type">The type this node represents</param>
        /// <param name="polymorphicTypes">The polymorphic types.</param>
        /// <returns></returns>
        public static object Read(Node node, Type type, Type[] polymorphicTypes = null)
        {
            return new JsonArchive(type, polymorphicTypes).Read(node);
        }

        /// <summary>
        /// deserializes an archive
        /// </summary>
        /// <typeparam name="T"> is the type of the deserialized object</typeparam>
        /// <param name="source">source stream</param>
        /// <param name="format">archive format</param>
        /// <param name="polymorphicTypes">additional deserialization types</param>
        /// <returns></returns>
        public static T Read<T>(Stream source, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            return (T)Read(source, typeof(T), format, polymorphicTypes);
        }
        /// <summary>
        /// deserializes an archive
        /// </summary>
        /// <typeparam name="T"> is the type of the deserialized object</typeparam>
        /// <param name="filePath">source file</param>
        /// <param name="format">archive format</param>
        /// <param name="polymorphicTypes">additional deserialization types</param>
        /// <returns></returns>
        public static T Read<T>(string filePath, ArchiveFormat format = ArchiveFormat.Guess, Type[] polymorphicTypes = null)
        {
            return (T)Read(filePath, typeof(T), format, polymorphicTypes);
        }
        /// <summary>
        /// Deserializes the arhive contained in the specified <see cref="Node"/>
        /// </summary>
        /// <typeparam name="T">the type this node represents</typeparam>
        /// <param name="node">The node.</param>
        /// <param name="polymorphicTypes">The polymorphic types.</param>
        /// <returns></returns>
        public static T Read<T>(Node node, Type[] polymorphicTypes = null)
        {
            return (T)Read(node, typeof(T), polymorphicTypes);
        }

        /// <summary>
        /// converts an archive to a new format and returns it as a string
        /// </summary>
        /// <param name="source">source <see cref="Stream"/></param>
        /// <param name="newFormat">new format</param>
        /// <param name="sourceFormat">source format</param>
        /// <param name="rootName">root name (for xml)</param>
        /// <remarks>if the newformat and the sourceformat are the same the conversion stil takes place</remarks>
        /// <returns></returns>
        public static string Convert(Stream source, ArchiveFormat newFormat = ArchiveFormat.Xml, ArchiveFormat sourceFormat = ArchiveFormat.Guess, string rootName = null)
        {
            var neutralformat = LoadNode(source, sourceFormat);
            neutralformat.Name = rootName ?? neutralformat.Name;

            using (var targetStream = new MemoryStream())
            {
                SaveNode(targetStream, neutralformat, newFormat);
                return Encoding.UTF8.GetString(targetStream.ToArray());
            }
        }
        /// <summary>
        /// converts an archive to a new format and returns it as a string
        /// </summary>
        /// <param name="filePath">source file</param>
        /// <param name="newFormat">new format</param>
        /// <param name="sourceFormat">source format</param>
        /// <param name="rootName">root name (for xml)</param>
        /// <remarks>if the newformat and the sourceformat are the same the conversion stil takes place</remarks>
        /// <returns></returns>
        public static string Convert(string filePath, ArchiveFormat newFormat = ArchiveFormat.Xml, ArchiveFormat sourceFormat = ArchiveFormat.Guess, string rootName = "Root")
        {
            if (sourceFormat == ArchiveFormat.Guess)
                sourceFormat = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return Convert(readStream, newFormat, sourceFormat, rootName);
        }

        /// <summary>
        /// writes the specified node to a <see cref="Stream"/>
        /// </summary>
        /// <param name="target">target stream</param>
        /// <param name="node">node to write</param>
        /// <param name="format">format</param>
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
        /// <summary>
        /// writes the specified node to a file
        /// </summary>
        /// <param name="filePath">target file</param>
        /// <param name="node">node to write</param>
        /// <param name="format">format</param>
        public static void SaveNode(string filePath, Node node, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var writeStream = new FileStream(filePath, FileMode.Create))
                SaveNode(writeStream,node,format);
        }

        /// <summary>
        /// Parses the <see cref="Stream"/> to a Node
        /// </summary>
        /// <param name="source">source stream</param>
        /// <param name="format">format</param>
        /// <returns></returns>
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
        /// <summary>
        /// Parses the file to a node
        /// </summary>
        /// <param name="filePath">source file</param>
        /// <param name="format">file format</param>
        /// <returns></returns>
        public static Node LoadNode(string filePath, ArchiveFormat format = ArchiveFormat.Guess)
        {
            if (format == ArchiveFormat.Guess)
                format = GuessFormat(filePath);

            using (var readStream = new FileStream(filePath, FileMode.Open))
                return LoadNode(readStream, format);
        }


        /// <summary>
        /// guesses the archive format of a file by extension
        /// </summary>
        /// <param name="filePath"></param>
        /// <remarks>if the extension is unknow it returns <see cref="T:ArchiveFormat.Guess"/></remarks>
        /// <returns></returns>
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
        /// <summary>
        /// guesses the archive format of a <see cref="Stream"/> by parsing it.
        /// </summary>
        /// <param name="source">source <see cref="Stream"/></param>
        /// <remarks>if the extension is unknow it returns <see cref="T:ArchiveFormat.Guess"/></remarks>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
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
                catch (Exception) { }

                source.Seek(0, SeekOrigin.Begin);
                try
                {
                    using (var reader = new StreamReader(source, Encoding.UTF8, true, 1024, true))
                    {
                        new YamlStream().Load(reader);
                        source.Seek(0, SeekOrigin.Begin);

                        var archive = reader.ReadToEnd();
                        var open = 0;
                        var close = 0;
                        foreach (var c in archive)
                        {
                            open = c == '{' ? open+1 : open;
                            close = c == '}' ? close + 1 : close;
                        }

                        return open == close
                            ? ArchiveFormat.Json
                            : ArchiveFormat.Yaml;
                    }
                }
                catch (Exception) { }

                return ArchiveFormat.Guess;
            }
        }
    }
}