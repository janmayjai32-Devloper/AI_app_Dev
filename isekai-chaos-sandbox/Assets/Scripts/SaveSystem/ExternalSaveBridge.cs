using System;
using System.IO;
using UnityEngine;

namespace IsekaiChaos.SaveSystem
{
    /// <summary>
    /// Platform abstraction for exporting/importing a save file outside the sandbox
    /// (so players of sideloaded APKs can back up before reinstalling).
    ///
    /// Android 10+ scoped storage forbids writing to arbitrary paths, so the shipping
    /// implementation should use the Storage Access Framework. Recommended drop-in plugins:
    ///   * NativeFilePicker (yasirkula)  — ACTION_CREATE_DOCUMENT / ACTION_OPEN_DOCUMENT
    ///   * NativeShare (yasirkula)       — share sheet (Drive, Discord, email, Files)
    /// Define ICS_NATIVE_FILE_PICKER once the plugin is imported to enable that path.
    /// </summary>
    public interface IExternalSaveBridge
    {
        void Export(string sourceFilePath, string suggestedName, Action<bool, string> onDone);
        void Import(Action<string> onPickedFilePath);   // null if cancelled
    }

    public static class ExternalSaveBridge
    {
        public static IExternalSaveBridge Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && ICS_NATIVE_FILE_PICKER
            return new AndroidSafBridge();
#else
            return new FolderBridge();
#endif
        }
    }

    /// <summary>
    /// Fallback: copies to/from "<persistentDataPath>/Exports". On PC this is a normal folder
    /// (opened in Explorer/Finder); on Android it is reachable via USB / file managers at
    /// Android/data/&lt;package&gt;/files/Exports.
    /// </summary>
    public class FolderBridge : IExternalSaveBridge
    {
        public static string ExportDir => Path.Combine(Application.persistentDataPath, "Exports");

        public void Export(string src, string suggestedName, Action<bool, string> onDone)
        {
            try
            {
                Directory.CreateDirectory(ExportDir);
                var dst = Path.Combine(ExportDir, suggestedName);
                File.Copy(src, dst, true);
#if UNITY_STANDALONE
                Application.OpenURL("file://" + ExportDir);
#endif
                onDone?.Invoke(true, dst);
            }
            catch (Exception e) { onDone?.Invoke(false, e.Message); }
        }

        /// <summary>Picks the newest .sav in the Exports folder (UI can list them instead).</summary>
        public void Import(Action<string> onPicked)
        {
            if (!Directory.Exists(ExportDir)) { onPicked?.Invoke(null); return; }
            string newest = null; DateTime best = DateTime.MinValue;
            foreach (var f in Directory.GetFiles(ExportDir, "*.sav"))
            {
                var t = File.GetLastWriteTimeUtc(f);
                if (t > best) { best = t; newest = f; }
            }
            onPicked?.Invoke(newest);
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR && ICS_NATIVE_FILE_PICKER
    public class AndroidSafBridge : IExternalSaveBridge
    {
        public void Export(string src, string suggestedName, Action<bool, string> onDone)
        {
            NativeFilePicker.ExportFile(src, success => onDone?.Invoke(success, success ? "Exported" : "Cancelled"));
        }

        public void Import(Action<string> onPicked)
        {
            NativeFilePicker.PickFile(path => onPicked?.Invoke(path), new[] { "*/*" });
        }
    }
#endif
}
