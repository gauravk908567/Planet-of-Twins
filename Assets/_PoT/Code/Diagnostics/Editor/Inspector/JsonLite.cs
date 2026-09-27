using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// A small JSON reader for <c>report.json</c>. The runtime writes it by hand, and JsonUtility can't read its
    /// maps (<c>sections{name:{key:value}}</c>). Objects come back as <see cref="JsonObject"/> (keys in file order),
    /// arrays as <c>List&lt;object&gt;</c>, numbers as double, plus string, bool and null.
    /// Bad input throws <see cref="FormatException"/>.
    /// </summary>
    public static class JsonLite
    {
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("JSON: no text");
            int i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw Error(i, "unexpected text after the end");
            return value;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(i, "unexpected end");
            switch (s[i])
            {
                case '{': return ReadObject(s, ref i);
                case '[': return ReadArray(s, ref i);
                case '"': return ReadString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ReadNumber(s, ref i);
            }
        }

        private static JsonObject ReadObject(string s, ref int i)
        {
            var result = new JsonObject();
            i++;   // {
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(i, "expected a key");
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(i, "expected ':'");
                i++;
                result.Add(new KeyValuePair<string, object>(key, ReadValue(s, ref i)));
                SkipSpace(s, ref i);
                if (i >= s.Length) throw Error(i, "unexpected end in an object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw Error(i, "expected ',' or '}'");
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var result = new List<object>();
            i++;   // [
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i >= s.Length) throw Error(i, "unexpected end in an array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw Error(i, "expected ',' or ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var result = new StringBuilder();
            i++;   // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return result.ToString();
                if (c != '\\') { result.Append(c); continue; }
                if (i >= s.Length) break;
                char escape = s[i++];
                switch (escape)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(i, "short \\u escape");
                        result.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(i - 1, "unknown escape");
                }
            }
            throw Error(i, "unterminated string");
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw Error(i, $"unexpected '{s[i]}'");
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw Error(start, "bad number");
            return value;
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error(i, $"expected '{word}'");
            i += word.Length;
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static FormatException Error(int at, string what) =>
            new FormatException($"JSON: {what} at character {at}");
    }

    /// <summary>A JSON object with its keys in file order.</summary>
    public sealed class JsonObject : List<KeyValuePair<string, object>>
    {
        public object Get(string key)
        {
            foreach (var pair in this)
                if (pair.Key == key) return pair.Value;
            return null;
        }

        /// <summary>The value as text ("" when missing; numbers and bools are written out).</summary>
        public string GetString(string key)
        {
            switch (Get(key))
            {
                case null: return string.Empty;
                case string text: return text;
                case double number: return number.ToString(CultureInfo.InvariantCulture);
                case bool flag: return flag ? "true" : "false";
                default: return string.Empty;
            }
        }

        public double GetNumber(string key) => Get(key) is double number ? number : 0;
        public JsonObject GetObject(string key) => Get(key) as JsonObject;
        public List<object> GetArray(string key) => Get(key) as List<object>;
    }
}
