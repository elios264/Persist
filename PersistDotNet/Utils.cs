using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

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

            if (theTypes.Length > 0)
                return theTypes[0];

            foreach (var @interface in type.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var args = @interface.GetGenericArguments();
                    return args[0];
                }
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
                return typeof(object);


            throw new ArgumentException($"{type} is not a Enumerable");
        }
        public static Tuple<Type,Type> GetDictionaryTypes(this Type type)
        {
            var types = type.GetGenericArguments();

            if (types.Length == 2)
                return Tuple.Create(types[0], types[1]);

            foreach (var @interface in type.GetInterfaces())
            {
                if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var args = @interface.GetGenericArguments();
                    return Tuple.Create(args[0], args[1]);
                }
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
                return Tuple.Create(typeof(object), typeof(object));

            throw new ArgumentException($"{type} is not a dictionary");
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

                if (getDependencies(item).Any(dependency => Visit<T>(dependency, getDependencies, visited)))
                    return true;

                visited[item] = false;
            }

            return false;
        }

        public static bool IsAnonymousType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType
                   && (type.GetTypeInfo().Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic
                   && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) || type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
                   && (type.Name.Contains("AnonymousType") || type.Name.Contains("AnonType"))
                   && type.GetTypeInfo().GetCustomAttributes(typeof(CompilerGeneratedAttribute)).Any();
        }

        public static void Assert(bool condition = false, string ifFail = null, [CallerMemberName] string caller = null)
        {
            if (!condition)
                throw new InvalidOperationException(ifFail ?? $"Invalid operation in method {caller}");
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