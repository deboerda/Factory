using UnityEngine;

public class LookAtTarget : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target != null)
        {
            // 在Update之后执行，确保摄像机位置已经更新完毕
            transform.LookAt(target);
        }
    }
}