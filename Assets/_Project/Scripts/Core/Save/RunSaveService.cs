using System;
using System.IO;
using UnityEngine;

public class RunSaveService
{
    private const string SaveFileName = "run_save.json";

    public void Save(RunSaveData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[RunSaveService] Save ignored because data is null.");
            return;
        }

        string path = GetRunSavePath();

        try
        {
            string directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunSaveService] Failed to save run in '{path}': {ex.Message}");
        }
    }

    public bool TryLoad(out RunSaveData data)
    {
        data = null;
        string path = GetRunSavePath();

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<RunSaveData>(json);
            if (data != null)
                return true;

            Debug.LogWarning($"[RunSaveService] Save file '{path}' could not be parsed (null data).");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RunSaveService] Failed to load run from '{path}': {ex.Message}");
            return false;
        }
    }

    private static string GetRunSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }
}