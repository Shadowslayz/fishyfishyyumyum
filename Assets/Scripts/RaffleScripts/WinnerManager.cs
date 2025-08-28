using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class WinnerManager : MonoBehaviour
{
    public static WinnerManager Instance { get; private set; }
    public List<WinnerEntry> winners = new List<WinnerEntry>();
    private string winnersPath => Path.Combine(Application.persistentDataPath, "winners.json");

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        // Detach from any parent before marking DontDestroyOnLoad
        transform.SetParent(null);
        DontDestroyOnLoad(this.gameObject);

        LoadWinners();
    }


    public void LoadWinners()
    {
        if (File.Exists(winnersPath))
        {
            string json = File.ReadAllText(winnersPath);
            winners = JsonUtility.FromJson<WinnerList>(json)?.list ?? new List<WinnerEntry>();
        }
    }

    public void SaveWinners()
    {
        WinnerList list = new WinnerList() { list = winners };
        string json = JsonUtility.ToJson(list, true);
        File.WriteAllText(winnersPath, json);
    }

    public bool IsWinner(string email)
    {
        return winners.Exists(w => w.email == email);
    }

    public void AddWinner(WinnerEntry entry)
    {
        if (!IsWinner(entry.email))
        {
            winners.Add(entry);
            SaveWinners();
            RaffleUIManager.Instance.AddWinnerToSession(entry);
        }
    }

    public List<WinnerEntry> GetWinners()
    {
        return winners;
    }

    [Serializable]
    private class WinnerList
    {
        public List<WinnerEntry> list;
    }
}
