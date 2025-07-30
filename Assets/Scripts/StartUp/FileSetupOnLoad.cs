using UnityEngine;
using System.IO;

public class FileSetupOnLoad : MonoBehaviour
{
    void Awake()
    {
        // 1. FishTextures folder: create and empty
        string fishTexturesDir = Path.Combine(Application.persistentDataPath, "FishTextures");
        if (!Directory.Exists(fishTexturesDir))
            Directory.CreateDirectory(fishTexturesDir);

        // 2. winners.json and revealed_fish.json: create if missing, write {} if empty
        string winnersFile = Path.Combine(Application.persistentDataPath, "winners.json");
        string revealedFishFile = Path.Combine(Application.persistentDataPath, "revealed_fish.json");

        EnsureJsonFileWinner(winnersFile);
        EnsureJsonFile(revealedFishFile);

        Debug.Log("FishTextures directory and JSON files checked/created.");
    }

    void EnsureJsonFile(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "{}");
        }
        else
        {
            string content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
                File.WriteAllText(path, "{}");
        }
    }

    void EnsureJsonFileWinner(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "{\"list\": []}");
        }
        else
        {
            string content = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(content))
                File.WriteAllText(path, "{\"list\": []}");
        }
    }
}
