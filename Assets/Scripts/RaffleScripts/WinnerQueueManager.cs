using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class WinnerQueueManager : MonoBehaviour
{
    public static WinnerQueueManager Instance { get; private set; }

    private Queue<WinnerEntry> queue = new Queue<WinnerEntry>();
    public float processDelay = 1.0f; // seconds between winner processing
    private bool isProcessing = false;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;
        DontDestroyOnLoad(this);
    }

    public void EnqueueWinner(WinnerEntry entry)
    {
        queue.Enqueue(entry);
        if (!isProcessing)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;
        while (queue.Count > 0)
        {
            WinnerEntry entry = queue.Dequeue();
            // Only process if still in raffle and have hunger left
            if (RaffleHungerManager.Instance.IsRaffleMode && RaffleHungerManager.Instance.Hunger > 0)
            {
                if (!WinnerManager.Instance.IsWinner(entry.email))
                {
                    WinnerManager.Instance.AddWinner(entry);
                    RaffleHungerManager.Instance.DecrementHunger();
                    Debug.Log($"[RaffleQueue] Winner added: {entry.name} ({entry.email}), Hunger now: {RaffleHungerManager.Instance.Hunger}");
                }
                else
                {
                    Debug.Log($"[RaffleQueue] Duplicate winner attempted: {entry.email}, NOT added, hunger NOT decremented.");
                }
            }
            else
            {
                Debug.Log($"[RaffleQueue] Not in raffle mode or hunger depleted, skipping: {entry.email}");
            }
            yield return new WaitForSeconds(processDelay);
        }
        isProcessing = false;
    }
}
