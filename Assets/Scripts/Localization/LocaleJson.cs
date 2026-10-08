using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Reads a locale file into a flat key -> text table. Nested objects become
// dotted keys, so translators can group strings:
//
//   { "menu": { "play": "Jogar" } }      ->   "menu.play" = "Jogar"
//
// Arrays are joined with spaces, numbers and booleans are kept as written,
// and null is skipped. JsonUtility can't read a dictionary, hence this.
public static class LocaleJson
{
    public static Dictionary<string, string> Flatten(string json)
    {
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(json)) return table;

        int i = 0;
        SkipSpace(json, ref i);
        if (i < json.Length && json[i] == '{') ReadObject(json, ref i, "", table);
        return table;
    }

    private static void ReadObject(string s, ref int i, string prefix, Dictionary<string, string> table)
    {
        i++;   // {
        while (true)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unterminated object");
            if (s[i] == '}') { i++; return; }
            if (s[i] == ',') { i++; continue; }

            string key = ReadString(s, ref i);
            SkipSpace(s, ref i);
            Expect(s, ref i, ':');
            SkipSpace(s, ref i);
            ReadValue(s, ref i, prefix.Length > 0 ? prefix + "." + key : key, table);
        }
    }

    private static void ReadValue(string s, ref int i, string key, Dictionary<string, string> table)
    {
        char c = s[i];
        if (c == '{') { ReadObject(s, ref i, key, table); return; }
        if (c == '"') { table[key] = ReadString(s, ref i); return; }
        if (c == '[')
        {
            var parts = new List<string>();
            i++;
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length) throw Error(s, i, "unterminated array");
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                var item = new Dictionary<string, string>();
                ReadValue(s, ref i, "x", item);
                string v;
                if (item.TryGetValue("x", out v)) parts.Add(v);
            }
            table[key] = string.Join(" ", parts.ToArray());
            return;
        }

        int start = i;
        while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
        string raw = s.Substring(start, i - start);
        if (raw != "null") table[key] = raw;
    }

    private static string ReadString(string s, ref int i)
    {
        Expect(s, ref i, '"');
        var sb = new StringBuilder();
        while (true)
        {
            if (i >= s.Length) throw Error(s, i, "unterminated string");
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }

            char e = s[i++];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                    i += 4;
                    break;
                default: sb.Append(e); break;   // \" \\ \/
            }
        }
    }

    private static void SkipSpace(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    private static void Expect(string s, ref int i, char c)
    {
        if (i >= s.Length || s[i] != c) throw Error(s, i, "expected '" + c + "'");
        i++;
    }

    private static FormatException Error(string s, int i, string what)
    {
        int line = 1;
        for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
        return new FormatException("Locale JSON, line " + line + ": " + what);
    }
}
