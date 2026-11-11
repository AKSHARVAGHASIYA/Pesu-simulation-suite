using UnityEngine;

public class DynamicHeightAdjuster : MonoBehaviour
{
    // Reference to the OVRCameraRig
    public OVRCameraRig ovrCameraRig;

    // Reference to the object that needs to be adjusted
    public GameObject objectToAdjust;

    // Multiplier to determine the object's height relative to the user's height
    [Range(0f, 1f)]
    public float heightMultiplier = 0.5f;

    void Start()
    {
        if (ovrCameraRig == null)
        {
            Debug.LogError("OVRCameraRig reference is missing.");
            return;
        }

        if (objectToAdjust == null)
        {
            Debug.LogError("Object to adjust is not assigned.");
            return;
        }
    }

    void Update()
    {
        AdjustObjectHeight();
    }

    void AdjustObjectHeight()
    {
        // Retrieve the user's height from the center eye anchor's local Y position
        float userHeight = ovrCameraRig.centerEyeAnchor.localPosition.y;

        // Calculate the new height for the object
        float newObjectHeight = userHeight * heightMultiplier;

        // Update the object's position
        objectToAdjust.transform.position = new Vector3(
            objectToAdjust.transform.position.x,
            newObjectHeight,
            objectToAdjust.transform.position.z
        );
    }
}
