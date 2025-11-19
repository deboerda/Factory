using UnityEngine;

/// <summary>
/// Machine tool guard controller
/// Click button to move guard +5 in Y-axis direction, click again to move -5
/// </summary>
public class MachineGuardController : MonoBehaviour
{
    [Header("Guard Settings")]
    public Transform guardTransform; // Guard Transform component
    public float movementSpeed = 5f; // Movement interpolation speed (larger=faster, recommended 2-10)

    private bool isMoving = false; // Whether currently moving
    private bool isRaised = false; // Whether currently in raised state (Y-axis +5)
    private float targetYPosition = 0f; // Target Y-axis position
    private float originalYPosition = 0f; // Original Y-axis position (initial position)

    /// <summary>
    /// Initialization method
    /// Ensure guardTransform is set
    /// </summary>
    private void Start()
    {
        // If guardTransform not specified, default to current game object's transform
        if (guardTransform == null)
        {
            guardTransform = transform;
            Debug.Log("Guard Transform not specified, using current game object's Transform");
        }

        // Record original Y-axis position
        originalYPosition = guardTransform.localPosition.y;
        targetYPosition = originalYPosition;
        Debug.Log($"Guard initialization complete, original Y-axis position: {originalYPosition}");
    }

    // 角度标准化方法已移除，不再需要

    /// <summary>
    /// Update position each frame
    /// Use Lerp to achieve smooth movement effect
    /// </summary>
    private void Update()
    {
        // If currently moving, execute smooth movement
        if (isMoving && guardTransform != null)
        {
            // Get current position
            Vector3 currentPosition = guardTransform.localPosition;
            
            // Use Lerp to achieve smooth transition
            float newYPosition = Mathf.Lerp(currentPosition.y, targetYPosition, movementSpeed * Time.deltaTime);
            
            // Apply new position, keep X and Z axes unchanged
            guardTransform.localPosition = new Vector3(currentPosition.x, newYPosition, currentPosition.z);
            
            // Check if close to target position (allow small error)
            if (Mathf.Abs(newYPosition - targetYPosition) < 0.1f)
            {
                // Ensure precise arrival at target position
                guardTransform.localPosition = new Vector3(currentPosition.x, targetYPosition, currentPosition.z);
                isMoving = false;
                Debug.Log($"Guard movement complete, current status: {(isRaised ? "raised" : "lowered")}, current Y-axis position: {targetYPosition}");
            }
        }
    }

    /// <summary>
    /// Method to toggle guard state
    /// This method can be bound to UI buttons to achieve Y-axis up/down movement
    /// </summary>
    public void ToggleGuardState()
    {
        // If currently moving, do not respond to new clicks
        if (isMoving)
        {
            Debug.Log("Guard is currently moving, please wait for completion before clicking again");
            return;
        }

        // Toggle state
        isRaised = !isRaised;
        
        // Set target position
        if (isRaised)
        {
            // Raised state: Y-axis +5
            targetYPosition = originalYPosition + 5f;
            Debug.Log($"Starting to raise guard, moving from current position {guardTransform.localPosition.y} to {targetYPosition}...");
        }
        else
        {
            // Lowered state: Y-axis returns to original position (-5)
            targetYPosition = originalYPosition;
            Debug.Log($"Starting to lower guard, moving from current position {guardTransform.localPosition.y} to {targetYPosition}...");
        }
        
        // Start moving
        isMoving = true;
    }
    
    /// <summary>
    /// Force raise guard
    /// </summary>
    public void RaiseGuard()
    {
        if (isMoving || isRaised)
            return;

        isRaised = true;
        targetYPosition = originalYPosition + 5f;
        isMoving = true;
        Debug.Log($"Starting to raise guard, moving from current position {guardTransform.localPosition.y} to {targetYPosition}...");
    }
    
    /// <summary>
    /// Force lower guard
    /// </summary>
    public void LowerGuard()
    {
        if (isMoving || !isRaised)
            return;

        isRaised = false;
        targetYPosition = originalYPosition;
        isMoving = true;
        Debug.Log($"Starting to lower guard, moving from current position {guardTransform.localPosition.y} to {targetYPosition}...");
    }
}