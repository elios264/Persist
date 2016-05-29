using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;

namespace elios.Persist
{
    internal static class Utils
    {
        public static Type GetEnumeratedType(this Type type)
        {
            // provided by Array
            var theType = type.GetElementType();
            if (null != theType) return theType;

            // otherwise provided by collection
            var theTypes = type.GetGenericArguments();
            if (theTypes.Length > 0) return theTypes[0];

            // otherwise is not an 'enumerated' type
            return null;
        }

        public static bool HasCircularDependency<T>(IEnumerable<T> source, Func<T, IEnumerable<T>> getDependencies)
        {
            return source.Any(item => Visit(item, getDependencies, new Dictionary<T, bool>()));
        }
        private static bool Visit<T>(T item, Func<T, IEnumerable<T>> getDependencies, IDictionary<T, bool> visited)
        {
            bool inProcess;
            var alreadyVisited = visited.TryGetValue(item, out inProcess);

            if (alreadyVisited)
            {
                if (inProcess) return true;
            }
            else
            {
                visited[item] = true;

                if (getDependencies(item).Any(dependency => Visit(dependency, getDependencies, visited)))
                    return true;

                visited[item] = false;
            }

            return false;
        }

        public static bool IsAnonymousType(this Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            // HACK: The only way to detect anonymous types right now.
            return type.GetTypeInfo().IsGenericType
                   && (type.GetTypeInfo().Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic
                   && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) || type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
                   && (type.Name.Contains("AnonymousType") || type.Name.Contains("AnonType"))
                   && type.GetTypeInfo().GetCustomAttributes(typeof(CompilerGeneratedAttribute)).Any();
        }

        public static void RemoveAllAttributes(this XmlDocument xmlDocument)
        {
            if (xmlDocument == null || !xmlDocument.HasChildNodes) return;

            foreach (var xmlElement in xmlDocument.SelectNodes(".//*").Cast<XmlElement>().Where(xmlElement => xmlElement.HasAttributes))
                xmlElement.Attributes.RemoveAll();
        }

        public static void ElementifyAllAttributes(this XmlDocument xmlDocument)
        {
            if (xmlDocument == null || !xmlDocument.HasChildNodes) return;

            foreach (var xmlElement in xmlDocument.SelectNodes(".//*").Cast<XmlElement>().Where(xmlElement => xmlElement.HasAttributes))
            {
                foreach (XmlAttribute xmlAttribute in xmlElement.Attributes)
                    xmlElement.AppendChild(xmlDocument.CreateElement(xmlAttribute.Name)).InnerText = xmlAttribute.Value;

                xmlElement.Attributes.RemoveAll();
            }
        }

        public class OnDispose : IDisposable
        {
            private readonly Action m_act;

            public OnDispose(Action act)
            {
                m_act = act;
            }

            public void Dispose()
            {
                m_act();
            }
        }

    }
}