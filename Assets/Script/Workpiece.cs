using UnityEngine;

/// <summary>
/// 工件基类，所有可被夹取的工件都应继承此类
/// </summary>
public class Workpiece : MonoBehaviour
{
    // 夹取状态
    [Header("Grabbing Status")]
    [Tooltip("Currently being held or not")]
    public bool IsHeld = false;
    
    [Tooltip("Transform of the clamp currently holding this workpiece")]
    public Transform CurrentClamp = null;
    
    // 原始父物体
    private Transform originalParent;
    
    // 原始世界位置和旋转（用于夹取后放下的恢复）
    private Vector3 originalWorldPosition;
    private Quaternion originalWorldRotation;
    
    // Position/rotation recording
    [Header("Position Recording")]
    [Tooltip("Record initial position, rotation and parent at game start")]
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Transform initialParent;
    
    // 是否已经记录了初始游戏位置
    private bool hasRecordedInitialPosition = false;
    
    // 刚体组件
    private Rigidbody rb;
    
    // Grabbing offset
    [Header("Grabbing Offset")]
    [Tooltip("Position offset when grabbed")]
    public Vector3 clampOffset = Vector3.zero;
    
    [Tooltip("Rotation offset when grabbed")]
    public Vector3 clampRotationOffset = Vector3.zero;
    
    // 被释放时是否添加初始速度
    [SerializeField] private bool addReleaseVelocity = true;
    
    // 被释放时添加的额外力
    [SerializeField] private Vector3 additionalReleaseForce = Vector3.up * 0.5f;
    
    // 是否在编辑器中显示夹取偏移的可视化
    [SerializeField] private bool showClampOffsetVisualization = true;
    
    // Ground detection
    [Header("Ground Detection")]
    [Tooltip("Ground tag for detecting if workpiece has landed")]
    public string groundTag = "Ground";
    
    [Tooltip("Velocity threshold for determining if workpiece is stationary")]
    public float velocityThresholdForReset = 0.1f;
    
    // 是否已触地
    private bool hasTouchedGround = false;
    
    // 是否已标记为需要重置
    private bool markedForReset = false;
    
    void Awake()
    {
        // 获取刚体组件
        SetupRigidbody();
    }
    
    void Start()
    {
        // Record initial position and rotation at game start
        SaveInitialState();
    }
    
    /// <summary>
    /// Save initial state (position, rotation, parent)
    /// </summary>
    private void SaveInitialState()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialParent = transform.parent;
        Debug.Log($"Saved initial state for {gameObject.name}");
    }
    
    /// <summary>
    /// Reset to initial position
    /// </summary>
    public void ResetToInitialPosition()
    {
        // Cancel previous reset calls
        CancelInvoke("ResetToInitialPosition");
        
        // Don't reset if still being held
        if (IsHeld)
            return;
            
        // Reset position, rotation and parent
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.SetParent(initialParent);
        
        // Reset physics state
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        Debug.Log($"Workpiece {gameObject.name} has been reset to initial position");
    }
    
    /// <summary>
    /// 工件被重置时的事件回调（可在子类中重写）
    /// </summary>
    protected virtual void OnWorkpieceReset()
    {
        // 重置触地和重置标记
        hasTouchedGround = false;
        markedForReset = false;
        
        // 子类可以重写此方法来添加额外的逻辑
    }
    
    /// <summary>
    /// 当工件与其他物体碰撞时调用
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // 检测是否与地面碰撞
        if (!hasTouchedGround && IsGroundObject(collision.gameObject))
        {
            hasTouchedGround = true;
            Debug.Log($"[工件] {name} 触地: {collision.gameObject.name}");
            
            // 如果触地时已经接近静止，直接标记为重置
            if (rb != null && rb.velocity.magnitude < velocityThresholdForReset)
            {
                MarkForReset();
            }
        }
    }
    
    /// <summary>
    /// 判断一个物体是否为地面
    /// </summary>
    /// <param name="gameObject">要判断的游戏对象</param>
    /// <returns>是否为地面</returns>
    private bool IsGroundObject(GameObject gameObject)
    {
        // 方法1：通过标签判断（优先）
        if (!string.IsNullOrEmpty(groundTag))
        {
            try
            {
                // 尝试使用CompareTag，如果标签不存在不会报错
                return gameObject.CompareTag(groundTag);
            }
            catch (UnityEngine.UnityException)
            {
                Debug.LogWarning($"[工件] 标签 '{groundTag}' 未定义，请在Tags & Layers中添加该标签或修改Inspector中的设置");
                // 继续使用备选方法
            }
        }
        
        // 方法2：备选检测方式 - 检测静态碰撞体（常见的地面特性）
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null && !collider.isTrigger)
        {
            // 检查是否是静态物体（不移动的地面）或有特定名称的对象
            Rigidbody rb = gameObject.GetComponent<Rigidbody>();
            bool isStatic = rb == null || rb.isKinematic;
            
            // 名称中包含Ground、Floor等关键词也可视为地面
            string objName = gameObject.name.ToLower();
            bool hasGroundName = objName.Contains("ground") || 
                                objName.Contains("floor") || 
                                objName.Contains("platform") || 
                                objName.Contains("table");
            
            return isStatic || hasGroundName;
        }
        
        return false;
    }
    
    /// <summary>
    /// 每帧更新，检测工件是否静止并需要重置
    /// </summary>
    private void Update()
    {
        // 如果已触地但尚未标记为重置，检查速度是否低于阈值
        if (hasTouchedGround && !markedForReset && !IsHeld)
        {
            if (rb != null && rb.velocity.magnitude < velocityThresholdForReset)
            {
                MarkForReset();
            }
        }
    }
    
    /// <summary>
    /// 标记工件需要重置
    /// </summary>
    private void MarkForReset()
    {
        if (!markedForReset && !IsHeld)
        {
            markedForReset = true;
            Debug.Log($"[工件] {name} 准备重置");
            
            // 获取WorkpieceParent组件并调用重置方法
            WorkpieceParent parentComponent = FindObjectOfType<WorkpieceParent>();
            if (parentComponent != null)
            {
                parentComponent.MarkWorkpieceForReset(this);
            }
            else
            {
                // 如果没有找到WorkpieceParent，直接重置
                Debug.LogWarning($"[工件] {name} 未找到WorkpieceParent组件，直接重置");
                ResetToInitialPosition();
            }
        }
    }
    
    /// <summary>
    /// 设置刚体组件
    /// </summary>
    private void SetupRigidbody()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[工件] {name} 没有刚体组件，将自动添加");
            rb = gameObject.AddComponent<Rigidbody>();
            
            // 设置合理的默认物理属性
            rb.mass = 1.0f;
            rb.drag = 0.1f;
            rb.angularDrag = 0.5f;
            rb.useGravity = true;
        }
    }
    
    /// <summary>
    /// 当工件被夹取时调用
    /// </summary>
    /// <param name="clampTransform">夹子的Transform组件</param>
    public virtual void OnPickedUp(Transform clampTransform)
    {
        if (IsHeld || clampTransform == null)
        {
            Debug.LogWarning($"[工件] {name} 夹取失败：已被夹取或夹子无效");
            return;
        }
        
        // 保存原始父物体和世界位置旋转
        originalParent = transform.parent;
        originalWorldPosition = transform.position;
        originalWorldRotation = transform.rotation;
        
        // 记录当前夹子
        CurrentClamp = clampTransform;
        
        // 成为夹子的子物体
        transform.SetParent(clampTransform);
        
        // 调整位置和旋转，使工件位于夹子合适的位置
        transform.localPosition = clampOffset;
        transform.localRotation = Quaternion.Euler(clampRotationOffset);
        
        // 禁用物理效果
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        IsHeld = true;
        Debug.Log($"[工件] {name} 被夹取，父物体: {clampTransform.name}");
        
        // 调用事件
        OnWorkpiecePickedUp();
    }
    
    /// <summary>
    /// 当工件被放下时调用
    /// </summary>
    public virtual void OnDropped()
    {
        if (!IsHeld)
        {
            Debug.LogWarning($"[工件] {name} 放下失败：未被夹取");
            return;
        }
        
        // 保存当前世界位置，用于恢复
        Vector3 dropPosition = transform.position;
        Quaternion dropRotation = transform.rotation;
        
        // 获取释放时的速度（如果需要）
        Vector3 releaseVelocity = Vector3.zero;
        if (addReleaseVelocity && CurrentClamp != null)
        {
            // 尝试获取夹子的刚体组件来计算速度
            Rigidbody clampRb = CurrentClamp.GetComponent<Rigidbody>();
            if (clampRb != null)
            {
                releaseVelocity = clampRb.velocity;
            }
        }
        
        // 恢复父物体关系
        if (originalParent != null)
        {
            // 如果原始父物体还是工件父组件，保持层级关系
            if (originalParent.GetComponentInParent<WorkpieceParent>() != null)
            {
                transform.SetParent(originalParent);
            }
            else
            {
                transform.SetParent(null);
            }
        }
        else
        {
            transform.SetParent(null);
        }
        
        // 设置为释放时的世界位置和旋转
        transform.position = dropPosition;
        transform.rotation = dropRotation;
        
        // 启用物理效果，使工件自由落体
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            
            // 应用释放速度和额外力
            if (releaseVelocity != Vector3.zero)
            {
                rb.velocity = releaseVelocity;
            }
            
            if (additionalReleaseForce != Vector3.zero)
            {
                rb.AddForce(additionalReleaseForce, ForceMode.Impulse);
            }
        }
        
        // 重置状态
        IsHeld = false;
        CurrentClamp = null;
        
        Debug.Log($"[工件] {name} 被放下");
        
        // 调用事件
        OnWorkpieceDropped();
    }
    
    /// <summary>
    /// 工件被夹取时的事件回调（可在子类中重写）
    /// </summary>
    protected virtual void OnWorkpiecePickedUp()
    {
        // 将工件的标签更改为"HeldWorkpiece"
        try
        {
            if (CompareTag("Workpiece"))
            {
                // 先检查目标标签是否存在
                try
                {
                    // 尝试临时设置一个对象来验证标签是否存在
                    GameObject tempObj = new GameObject("temp");
                    tempObj.tag = "HeldWorkpiece";
                    DestroyImmediate(tempObj);
                    
                    // 如果成功，正式设置标签
                    tag = "HeldWorkpiece";
                    Debug.Log($"[工件] {name} 标签已更改为 HeldWorkpiece");
                }
                catch (UnityEngine.UnityException)
                {
                    Debug.LogWarning($"[工件] 'HeldWorkpiece' 标签未定义，无法更改标签，但夹取功能正常");
                }
            }
        }
        catch (UnityEngine.UnityException)
        {
            // 如果标签操作失败，记录警告但不影响夹取功能
            Debug.LogWarning($"[工件] 标签更改失败，请确保 'HeldWorkpiece' 标签已定义");
        }
        
        // 子类可以重写此方法来添加额外的逻辑
    }
    
    /// <summary>
    /// 工件被放下时的事件回调（可在子类中重写）
    /// </summary>
    protected virtual void OnWorkpieceDropped()
    {
        // 恢复工件的标签为"Workpiece"
        try
        {
            if (CompareTag("HeldWorkpiece"))
            {
                // 先检查目标标签是否存在
                try
                {
                    // 尝试临时设置一个对象来验证标签是否存在
                    GameObject tempObj = new GameObject("temp");
                    tempObj.tag = "Workpiece";
                    DestroyImmediate(tempObj);
                    
                    // 如果成功，正式设置标签
                    tag = "Workpiece";
                    Debug.Log($"[工件] {name} 标签已恢复为 Workpiece");
                }
                catch (UnityEngine.UnityException)
                {
                    Debug.LogWarning($"[工件] 'Workpiece' 标签未定义，无法恢复标签，但放下功能正常");
                }
            }
        }
        catch (UnityEngine.UnityException)
        {
            // 如果标签操作失败，记录警告但不影响放下功能
            Debug.LogWarning($"[工件] 标签恢复失败，请确保 'Workpiece' 标签已定义");
        }
        
        // 子类可以重写此方法来添加额外的逻辑
    }
    
    /// <summary>
    /// 更新工件相对于夹子的位置偏移
    /// </summary>
    /// <param name="newOffset">新的位置偏移量</param>
    public void UpdateClampOffset(Vector3 newOffset)
    {
        clampOffset = newOffset;
        
        // 如果当前被夹取，立即应用新的偏移
        if (IsHeld && CurrentClamp != null)
        {
            transform.localPosition = clampOffset;
        }
    }
    
    /// <summary>
    /// 更新工件相对于夹子的旋转偏移
    /// </summary>
    /// <param name="newRotationOffset">新的旋转偏移量（欧拉角）</param>
    public void UpdateClampRotationOffset(Vector3 newRotationOffset)
    {
        clampRotationOffset = newRotationOffset;
        
        // 如果当前被夹取，立即应用新的旋转偏移
        if (IsHeld && CurrentClamp != null)
        {
            transform.localRotation = Quaternion.Euler(clampRotationOffset);
        }
    }
    
    /// <summary>
    /// 在编辑器中可视化显示夹取偏移
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showClampOffsetVisualization)
            return;
        
        // 绘制位置偏移可视化
        // 从物体中心到偏移位置的连线
        Gizmos.color = new Color(0, 0, 1, 0.7f); // 蓝色表示位置偏移
        Vector3 offsetPosition = transform.position + transform.TransformDirection(clampOffset);
        Gizmos.DrawLine(transform.position, offsetPosition);
        
        // 在偏移位置绘制一个小立方体
        Gizmos.color = Color.blue;
        Gizmos.DrawCube(offsetPosition, new Vector3(0.05f, 0.05f, 0.05f));
        
        // 绘制旋转偏移的可视化（使用坐标轴表示）
        // 计算旋转后的方向
        Quaternion rotationOffset = Quaternion.Euler(clampRotationOffset);
        
        // 保存当前变换
        Matrix4x4 originalMatrix = Gizmos.matrix;
        
        // 设置新的矩阵，将坐标轴放在偏移位置并应用旋转
        Gizmos.matrix = Matrix4x4.TRS(offsetPosition, transform.rotation * rotationOffset, Vector3.one);
        
        // 绘制坐标轴
        float axisLength = 0.15f;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, new Vector3(axisLength, 0, 0));
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, new Vector3(0, axisLength, 0));
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, new Vector3(0, 0, axisLength));
        
        // 恢复原始矩阵
        Gizmos.matrix = originalMatrix;
    }
}