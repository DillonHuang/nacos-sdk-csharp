﻿[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Nacos.Microsoft.Extensions.Configuration.Tests")]

namespace Nacos.Microsoft.Extensions.Configuration
{
    using global::Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Globalization;
    using global::System.IO;
    using global::System.Linq;
    using Nacos.V2;

    internal class DefaultJsonConfigurationStringParser : INacosConfigurationParser
    {
        private DefaultJsonConfigurationStringParser()
        {
        }

        internal static DefaultJsonConfigurationStringParser Instance = new DefaultJsonConfigurationStringParser();

        private readonly IDictionary<string, string> _data = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _context = new Stack<string>();
        private string _currentPath;

        private JsonTextReader _reader;

        public IDictionary<string, string> Parse(string input)
            => new DefaultJsonConfigurationStringParser().ParseString(input);

        private IDictionary<string, string> ParseString(string input)
        {
            _data.Clear();
            _reader = new JsonTextReader(new StringReader(input))
            {
                DateParseHandling = DateParseHandling.None
            };

            var jsonConfig = JObject.Load(_reader);

            VisitJObject(jsonConfig);

            return _data;
        }

        private void VisitJObject(JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                EnterContext(property.Name);
                VisitProperty(property);
                ExitContext();
            }
        }

        private void VisitProperty(JProperty property)
        {
            VisitToken(property.Value);
        }

        private void VisitToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    VisitJObject(token.Value<JObject>());
                    break;

                case JTokenType.Array:
                    VisitArray(token.Value<JArray>());
                    break;

                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Bytes:
                case JTokenType.Raw:
                case JTokenType.Null:
                    VisitPrimitive(token.Value<JValue>());
                    break;

                default:
                    throw new FormatException(
                        $"Unsupported JSON token '{_reader.TokenType}' was found. Path '{_reader.Path}', line {_reader.LineNumber} position {_reader.LinePosition}.");
            }
        }

        private void VisitArray(JArray array)
        {
            for (int index = 0; index < array.Count; index++)
            {
                EnterContext(index.ToString());
                VisitToken(array[index]);
                ExitContext();
            }
        }

        private void VisitPrimitive(JValue data)
        {
            var key = _currentPath;

            if (_data.ContainsKey(key))
            {
                throw new FormatException($"A duplicate key '{key}' was found.");
            }

            _data[key] = data.ToString(CultureInfo.InvariantCulture);
        }

        private void EnterContext(string context)
        {
            _context.Push(context);
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }

        private void ExitContext()
        {
            _context.Pop();
            _currentPath = ConfigurationPath.Combine(_context.Reverse());
        }
    }
}
