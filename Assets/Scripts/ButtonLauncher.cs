// Required namespaces
using UnityEngine; // Provides Unity-specific classes and functions
using TMPro; // For using TextMeshPro UI elements
using Oculus.Interaction; // For Oculus-specific interaction components

// Defines a class that handles behavior of MCQ buttons
public class MCQButtonHandler : MonoBehaviour
{
    // Reference to the Text label that displays the option text on the button
    [SerializeField]
    private TMP_Text label;

    // Reference to the PokeInteractable component, which detects poke interactions (touch-based input)
    [SerializeField]
    private PokeInteractable pokeInteractable;

    // Reference to the SessionLauncher script that will handle the session launching logic
    [SerializeField]
    private SessionLauncher manager;

    // Stores the actual session code to be submitted when the button is pressed
    private string sessionCode;

    // Called when the object is first initialized
    private void Awake()
    {
        // Subscribe to the pointer event so we can detect when the button is interacted with
        pokeInteractable.WhenPointerEventRaised += HandlePointerEvent;
    }

    // Called when the object is destroyed (e.g., scene unload or GameObject disabled)
    private void OnDestroy()
    {
        // Unsubscribe from the pointer event to avoid memory leaks or null reference exceptions
        pokeInteractable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    // Public method to set both the display text and session code associated with this button
    public void SetOption(string displayText, string codeToSend)
    {
        // Assign the session code that will be sent on interaction
        sessionCode = codeToSend;

        // If label is assigned, set the UI text to the provided display text
        if (label != null)
        {
            label.text = displayText;
        }
    }

    // Method that handles pointer events raised by the poke interaction
    private void HandlePointerEvent(PointerEvent evt)
    {
        // If the event type is "Unselect" (finger leaves the button), trigger the session
        if (evt.Type == PointerEventType.Unselect)
        {
            // Call the session launcher's method to verify and launch the session with the stored code
            manager.VerifyAndLaunchSessionExternally(sessionCode);
        }
    }
}
