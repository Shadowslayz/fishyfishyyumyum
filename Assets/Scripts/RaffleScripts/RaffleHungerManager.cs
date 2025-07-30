using UnityEngine;

public class RaffleHungerManager : MonoBehaviour
{
    public static RaffleHungerManager Instance { get; private set; }
    public bool IsRaffleMode { get; private set; } = false;
    public int Hunger { get; private set; } = 0;
    public int MaxHunger { get; private set; } = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
        DontDestroyOnLoad(this); // So the manager persists across scenes
    }

    public void StartRaffleMode(int initialHunger)
    {
        IsRaffleMode = true;
        Hunger = initialHunger;
        MaxHunger = initialHunger;
        RaffleUIManager.Instance.UpdateHunger(Hunger);
        // Trigger any UI update here
    }

    public void StopRaffleMode()
    {
        IsRaffleMode = false;
        Hunger = 0;
        MaxHunger = 0;
        RaffleUIManager.Instance.HideRafflePanel();
    }

    public void DecrementHunger()
    {
        if (Hunger > 0) Hunger--;
        RaffleUIManager.Instance.UpdateHunger(Hunger);
        // Update UI here as well
    }

    public void IncrementHunger()
    {
        Hunger++;
        MaxHunger++;
        RaffleUIManager.Instance.UpdateHunger(Hunger);
        // Update UI
    }
}
