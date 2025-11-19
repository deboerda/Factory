using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Workpiece parent component class for managing all workpieces
/// </summary>
public class WorkpieceParent : MonoBehaviour
{
    // List of all workpieces
    private List<Workpiece> workpieces = new List<Workpiece>();
    
    // Workpiece object pool - for managing all reusable workpieces
    // Using dictionary storage for improved lookup performance
    private Dictionary<string, Workpiece> workpiecePool = new Dictionary<string, Workpiece>();
    
    // Workpieces stored by type, improves performance for type-based lookups
    private Dictionary<string, List<Workpiece>> workpiecesByType = new Dictionary<string, List<Workpiece>>();
    
    // Object pool cleanup interval (seconds)
    [SerializeField] private float poolCleanupInterval = 30.0f;
    
    // Last cleanup time
    private float lastCleanupTime = 0.0f;
    
    // Control whether workpieces auto-reset after hitting ground
    [SerializeField] private bool autoResetOnGround = true;
    
    // Reset delay time after workpiece collides with ground (seconds)
    [SerializeField] private float resetDelayAfterGround = 1.0f;
    
    // Detection range radius
    [SerializeField] private float detectionRadius = 0.5f;
    
    // Minimum and maximum detection range values
    [SerializeField] private float minDetectionRadius = 0.1f;
    [SerializeField] private float maxDetectionRadius = 2.0f;
    
    // Whether to show detection range in editor
    [SerializeField] private bool showDetectionRadius = true;
    
    // Detection range center position (local coordinates relative to transform)
    [SerializeField] private Vector3 detectionCenter = Vector3.zero;
    
    /// <summary>
    /// Get all child workpieces during initialization
    /// </summary>
    private void Awake()
    {
        // Initialize workpiece list and object pool
        InitializeWorkpiecePool();
    }
    
    /// <summary>
    /// Initialize workpiece object pool
    /// </summary>
    private void InitializeWorkpiecePool()
    {
        RefreshWorkpieceList();
        
        // Add all workpieces to object pool
        foreach (var workpiece in workpieces)
        {
            if (workpiece != null)
            {
                AddToPoolAndTypeDictionary(workpiece);
            }
        }
    }
    
    /// <summary>
    /// Add workpiece to object pool and type dictionary
    /// </summary>
    /// <param name="workpiece">Workpiece to add</param>
    private void AddToPoolAndTypeDictionary(Workpiece workpiece)
    {
        if (workpiece == null)
            return;
            
        string uniqueKey = GetWorkpieceUniqueKey(workpiece);
        if (!workpiecePool.ContainsKey(uniqueKey))
        {
            workpiecePool.Add(uniqueKey, workpiece);
            
            // Only output logs in debug mode
            if (Debug.isDebugBuild)
            {
                Debug.Log("[Workpiece Pool] Add workpiece to object pool: " + uniqueKey);
            }
        }
        
        // Add to type-sorted dictionary
        string typeName = workpiece.GetType().Name;
        if (!workpiecesByType.ContainsKey(typeName))
        {
            workpiecesByType[typeName] = new List<Workpiece>();
        }
        
        if (!workpiecesByType[typeName].Contains(workpiece))
        {
            workpiecesByType[typeName].Add(workpiece);
        }
    }
    
    /// <summary>
    /// Get workpiece unique identifier
    /// </summary>
    /// <param name="workpiece">Workpiece instance</param>
    /// <returns>Unique key value</returns>
    private string GetWorkpieceUniqueKey(Workpiece workpiece)
    {
        // Use workpiece name and initial position as unique identifier
        return $"{workpiece.name}_ID_{workpiece.GetInstanceID()}";
    }
    
    void OnValidate()
    {
        // Ensure detection radius is within valid range
        detectionRadius = Mathf.Clamp(detectionRadius, minDetectionRadius, maxDetectionRadius);
        
        // Ensure reset delay time is positive
        resetDelayAfterGround = Mathf.Max(0.1f, resetDelayAfterGround);
        
        // Ensure cleanup interval is positive
        poolCleanupInterval = Mathf.Max(5.0f, poolCleanupInterval);
    }
    
    void Update()
    {
        // Periodically cleanup null references in object pool
        if (Time.time - lastCleanupTime > poolCleanupInterval)
        {
            CleanupPool();
            lastCleanupTime = Time.time;
        }
    }
    
    /// <summary>
    /// Cleanup null references and invalid objects in object pool
    /// </summary>
    private void CleanupPool()
    {
        // Create list of keys to remove to avoid modifying dictionary during iteration
        List<string> keysToRemove = new List<string>();
        
        // Check and remove invalid workpiece references
        foreach (var kvp in workpiecePool)
        {
            if (kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        // Execute removal operations
        foreach (string key in keysToRemove)
        {
            workpiecePool.Remove(key);
        }
        
        // Also cleanup type-sorted lists
        foreach (var typeList in workpiecesByType.Values)
        {
            typeList.RemoveAll(item => item == null);
        }
        
        // Cleanup empty lists
        List<string> emptyTypeKeys = new List<string>();
        foreach (var kvp in workpiecesByType)
        {
            if (kvp.Value.Count == 0)
            {
                emptyTypeKeys.Add(kvp.Key);
            }
        }
        
        foreach (string key in emptyTypeKeys)
        {
            workpiecesByType.Remove(key);
        }
        
        // Cleanup main workpiece list
        workpieces.RemoveAll(item => item == null);
        
        if (keysToRemove.Count > 0 || emptyTypeKeys.Count > 0)
        {
            Debug.Log("[Workpiece Pool] Cleaned up " + keysToRemove.Count + " invalid references and " + emptyTypeKeys.Count + " empty type lists.");
        }
    }
    
    /// <summary>
    /// Display detection range in editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Display detection range in editor (for debugging only)
        if (showDetectionRadius)
        {
            // Calculate detection center in world coordinates
            Vector3 worldDetectionCenter = GetWorldDetectionCenter();
            
            // Draw detection range sphere
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawSphere(worldDetectionCenter, detectionRadius);
            
            // Draw line from object center to detection center for visualization
            Gizmos.color = new Color(1, 0.5f, 0, 0.7f);
            Gizmos.DrawLine(transform.position, worldDetectionCenter);
            
            // Draw small cube at detection center to mark center point
            Gizmos.color = Color.red;
            Gizmos.DrawCube(worldDetectionCenter, new Vector3(0.05f, 0.05f, 0.05f));
        }
    }
    
    /// <summary>
    /// Refresh workpiece list, reacquire workpiece components from all child objects
    /// </summary>
    public void RefreshWorkpieceList()
    {
        workpieces.Clear();
        
        // Use tag filtering, only get workpieces with "Workpiece" tag
        Workpiece[] allWorkpieces = GetComponentsInChildren<Workpiece>();
        foreach (var workpiece in allWorkpieces)
        {
            if (workpiece != null)
            {
                // Check if workpiece has "Workpiece" tag, skip if not
                try
                {
                    if (workpiece.CompareTag("Workpiece"))
                    {
                        workpieces.Add(workpiece);
                    }
                }
                catch (UnityEngine.UnityException)
                {
                    // If tag doesn't exist, log warning and skip the workpiece
                    Debug.LogWarning($"[Workpiece Pool] Workpiece {workpiece.name} has no Workpiece tag and will be ignored");
                }
            }
        }
        
        Debug.Log($"[Workpiece Pool] Refreshed workpiece list, found {workpieces.Count} tagged workpieces");
    }
    
    /// <summary>
    /// Find workpieces near specified position
    /// </summary>
    /// <param name="position">Search position, if not specified uses own detection center (follows object movement)</param>
    /// <param name="radiusOverride">Optional custom detection radius, uses default if not specified</param>
    /// <returns>First found workpiece, returns null if none found</returns>
    public Workpiece FindWorkpieceNearPosition(Vector3? position = null, float? radiusOverride = null)
    {
        // If position not specified, use own detection center (follows object movement)
        Vector3 searchPosition = position ?? GetWorldDetectionCenter();
        float radiusToUse = radiusOverride ?? detectionRadius;
        
        Workpiece closestWorkpiece = null;
        float closestDistance = float.MaxValue;
        
        // Refresh workpiece list to ensure we can detect all moving workpieces
        RefreshWorkpieceList();
        
        foreach (var workpiece in workpieces)
        {
            // Skip already held workpieces (dual check via tag and status)
            if (workpiece == null || workpiece.IsHeld || workpiece.CompareTag("HeldWorkpiece"))
                continue;
            
            // Calculate distance
            float distance = Vector3.Distance(searchPosition, workpiece.transform.position);
            
            // If within detection range and closest
            if (distance <= radiusToUse && distance < closestDistance)
            {
                closestDistance = distance;
                closestWorkpiece = workpiece;
            }
        }
        
        return closestWorkpiece;
    }
    
    /// <summary>
    /// Find other workpieces around a specific workpiece
    /// </summary>
    /// <param name="targetWorkpiece">Target workpiece, detection center will be set at this workpiece's position</param>
    /// <param name="radiusOverride">Optional custom detection radius, uses default if not specified</param>
    /// <returns>First found workpiece, returns null if none found</returns>
    public Workpiece FindWorkpieceNearWorkpiece(Workpiece targetWorkpiece, float? radiusOverride = null)
    {
        // If target workpiece is null, return null
        if (targetWorkpiece == null)
            return null;
        
        // Use target workpiece's position as detection center
        return FindWorkpieceNearPosition(targetWorkpiece.transform.position, radiusOverride);
    }
    
    // Note: Periodic cleanup logic has been implemented in the Update method above
    
    /// <summary>
    /// Get detection center position in world coordinates
    /// </summary>
    /// <returns>Detection center in world coordinates</returns>
    public Vector3 GetWorldDetectionCenter()
    {
        return transform.TransformPoint(detectionCenter);
    }
    
    /// <summary>
    /// Update detection range
    /// </summary>
    /// <param name="newRadius">New detection range radius</param>
    public void UpdateDetectionRadius(float newRadius)
    {
        detectionRadius = Mathf.Clamp(newRadius, minDetectionRadius, maxDetectionRadius);
    }
    
    /// <summary>
    /// Get all workpieces
    /// </summary>
    /// <returns>Copy of workpiece list</returns>
    public List<Workpiece> GetAllWorkpieces()
    {
        return new List<Workpiece>(workpieces);
    }
    
    /// <summary>
    /// Add new workpiece to management list and object pool
    /// </summary>
    /// <param name="workpiece">New workpiece</param>
    public void AddWorkpiece(Workpiece workpiece)
    {
        if (workpiece != null && !workpieces.Contains(workpiece))
        {
            workpieces.Add(workpiece);
            AddToPoolAndTypeDictionary(workpiece);
        }
    }
    
    /// <summary>
    /// Reset specified workpiece to initial position
    /// </summary>
    /// <param name="workpiece">Workpiece to reset</param>
    public void ResetWorkpiece(Workpiece workpiece)
    {
        if (workpiece != null)
        {
            Debug.Log($"[Workpiece Pool] Reset workpiece: {workpiece.name}");
            workpiece.ResetToInitialPosition();
        }
    }
    
    /// <summary>
    /// Reset all workpieces to initial position
    /// </summary>
    public void ResetAllWorkpieces()
    {
        Debug.Log("[Workpiece Pool] Reset all workpieces to initial position");
        foreach (var workpiece in workpieces)
        {
            if (workpiece != null && !workpiece.IsHeld)
            {
                workpiece.ResetToInitialPosition();
            }
        }
    }
    
    /// <summary>
    /// Get workpiece from object pool
    /// </summary>
    /// <param name="workpieceName">Workpiece name</param>
    /// <returns>Found workpiece or null</returns>
    public Workpiece GetWorkpieceFromPool(string workpieceName)
    {
        // First try to find from type-sorted lists for better performance
        foreach (var typeList in workpiecesByType.Values)
        {
            foreach (var workpiece in typeList)
            {
                if (workpiece != null && workpiece.name == workpieceName && !workpiece.IsHeld)
                {
                    return workpiece;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Get available workpiece from object pool by type
    /// </summary>
    /// <typeparam name="T">Workpiece type</typeparam>
    /// <returns>Found workpiece or null</returns>
    public T GetWorkpieceByType<T>() where T : Workpiece
    {
        string typeName = typeof(T).Name;
        if (workpiecesByType.TryGetValue(typeName, out List<Workpiece> typeList))
        {
            foreach (var workpiece in typeList)
            {
                if (workpiece != null && !workpiece.IsHeld)
                {
                    return workpiece as T;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Mark workpiece for reset
    /// <summary>
    /// <param name="workpiece">Workpiece that needs reset</param>
    public void MarkWorkpieceForReset(Workpiece workpiece)
    {
        if (workpiece != null && !workpiece.IsHeld && autoResetOnGround)
        {
            StartCoroutine(ResetWorkpieceWithDelay(workpiece, resetDelayAfterGround));
        }
    }
    
    /// <summary>
    /// Coroutine for delayed workpiece reset
    /// </summary>
    /// <param name="workpiece">Workpiece to reset</param>
    /// <param name="delay">Delay time (seconds)</param>
    private System.Collections.IEnumerator ResetWorkpieceWithDelay(Workpiece workpiece, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Check workpiece status again to ensure it still needs reset
        if (workpiece != null && !workpiece.IsHeld)
        {
            ResetWorkpiece(workpiece);
        }
    }
    
    /// <summary>
    /// Remove workpiece from management list
    /// </summary>
    /// <param name="workpiece">Workpiece to remove</param>
    public void RemoveWorkpiece(Workpiece workpiece)
    {
        if (workpiece != null && workpieces.Contains(workpiece))
        {
            workpieces.Remove(workpiece);
        }
    }
}