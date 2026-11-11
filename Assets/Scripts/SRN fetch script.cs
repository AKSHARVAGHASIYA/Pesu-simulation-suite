// Required Unity and external namespaces
using UnityEngine;
using TMPro; // For TextMeshPro UI elements
using UnityEngine.EventSystems; // For handling pointer events (like button press)
using UnityEngine.Networking; // For making web requests
using System.Collections;
using System.Collections.Generic;
using MiniJSON; // For parsing and serializing JSON
using Oculus.Interaction; // For Oculus-specific interaction components

public class SRNSessionFetcher : MonoBehaviour
{
    // UI input field and status display
    [Header("UI")]
    [SerializeField]
    private TMP_InputField inputField;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text sessionInfoText;

    // Button for submitting SRN input
    [Header("Submit Button")]
    [SerializeField]
    private PokeInteractable pokeInteractable;

    [SerializeField]
    private GameObject srnui; // UI showing SRN input screen

    [SerializeField]
    private GameObject sessioncodeui; // UI showing session selection

    // Configurable settings
    [Header("Settings")]
    [SerializeField]
    private int maxLength = 13; // Max SRN character length

    [SerializeField]
    private float focusRefresh = 2f; // Time after which input field is refocused

    [SerializeField]
    private float netTimeout = 15f; // Request timeout in seconds

    // Session selection buttons
    [Header("Session Buttons")]
    [SerializeField]
    private MCQButtonHandler button1;

    [SerializeField]
    private MCQButtonHandler button2;

    [SerializeField]
    private MCQButtonHandler button3;

    // Endpoint to fetch session data from Supabase Edge Function
    private const string ENDPOINT =
        "https://twwgbdwnrsntinfhpplr.supabase.co/functions/v1/student-session";
    private float focusTimer = 0f; // Tracks time to manage input focus

    // Subscribe to button events
    private void Awake()
    {
        if (pokeInteractable != null)
        {
            pokeInteractable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    // Unsubscribe when destroyed
    private void OnDestroy()
    {
        if (pokeInteractable != null)
        {
            pokeInteractable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }

    // Initialize component state
    private void Start()
    {
        DisableAllButtons();
        inputField.characterLimit = maxLength; // Limit input field length
        inputField.onValueChanged.AddListener(OnInputChanged); // Normalize SRN to uppercase
        SetStatus("Enter SRN and press button…");
        Invoke(nameof(OpenKeyboard), 0.5f); // Open input after short delay
    }

    // Keeps input field focused every few seconds
    private void Update()
    {
        focusTimer += Time.deltaTime;
        if (focusTimer >= focusRefresh)
        {
            focusTimer = 0f;
            if (!inputField.isFocused)
                OpenKeyboard();
        }
    }

    // Force uppercase and trim SRN input
    private void OnInputChanged(string raw)
    {
        string txt = raw.ToUpper();
        if (txt.Length > maxLength)
            txt = txt[..maxLength];

        if (inputField.text != txt)
        {
            inputField.text = txt;
            inputField.caretPosition = txt.Length;
        }
    }

    // Triggered when button is released
    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Unselect)
        {
            Debug.Log("🟢 Button pressed: fetching sessions...");
            FetchButtonPressed();
        }
    }

    // Called on submit, validates SRN then begins fetch
    private void FetchButtonPressed()
    {
        string srn = inputField.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(srn))
        {
            SetStatus("SRN is empty");
            return;
        }

        SetStatus("Fetching active sessions...");
        StartCoroutine(FetchSessionCoroutine(srn));
    }

    // Focuses and activates the input field
    private void OpenKeyboard()
    {
        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.OnPointerClick(new PointerEventData(EventSystem.current));
        inputField.ActivateInputField();
    }

    // Coroutine to fetch sessions via HTTP POST request
    private IEnumerator FetchSessionCoroutine(string srn)
    {
        var dict = new Dictionary<string, string> { { "srn", srn } };
        string json = Json.Serialize(dict);

        UnityWebRequest req = new UnityWebRequest(ENDPOINT, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = (int)netTimeout
        };

        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        // Handle network or HTTP error
        if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 300)
        {
            Debug.LogError($"❌ Net error: {req.result} {req.error} HTTP {req.responseCode}");
            Debug.LogError("Body: " + req.downloadHandler.text);
            SetStatus("Failed to fetch session data");
            yield break;
        }

        // Parse and process the session data
        List<object> parsed = Json.Deserialize(req.downloadHandler.text) as List<object>;
        if (parsed == null || parsed.Count == 0)
        {
            SetStatus("No active sessions found.");
            sessionInfoText.text = "";
            srnui.SetActive(true);
            sessioncodeui.SetActive(false);
            DisableAllButtons();
            yield break;
        }

        // Populate UI with received session info
        SetStatus("Active sessions:");
        sessioncodeui.SetActive(true);
        sessionInfoText.text = "";

        MCQButtonHandler[] buttons = { button1, button2, button3 };
        int count = Mathf.Min(parsed.Count, buttons.Length);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < count)
            {
                var session = parsed[i] as Dictionary<string, object>;
                string code = session["session_code"]?.ToString();
                string proc = session["procedure_name"]?.ToString();
                string mode = session["mode"]?.ToString();
                string display = $"{code} | {proc} | {mode}";

                buttons[i].SetOption(display, code); // Set button text and associated session code
                buttons[i].gameObject.SetActive(true);
            }
            else
            {
                buttons[i].gameObject.SetActive(false);
            }
        }

        sessionInfoText.text = "";
        srnui.SetActive(false);
    }

    // Hides all the session buttons
    private void DisableAllButtons()
    {
        if (button1 != null)
            button1.gameObject.SetActive(false);
        if (button2 != null)
            button2.gameObject.SetActive(false);
        if (button3 != null)
            button3.gameObject.SetActive(false);
    }

    // Updates the status label and logs to console
    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"ℹ Status → {msg}");
    }
}
