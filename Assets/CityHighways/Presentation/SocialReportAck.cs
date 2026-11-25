using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Validates the existing social.report JSON contract before removing a
    /// durable outbox entry. JsonUtility alone silently supplies defaults for
    /// missing or wrongly typed primitive fields (e.g. {} -> improved=false).
    /// No server deployment or protocol version change is required.
    /// </summary>
    public static class SocialReportAck
    {
        public const int MaxCharacters = 16 * 1024;

        public static bool TryParse(string json, out SocialCloud.ReportResponse response)
        {
            response = null;
            if (string.IsNullOrEmpty(json) || json.Length > MaxCharacters) return false;

            var reader = new Reader(json);
            if (!reader.Read(out var value) || !(value is Dictionary<string, object> root))
                return false;

            if (!root.TryGetValue("improved", out var improved) || !(improved is bool) ||
                !root.TryGetValue("best", out var rawBest) || !ReadInt(rawBest, -100, 1000, out int best) ||
                !root.TryGetValue("club", out var rawClub))
                return false;

            SocialCloud.ReportClub clubAck = null;
            if (rawClub != null)
            {
                if (!(rawClub is Dictionary<string, object> club) ||
                    !club.TryGetValue("kind", out var kind) || !(kind is string kindName) ||
                    string.IsNullOrEmpty(kindName) || kindName.Length > 64 ||
                    !club.TryGetValue("amount", out var amount) || !ReadInt(amount, 0, 10000, out int credited) ||
                    !club.TryGetValue("closed", out var closed) || !(closed is bool) ||
                    !club.TryGetValue("logo", out var logo) ||
                    (logo != null && (!(logo is string logoText) || logoText.Length > 128)))
                    return false;

                clubAck = new SocialCloud.ReportClub
                {
                    kind = kindName,
                    amount = credited,
                    closed = (bool)closed,
                    logo = logo as string
                };
            }

            // Unity's JsonUtility materializes a ReportClub even for JSON
            // "club": null. Build the acknowledged response directly from
            // verified values to preserve null and avoid implicit defaults.
            response = new SocialCloud.ReportResponse
            {
                improved = (bool)improved,
                best = best,
                club = clubAck
            };
            return true;
        }

        static bool ReadInt(object value, int min, int max, out int number)
        {
            number = 0;
            return value is NumberToken raw &&
                int.TryParse(raw.Text, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out number) &&
                number >= min && number <= max;
        }

        sealed class NumberToken
        {
            public readonly string Text;
            public NumberToken(string text) { Text = text; }
        }

        /// <summary>
        /// Small bounded JSON reader for validating field presence and types.
        /// Unknown fields are parsed, not substring-searched: escaped quotes,
        /// nesting and duplicate keys cannot impersonate required fields.
        /// </summary>
        sealed class Reader
        {
            const int MaxDepth = 12;
            const int MaxMembers = 128;
            readonly string _json;
            int _pos;

            public Reader(string json) { _json = json; }

            public bool Read(out object value)
            {
                value = null;
                return ReadValue(0, out value) && WhiteSpace() && _pos == _json.Length;
            }

            bool WhiteSpace()
            {
                while (_pos < _json.Length)
                {
                    char c = _json[_pos];
                    if (c != ' ' && c != '\r' && c != '\n' && c != '\t') break;
                    _pos++;
                }
                return true;
            }

            bool Consume(char expected)
            {
                WhiteSpace();
                if (_pos >= _json.Length || _json[_pos] != expected) return false;
                _pos++;
                return true;
            }

            bool ReadValue(int depth, out object value)
            {
                value = null;
                if (depth > MaxDepth) return false;
                WhiteSpace();
                if (_pos >= _json.Length) return false;
                char c = _json[_pos];
                if (c == '{') return ReadObject(depth + 1, out value);
                if (c == '[') return ReadArray(depth + 1, out value);
                if (c == '"')
                {
                    if (!ReadString(out string text)) return false;
                    value = text;
                    return true;
                }
                if (c == 't' && Literal("true")) { value = true; return true; }
                if (c == 'f' && Literal("false")) { value = false; return true; }
                if (c == 'n' && Literal("null")) return true;
                return ReadNumber(out value);
            }

            bool Literal(string text)
            {
                if (_pos + text.Length > _json.Length ||
                    string.CompareOrdinal(_json, _pos, text, 0, text.Length) != 0) return false;
                _pos += text.Length;
                return true;
            }

            bool ReadObject(int depth, out object value)
            {
                value = null;
                if (!Consume('{')) return false;
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                if (Consume('}')) { value = fields; return true; }
                while (fields.Count < MaxMembers)
                {
                    WhiteSpace();
                    if (!ReadString(out string key) || fields.ContainsKey(key) ||
                        !Consume(':') || !ReadValue(depth, out var field)) return false;
                    fields.Add(key, field);
                    if (Consume('}')) { value = fields; return true; }
                    if (!Consume(',')) return false;
                }
                return false;
            }

            bool ReadArray(int depth, out object value)
            {
                value = null;
                if (!Consume('[')) return false;
                var items = new List<object>();
                if (Consume(']')) { value = items; return true; }
                while (items.Count < MaxMembers)
                {
                    if (!ReadValue(depth, out var item)) return false;
                    items.Add(item);
                    if (Consume(']')) { value = items; return true; }
                    if (!Consume(',')) return false;
                }
                return false;
            }

            bool ReadString(out string result)
            {
                result = null;
                if (_pos >= _json.Length || _json[_pos++] != '"') return false;
                var text = new StringBuilder();
                while (_pos < _json.Length)
                {
                    char c = _json[_pos++];
                    if (c == '"') { result = text.ToString(); return true; }
                    if (c < 0x20) return false;
                    if (c != '\\') { text.Append(c); continue; }
                    if (_pos >= _json.Length) return false;
                    char escaped = _json[_pos++];
                    switch (escaped)
                    {
                        case '"': text.Append('"'); break;
                        case '\\': text.Append('\\'); break;
                        case '/': text.Append('/'); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'n': text.Append('\n'); break;
                        case 'r': text.Append('\r'); break;
                        case 't': text.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _json.Length) return false;
                            int hex = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                char digit = _json[_pos++];
                                int number = digit >= '0' && digit <= '9' ? digit - '0' :
                                    digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 :
                                    digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 : -1;
                                if (number < 0) return false;
                                hex = hex * 16 + number;
                            }
                            text.Append((char)hex);
                            break;
                        default: return false;
                    }
                }
                return false;
            }

            bool ReadNumber(out object value)
            {
                value = null;
                int start = _pos;
                if (_pos < _json.Length && _json[_pos] == '-') _pos++;
                if (_pos >= _json.Length) return false;
                if (_json[_pos] == '0') _pos++;
                else
                {
                    if (_json[_pos] < '1' || _json[_pos] > '9') return false;
                    while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') _pos++;
                }
                if (_pos < _json.Length && _json[_pos] == '.')
                {
                    _pos++;
                    if (!Digits()) return false;
                }
                if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    _pos++;
                    if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                    if (!Digits()) return false;
                }
                value = new NumberToken(_json.Substring(start, _pos - start));
                return true;
            }

            bool Digits()
            {
                int start = _pos;
                while (_pos < _json.Length && _json[_pos] >= '0' && _json[_pos] <= '9') _pos++;
                return _pos > start;
            }
        }
    }
}
