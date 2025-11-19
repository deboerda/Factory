using UnityEngine;
using MySqlConnector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
// Random class is already included in UnityEngine namespace

public class ArmRealTimeSimulator : MonoBehaviour
{
    [Header("Database Settings")]
    public string server = "192.168.249.50";
    public string database = "sbk001";
    public string user = "root";
    public string password = "sbkadmin";
    public int port = 3306;
    public string tableName = "unity_axis";

    [Header("Robot Arm Components")]
    public Transform armA;
    public Transform armC;
    public Transform axisB;

    [Header("Gantry Arm Clamp Components")]
    [Tooltip("First pair of clamps for Z1 gantry arm")]
    public Transform z1Clamp1Front;  // Z1 clamp 1 front
    public Transform z1Clamp1Back;   // Z1 clamp 1 back
    [Tooltip("Second pair of clamps for Z1 gantry arm")]
    public Transform z1Clamp2Front;  // Z1 clamp 2 front
    public Transform z1Clamp2Back;   // Z1 clamp 2 back

    [Tooltip("First pair of clamps for Z2 gantry arm")]
    public Transform z2Clamp1Front;  // Z2 clamp 1 front
    public Transform z2Clamp1Back;   // Z2 clamp 1 back
    [Tooltip("Second pair of clamps for Z2 gantry arm")]
    public Transform z2Clamp2Front;  // Z2 clamp 2 front
    public Transform z2Clamp2Back;   // Z2 clamp 2 back
    
    #region Mounting Scheme Related
    // Workpiece mounting related variables - switched to tag-based scheme
    [Header("Workpiece Mounting Settings")]
    [Tooltip("Use tag-based scheme to manage workpieces, no longer rely on WorkpieceParent component")]
    [SerializeField] private bool useTagBasedWorkpieceManagement = true;
    
    [Tooltip("Currently held workpiece")]
    private Workpiece heldWorkpiece = null;
    
    [Tooltip("Detection range radius")]
    [SerializeField] private float detectionRadius = 0.25f;
    
    [Tooltip("Z1 clamp detection center offset (in clamp's local coordinate system)")]
    [SerializeField] private Vector3 z1DetectionCenter = Vector3.zero;
    
    [Tooltip("Z2 clamp detection center offset (in clamp's local coordinate system)")]
    [SerializeField] private Vector3 z2DetectionCenter = new Vector3(0, 0, 0.05f);
    
    [Tooltip("Whether to show detection range Gizmos")]
    [SerializeField] private bool showDetectionGizmos = true;
    
    [Tooltip("Position offset when picking up workpiece")]
    [SerializeField] private Vector3 clamp1WorkpieceOffset = new Vector3(0, 0, 0.1f);
    
    [Tooltip("Rotation offset when picking up workpiece (Euler angles)")]
    [SerializeField] private Vector3 clamp1WorkpieceRotation = Vector3.zero;
    
    [Tooltip("Position offset when Z2 picks up workpiece")]
    [SerializeField] private Vector3 clamp2WorkpieceOffset = new Vector3(0, 0, 0.1f);
    
    [Tooltip("Rotation offset when Z2 picks up workpiece (Euler angles)")]
    [SerializeField] private Vector3 clamp2WorkpieceRotation = Vector3.zero;
    
    [Tooltip("Layer limit for detecting workpieces")]
    [SerializeField] private int maxRaycastLayers = 1;
    
    // Previous frame clamp states, used to detect state changes
    private int lastZ1State = 0;
    private int lastZ2State = 0;
    
    // Record which clamp currently holds the workpiece
    private Transform currentHoldingClamp = null;
    
    // Tag-based scheme no longer needs WorkpieceParent cache mechanism
    // Last detected workpiece list (deprecated, kept for compatibility)
    private List<Workpiece> nearbyWorkpiecesCache = new List<Workpiece>();
    private float lastCacheUpdateTime = 0f;
    private float cacheUpdateInterval = 0.1f; // Cache update interval (seconds) - deprecated
    #endregion

    [Header("Motion Parameters")]
    public float syncInterval = 0.5f;
    public float smoothFactor = 0.5f;
    public float clampRotateAngle = 10f; // Clamp rotation angle
    
    #region Tag-based Scheme Interaction Methods
    /// <summary>
    /// Check if the given clamp is the current grabbing clamp of the held workpiece
    /// </summary>
    /// <param name="clampTransform">The clamp to check</param>
    /// <returns>Returns true if it is the current grabbing clamp</returns>
    private bool IsHoldingWithClamp(Transform clampTransform)
    {
        return heldWorkpiece != null && currentHoldingClamp == clampTransform;
    }
    
    /// <summary>
    /// Get the workpiece offset settings corresponding to the clamp
    /// </summary>
    /// <param name="clampTransform">The Transform component of the clamp</param>
    /// <param name="positionOffset">Output position offset</param>
    /// <param name="rotationOffset">Output rotation offset</param>
    private void GetClampWorkpieceOffsets(Transform clampTransform, out Vector3 positionOffset, out Vector3 rotationOffset)
    {
        // Determine which clamp it is
        if (clampTransform == z1Clamp1Front || clampTransform == z1Clamp1Back || 
            clampTransform == z1Clamp2Front || clampTransform == z1Clamp2Back)
        {
            positionOffset = clamp1WorkpieceOffset;
            rotationOffset = clamp1WorkpieceRotation;
        }
        else if (clampTransform == z2Clamp1Front || clampTransform == z2Clamp1Back || 
                 clampTransform == z2Clamp2Front || clampTransform == z2Clamp2Back)
        {
            positionOffset = clamp2WorkpieceOffset;
            rotationOffset = clamp2WorkpieceRotation;
        }
        else
        {
            // Default offset
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Pick up workpiece using tag-based scheme - implements the core logic for workpiece to follow clamp movement
    /// </summary>
    /// <param name="clampTransform">The clamp performing the pickup</param>
    /// <returns>Whether the workpiece was successfully picked up</returns>
    private bool PickupWorkpiece(Transform clampTransform)
    {
        Debug.Log($"[Tag-based Scheme] Attempting to pick up workpiece - Clamp: {clampTransform?.name}");
        
        // Check if already holding a workpiece
        if (heldWorkpiece != null)
        {
            Debug.Log("[Tag-based Scheme] Already holding a workpiece, cannot pick up again");
            return false;
        }
        
        // Check clamp validity
        if (clampTransform == null)
        {
            Debug.LogWarning("[Tag-based Scheme] Invalid clamp reference, cannot pick up workpiece");
            return false;
        }
        
        // Find workpiece near clamp position (using tag-based scheme)
        Workpiece targetWorkpiece = FindWorkpieceNearClamp(clampTransform);
        
        if (targetWorkpiece != null && !targetWorkpiece.IsHeld)
        {
            // Get corresponding offset settings
            Vector3 positionOffset, rotationOffset;
            GetClampWorkpieceOffsets(clampTransform, out positionOffset, out rotationOffset);
            
            // Update workpiece offset settings
            targetWorkpiece.UpdateClampOffset(positionOffset);
            targetWorkpiece.UpdateClampRotationOffset(rotationOffset);
            
            // Set to held state
            heldWorkpiece = targetWorkpiece;
            currentHoldingClamp = clampTransform;
            
            // Call workpiece's picked up method to implement mounting and following
            targetWorkpiece.OnPickedUp(clampTransform);
            
            Debug.Log($"[Tag-based Scheme] Successfully picked up workpiece: {targetWorkpiece.name}, using clamp: {clampTransform.name}" +
                      $" | Position offset: {positionOffset} | Rotation offset: {rotationOffset}");
            return true;
        }
        else if (targetWorkpiece != null && targetWorkpiece.IsHeld)
        {
            Debug.LogWarning($"[Tag-based Scheme] Workpiece {targetWorkpiece.name} is already held, cannot pick up again");
            return false;
        }
        else
        {
            Debug.Log("[Tag-based Scheme] No workpiece found for pickup");
        }
        
        return false;
    }
    
    /// <summary>
    /// Find pickup-able workpiece near clamp - tag-based scheme using Unity tag system
    /// </summary>
    /// <param name="clampTransform">The Transform component of the clamp</param>
    /// <returns>The first pickup-able workpiece found, returns null if none</returns>
    private Workpiece FindWorkpieceNearClamp(Transform clampTransform)
    {
        if (clampTransform == null)
            return null;
            
        // Get the detection center corresponding to the clamp
        Vector3 detectionCenter = GetClampDetectionCenter(clampTransform);
        
        // Use tag-based scheme to find workpieces - find all GameObjects with "Workpiece" tag
        GameObject[] workpieceObjects = new GameObject[0];
        GameObject[] heldWorkpieceObjects = new GameObject[0];
        
        try
        {
            workpieceObjects = GameObject.FindGameObjectsWithTag("Workpiece");
        }
        catch (UnityEngine.UnityException)
        {
            Debug.LogWarning("[Tag-based Scheme] 'Workpiece' tag is not defined, please add this tag in Tags & Layers");
        }
        
        try
        {
            heldWorkpieceObjects = GameObject.FindGameObjectsWithTag("HeldWorkpiece");
        }
        catch (UnityEngine.UnityException)
        {
            Debug.LogWarning("[Tag-based Scheme] 'HeldWorkpiece' tag is not defined, please add this tag in Tags & Layers");
        }
        
        // Record tag search results
        Debug.Log($"[Tag-based Scheme] Found {workpieceObjects.Length} Workpiece tagged objects, {heldWorkpieceObjects.Length} HeldWorkpiece tagged objects");
        Workpiece nearestWorkpiece = null;
        float nearestDistance = float.MaxValue;
        
        foreach (GameObject obj in workpieceObjects)
        {
            if (obj == null) continue;
            
            // Get Workpiece component
            Workpiece workpiece = obj.GetComponent<Workpiece>();
            
            if (workpiece == null)
            {
                Debug.LogWarning($"[Tag-based Scheme] Object {obj.name} has Workpiece tag but no Workpiece component");
                continue;
            }
            
            // Check if workpiece is valid and not held
            if (!workpiece.IsHeld && workpiece.gameObject.activeInHierarchy)
            {
                // Calculate distance
                float distance = Vector3.Distance(detectionCenter, workpiece.transform.position);
                
                // Check if within detection range and closer than previously found
                if (distance <= detectionRadius && distance < nearestDistance)
                {
                    nearestWorkpiece = workpiece;
                    nearestDistance = distance;
                    Debug.Log($"[Tag-based Scheme] Found candidate workpiece {obj.name}, distance: {distance:F2}");
                }
            }
        }
        
        // Also check workpieces with HeldWorkpiece tag (error tolerance handling)
        foreach (GameObject obj in heldWorkpieceObjects)
        {
            if (obj == null) continue;
            
            // Get Workpiece component
            Workpiece workpiece = obj.GetComponent<Workpiece>();
            
            if (workpiece == null)
            {
                Debug.LogWarning($"[Tag-based Scheme] Object {obj.name} has HeldWorkpiece tag but no Workpiece component");
                continue;
            }
            
            // Check if workpiece is valid and not held (theoretically workpieces with HeldWorkpiece tag should not be pickup-able)
            if (!workpiece.IsHeld && workpiece.gameObject.activeInHierarchy)
            {
                // Calculate distance
                float distance = Vector3.Distance(detectionCenter, workpiece.transform.position);
                
                // Check if within detection range and closer than previously found
                if (distance <= detectionRadius && distance < nearestDistance)
                {
                    nearestWorkpiece = workpiece;
                    nearestDistance = distance;
                    Debug.LogWarning($"[Tag-based Scheme] Found workpiece tagged as HeldWorkpiece but with unheld status: {workpiece.name}, distance: {distance:F2}");
                }
            }
        }
        
        // Additional raycast detection to ensure workpiece is directly in front of detection center
        if (nearestWorkpiece != null)
        {
            Vector3 direction = nearestWorkpiece.transform.position - detectionCenter;
            RaycastHit hit;
            if (Physics.Raycast(detectionCenter, direction.normalized, out hit, detectionRadius * 2, -1, QueryTriggerInteraction.Ignore))
            {
                Workpiece hitWorkpiece = hit.collider.GetComponentInParent<Workpiece>();
                if (hitWorkpiece == nearestWorkpiece)
                {
                    Debug.Log($"[Tag-based Scheme] Raycast confirmed workpiece: {nearestWorkpiece.name} is directly in front of detection center");
                    return nearestWorkpiece;
                }
            }
            else
            {
                // If raycast detection fails but distance is very close, still allow pickup (optimized for Arm C clamp)
                if (nearestDistance < detectionRadius * 0.5f)
                {
                    Debug.LogWarning($"[Tag-based Scheme] Raycast detection failed but distance is very close ({nearestDistance:F3}m), allowing pickup of workpiece: {nearestWorkpiece.name}");
                    return nearestWorkpiece;
                }
            }
        }
        
        if (nearestWorkpiece != null)
        {
            Debug.Log($"[Tag-based Scheme] Found pickup-able workpiece: {nearestWorkpiece.name}, distance from clamp: {nearestDistance:F3}m");
        }
        else
        {
            Debug.Log($"[Tag-based Scheme] No pickup-able workpiece found within {detectionRadius}m range near clamp. Please ensure:\n1. Workpiece has Workpiece component added\n2. Workpiece tag is set to 'Workpiece'\n3. Workpiece is near clamp (default within 0.5m range)");
        }
        
        return nearestWorkpiece;
    }
    
    /// <summary>
    /// 放下工件 - 标签方案，优化自由落体效果
    /// </summary>
    private bool DropWorkpiece()
    {
        if (heldWorkpiece == null) // 如果没有持有工件，则不进行放下操作
        {
            Debug.Log("[标签方案] 没有持有工件，无法放下");
            return false;
        }
            
        // 记录放下前的工件信息
        string droppedWorkpieceName = heldWorkpiece.name;
        Transform previousClamp = currentHoldingClamp;
        
        // 获取放下时的位置和速度信息（用于增强自由落体效果）
        Vector3 dropPosition = heldWorkpiece.transform.position;
        Vector3 clampVelocity = Vector3.zero;
        
        // 尝试获取夹子的刚体组件以计算速度
        if (previousClamp != null)
        {
            Rigidbody clampRb = previousClamp.GetComponent<Rigidbody>();
            if (clampRb != null)
            {
                clampVelocity = clampRb.velocity;
            }
        }

        // 在放下工件前，确保标签已正确设置
        try
        {
            if (heldWorkpiece.CompareTag("HeldWorkpiece"))
            {
                // 尝试设置标签，如果标签不存在则忽略
                try
                {
                    GameObject tempObj = new GameObject("temp");
                    tempObj.tag = "Workpiece";
                    DestroyImmediate(tempObj);
                    
                    heldWorkpiece.tag = "Workpiece";
                    Debug.Log($"[标签方案-自由落体] 工件 {heldWorkpiece.name} 标签已恢复为 Workpiece");
                }
                catch (UnityEngine.UnityException)
                {
                    Debug.LogWarning($"[标签方案-自由落体] 'Workpiece' 标签未定义，无法恢复标签，但放下功能正常");
                }
            }
        }
        catch (UnityEngine.UnityException)
        {
            Debug.LogWarning($"[标签方案-自由落体] 标签操作失败，但放下功能继续执行");
        }
        
        // 调用工件的放下方法，实现自由落体
        heldWorkpiece.OnDropped();
        
        // 额外的调试信息
        Debug.Log($"[标签方案-自由落体] 放下了工件: {droppedWorkpieceName} | 之前的夹子: {previousClamp?.name}");
        Debug.Log($"[标签方案-自由落体] 放下位置: {dropPosition} | 初始速度: {clampVelocity}");
        
        // 清空持有状态
        heldWorkpiece = null;
        currentHoldingClamp = null;
        
        return true;
    }
    
    /// <summary>
    /// Drop workpiece if the specified clamp is holding it
    /// </summary>
    /// <param name="clampTransform">The Transform component of the clamp</param>
    private void DropWorkpieceIfHeldByClamp(Transform clampTransform)
    {
        if (heldWorkpiece != null && currentHoldingClamp == clampTransform)
        {
            DropWorkpiece();
        }
    }
    
    /// <summary>
    /// Draw detection range Gizmos in editor
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showDetectionGizmos)
            return;
            
        // Draw Z1 clamp detection range and detection center
        if (z1Clamp1Front != null)
        {
            // Get Z1 detection center world coordinates
            Vector3 z1Center = z1Clamp1Front.TransformPoint(z1DetectionCenter);
            
            // Draw line from clamp to detection center
            Gizmos.color = Color.white;
            Gizmos.DrawLine(z1Clamp1Front.position, z1Center);
            
            // Draw detection center marker
            Gizmos.color = Color.cyan;
            Gizmos.DrawCube(z1Center, new Vector3(0.03f, 0.03f, 0.03f));
            
            // Draw detection range sphere
            Gizmos.color = new Color(0, 0.8f, 1, 0.3f); // Semi-transparent blue
            Gizmos.DrawSphere(z1Center, detectionRadius);
            
            // Draw line from clamp to offset position
            Vector3 offsetPos = z1Clamp1Front.TransformPoint(clamp1WorkpieceOffset);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(z1Clamp1Front.position, offsetPos);
            Gizmos.DrawSphere(offsetPos, 0.02f); // Draw offset point
        }
        
        // Draw Z2 clamp detection range and detection center
        if (z2Clamp1Front != null)
        {
            // Get Z2 detection center world coordinates
            Vector3 z2Center = z2Clamp1Front.TransformPoint(z2DetectionCenter);
            
            // Draw line from clamp to detection center
            Gizmos.color = Color.white;
            Gizmos.DrawLine(z2Clamp1Front.position, z2Center);
            
            // Draw detection center marker
            Gizmos.color = Color.yellow;
            Gizmos.DrawCube(z2Center, new Vector3(0.03f, 0.03f, 0.03f));
            
            // Draw detection range sphere
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f); // Semi-transparent orange
            Gizmos.DrawSphere(z2Center, detectionRadius);
            
            // Draw line from clamp to offset position
            Vector3 offsetPos = z2Clamp1Front.TransformPoint(clamp2WorkpieceOffset);
            Gizmos.color = new Color(1, 0.6f, 0);
            Gizmos.DrawLine(z2Clamp1Front.position, offsetPos);
            Gizmos.DrawSphere(offsetPos, 0.02f); // Draw offset point
        }
    }
    
    /// <summary>
    /// Get the detection center position corresponding to the clamp (world coordinates)
    /// </summary>
    /// <param name="clampTransform">The Transform component of the clamp</param>
    /// <returns>Detection center world coordinates</returns>
    private Vector3 GetClampDetectionCenter(Transform clampTransform)
    {
        if (clampTransform == null)
            return Vector3.zero;
            
        // Determine which clamp and return corresponding detection center
        if (clampTransform == z1Clamp1Front || clampTransform == z1Clamp1Back || 
            clampTransform == z1Clamp2Front || clampTransform == z1Clamp2Back)
        {
            // Use z1DetectionCenter for Z1 clamps
            return clampTransform.TransformPoint(z1DetectionCenter);
        }
        else if (clampTransform == z2Clamp1Front || clampTransform == z2Clamp1Back || 
                 clampTransform == z2Clamp2Front || clampTransform == z2Clamp2Back)
        {
            // Use z2DetectionCenter for Z2 clamps
            return clampTransform.TransformPoint(z2DetectionCenter);
        }
        else
        {
            // Default to clamp position
            return clampTransform.position;
        }
    }
    
    /// <summary>
    /// 如果指定的夹子持有工件，则放下它
    /// </summary>
    /// <param name="clampTransform">要检查的夹子</param>
    /// <returns>是否成功放下工件</returns>
    private bool DropWorkpieceIfHeldBy(Transform clampTransform)
    {
        if (IsHoldingWithClamp(clampTransform))
        {
            return DropWorkpiece();
        }
        return false;
    }

    [Header("状态信息(运行时显示)")]
    public float rawAValue = 0;
    public float rawBValue = 0;
    public float rawCValue = 0;
    public float rawZ1Value = 0;
    public float rawZ2Value = 0;
    public string lastSyncTime = "未同步";

    private float targetAY;
    private float targetCY;
    private float targetBZ;
    private int targetZ1State = 0; // 0:闭合, 1:打开
    private int targetZ2State = 0;
    
    void Start()
    {
        InitializePositions();
        InitializeClamps();
        StartCoroutine(RealtimeSyncCoroutine());
    }

    void Update()
    {
        UpdateArmPositions();
        UpdateClampRotation(z1Clamp1Front, z1Clamp1Back, targetZ1State);
        UpdateClampRotation(z1Clamp2Front, z2Clamp2Back, targetZ1State);
        UpdateClampRotation(z2Clamp1Front, z2Clamp1Back, targetZ2State);
        UpdateClampRotation(z2Clamp2Front, z2Clamp2Back, targetZ2State);
        
        // 检测夹子状态变化并控制挂载互动
        HandleClampStateChanges();
    }
    
    /// <summary>
    /// 处理夹子状态变化，控制工件的挂载夹起和放下
    /// </summary>
    private void HandleClampStateChanges()
    {
        // 记录当前持有状态
        Debug.Log($"[挂载方案] 当前状态 - 持有工件: {(heldWorkpiece != null ? heldWorkpiece.name : "无")}, 持有夹子: {(currentHoldingClamp != null ? currentHoldingClamp.name : "无")}");
        
        // Z1夹子状态变化检测
        if (targetZ1State != lastZ1State)
        {
            Debug.Log($"[挂载方案] Z1夹子状态变化: {lastZ1State} → {targetZ1State}");
            
            // 如果从打开变为闭合（1→0），尝试夹起工件
            if (lastZ1State == 1 && targetZ1State == 0)
            {
                Debug.Log("[挂载方案] Z1夹子闭合，尝试夹起工件");
                // 尝试用Z1夹子夹起工件（使用前夹作为主要参考）
                PickupWorkpiece(z1Clamp1Front);
            }
            // 如果从闭合变为打开（0→1），如果持有工件则放下
            else if (lastZ1State == 0 && targetZ1State == 1)
            {
                Debug.Log("[挂载方案] Z1夹子打开，检查是否需要放下工件");
                // 如果是Z1夹子持有的工件，则放下
                DropWorkpieceIfHeldBy(z1Clamp1Front);
            }
            lastZ1State = targetZ1State;
        }
        
        // Z2夹子状态变化检测
        if (targetZ2State != lastZ2State)
        {
            Debug.Log($"[挂载方案] Z2夹子状态变化: {lastZ2State} → {targetZ2State}");
            
            // 如果从打开变为闭合（1→0），尝试夹起工件
            if (lastZ2State == 1 && targetZ2State == 0)
            {
                Debug.Log("[挂载方案] Z2夹子闭合，尝试夹起工件");
                // 尝试用Z2夹子夹起工件（使用前夹作为主要参考）
                PickupWorkpiece(z2Clamp1Front);
                PickupWorkpiece(z2Clamp2Front);
            }
            // 如果从闭合变为打开（0→1），如果持有工件则放下
            else if (lastZ2State == 0 && targetZ2State == 1)
            {
                Debug.Log("[挂载方案] Z2夹子打开，检查是否需要放下工件");
                // 如果是Z2夹子持有的工件，则放下
            DropWorkpieceIfHeldBy(z2Clamp1Front);
            DropWorkpieceIfHeldBy(z2Clamp2Front);
            }
            lastZ2State = targetZ2State;
        }
    }
    
    // Remove skylight rotation update method
    private void InitializePositions()
    {
        // Initialize positions: two robot arms as a whole, move along Z-axis via axisB
        if (armA != null) armA.localPosition = new Vector3(0, 0, 0);
        if (armC != null) armC.localPosition = new Vector3(0, 0, -3);
        if (axisB != null) axisB.localPosition = new Vector3(0, 0, 0); // B-axis initial position
        
        targetAY = 0;
        targetCY = 0;
        targetBZ = 0;
    }

    private void InitializeClamps()
    {
        // Initialize to closed state (according to new rotation logic: front +10, back -10)
        UpdateClampRotation(z1Clamp1Front, z1Clamp1Back, 0);
        UpdateClampRotation(z1Clamp2Front, z1Clamp2Back, 0);
        UpdateClampRotation(z2Clamp1Front, z2Clamp1Back, 0);
        UpdateClampRotation(z2Clamp2Front, z2Clamp2Back, 0);
    }


    #endregion

    #region Initialization and Movement Control
    private void UpdateArmPositions()
    {
        if (armA != null)
        {
            Vector3 aPos = armA.localPosition;
            aPos.y = Mathf.Lerp(aPos.y, targetAY, smoothFactor * Time.deltaTime * 20);
            armA.localPosition = aPos;
        }

        if (armC != null)
        {
            Vector3 cPos = armC.localPosition;
            cPos.y = Mathf.Lerp(cPos.y, targetCY, smoothFactor * Time.deltaTime * 20);
            armC.localPosition = cPos;
        }

        if (axisB != null)
        {
            Vector3 bPos = axisB.localPosition;
            bPos.z = Mathf.Lerp(bPos.z, targetBZ, smoothFactor * Time.deltaTime * 20);
            axisB.localPosition = bPos;
        }
    }

    private void UpdateClampRotation(Transform front, Transform back, int state)
    {
        if (front == null || back == null) return;

        // State 1: open (front -10, back +10); State 0: closed (front +10, back -10)
        float targetFrontZ = state == 1 ? -clampRotateAngle : clampRotateAngle;
        float targetBackZ = state == 1 ? clampRotateAngle : -clampRotateAngle;

        front.localRotation = Quaternion.Lerp(
            front.localRotation,
            Quaternion.Euler(0, 0, targetFrontZ),
            smoothFactor * Time.deltaTime * 40
        );

        back.localRotation = Quaternion.Lerp(
            back.localRotation,
            Quaternion.Euler(0, 0, targetBackZ),
            smoothFactor * Time.deltaTime * 40
        );
    }
    #endregion

    #region Data Synchronization (Core Fix: Z1/Z2 Identification)
    private IEnumerator RealtimeSyncCoroutine()
    {
        while (true)
        {
            _ = ReadLatestDataAsync();
            yield return new WaitForSeconds(syncInterval);
        }
    }

    private async Task ReadLatestDataAsync()
    {
        string connStr = $"Server={server};Database={database};User={user};Password={password};Port={port};";
        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                await conn.OpenAsync();

                // Update to new database field names
                string query = $@"
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.A' ORDER BY _TIMESTAMP DESC LIMIT 1)
                    UNION ALL
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.B' ORDER BY _TIMESTAMP DESC LIMIT 1)
                    UNION ALL
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.C' ORDER BY _TIMESTAMP DESC LIMIT 1)
                    UNION ALL
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.Z1_open' ORDER BY _TIMESTAMP DESC LIMIT 1)
                    UNION ALL
                    (SELECT _NAME, _VALUE FROM {tableName} WHERE _NAME = 'FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.Z2_open' ORDER BY _TIMESTAMP DESC LIMIT 1);";

                using (var cmd = new MySqlCommand(query, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string name = reader.GetString("_NAME");
                        string valueStr = reader.GetString("_VALUE");

                        // Parse numeric values (compatible with integers and decimals)
                        if (float.TryParse(valueStr, out float value))
                        {
                            // Strictly match axis names
                            if (name == "FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.A")
                            {
                                rawAValue = value;
                                // Fix mapping: when real is 0, game is 0; when real is negative extreme, game is negative extreme
                                targetAY = ConvertRange(rawAValue, 0, -1518, 0, -6);
                            }
                            else if (name == "FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.B")
                            {
                                rawBValue = value;
                                // Update to new B-axis range: real -20816 to -247, game -30 to 0
                                // Use original value mapping according to new range
                                targetBZ = ConvertRange(rawBValue, -20816f, -247f, -30f, 0f);
                            }
                            else if (name == "FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.C")
                            {
                                rawCValue = value;
                                // Fix mapping: when real is 0, game is 0; when real is negative extreme, game is negative extreme
                                targetCY = ConvertRange(rawCValue, 0, -1544, 0, -6);
                            }
                            // Z1 status
                            else if (name == "FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.Z1_open")
                            {
                                rawZ1Value = value;
                                
                                targetZ1State = (int)Mathf.Clamp(value, 0, 1);
                                Debug.Log($"Z1 status updated to: {targetZ1State} (raw value: {rawZ1Value})");
                            }
                            // Z2 status
                            else if (name == "FD_QZ_RQ_06_FANUC.兰迪斯机械手1703.Z2_open")
                            {
                                rawZ2Value = value;
                                targetZ2State = (int)Mathf.Clamp(value, 0, 1); // Limit to 0 or 1
                                Debug.Log($"Z2 status updated to: {targetZ2State} (raw value: {rawZ2Value})");
                            }
                        }
                    }
                }

                lastSyncTime = DateTime.Now.ToString("HH:mm:ss.fff");
                Debug.Log($"<color=green>Landis Robot Arm sync successful: A={rawAValue}, B={rawBValue}, C={rawCValue}, Z1={rawZ1Value}, Z2={rawZ2Value}</color>");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"<color=red>Sync failed: {ex.Message}</color>");
        }
    }

    // Coordinate Range Transformation - Fix mapping relationship
    private float ConvertRange(float value, float inMin, float inMax, float outMin, float outMax)
    {
        // Ensure proper handling when input range minimum is greater than maximum
        float actualInMin = Mathf.Min(inMin, inMax);
        float actualInMax = Mathf.Max(inMin, inMax);
        float actualOutMin = Mathf.Min(outMin, outMax);
        float actualOutMax = Mathf.Max(outMin, outMax);
        
        // Clamp value within input range
        value = Mathf.Clamp(value, actualInMin, actualInMax);
        
        // Calculate mapped value
        float normalizedValue = (value - actualInMin) / (actualInMax - actualInMin);
        
        // Determine output direction based on input range order
        if (inMin > inMax) // If input range is reversed (e.g. 0 to -1518)
        {
            normalizedValue = 1 - normalizedValue;
        }
        
        // Adjust if output range is also reversed
        if (outMin > outMax)
        {
            normalizedValue = 1 - normalizedValue;
        }
        
        return normalizedValue * (actualOutMax - actualOutMin) + actualOutMin;
    }
    #endregion

    #region UI Display
    void OnGUI()
    {
        // 1.Preserve the original GUI style and colors  
        Color originalColor = GUI.color;
        GUIStyle originalLabelStyle = new GUIStyle(GUI.skin.label);
        int originalFontSize = originalLabelStyle.fontSize;

        // 2. Set Color is brown
        Color brownColor = new Color(165f / 255f, 42f / 255f, 42f / 255f);
        GUI.color = brownColor;

        // 3. Dynamically calculate font size
        int dynamicFontSize = Mathf.RoundToInt(Screen.height * 0.015f);
        dynamicFontSize = Mathf.Max(dynamicFontSize, 12); // ensure mix text scale
        originalLabelStyle.fontSize = dynamicFontSize;

        // 4. define active region scale and location 
        float areaWidth = Screen.width * 0.30f;
        float areaX = Screen.width - areaWidth;
        float areaY = 20f;

        // Recalculate area height, remove skylight part
        GUIContent tempContent = new GUIContent("Test text"); // Sample text for height calculation
        Vector2 labelSize = originalLabelStyle.CalcSize(tempContent);
        float lineHeight = labelSize.y; // Get actual single line text height

        // Calculate total height: title + time + spacing + 3 axes + spacing + 2 states + extra buffer
        float areaHeight = (1 * lineHeight) + (1 * lineHeight) + 10f +
                           (3 * lineHeight) + 10f +
                           (2 * (lineHeight * 1.2f)) + 20f; // 20f as final buffer

        // 5. Begin defining a GUI area
        GUILayout.BeginArea(new Rect(areaX, areaY, areaWidth, areaHeight));

        // --- Draw UI elements --- Landis Robot Arm status display
        GUILayout.Label("<b>Landis Robot Arm Sync Status</b>", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Label($"Last sync: {lastSyncTime}", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Space(10);

        // Display raw values and converted in-game coordinates
        GUILayout.Label($"A-axis: {rawAValue:F1} → Y={targetAY:F2}", originalLabelStyle, GUILayout.Height(lineHeight));
        // Display B-axis mapping info (use raw value directly)
        GUILayout.Label($"B-axis: {rawBValue:F1} → Z={targetBZ:F2}", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Label($"C-axis: {rawCValue:F1} → Y={targetCY:F2}", originalLabelStyle, GUILayout.Height(lineHeight));
        GUILayout.Space(10);

        // State style
        GUIStyle stateStyle = new GUIStyle(originalLabelStyle);
        stateStyle.fontSize = Mathf.RoundToInt(dynamicFontSize * 1.1f);
        stateStyle.fontStyle = FontStyle.Bold;
        float stateLineHeight = lineHeight * 1.2f; // State lines are taller

        // Display gripper status
        GUI.color = (targetZ1State == 1) ? Color.green : Color.red;
        GUILayout.Label($"A-gripper: {rawZ1Value:F0} → {(targetZ1State == 1 ? "Open" : "Closed")}", stateStyle, GUILayout.Height(stateLineHeight));

        GUI.color = (targetZ2State == 1) ? Color.green : Color.red;
        GUILayout.Label($"C-gripper: {rawZ2Value:F0} → {(targetZ2State == 1 ? "Open" : "Closed")}", stateStyle, GUILayout.Height(stateLineHeight));

        // --- Drawing end ---

        GUILayout.EndArea();

        // 6. Restore original GUI colors and styles
        GUI.color = originalColor;
        GUI.skin.label.fontSize = originalFontSize;
    }


    #endregion
}