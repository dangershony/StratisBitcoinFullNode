using System;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Obsidian.Features.X1Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.Features.X1Wallet.Tools
{
    public static class Serializer
    {
        static readonly Lazy<JsonSerializerSettings> Settings = new Lazy<JsonSerializerSettings>(CreateSettings);

        static readonly Lazy<JsonSerializerSettings> PrintSettings = new Lazy<JsonSerializerSettings>(CreatePrintSettings);

        static string _cache;

        static JsonSerializerSettings CreateSettings()
        {
            var jsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            jsonSettings.Converters.Add(new UInt256JsonConverter());
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
            return jsonSettings;
        }

        static JsonSerializerSettings CreatePrintSettings()
        {
            var jsonSettings = new JsonSerializerSettings();
            jsonSettings.Converters.Add(new UInt256JsonConverter());
            jsonSettings.NullValueHandling = NullValueHandling.Ignore;
            return jsonSettings;
        }


        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, Settings.Value);
        }

        public static T Deserialize<T>(string serialized)
        {
            return JsonConvert.DeserializeObject<T>(serialized, Settings.Value);
        }

        public static string Print<T>(T obj, string customHeader = null)
        {
            if (obj == null)
                return string.Empty;

            var serialized = JsonConvert.SerializeObject(obj, Formatting.Indented, PrintSettings.Value);
            if (serialized == _cache)
                return _cache;

            var body = Strip(serialized);
            var padded = Pad(body);
            var namedItem = $"{nameof(StringItem.NamedItem)}:";
            padded = padded.Replace(namedItem, new string(Spaces, 0, namedItem.Length));
            var header = customHeader ?? obj.GetType().Name;
            var headerLine = $"======= {header} ======={Environment.NewLine}";
            _cache = $"{headerLine}{padded}";
            return _cache;
        }

        static readonly char[] Spaces = new[] { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', };

        static string Pad(string body)
        {
            int tabAt = Spaces.Length;
            var sb = new StringBuilder();
            var lines = body.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                var edit = line;
                if (edit.StartsWith("  "))
                    edit = line.Substring(2);

                var cindex = edit.IndexOf(':');
                if (cindex > 0)
                {
                    var diff = tabAt - cindex;
                    if (diff > 0)
                    {
                        var padded = edit.Insert(cindex + 1, new string(Spaces, 0, diff));
                        sb.AppendLine(padded);
                    }
                }

            }

            return sb.ToString();
        }

        static string Strip(string s)
        {
            var chars = s.ToCharArray();
            var result = new char[chars.Length];
            var cursor = 0;
            for (var i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                switch (c)
                {
                    case var _ when char.IsWhiteSpace(c) || c == '\\' || c == '!':
                        break;
                    case var isLowerCase when (isLowerCase >= 97 && isLowerCase <= 122): // a - z
                        break;
                    case var isUpperCase when (isUpperCase >= 65 && isUpperCase <= 90): // A - Z
                        break;
                    case var isNum when (isNum >= 45 && isNum <= 58): // minus, dot, slash, 0..9, :
                        break;
                    default:
                        continue;

                }
                result[cursor++] = c;
            }

            return new string(result, 0, cursor);
        }


    }
}
