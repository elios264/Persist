using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        /// Gets or sets a value indicating whether to stream a pretty json
        /// </summary>
        public bool PrettyJson { get; set; } = true;

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
            SaveNode(target, root, PrettyJson);
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
        /// <param name="target">target <see cref="Stream"/></param>
        /// <param name="node"></param>
        /// <param name="prettyPrint">breaklines and everything?</param>
        public static void SaveNode(Stream target, Node node, bool prettyPrint)
        {
            YamlDocument doc = new YamlDocument(new YamlMappingNode());
            YamlArchive.WriteNode((YamlMappingNode)doc.RootNode, node);

            object yamlDynamicObj;
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    new YamlStream(doc).Save(writer);

                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                    yamlDynamicObj = new Deserializer().Deserialize(reader);
            }

            if (prettyPrint)
            {
                string prettyJson;
                using (var stringWriter = new StringWriter())
                {
                    new Serializer(SerializationOptions.JsonCompatible).Serialize(stringWriter, yamlDynamicObj);
                    prettyJson = JsonFormatterPlus.JsonFormatter.Format(stringWriter.ToString());
                }

                using (var jsonWriter = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                    jsonWriter.Write(prettyJson);
            }
            else
            {
                using (var jsonWriter = new StreamWriter(target, new UTF8Encoding(false), 1024, true))
                    new Serializer(SerializationOptions.JsonCompatible).Serialize(jsonWriter, yamlDynamicObj);
            }
        }
    }
}

//Credis to https://github.com/MatthewKing/JsonFormatterPlus
namespace JsonFormatterPlus
{
    internal static class JsonFormatter
    {
        public static string Format(string json)
        {
            var context = new JsonFormatterStrategyContext();
            var formatter = new JsonFormatterInternal(context);

            return formatter.Format(json);
        }

        public static string Minify(string json)
        {
            return Regex.Replace(json, @"(""(?:[^""\\]|\\.)*"")|\s+", "$1");
        }
    }

    internal sealed class FormatterScopeState
    {
        public enum JsonScope
        {
            Object,
            Array
        }

        private readonly Stack<JsonScope> scopeStack = new Stack<JsonScope>();

        public bool IsTopTypeArray
        {
            get
            {
                return scopeStack.Count > 0
                    && scopeStack.Peek() == JsonScope.Array;
            }
        }

        public int ScopeDepth
        {
            get
            {
                return scopeStack.Count;
            }
        }

        public void PushObjectContextOntoStack()
        {
            scopeStack.Push(JsonScope.Object);
        }

        public JsonScope PopJsonType()
        {
            return scopeStack.Pop();
        }

        public void PushJsonArrayType()
        {
            scopeStack.Push(JsonScope.Array);
        }
    }
    internal sealed class JsonFormatterInternal
    {
        private readonly JsonFormatterStrategyContext context;

        public JsonFormatterInternal(JsonFormatterStrategyContext context)
        {
            this.context = context;

            this.context.ClearStrategies();
            this.context.AddCharacterStrategy(new OpenBracketStrategy());
            this.context.AddCharacterStrategy(new CloseBracketStrategy());
            this.context.AddCharacterStrategy(new OpenSquareBracketStrategy());
            this.context.AddCharacterStrategy(new CloseSquareBracketStrategy());
            this.context.AddCharacterStrategy(new SingleQuoteStrategy());
            this.context.AddCharacterStrategy(new DoubleQuoteStrategy());
            this.context.AddCharacterStrategy(new CommaStrategy());
            this.context.AddCharacterStrategy(new ColonCharacterStrategy());
            this.context.AddCharacterStrategy(new SkipWhileNotInStringStrategy('\n'));
            this.context.AddCharacterStrategy(new SkipWhileNotInStringStrategy('\r'));
            this.context.AddCharacterStrategy(new SkipWhileNotInStringStrategy('\t'));
            this.context.AddCharacterStrategy(new SkipWhileNotInStringStrategy(' '));
        }

        public string Format(string json)
        {
            if (json == null)
                return string.Empty;

            if (json.Trim() == string.Empty)
                return string.Empty;

            StringBuilder input = new StringBuilder(json);
            StringBuilder output = new StringBuilder();

            PrettyPrintCharacter(input, output);

            return output.ToString();
        }

        private void PrettyPrintCharacter(StringBuilder input, StringBuilder output)
        {
            for (int i = 0; i < input.Length; i++)
            {
                context.PrettyPrintCharacter(input[i], output);
            }
        }
    }
    internal sealed class JsonFormatterStrategyContext
    {
        private const string Space = " ";
        private const int SpacesPerIndent = 4;

        private string indent = string.Empty;

        private char currentCharacter;
        private char previousChar;

        private StringBuilder outputBuilder;

        private readonly FormatterScopeState scopeState = new FormatterScopeState();
        private readonly IDictionary<char, ICharacterStrategy> strategies = new Dictionary<char, ICharacterStrategy>();
        
        public string Indent
        {
            get
            {
                if (indent == string.Empty)
                {
                    InitializeIndent();
                }

                return indent;
            }
        }
        private void InitializeIndent()
        {
            for (int i = 0; i < SpacesPerIndent; i++)
            {
                indent += Space;
            }
        }
        public bool IsInArrayScope
        {
            get
            {
                return scopeState.IsTopTypeArray;
            }
        }
        private void AppendIndents(int indents)
        {
            for (int i = 0; i < indents; i++)
            {
                outputBuilder.Append(Indent);
            }
        }

        public bool IsProcessingVariableAssignment;
        public bool IsProcessingDoubleQuoteInitiatedString { get; set; }
        public bool IsProcessingSingleQuoteInitiatedString { get; set; }

        public bool IsProcessingString
        {
            get
            {
                return IsProcessingDoubleQuoteInitiatedString
                       || IsProcessingSingleQuoteInitiatedString;
            }
        }
        public bool IsStart
        {
            get { return outputBuilder.Length == 0; }
        }
        public bool WasLastCharacterABackSlash
        {
            get { return previousChar == '\\'; }
        }
        public void PrettyPrintCharacter(char curChar, StringBuilder output)
        {
            currentCharacter = curChar;

            var strategy = strategies.ContainsKey(curChar)
                ? strategies[curChar]
                : new DefaultCharacterStrategy();

            outputBuilder = output;

            strategy.Execute(this);

            previousChar = curChar;
        }
        public void AppendCurrentChar()
        {
            outputBuilder.Append(currentCharacter);
        }
        public void AppendNewLine()
        {
            outputBuilder.Append(Environment.NewLine);
        }
        public void BuildContextIndents()
        {
            AppendNewLine();
            AppendIndents(scopeState.ScopeDepth);
        }
        public void EnterObjectScope()
        {
            scopeState.PushObjectContextOntoStack();
        }
        public void CloseCurrentScope()
        {
            scopeState.PopJsonType();
        }
        public void EnterArrayScope()
        {
            scopeState.PushJsonArrayType();
        }
        public void AppendSpace()
        {
            outputBuilder.Append(Space);
        }
        public void ClearStrategies()
        {
            strategies.Clear();
        }
        public void AddCharacterStrategy(ICharacterStrategy strategy)
        {
            strategies[strategy.ForWhichCharacter] = strategy;
        }
    }
    internal interface ICharacterStrategy
    {
        void Execute(JsonFormatterStrategyContext context);
        char ForWhichCharacter { get; }
    }
    internal sealed class CloseBracketStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (context.IsProcessingString)
            {
                context.AppendCurrentChar();
                return;
            }

            PeformNonStringPrint(context);
        }
        private static void PeformNonStringPrint(JsonFormatterStrategyContext context)
        {
            context.CloseCurrentScope();
            context.BuildContextIndents();
            context.AppendCurrentChar();
        }
        public char ForWhichCharacter
        {
            get { return '}'; }
        }
    }
    internal sealed class CloseSquareBracketStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (context.IsProcessingString)
            {
                context.AppendCurrentChar();
                return;
            }

            context.CloseCurrentScope();
            context.BuildContextIndents();
            context.AppendCurrentChar();
        }
        public char ForWhichCharacter
        {
            get { return ']'; }
        }
    }
    internal sealed class ColonCharacterStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (context.IsProcessingString)
            {
                context.AppendCurrentChar();
                return;
            }

            context.IsProcessingVariableAssignment = true;
            context.AppendCurrentChar();
            context.AppendSpace();
        }
        public char ForWhichCharacter
        {
            get { return ':'; }
        }
    }
    internal sealed class CommaStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            context.AppendCurrentChar();

            if (context.IsProcessingString)
            {
                return;
            }

            context.BuildContextIndents();
            context.IsProcessingVariableAssignment = false;
        }

        public char ForWhichCharacter
        {
            get { return ','; }
        }
    }
    internal sealed class DefaultCharacterStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            context.AppendCurrentChar();
        }
        public char ForWhichCharacter
        {
            get
            {
                const string msg = "This strategy was not intended for any particular character.";
                throw new InvalidOperationException(msg);
            }
        }
    }
    internal sealed class DoubleQuoteStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (!context.IsProcessingSingleQuoteInitiatedString && !context.WasLastCharacterABackSlash)
                context.IsProcessingDoubleQuoteInitiatedString = !context.IsProcessingDoubleQuoteInitiatedString;

            context.AppendCurrentChar();
        }
        public char ForWhichCharacter
        {
            get { return '"'; }
        }
    }
    internal sealed class OpenBracketStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (context.IsProcessingString)
            {
                context.AppendCurrentChar();
                return;
            }

            context.AppendCurrentChar();
            context.EnterObjectScope();

            if (!IsBeginningOfNewLineAndIndentionLevel(context)) return;

            context.BuildContextIndents();
        }
        private static bool IsBeginningOfNewLineAndIndentionLevel(JsonFormatterStrategyContext context)
        {
            return context.IsProcessingVariableAssignment || (!context.IsStart && !context.IsInArrayScope);
        }
        public char ForWhichCharacter
        {
            get { return '{'; }
        }
    }
    internal sealed class OpenSquareBracketStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            context.AppendCurrentChar();

            if (context.IsProcessingString)
            {
                return;
            }

            context.EnterArrayScope();
            context.BuildContextIndents();
        }
        public char ForWhichCharacter
        {
            get { return '['; }
        }
    }
    internal sealed class SingleQuoteStrategy : ICharacterStrategy
    {
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (!context.IsProcessingDoubleQuoteInitiatedString && !context.WasLastCharacterABackSlash)
                context.IsProcessingSingleQuoteInitiatedString = !context.IsProcessingSingleQuoteInitiatedString;

            context.AppendCurrentChar();
        }
        public char ForWhichCharacter
        {
            get { return '\''; }
        }
    }
    internal sealed class SkipWhileNotInStringStrategy : ICharacterStrategy
    {
        private readonly char selectionCharacter;

        public SkipWhileNotInStringStrategy(char selectionCharacter)
        {
            this.selectionCharacter = selectionCharacter;
        }
        public void Execute(JsonFormatterStrategyContext context)
        {
            if (context.IsProcessingString)
            {
                context.AppendCurrentChar();
            }
        }
        public char ForWhichCharacter
        {
            get
            {
                return selectionCharacter;
            }
        }
    }
}