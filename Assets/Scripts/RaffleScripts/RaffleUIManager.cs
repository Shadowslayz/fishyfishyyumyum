using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class RaffleUIManager : MonoBehaviour
{
    public static RaffleUIManager Instance { get; private set; }

    public GameObject rafflePanel; // Assign your RafflePanel here
    public TextMeshProUGUI hungerText; // Assign your HungerText
    public Transform winnersContent; // Assign the Content under the ScrollView
    public GameObject winnerEntryPrefab; // Assign your winner entry prefab
    public ScrollRect scrollRect; // Assign the ScrollView's ScrollRect
    public float loopScrollSpeed = 0.05f; // Tweak as needed

    private bool isLoopScrolling = false;

    private List<GameObject> winnerEntries = new List<GameObject>();
    private List<WinnerEntry> sessionWinners = new List<WinnerEntry>(); // <-- Only this session

    void Start()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        rafflePanel.SetActive(false); // Hide on start
    }

    void Update()
    {
        bool show = RaffleHungerManager.Instance != null && RaffleHungerManager.Instance.IsRaffleMode;
        if (show)
        {
            UpdateHunger(RaffleHungerManager.Instance.Hunger);

            if (isLoopScrolling && scrollRect.content.rect.height > scrollRect.viewport.rect.height)
            {
                scrollRect.verticalNormalizedPosition -= loopScrollSpeed * Time.deltaTime;
                if (scrollRect.verticalNormalizedPosition <= 0f)
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }
        else
        {
            StopLoopScroll();
        }
    }

    public void UpdateHunger(int hunger)
    {
        hungerText.text = "Hunger: " + hunger;
    }

    // Call this to show panel and reset session winner list!
    public void ShowRafflePanel()
    {
        rafflePanel.SetActive(true);
        ClearWinners();
        sessionWinners.Clear();
    }

    public void HideRafflePanel()
    {
        rafflePanel.SetActive(false);
        StopLoopScroll();
    }

    // Only adds a new winner to the session and UI
    public void AddWinnerToSession(WinnerEntry winner)
    {
        sessionWinners.Add(winner);

        var go = Instantiate(winnerEntryPrefab, winnersContent);
        if (string.IsNullOrEmpty(winner.ig))
            go.GetComponent<TMP_Text>().text = $"{winner.name}";
        else
            go.GetComponent<TMP_Text>().text = $"{winner.name} ({winner.ig})";
        winnerEntries.Add(go);

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f; // Scroll to bottom (show latest)
        Canvas.ForceUpdateCanvases();

        StartLoopScroll();
    }

    // If you ever want to repopulate the UI from the session list:
    public void RefreshSessionWinners()
    {
        ClearWinners();
        foreach (var winner in sessionWinners)
        {
            var go = Instantiate(winnerEntryPrefab, winnersContent);
            if (string.IsNullOrEmpty(winner.ig))
                go.GetComponent<TMP_Text>().text = $"{winner.name}";
            else
                go.GetComponent<TMP_Text>().text = $"{winner.name} ({winner.ig})";
            winnerEntries.Add(go);
        }
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
        Canvas.ForceUpdateCanvases();

        StartLoopScroll();
    }

    private void StartLoopScroll()
    {
        isLoopScrolling = true;
    }

    private void StopLoopScroll()
    {
        isLoopScrolling = false;
        scrollRect.verticalNormalizedPosition = 1f; // Reset to top
    }

    public void ClearWinners()
    {
        foreach (var entry in winnerEntries)
            Destroy(entry);
        winnerEntries.Clear();
    }
}
