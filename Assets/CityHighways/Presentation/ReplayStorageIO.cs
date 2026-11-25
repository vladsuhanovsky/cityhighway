using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Pure filesystem primitives for local replay persistence. No Unity
    /// singletons: paths and limits can be exercised in isolated NUnit tests.
    /// </summary>
    public static class ReplayStorageIO
    {
        public const int MaxIdLength = 80;
        public const int MaxIndexBytes = 512 * 1024;
        public const int MaxLocalReplayBytes = 8 * 1024 * 1024;

        public static bool IsSafeId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
                return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                bool digit = c >= '0' && c <= '9';
                if (!letter && !digit && (i == 0 || (c != '-' && c != '_')))
                    return false;
            }

            // index.json is the metadata file in the same directory;
            // device names must never be treated as files on Windows.
            string upper = id.ToUpperInvariant();
            if (upper == "INDEX" || upper == "CON" || upper == "PRN" ||
                upper == "AUX" || upper == "NUL")
                return false;
            if (upper.Length == 4 &&
                (upper.StartsWith("COM", StringComparison.Ordinal) ||
                 upper.StartsWith("LPT", StringComparison.Ordinal)) &&
                upper[3] >= '1' && upper[3] <= '9')
                return false;
            return true;
        }

        public static string PathFor(string folder, string id)
        {
            if (string.IsNullOrEmpty(folder) || !IsSafeId(id))
                throw new ArgumentException("Unsafe replay ID or missing storage folder.");
            return Path.Combine(folder, id + ".json");
        }

        public const int MaxRecoveryScan = 300;

        /// <summary>
        /// Enumerate only safe replay candidates, excluding index.json and
        /// already indexed files. A hard scan cap keeps startup predictable.
        /// </summary>
        public static IEnumerable<string> UnindexedIds(string folder, ISet<string> indexedIds)
        {
            if (indexedIds == null) throw new ArgumentNullException(nameof(indexedIds));
            if (!Directory.Exists(folder)) yield break;
            int scanned = 0;
            foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (++scanned > MaxRecoveryScan) yield break;
                string id = Path.GetFileNameWithoutExtension(file);
                if (IsSafeId(id) && !indexedIds.Contains(id))
                    yield return id;
            }
        }

        /// <summary>Read at most maxBytes, using strict UTF-8 and no unbounded ReadAllText.</summary>
        public static string ReadBoundedUtf8(string path, int maxBytes)
        {
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length > maxBytes - count)
                        throw new InvalidDataException("Replay file exceeds permitted size.");
                    output.Write(buffer, 0, count);
                }
                string json = new UTF8Encoding(false, true).GetString(output.ToArray());
                return json.Length > 0 && json[0] == '\uFEFF' ? json.Substring(1) : json;
            }
        }

        /// <summary>
        /// Verify serialized UTF-8 bytes before any filesystem mutation.
        /// String length is not a byte-size bound for non-ASCII players.
        /// </summary>
        public static void RequireUtf8Size(string content, int maxBytes)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (new UTF8Encoding(false, true).GetByteCount(content) > maxBytes)
                throw new InvalidDataException("Replay JSON exceeds permitted byte size.");
        }

        /// <summary>
        /// Write a new temporary sibling and atomically move/replace it.
        /// If replacement is unavailable, fail without truncating the old file.
        /// </summary>
        public static void WriteAtomic(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Missing file path.");
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("Missing directory.");
            Directory.CreateDirectory(folder);
            string pending = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(pending, content, new UTF8Encoding(false, true));
                if (File.Exists(path))
                    File.Replace(pending, path, null);
                else
                    File.Move(pending, path);
            }
            finally
            {
                try { if (File.Exists(pending)) File.Delete(pending); }
                catch (IOException) { /* A failed cleanup must not hide the original error. */ }
            }
        }
    }
}
