using UnityEngine;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(LookAtTarget))]
public class OrbitCameraSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class OrbitView
    {
        public string viewName = "视角";
        public float radius = 10.0f;
        public float azimuth = 0.0f;
        public float elevation = 30.0f;
    }

    // 简化后的JSON数据模型
    [System.Serializable]
    private class ViewChangeData
    {
        public int viewIndex;       // 仅保留视角索引
        public string viewName;     // 仅保留视角名称
    }

    [Header("轨道目标点")]
    public Transform orbitPivot;

    [Header("视角预设")]
    public OrbitView[] views = new OrbitView[9];

    [Header("过渡参数")]
    [Range(0.1f, 10f)] public float transitionSpeed = 5.0f;

    private int currentViewIndex = -1;
    private float currentRadius, targetRadius;
    private float currentAzimuth, targetAzimuth;
    private float currentElevation, targetElevation;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void PostViewInfo(string viewInfo);
#endif

    private void Awake()
    {
        if (orbitPivot == null)
        {
            GameObject pivotObj = GameObject.Find("OrbitPivot");
            if (pivotObj != null)
            {
                orbitPivot = pivotObj.transform;
                Debug.Log($"自动绑定轨道中心点: {pivotObj.name}");
            }
            else
            {
                Debug.LogWarning("未设置轨道中心点，请在Inspector面板指定Transform");
            }
        }
    }

    private void Start()
    {
        if (orbitPivot != null)
        {
            GetComponent<LookAtTarget>().target = orbitPivot;
        }

        if (views.Length > 0 && orbitPivot != null)
        {
            SetTargetView(8);
        }
    }

    private void Update()
    {
        if (currentViewIndex != -1 && orbitPivot != null)
        {
            currentRadius = Mathf.Lerp(currentRadius, targetRadius, transitionSpeed * Time.deltaTime);
            currentAzimuth = Mathf.LerpAngle(currentAzimuth, targetAzimuth, transitionSpeed * Time.deltaTime);
            currentElevation = Mathf.LerpAngle(currentElevation, targetElevation, transitionSpeed * Time.deltaTime);
            UpdateCameraPosition();
        }
    }

    private void UpdateCameraPosition()
    {
        if (orbitPivot == null) return;

        float radElevation = currentElevation * Mathf.Deg2Rad;
        float radAzimuth = currentAzimuth * Mathf.Deg2Rad;

        Vector3 offset;
        offset.x = currentRadius * Mathf.Sin(radElevation) * Mathf.Sin(radAzimuth);
        offset.y = currentRadius * Mathf.Cos(radElevation);
        offset.z = currentRadius * Mathf.Sin(radElevation) * Mathf.Cos(radAzimuth);

        transform.position = orbitPivot.position + offset;
    }

    public void SetTargetView(int index)
    {
        if (index < 0 || index >= views.Length)
        {
            Debug.LogWarning($"无效视角索引: {index}（范围应为 0 ~ {views.Length - 1}）");
            return;
        }
        
        currentViewIndex = index;
        targetRadius = views[index].radius;
        targetAzimuth = views[index].azimuth;
        targetElevation = views[index].elevation;

        if (currentRadius == 0)
        {
            currentRadius = targetRadius;
            currentAzimuth = targetAzimuth;
            currentElevation = targetElevation;
        }

        // 仅序列化需要的字段
        ViewChangeData data = new ViewChangeData
        {
            viewIndex = currentViewIndex + 1,
            viewName = views[index].viewName
        };
        string viewInfo = JsonUtility.ToJson(data);

        // 控制台输出简化后的信息
        Debug.Log($"视角切换信息: {viewInfo}");

#if UNITY_WEBGL && !UNITY_EDITOR
        PostViewInfo(viewInfo);
#endif
    }

    public void CaptureCurrentViewToIndex(int index)
    {
        if (orbitPivot == null)
        {
            Debug.LogError("保存视角失败：未设置轨道中心点");
            return;
        }
        if (index < 0 || index >= views.Length)
        {
            Debug.LogError($"保存视角失败：无效索引 {index}");
            return;
        }

        Vector3 dir = transform.position - orbitPivot.position;
        views[index].radius = dir.magnitude;
        views[index].azimuth = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        views[index].elevation = Mathf.Atan2(Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z), dir.y) * Mathf.Rad2Deg;

        Debug.Log($"已保存当前视角到索引 {index}（{views[index].viewName}）");
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void SwitchToView1() => SetTargetView(0);
    public void SwitchToView2() => SetTargetView(1);
    public void SwitchToView3() => SetTargetView(2);
    public void SwitchToView4() => SetTargetView(3);
    public void SwitchToView5() => SetTargetView(4);
    public void SwitchToView6() => SetTargetView(5);
    public void SwitchToView7() => SetTargetView(6);
    public void SwitchToView8() => SetTargetView(7);
    public void SwitchToView9() => SetTargetView(8);
}

#if UNITY_EDITOR
[CustomEditor(typeof(OrbitCameraSwitcher))]
public class OrbitCameraSwitcherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        OrbitCameraSwitcher switcher = (OrbitCameraSwitcher)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);

        for (int i = 0; i < switcher.views.Length; i++)
        {
            if (GUILayout.Button($"保存当前视角到「{switcher.views[i].viewName}」（索引 {i}）"))
            {
                switcher.CaptureCurrentViewToIndex(i);
            }
        }
    }
}
#endif