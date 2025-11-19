using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 场景加载器，支持按钮调用和进度显示
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Header("加载界面组件")]
    [Tooltip("加载时显示的面板（包含进度条）")]
    public GameObject loadingPanel;
    [Tooltip("显示加载进度的滑条")]
    public Slider progressBar;
    [Tooltip("显示百分比的文本")]
    public Text progressText;

    private void Awake()
    {
        // 初始化加载状态，默认隐藏
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 通过场景名称加载场景（按钮直接调用）
    /// </summary>
    /// <param name="sceneName">场景名称，必须在Build Settings中添加</param>
    public void LoadSceneByName(string sceneName)
    {
        // 检查场景是否有效
        if (IsSceneValid(sceneName))
        {
            StartCoroutine(LoadSceneAsync(sceneName));
        }
        else
        {
            Debug.LogError($"场景 {sceneName} 不存在或未添加到Build Settings中");
        }
    }

    /// <summary>
    /// 通过场景索引加载场景（按钮直接调用）
    /// </summary>
    /// <param name="sceneIndex">场景索引，对应Build Settings中的顺序</param>
    public void LoadSceneByIndex(int sceneIndex)
    {
        // 检查索引是否有效
        if (IsIndexValid(sceneIndex))
        {
            StartCoroutine(LoadSceneAsync(sceneIndex));
        }
        else
        {
            Debug.LogError($"场景索引 {sceneIndex} 无效");
        }
    }

    /// <summary>
    /// 异步加载场景并显示进度
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        // 显示加载界面
        ShowLoadingPanel(true);

        // 异步加载场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false; // 先不激活场景，等进度到100%

        // 更新进度条
        while (!asyncLoad.isDone)
        {
            // 进度值范围是0~1，注意：场景在90%时会暂停，需要手动激活
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateProgress(progress);

            // 当进度达到100%时，激活场景
            if (progress >= 1.0f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        // 隐藏加载界面控件（可选，因为新场景会覆盖）
        ShowLoadingPanel(false);
    }

    /// <summary>
    /// 异步加载场景通过索引并显示进度
    /// </summary>
    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        ShowLoadingPanel(true);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            UpdateProgress(progress);

            if (progress >= 1.0f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }

        ShowLoadingPanel(false);
    }

    /// <summary>
    /// 更新加载进度显示
    /// </summary>
    private void UpdateProgress(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = progress;
        }

        if (progressText != null)
        {
            // 显示百分比，注意四舍五入到整数
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }
    }

    /// <summary>
    /// 显示/隐藏加载界面控件
    /// </summary>
    private void ShowLoadingPanel(bool isShow)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(isShow);
        }
    }

    /// <summary>
    /// 检查场景名称是否有效
    /// </summary>
    private bool IsSceneValid(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查场景索引是否有效
    /// </summary>
    private bool IsIndexValid(int index)
    {
        return index >= 0 && index < SceneManager.sceneCountInBuildSettings;
    }

    /// <summary>
    /// 退出游戏（PC端有效）
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("退出游戏");
        Application.Quit();

        // 在编辑器中停止播放
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}