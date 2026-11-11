// Required namespaces for Unity and supporting systems
using UnityEngine;
using TMPro; // For TextMeshPro UI elements
using UnityEngine.EventSystems; // For input field interactions
using UnityEngine.Networking; // For sending HTTP requests
using System.Collections; // For using Coroutines
using System.Collections.Generic; // For generic collections like Dictionary
using MiniJSON; // For JSON serialization/deserialization
using Oculus.Interaction; // For Oculus Interaction SDK features like PokeInteractable

public class SessionLauncher : MonoBehaviour
{
    // --- UI Elements ---
    [Header("UI")]
    [SerializeField]
    private TMP_InputField inputField; // Text input field for session code

    [SerializeField]
    private TMP_Text statusText; // Text for showing status messages to the user

    // --- Submit Button ---
    [Header("Submit Button")]
    [SerializeField]
    private PokeInteractable pokeInteractable; // Oculus button-like interaction trigger

    [SerializeField]
    private GameObject srn; // UI for SRN input or confirmation

    [SerializeField]
    private GameObject session; // UI for session input

    // --- Settings ---
    [Header("Settings")]
    [SerializeField]
    private int maxLength = 8; // Max session code length

    [SerializeField]
    private float focusRefresh = 2f; // How often to re-focus input

    [SerializeField]
    private float netTimeout = 15f; // Timeout for network requests

    // --- Constants and state ---
    private const string ENDPOINT =
        "https://twwgbdwnrsntinfhpplr.supabase.co/functions/v1/verify-session";
    private string lastSubmittedCode = ""; // Last session code submitted
    private float focusTimer = 0f; // Timer to track focus refresh

    // Subscribes to pointer events on Awake
    private void Awake()
    {
        if (pokeInteractable != null)
            pokeInteractable.WhenPointerEventRaised += HandlePointerEvent;
    }

    // Unsubscribes from pointer events when destroyed
    private void OnDestroy()
    {
        if (pokeInteractable != null)
            pokeInteractable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    // Initialization logic
    private void Start()
    {
        inputField.characterLimit = maxLength; // Limit characters in input
        inputField.onValueChanged.AddListener(OnInputChanged); // Add listener to format input
        SetStatus("Enter session code and press button…");
        Invoke(nameof(OpenKeyboard), 0.5f); // Automatically focus keyboard
    }

    // Refresh focus on input field periodically
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

    // Ensures input is uppercase and within max length
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

    // Handles poke interaction
    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Unselect)
        {
            Debug.Log("🟢 Poke button pressed");
            SubmitCode();
        }
    }

    // Public method to launch session externally
    public void VerifyAndLaunchSessionExternally(string sessionCode)
    {
        if (string.IsNullOrEmpty(sessionCode) || sessionCode.Length < maxLength)
        {
            Debug.LogWarning("❗ Invalid session code passed to external launch function.");
            SetStatus("Invalid session code");
            return;
        }

        lastSubmittedCode = sessionCode.ToUpper();
        SetStatus("Verifying external code…");
        StartCoroutine(VerifySession(lastSubmittedCode));
    }

    // Validates input field and initiates verification
    private void SubmitCode()
    {
        string code = inputField.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code) || code.Length < maxLength)
        {
            SetStatus("Enter full session code");
            return;
        }

        lastSubmittedCode = code;
        SetStatus("Verifying code…");
        StartCoroutine(VerifySession(code));
    }

    // Re-focuses input field
    private void OpenKeyboard()
    {
        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.OnPointerClick(new PointerEventData(EventSystem.current));
        inputField.ActivateInputField();
    }

    // Sends session code to backend for verification
    private IEnumerator VerifySession(string code)
    {
        var dict = new Dictionary<string, object> { { "session_code", code } };
        string json = Json.Serialize(dict);
        Debug.Log($"📤 JSON → {json}");

        UnityWebRequest req = new UnityWebRequest(ENDPOINT, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = (int)netTimeout
        };
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (
            req.result != UnityWebRequest.Result.Success
            || req.responseCode < 200
            || req.responseCode >= 300
        )
        {
            Debug.LogError($" Net error: {req.result} {req.error} HTTP {req.responseCode}");
            Debug.LogError("Body: " + req.downloadHandler.text);
            SetStatus("Network error / Invalid Session Code");
            yield break;
        }

        var parsed = Json.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
        if (parsed == null)
        {
            Debug.LogError("❌ JSON parse failed");
            SetStatus("Invalid response");
            yield break;
        }

        if (parsed.TryGetValue("error", out object errObj))
        {
            Debug.LogError("❌ Supabase error: " + errObj);
            SetStatus("Invalid session code");
            yield break;
        }

        // Extract details
        string targetPackage = parsed["package_name"].ToString();
        bool isPractice = System.Convert.ToBoolean(parsed["is_practice"]);
        bool isEvaluation = System.Convert.ToBoolean(parsed["is_evaluation"]);
        string jwt = parsed.ContainsKey("jwt") ? parsed["jwt"].ToString() : "";

        Debug.Log($"✅ Valid → launching {targetPackage}");
        SetStatus($"Launching {targetPackage}…");
        session.SetActive(false);
        srn.SetActive(true);
        LaunchPackage(targetPackage, isPractice, isEvaluation, jwt);
    }

    // Launches external Unity-based Android activity
    // Launches external Unity-based Android activity
    private void LaunchPackage(string pkg, bool practice, bool eval, string jwt)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // Define the activity name in the target Unity-based Android app
            string activity = "com.unity3d.player.UnityPlayerGameActivity";

            // Access the current running Unity Android activity
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject curActivity = unityPlayer.GetStatic<AndroidJavaObject>(
                "currentActivity"
            );

            // Create a new Android Intent to launch another app/activity
            AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent");

            // Set the component (i.e., app package and activity) to be launched
            AndroidJavaObject componentName = new AndroidJavaObject(
                "android.content.ComponentName",
                pkg,
                activity
            );
            intent.Call<AndroidJavaObject>("setComponent", componentName);

            // Set the intent's action to MAIN, meaning it's meant to start an app
            intent.Call<AndroidJavaObject>("setAction", "android.intent.action.MAIN");

            // Add the LAUNCHER category to ensure the activity launches in the foreground like a regular app
            intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.LAUNCHER");

            // Attach the session code as a key-value extra in the intent
            intent.Call<AndroidJavaObject>("putExtra", "session_code", lastSubmittedCode);

            // Attach a boolean extra indicating whether this session is a practice session
            intent.Call<AndroidJavaObject>("putExtra", "is_practice", practice);

            // Attach a boolean extra indicating whether this session is an evaluation session
            intent.Call<AndroidJavaObject>("putExtra", "is_evaluation", eval);

            // Attach the JWT token used for authenticating the session in the launched app
            intent.Call<AndroidJavaObject>("putExtra", "jwt", jwt);

            // Set a flag to ensure the activity starts in a new task (required for Unity-launched activities)
            const int FLAG_ACTIVITY_NEW_TASK = 0x10000000;
            intent.Call<AndroidJavaObject>("addFlags", FLAG_ACTIVITY_NEW_TASK);

            // Launch the target activity using the current Unity activity context
            curActivity.Call("startActivity", intent);

            // Log success message in Unity Console
            Debug.Log($"🚀 Launched {pkg}/{activity}");

            // Optional: Update the in-app UI status
            SetStatus("Session launched ✔");
        }
        catch (System.Exception ex)
        {
            // Catch any exceptions thrown during the launch process and log the error
            Debug.LogError("❌ LaunchPackage Exception: " + ex);
            SetStatus("Launch failed");
        }
#else
        // If we're in the Unity Editor (not on Android), just simulate the launch in logs
        Debug.Log(
            $"[Editor] Would launch '{pkg}' with session_code={lastSubmittedCode}, is_practice={practice}, is_evaluation={eval}, jwt={jwt}"
        );
        SetStatus("[Editor] Pretend-launch");
#endif
    }

    // Displays a message and logs it to console
    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"ℹ Status → {msg}");
    }
}
