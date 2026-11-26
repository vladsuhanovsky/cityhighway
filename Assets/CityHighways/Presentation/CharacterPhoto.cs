using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Explicit local photo export. No HUD, account data, upload or game-state changes.</summary>
    public static class CharacterPhoto
    {
        public const int MaxDimension = 2048;
        public static string Folder => Path.Combine(Application.persistentDataPath, "Photos");

        // A fresh file for each explicit capture; never overwrite existing photos.
        public static string WritePng(string folder, byte[] png)
        {
            if (string.IsNullOrWhiteSpace(folder) || png == null || png.Length < 8 ||
                png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 ||
                png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10)
                throw new ArgumentException("Expected PNG data and a destination folder.");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "CityHighways-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) +
                "-" + Guid.NewGuid().ToString("N") + ".png");
            bool created = false;
            try
            {
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    stream.Write(png, 0, png.Length);
                }
                return path;
            }
            catch
            {
                if (created) try { File.Delete(path); } catch { }
                throw;
            }
        }

        public static bool TrySave(RenderTexture source, out string path)
        {
            path = null;
            if (source == null || !source.IsCreated() ||
                source.width < 1 || source.height < 1 ||
                source.width > MaxDimension || source.height > MaxDimension) return false;
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                copy = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                RenderTexture.active = source;
                copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);
                copy.Apply(false,false);
                path = WritePng(Folder, copy.EncodeToPNG());
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Character photo failed: " + ex.Message);
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (copy != null)
                {
                    if (Application.isPlaying) Object.Destroy(copy); else Object.DestroyImmediate(copy);
                }
            }
        }
    }
}
