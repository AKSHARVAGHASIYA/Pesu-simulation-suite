// /*
//  * SessionLauncher.cs  – auto-fire when length == 8
//  * (changes marked with ★ NEW)
//  */
// using UnityEngine;
// using TMPro;
// using UnityEngine.EventSystems;
// using UnityEngine.Networking;
// using System.Collections;
// using System.Collections.Generic;
// using MiniJSON;

// public class SessionLauncher : MonoBehaviour
// {
//     /* --------------- Inspector fields --------------- */
//     [Header("UI")]
//     [SerializeField] private TMP_InputField inputField;
//     [SerializeField] private TMP_Text statusText;

//     [Header("Settings")]
//     [SerializeField] private int maxLength = 8;
//     [SerializeField] private float focusRefresh = 2f;
//     [SerializeField] private float netTimeout = 15f;

//     private const string ENDPOINT =
//         "https://twwgbdwnrsntinfhpplr.supabase.co/functions/v1/verify-session";

//     private float focusTimer;
//     private string lastSubmittedCode = "";

//     /* ------------------------------------------------------------------ */
//     #region Life-cycle & Input

//     void Start()
//     {
//         if (inputField == null) inputField = GetComponent<TMP_InputField>();
//         if (statusText == null) statusText = GetComponentInChildren<TMP_Text>();

//         if (inputField == null)
//         {
//             Debug.LogError("❌ No TMP_InputField referenced.");
//             enabled = false;
//             return;
//         }

//         inputField.characterLimit = maxLength;
//         inputField.onValueChanged.AddListener(OnInputChanged);
//         inputField.onSubmit.AddListener(OnInputSubmitted);

//         SetStatus("Enter session code…");
//         Invoke(nameof(OpenKeyboard), 0.5f);
//     }

//     void Update()
//     {
//         focusTimer += Time.deltaTime;
//         if (focusTimer >= focusRefresh)
//         {
//             focusTimer = 0f;
//             if (!inputField.isFocused) OpenKeyboard();
//         }
//     }

//     void OnInputChanged(string raw)
//     {
//         string txt = raw.ToUpper();
//         if (txt.Length > maxLength) txt = txt[..maxLength];

//         if (inputField.text != txt)
//         {
//             inputField.text = txt;
//             inputField.caretPosition = txt.Length;
//         }

//         /* ★ NEW: auto-submit once the code reaches the exact length */
//         if (txt.Length == maxLength && txt != lastSubmittedCode)
//         {
//             OnInputSubmitted(txt);      // fire just once for this code
//         }
//     }

//     void OnInputSubmitted(string raw)
//     {
//         lastSubmittedCode = raw.ToUpper();
//         Debug.Log($"🔸 SUBMIT '{lastSubmittedCode}'");
//         SetStatus("Verifying code…");
//         StartCoroutine(VerifySession(lastSubmittedCode));
//     }

//     void OpenKeyboard()
//     {
//         EventSystem.current.SetSelectedGameObject(inputField.gameObject);
//         inputField.OnPointerClick(new PointerEventData(EventSystem.current));
//         inputField.ActivateInputField();
//     }

//     #endregion
//     /* ------------------------------------------------------------------ */
//     #region Supabase verification

//     IEnumerator VerifySession(string code)
//     {
//         var dict = new Dictionary<string, object> { { "session_code", code } };
//         string json = MiniJSON.Json.Serialize(dict);
//         Debug.Log($"📤 JSON → {json}");

//         UnityWebRequest req = new UnityWebRequest(ENDPOINT, UnityWebRequest.kHttpVerbPOST)
//         {
//             uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
//             downloadHandler = new DownloadHandlerBuffer(),
//             timeout = (int)netTimeout
//         };
//         req.SetRequestHeader("Content-Type", "application/json");

//         yield return req.SendWebRequest();

//         if (req.result != UnityWebRequest.Result.Success ||
//             req.responseCode < 200 || req.responseCode >= 300)
//         {
//             Debug.LogError($"❌ Net error: {req.result} {req.error} HTTP {req.responseCode}");
//             Debug.LogError($"Body: {req.downloadHandler.text}");
//             SetStatus("Network / server error 😕");
//             yield break;
//         }

//         var parsed = MiniJSON.Json.Deserialize(req.downloadHandler.text) as Dictionary<string, object>;
//         if (parsed == null)
//         {
//             Debug.LogError("❌ JSON parse failed");
//             SetStatus("Invalid response");
//             yield break;
//         }
//         if (parsed.TryGetValue("error", out object errObj))
//         {
//             Debug.LogError("❌ Supabase error: " + errObj);
//             SetStatus("Invalid session code");
//             yield break;
//         }

//         string targetPackage = parsed["package_name"].ToString();
//         bool isPractice = System.Convert.ToBoolean(parsed["is_practice"]);
//         bool isEvaluation = System.Convert.ToBoolean(parsed["is_evaluation"]);
//         string jwt = parsed.ContainsKey("jwt") ? parsed["jwt"].ToString() : "";

//         Debug.Log($"✅ Valid → launching {targetPackage}");
//         SetStatus($"Launching {targetPackage}…");
//         LaunchPackage(targetPackage, isPractice, isEvaluation, jwt);
//     }

//     #endregion
//     /* ------------------------------------------------------------------ */
//     #region Android Intent launcher

//     void LaunchPackage(string pkg, bool practice, bool eval, string jwt)
//     {
// #if UNITY_ANDROID && !UNITY_EDITOR
//     try
//     {
//         string activity = "com.unity3d.player.UnityPlayerGameActivity"; // fully qualified!

//         AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
//         AndroidJavaObject curActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

//         AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent");
//         AndroidJavaObject componentName = new AndroidJavaObject("android.content.ComponentName", pkg, activity);
//         intent.Call<AndroidJavaObject>("setComponent", componentName);

//         intent.Call<AndroidJavaObject>("setAction", "android.intent.action.MAIN");
//         intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.LAUNCHER");

//         // Extras
//         intent.Call<AndroidJavaObject>("putExtra", "session_code", lastSubmittedCode);
//         intent.Call<AndroidJavaObject>("putExtra", "is_practice", practice);
//         intent.Call<AndroidJavaObject>("putExtra", "is_evaluation", eval);
//         intent.Call<AndroidJavaObject>("putExtra", "jwt", jwt);

//         // FLAG_ACTIVITY_NEW_TASK
//         const int FLAG_ACTIVITY_NEW_TASK = 0x10000000;
//         intent.Call<AndroidJavaObject>("addFlags", FLAG_ACTIVITY_NEW_TASK);

//         curActivity.Call("startActivity", intent);
//         Debug.Log($"🚀 Successfully launched {pkg}/{activity}");
//         SetStatus("Session launched ✔");
//     }
//     catch (System.Exception ex)
//     {
//         Debug.LogError("❌ LaunchPackage Exception: " + ex);
//         SetStatus("Launch failed");
//     }
// #else
//         Debug.Log($"[Editor] Would launch '{pkg}' with session_code={lastSubmittedCode}, is_practice={practice}, is_evaluation={eval}, jwt={jwt}");
//         SetStatus("[Editor] Pretend-launch");
// #endif
//     }



//     #endregion
//     /* ------------------------------------------------------------------ */
//     #region Helpers

//     void SetStatus(string msg)
//     {
//         if (statusText != null) statusText.text = msg;
//         Debug.Log($"ℹ Status → {msg}");
//     }

//     #endregion
// }
