using System;
using System.Runtime.InteropServices;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Выбор картинки с диска. В редакторе — окно Unity, в сборке под Windows — стандартное окно «Открыть»
    /// (comdlg32). На других системах окна нет: Supported = false, и страница профиля предлагает ввести путь.
    /// </summary>
    public static class FilePicker
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif

        /// <summary>Путь к выбранной картинке; null — отменили или окна на этой системе нет.</summary>
        public static string PickImage()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Картинка для аватара", "",
                new[] { "Картинки", "png,jpg,jpeg" });
            return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
            return Win32.Open("Картинка для аватара", "Картинки (PNG, JPG)\0*.png;*.jpg;*.jpeg\0\0");
#else
            return null;
#endif
        }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        static class Win32
        {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            class OpenFileName
            {
                public int structSize;
                public IntPtr dlgOwner;
                public IntPtr instance;
                public string filter;
                public string customFilter;
                public int maxCustFilter;
                public int filterIndex;
                public IntPtr file;
                public int maxFile;
                public string fileTitle;
                public int maxFileTitle;
                public string initialDir;
                public string title;
                public int flags;
                public short fileOffset;
                public short fileExtension;
                public string defExt;
                public IntPtr custData;
                public IntPtr hook;
                public string templateName;
                public IntPtr reservedPtr;
                public int reservedInt;
                public int flagsEx;
            }

            [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern bool GetOpenFileNameW([In, Out] OpenFileName ofn);

            [DllImport("user32.dll")]
            static extern IntPtr GetActiveWindow();

            const int MaxPath = 4096;
            const int OfnFileMustExist = 0x1000, OfnPathMustExist = 0x800, OfnNoChangeDir = 0x8, OfnExplorer = 0x80000;

            public static string Open(string title, string filter)
            {
                var ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(ofn);
                ofn.dlgOwner = GetActiveWindow();
                ofn.filter = filter;
                ofn.filterIndex = 1;
                ofn.maxFile = MaxPath;
                ofn.file = Marshal.AllocHGlobal(MaxPath * 2);
                try
                {
                    // Буфер имени файла должен начинаться с нуля.
                    Marshal.Copy(new byte[MaxPath * 2], 0, ofn.file, MaxPath * 2);
                    ofn.title = title;
                    ofn.flags = OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnExplorer;
                    if (!GetOpenFileNameW(ofn)) return null;
                    string path = Marshal.PtrToStringUni(ofn.file);
                    return string.IsNullOrEmpty(path) ? null : path;
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(ofn.file);
                }
            }
        }
#endif
    }
}
