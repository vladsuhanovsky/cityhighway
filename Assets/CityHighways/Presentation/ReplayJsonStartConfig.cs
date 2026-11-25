using System;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Verifies that the original downloaded JSON explicitly contains an
    /// object-valued start.config. Unity's JsonUtility may materialize a
    /// default GameConfig even if the wire field was absent or null, so
    /// checking only the deserialized object is insufficient.
    ///
    /// This is an allocation-light structural check: it skips values without
    /// materializing the potentially 20,000-command replay history.
    /// </summary>
    public static class ReplayJsonStartConfig
    {
        const int MaxDepth = 24;

        public static bool HasExplicitConfig(string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > Replays.MaxCloudReplayJsonBytes)
                return false;
            var reader = new Reader(json);
            return reader.Check();
        }

        sealed class Reader
        {
            readonly string _json;
            int _pos;
            bool _foundStart;
            bool _foundConfig;

            public Reader(string json) { _json = json; }

            public bool Check()
            {
                if (!ReadObject(0, 0)) return false;
                WhiteSpace();
                return _pos == _json.Length && _foundStart && _foundConfig;
            }

            void WhiteSpace()
            {
                while (_pos < _json.Length)
                {
                    char c = _json[_pos];
                    if (c != ' ' && c != '\t' && c != '\r' && c != '\n') break;
                    _pos++;
                }
            }

            bool Take(char c)
            {
                WhiteSpace();
                if (_pos >= _json.Length || _json[_pos] != c) return false;
                _pos++;
                return true;
            }

            char Peek()
            {
                WhiteSpace();
                return _pos < _json.Length ? _json[_pos] : '\0';
            }

            bool ReadString(out string value)
            {
                value = null;
                if (!Take('"')) return false;
                int begin = _pos;
                bool escaped = false;
                while (_pos < _json.Length)
                {
                    char c = _json[_pos++];
                    if (c == '"')
                    {
                        // Escaped property names are conservatively not
                        // recognized as start/config; Unity emits plain keys.
                        if (!escaped) value = _json.Substring(begin, _pos - begin - 1);
                        return true;
                    }
                    if (c < 0x20) return false;
                    if (c != '\\') continue;
                    escaped = true;
                    if (_pos >= _json.Length) return false;
                    char e = _json[_pos++];
                    if (e == 'u')
                    {
                        if (_pos + 4 > _json.Length) return false;
                        for (int i = 0; i < 4; i++)
                        {
                            char hex = _json[_pos++];
                            if (!((hex >= '0' && hex <= '9') ||
                                  (hex >= 'a' && hex <= 'f') ||
                                  (hex >= 'A' && hex <= 'F')))
                                return false;
                        }
                    }
                    else if (e != '"' && e != '\\' && e != '/' &&
                             e != 'b' && e != 'f' && e != 'n' &&
                             e != 'r' && e != 't')
                        return false;
                }
                return false;
            }

            bool Literal(string word)
            {
                if (_pos + word.Length > _json.Length ||
                    string.CompareOrdinal(_json, _pos, word, 0, word.Length) != 0)
                    return false;
                _pos += word.Length;
                return true;
            }

            bool Number()
            {
                if (_pos < _json.Length && _json[_pos] == '-') _pos++;
                if (_pos >= _json.Length) return false;
                if (_json[_pos] == '0') _pos++;
                else
                {
                    if (_json[_pos] < '1' || _json[_pos] > '9') return false;
                    while (_pos < _json.Length && _json[_pos] >= '0' &&
                           _json[_pos] <= '9') _pos++;
                }

                if (_pos < _json.Length && _json[_pos] == '.')
                {
                    _pos++;
                    int begin = _pos;
                    while (_pos < _json.Length && _json[_pos] >= '0' &&
                           _json[_pos] <= '9') _pos++;
                    if (_pos == begin) return false;
                }
                if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    _pos++;
                    if (_pos < _json.Length && (_json[_pos] == '+' ||
                                                _json[_pos] == '-')) _pos++;
                    int begin = _pos;
                    while (_pos < _json.Length && _json[_pos] >= '0' &&
                           _json[_pos] <= '9') _pos++;
                    if (_pos == begin) return false;
                }
                return true;
            }

            bool Value(int depth)
            {
                if (depth > MaxDepth) return false;
                switch (Peek())
                {
                    case '{': return ReadObject(depth + 1, -1);
                    case '[': return ReadArray(depth + 1);
                    case '"': return ReadString(out _);
                    case 't': return Literal("true");
                    case 'f': return Literal("false");
                    case 'n': return Literal("null");
                    default: return Number();
                }
            }

            // level 0 = replay root, 1 = its start object,
            // -1 = unrelated nested object.
            bool ReadObject(int depth, int level)
            {
                if (depth > MaxDepth || !Take('{')) return false;
                if (Take('}')) return true;
                do
                {
                    if (!ReadString(out string key) || !Take(':')) return false;
                    if (level == 0 && key == "start")
                    {
                        if (_foundStart || Peek() != '{' ||
                            !ReadObject(depth + 1, 1)) return false;
                        _foundStart = true;
                    }
                    else if (level == 1 && key == "config")
                    {
                        if (_foundConfig || Peek() != '{' ||
                            !ReadObject(depth + 1, -1)) return false;
                        _foundConfig = true;
                    }
                    else if (!Value(depth + 1)) return false;

                    if (Take('}')) return true;
                } while (Take(','));
                return false;
            }

            bool ReadArray(int depth)
            {
                if (depth > MaxDepth || !Take('[')) return false;
                if (Take(']')) return true;
                do
                {
                    if (!Value(depth + 1)) return false;
                    if (Take(']')) return true;
                } while (Take(','));
                return false;
            }
        }
    }
}
