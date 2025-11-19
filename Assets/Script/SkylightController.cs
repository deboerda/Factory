using UnityEngine;
using MySqlConnector;
using System;
using System.Collections;
using System.Threading.Tasks;

/// <summary>
/// Skylight controller class
/// Responsible for skylight database synchronization and rotation control
/// </summary>
public class SkylightController : MonoBehaviour
{    
    [Header("Database Settings")]
    public string server = "192.168.249.50";
    public string database = "sbk001";
    public string user = "root";
    public string password = "sbkadmin";
    public int port = 3306;
    public string tableName = "unity_axis";

    [Header("Skylight Components")]
    public Transform skylight; // Skylight component reference

    [Header("Movement Parameters")]
    public float syncInterval = 0.5f; // Sync interval
    public float skylightRotateSpeed = 2.0f; // Skylight rotation speed

    [Header("Status Info (Runtime Display)")]
    public int rawSkylightOpenValue = 90; // Raw skylight open status value
    public int rawSkylightCloseValue = -90; // Raw skylight close status value
    public string lastSyncTime = "Not synced"; // Last sync time

    private float targetSkylightRotation = -90f; // Target skylight rotation angle: -90 for closed, 90 for open
    private bool isInitialized = false; // Initialization flag

    /// <summary>
    /// Initialize on start
    /// </summary>
    void Start()
    {
        InitializeSkylight();
        StartCoroutine(RealtimeSyncCoroutine());
    }

    /// <summary>
    /// Update skylight rotation each frame
    /// </summary>
    void Update()
    {
        UpdateSkylightRotation();
    }

    /// <summary>
    /// Initialize skylight status
    /// </summary>
    private void InitializeSkylight()
    {
        // Initialize skylight status to closed
        targetSkylightRotation = -90f;
        isInitialized = true;

        // If skylight component is specified, ensure initial rotation is correctly set
        if (skylight != null)
        {
            Vector3 initialRotation = skylight.localEulerAngles;
            skylight.localEulerAngles = new Vector3(-90f, initialRotation.y, initialRotation.z);
            Debug.Log("Skylight initialization complete: set to closed state");
        }
        else
        {
            Debug.LogWarning("Skylight component not specified in Inspector, please ensure skylight variable is correctly set at runtime");
        }
    }

    /// <summary>
    /// Update skylight rotation
    /// Implement smooth rotation to target angle, limited between -90 to 90 degrees
    /// </summary>
    private void UpdateSkylightRotation()
    {
        if (!isInitialized || skylight == null) return;

        // Get current skylight rotation
        Vector3 currentRotation = skylight.localEulerAngles;

        // Due to Unity's special rotation angle handling, ensure x-axis rotation is within -180 to 180 degree range
        if (currentRotation.x > 180f)
        {
            currentRotation.x -= 360f;
        }

        // Ensure target angle is within -90 to 90 degree range
        float clampedTargetRotation = Mathf.Clamp(targetSkylightRotation, -90f, 90f);

        // Smoothly rotate to target angle
        float newXRotation = Mathf.MoveTowardsAngle(currentRotation.x, clampedTargetRotation, skylightRotateSpeed * Time.deltaTime * 60f);

        // Ensure final angle is within -90 to 90 degree range again
        newXRotation = Mathf.Clamp(newXRotation, -90f, 90f);

        // Apply new rotation
        skylight.localEulerAngles = new Vector3(newXRotation, currentRotation.y, currentRotation.z);
    }

    #region Data Synchronization
    /// <summary>
    /// Real-time synchronization coroutine
    /// Periodically query skylight status from database
    /// </summary>
    private IEnumerator RealtimeSyncCoroutine()
    {
        while (true)
        {
            _ = ReadSkylightDataAsync();
            yield return new WaitForSeconds(syncInterval);
        }
    }

    /// <summary>
    /// Asynchronously read skylight data
    /// Query skylight open/close status from database
    /// </summary>
    private async Task ReadSkylightDataAsync()
    {
        string connStr = $"Server={server};Database={database};User={user};Password={password};Port={port};";
        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Query skylight open and close status
                string query = $@"
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_LDS.OP90.天窗开' ORDER BY _TIMESTAMP DESC LIMIT 1)
                    UNION ALL
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_LDS.OP90.天窗关' ORDER BY _TIMESTAMP DESC LIMIT 1);";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string name = reader.GetString("_NAME");
                        string valueStr = reader.GetString("_VALUE");

                        // Parse value
                        if (float.TryParse(valueStr, out float value))
                        {
                            // Skylight open status processing
                            if (name == "FD_QZ_RQ_06_LDS.OP90.天窗开")
                            {
                                int newOpenValue = (int)Mathf.Clamp(value, 0, 1);
                                if (newOpenValue != rawSkylightOpenValue)
                                {
                                    rawSkylightOpenValue = newOpenValue;
                                    Debug.Log($"Skylight open status updated to: {rawSkylightOpenValue}");
                                }
                                if (rawSkylightOpenValue == 1)
                                {
                                    targetSkylightRotation = 90f; // Rotate to 90 degrees when skylight opens
                                  
                                }
                            }
                            // Skylight close status processing
                            else if (name == "FD_QZ_RQ_06_LDS.OP90.天窗关")
                            {
                                int newCloseValue = (int)Mathf.Clamp(value, 0, 1);
                                if (newCloseValue != rawSkylightCloseValue)
                                {
                                    rawSkylightCloseValue = newCloseValue;
                                    Debug.Log($"Skylight close status updated to: {rawSkylightCloseValue}");
                                }
                                if (rawSkylightCloseValue == 1)
                                {
                                    targetSkylightRotation = -90f; // Rotate to -90 degrees when skylight closes
                               
                                }
                            }
                        }
                    }

                    // Safety check: output warning if skylight component is not specified
                    if (skylight == null && isInitialized)
                    {
                        Debug.LogWarning("Skylight component not specified in Inspector, please ensure skylight variable is correctly set at runtime");
                    }
                }

                lastSyncTime = DateTime.Now.ToString("HH:mm:ss.fff");
                Debug.Log($"<color=green>Skylight sync successful: skylight open={rawSkylightOpenValue}, skylight close={rawSkylightCloseValue}</color>");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"<color=red>Skylight sync failed: {ex.Message}</color>");
        }
    }
    #endregion

    #region UI Display
    /// <summary>
    /// GUI display method
    /// Display skylight status information on game interface
    /// </summary>
    void OnGUI()
    {
        // Save original GUI style and color
        Color originalColor = GUI.color;
        GUIStyle originalLabelStyle = new GUIStyle(GUI.skin.label);
        int originalFontSize = originalLabelStyle.fontSize;

        // Set brown color
        Color brownColor = new Color(165f / 255f, 42f / 255f, 42f / 255f);
        GUI.color = brownColor;

        // Dynamically calculate font size
        int dynamicFontSize = Mathf.RoundToInt(Screen.height * 0.015f);
        dynamicFontSize = Mathf.Max(dynamicFontSize, 12); // Ensure minimum text size
        originalLabelStyle.fontSize = dynamicFontSize;

        // Define active area size and position
        float areaWidth = Screen.width * 0.30f;
        float areaX = 20f; // Display on left side, distinguish from right area of original script
        float areaY = 300f;

        // Calculate text line height
        GUIContent tempContent = new GUIContent("Test text");
        Vector2 labelSize = originalLabelStyle.CalcSize(tempContent);
        float lineHeight = labelSize.y;

        // Calculate total area height: title + time + skylight status + extra buffer
        float areaHeight = (1 * lineHeight) + (1 * lineHeight) + 10f +
                           (3 * lineHeight) + (skylight != null ? lineHeight : 0) + 20f;

        // Start defining GUI area
        GUILayout.BeginArea(new Rect(areaX, areaY, areaWidth, areaHeight));

        // Draw UI elements
        GUILayout.Label("<b>Skylight Control Status</b>", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Label($"Last sync: {lastSyncTime}", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Space(10);

        // Skylight status display
        GUILayout.Label($"Skylight open status: {rawSkylightOpenValue}", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Label($"Skylight close status: {rawSkylightCloseValue}", originalLabelStyle, GUILayout.Height(lineHeight));
        

        

        // End GUI area
        GUILayout.EndArea();

        // Restore original GUI color and style
        GUI.color = originalColor;
        GUI.skin.label.fontSize = originalFontSize;
    }
    #endregion
}

    