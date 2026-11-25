using System;
using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

public static class ClassicSaveRecoveryMenu
{
    const string MenuPath = "City Highways/Recover Classic Save From Backup";

    [MenuItem(MenuPath, true)]
    static bool CanRecover() => !EditorApplication.isPlayingOrWillChangePlaymode &&
        File.Exists(SaveFileStorage.BackupPath(Path.Combine(Application.persistentDataPath, "cityhighways_save.json")));

    [MenuItem(MenuPath)]
    static void Recover()
    {
        if (!CanRecover()) return;
        if (!EditorUtility.DisplayDialog("Restore Classic save",
            "Replace the current Classic save with the previous backup? You may lose recent turns. " +
            "The current file will first be archived. The backup is checked against the current game map.",
            "Restore backup", "Cancel")) return;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "cityhighways_save.json");
            string archive = SaveFileStorage.RestoreBackup(path, null);
            EditorUtility.DisplayDialog("Save restored", archive == null ? "No previous primary file existed." :
                "Original archived at:\n" + archive, "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Restore failed", ex.Message, "OK");
        }
    }
}
