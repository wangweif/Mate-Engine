using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VRM;
using UniGLTF;
using UniVRM10;
using System;
using System.Collections;
using System.Reflection;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class VRMLoader : MonoBehaviour
{
    public GameObject mainModel;
    public GameObject customModelOutput;
    public RuntimeAnimatorController animatorController;
    public GameObject componentTemplatePrefab;

    private GameObject currentModel;
    private RuntimeGltfInstance currentGltf;
    [SerializeField] private string modelName = "";
    private const string SelectedModelPrefsKey = "MATE_ENGINE_SELECTED_VRM";    

    void Start()
    {
        var savedModel = PlayerPrefs.GetString(SelectedModelPrefsKey, "");
        if (!string.IsNullOrEmpty(savedModel))
        {
            modelName = savedModel;
        }
        else
        {
            modelName = "xiaozhi.vrm";
        }
        string modelsPath;
        if (Application.dataPath.Contains("/Assets"))
        {
            modelsPath = Path.Combine(Application.dataPath, "StreamingAssets/Models");
        }
        else
        {
            modelsPath = Path.Combine(Application.streamingAssetsPath,"Models");
        }

        if (Directory.Exists(modelsPath))
        {
            // 递归查找所有子目录中的 .vrm
            string[] files = Directory.GetFiles(modelsPath, "*.vrm", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if(Path.GetFileName(file) == modelName)
                {
                    LoadDefaultModelOnly(file);
                    return;
                }
            }
        }

        // 如果默认模型不存在，使用内置默认模型
        ActivateDefaultModel();
    }
    // 已移除随机头像加载功能
    // private void TryLoadRandomAvatar() - 功能已禁用


    // 已移除文件对话框加载功能 - 仅支持默认模型
    // public void OpenFileDialogAndLoadVRM() - 功能已禁用

    /// <summary>
    /// 仅用于加载默认模型的简化方法
    /// </summary>
    private async void LoadDefaultModelOnly(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            byte[] fileData = await Task.Run(() => File.ReadAllBytes(path));
            if (fileData == null || fileData.Length == 0) return;

            GameObject loadedModel = null;

            try
            {
                var glbData = new GlbFileParser(path).Parse();
                var vrm10Data = Vrm10Data.Parse(glbData);
                if (vrm10Data != null)
                {
                    using var importer10 = new Vrm10Importer(vrm10Data);
                    var instance10 = await importer10.LoadAsync(new ImmediateCaller());
                    if (instance10.Root != null)
                    {
                        loadedModel = instance10.Root;
                        currentGltf = instance10;
                        loadedModel.AddComponent<GltfInstanceDisposer>().Bind(instance10);
                    }
                }
            }
            catch { }

            if (loadedModel == null)
            {
                try
                {
                    using var gltfData = new GlbBinaryParser(fileData, path).Parse();
                    VRMImporterContext importer = null;
                    try
                    {
                        importer = new VRMImporterContext(new VRMData(gltfData));
                        var instance = await importer.LoadAsync(new ImmediateCaller());
                        if (instance.Root != null)
                        {
                            loadedModel = instance.Root;
                            currentGltf = instance;
                            loadedModel.AddComponent<GltfInstanceDisposer>().Bind(instance);
                        }
                    }
                    finally
                    {
                        importer?.Dispose();
                    }
                }
                catch { return; }
            }

            if (loadedModel == null) return;

            FinalizeDefaultModel(loadedModel);
        }
        catch (Exception ex)
        {
            Debug.LogError("[VRMLoader] Failed to load default model: " + ex.Message);
            // 如果默认模型加载失败，使用内置默认模型
            ActivateDefaultModel();
        }
    }

    /// <summary>
    /// 已禁用 - 只允许加载默认模型
    /// </summary>
    public async void LoadVRM(string path)
    {
        Debug.LogWarning("[VRMLoader] LoadVRM is disabled - only default model loading is supported");
        // 不执行任何加载操作，只保留方法签名以避免编译错误
        return;
    }

    // 已移除AssetBundle加载功能 - 只支持默认模型
    // private void LoadAssetBundleModel() - 功能已禁用

    /// <summary>
    /// 专门用于处理默认模型的最终设置
    /// </summary>
    private void FinalizeDefaultModel(GameObject loadedModel)
    {
        DisableMainModel();
        ClearPreviousCustomModel();

        loadedModel.transform.SetParent(customModelOutput.transform, false);
        loadedModel.transform.localPosition = Vector3.zero;
        loadedModel.transform.localRotation = Quaternion.identity;
        loadedModel.transform.localScale = Vector3.one;
        currentModel = loadedModel;

        EnableSkinnedMeshRenderers(currentModel);
        AssignAnimatorController(currentModel);
        InjectComponentsFromPrefab(componentTemplatePrefab, currentModel);

        // 禁用鼠标追踪动画
        DisableMouseTrackingAnimation(currentModel);

        var changer = FindFirstObjectByType<MEValueChanger>();
        if (changer != null)
            changer.SendMessage("TryAttachCustomVRM", SendMessageOptions.DontRequireReceiver);

        if (MEModLoader.Instance != null)
            MEModLoader.Instance.AssignHandlersForCurrentAvatar(loadedModel);

        StartCoroutine(ReleaseRamAndUnloadAssetsCo());
        SettingsHandlerUtility.ReloadAllSettingsHandlers();
    }

    private void FinalizeLoadedModel(GameObject loadedModel, string path)
    {
        DisableMainModel();
        ClearPreviousCustomModel();

        loadedModel.transform.SetParent(customModelOutput.transform, false);
        loadedModel.transform.localPosition = Vector3.zero;
        loadedModel.transform.localRotation = Quaternion.identity;
        loadedModel.transform.localScale = Vector3.one;
        currentModel = loadedModel;

        EnableSkinnedMeshRenderers(currentModel);
        AssignAnimatorController(currentModel);
        InjectComponentsFromPrefab(componentTemplatePrefab, currentModel);

        // 禁用鼠标追踪动画
        DisableMouseTrackingAnimation(currentModel);

        var changer = FindFirstObjectByType<MEValueChanger>();
        if (changer != null)
            changer.SendMessage("TryAttachCustomVRM", SendMessageOptions.DontRequireReceiver);

        string displayName = Path.GetFileNameWithoutExtension(path);
        string author = "Unknown";
        string version = "Unknown";
        string fileType = "Unknown";
        Texture2D thumbnail = null;
        bool isME = path.EndsWith(".me", StringComparison.OrdinalIgnoreCase);

        var vrm10Instance = loadedModel.GetComponent<UniVRM10.Vrm10Instance>();
        if (vrm10Instance != null && vrm10Instance.Vrm != null && vrm10Instance.Vrm.Meta != null)
        {
            displayName = vrm10Instance.Vrm.Meta.Name ?? displayName;
            author = (vrm10Instance.Vrm.Meta.Authors != null && vrm10Instance.Vrm.Meta.Authors.Count > 0) ? vrm10Instance.Vrm.Meta.Authors[0] : "Unknown";
            version = vrm10Instance.Vrm.Meta.Version ?? "Unknown";
            fileType = isME ? ".ME (VRM1.X)" : "VRM1.X";
            thumbnail = vrm10Instance.Vrm.Meta.Thumbnail;
        }
        else
        {
            var vrmMeta = loadedModel.GetComponent<VRM.VRMMeta>();
            if (vrmMeta != null && vrmMeta.Meta != null)
            {
                var meta = vrmMeta.Meta;
                displayName = !string.IsNullOrEmpty(meta.Title) ? meta.Title : displayName;
                author = !string.IsNullOrEmpty(meta.Author) ? meta.Author : "Unknown";
                version = !string.IsNullOrEmpty(meta.Version) ? meta.Version : "Unknown";
                fileType = isME ? ".ME (VRM0.X)" : "VRM0.X";
                thumbnail = meta.Thumbnail;
            }
        }

        // 只为默认模型加载，不需要保存到库或刷新库UI
        // 已移除库保存和刷新功能

        StartCoroutine(DelayedRefreshStats());

        if (MEModLoader.Instance != null)
            MEModLoader.Instance.AssignHandlersForCurrentAvatar(loadedModel);

        StartCoroutine(ReleaseRamAndUnloadAssetsCo());
        SettingsHandlerUtility.ReloadAllSettingsHandlers();
    }

    public Texture2D MakeReadableCopy(Texture texture)
    {
        if (texture == null) return null;
        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0);
        Graphics.Blit(texture, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return readable;
    }

    /// <summary>
    /// 重置到默认模型
    /// </summary>
    public void ResetModel()
    {
        ClearPreviousCustomModel(skipRawImageCleanup: true);
        EnableMainModel();

        if (MEModLoader.Instance != null && mainModel != null)
            MEModLoader.Instance.AssignHandlersForCurrentAvatar(mainModel);

        StartCoroutine(ReleaseRamAndUnloadAssetsCo());
        Debug.Log("[VRMLoader] Reset to default model");
    }

    private void DisableMainModel()
    {
        if (mainModel != null)
            mainModel.SetActive(false);
    }

    private void EnableMainModel()
    {
        if (mainModel != null)
            mainModel.SetActive(true);
    }

    private void ClearPreviousCustomModel(bool skipRawImageCleanup = false)
    {
        if (customModelOutput != null)
        {
            foreach (Transform child in customModelOutput.transform)
            {
                if (child.gameObject == mainModel) continue;
                CleanupRawImages(child.gameObject);
                Destroy(child.gameObject);
            }
        }

        currentGltf = null;

        if (!skipRawImageCleanup)
            CleanupAllRawImagesInScene();
    }

    private void EnableSkinnedMeshRenderers(GameObject model)
    {
        foreach (var skinnedMesh in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skinnedMesh.enabled = true;
    }

    private void AssignAnimatorController(GameObject model)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator != null && animatorController != null)
            animator.runtimeAnimatorController = animatorController;
    }

    /// <summary>
    /// 禁用鼠标追踪动画、手部交互、待机动画、舞蹈功能和IK系统
    /// </summary>
    private void DisableMouseTrackingAnimation(GameObject model)
    {
        if (model == null) return;

        var animator = model.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // 设置Animator参数为Idle状态
            SetAnimatorToIdlePose(animator);

            // 启用Animator
            animator.enabled = true;

            // 立即强制播放Idle状态，并从动画开始位置播放（避免入场动画）
            animator.Play("Idle", 0, 0f);

            // 设置动画速度为0，冻结Idle动画
            animator.speed = 0f;

            // 使用协程持续保持在Idle状态
            StartCoroutine(KeepInIdleState(animator));
        }

        // 禁用所有交互和动画控制相关的组件
        var allComponents = model.GetComponentsInChildren<MonoBehaviour>(true);
        int disabledCount = 0;

        foreach (var comp in allComponents)
        {
            if (comp == null) continue;

            string typeName = comp.GetType().Name;

            // 禁用鼠标追踪、手部交互、动画控制、舞蹈和IK相关的组件
            if (typeName.Contains("MouseTracking") ||
                typeName.Contains("Mouse") ||
                typeName.Contains("HandHolder") ||
                typeName.Contains("Hand") ||
                typeName.Contains("AnimatorController") ||
                typeName.Contains("Dance") ||
                typeName.Contains("IK"))
            {
                comp.enabled = false;
                disabledCount++;
            }
        }

        if (disabledCount > 0)
        {
            Debug.Log($"[VRMLoader] Total interaction and control components disabled: {disabledCount}");
        }
        else
        {
        }
    }

    /// <summary>
    /// 持续保持在Idle状态
    /// </summary>
    private IEnumerator KeepInIdleState(Animator animator)
    {
        // 持续几帧确保在Idle状态
        for (int i = 0; i < 10; i++)
        {
            if (animator != null)
            {
                // 每帧都强制保持在Idle状态
                SetAnimatorToIdlePose(animator);

                // 检查当前状态，如果不是Idle就强制切换
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!stateInfo.IsName("Idle"))
                {
                    animator.Play("Idle", 0, 0f);
                }
            }
            yield return null;
        }

    }

    /// <summary>
    /// 设置Animator参数让角色保持站立姿势
    /// </summary>
    private void SetAnimatorToIdlePose(Animator animator)
    {
        if (animator == null) return;

        // 设置所有动画状态为false，让角色进入Idle状态
        animator.SetBool("isDancing", false);
        animator.SetBool("isDragging", false);
        animator.SetBool("isBigScreen", false);
        animator.SetBool("isBigScreenAlarm", false);
        animator.SetBool("isBigScreenSaver", false);
        animator.SetBool("isWindowSit", false);
        animator.SetBool("isSitting", false);

        // 设置IdleIndex为0，确保使用第一个站立姿势
        animator.SetFloat("IdleIndex", 0f);
        animator.SetFloat("DanceIndex", 0f);

        // 设置性别（可选）
        animator.SetFloat("isMale", 1f);
        animator.SetFloat("isFemale", 0f);

    }

    private void InjectComponentsFromPrefab(GameObject prefabTemplate, GameObject targetModel)
    {
        if (prefabTemplate == null || targetModel == null) return;

        var templateObj = Instantiate(prefabTemplate);
        var animator = targetModel.GetComponentInChildren<Animator>();

        foreach (var templateComp in templateObj.GetComponents<MonoBehaviour>())
        {
            var type = templateComp.GetType();
            if (targetModel.GetComponent(type) != null) continue;
            var newComp = targetModel.AddComponent(type);
            CopyComponentValues(templateComp, newComp);

            if (animator != null)
            {
                var setAnimMethod = type.GetMethod("SetAnimator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setAnimMethod != null) setAnimMethod.Invoke(newComp, new object[] { animator });

                var animatorField = type.GetField("animator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (animatorField != null && animatorField.FieldType == typeof(Animator)) animatorField.SetValue(newComp, animator);
            }
        }
        Destroy(templateObj);
    }

    private void CopyComponentValues(Component source, Component destination)
    {
        var type = source.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.IsDefined(typeof(SerializeField), true) || field.IsPublic)
                field.SetValue(destination, field.GetValue(source));
        }
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(p => p.CanWrite && p.GetSetMethod(true) != null);
        foreach (var prop in props)
        {
            try { prop.SetValue(destination, prop.GetValue(source)); }
            catch { }
        }
    }

    private System.Collections.IEnumerator DelayedRefreshStats()
    {
        yield return null;
        var stats = FindFirstObjectByType<RuntimeModelStats>();
        if (stats != null)
            stats.RefreshNow();
    }

    public int GetTotalPolygons(GameObject model)
    {
        int total = 0;
        foreach (var meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = meshFilter.sharedMesh;
            if (mesh != null)
                total += mesh.triangles.Length / 3;
        }
        foreach (var skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = skinned.sharedMesh;
            if (mesh != null)
                total += mesh.triangles.Length / 3;
        }
        return total;
    }

    public void ActivateDefaultModel()
    {
        ClearPreviousCustomModel(skipRawImageCleanup: true);
        EnableMainModel();

        if (MEModLoader.Instance != null && mainModel != null)
            MEModLoader.Instance.AssignHandlersForCurrentAvatar(mainModel);

        StartCoroutine(ReleaseRamAndUnloadAssetsCo());
        SettingsHandlerUtility.ReloadAllSettingsHandlers();
        Debug.Log("[VRMLoader] Activated default model");
    }

    private System.Collections.IEnumerator ReleaseRamAndUnloadAssetsCo()
    {
        yield return Resources.UnloadUnusedAssets();
        yield return null;
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }

    private void CleanupRawImages(GameObject obj)
    {
        if (obj == null) return;
        var rawImages = obj.GetComponentsInChildren<RawImage>(true);
        foreach (var rawImage in rawImages)
            rawImage.texture = null;
    }

    private void CleanupAllRawImagesInScene()
    {
        var rawImages = GameObject.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var rawImage in rawImages)
            rawImage.texture = null;
    }

    // 已移除DLC相关方法，因为只支持默认模型加载
    // private bool IsDLCReference() - 功能已禁用
    // private GameObject FindDLCByName() - 功能已禁用

    public GameObject GetCurrentModel()
    {
        return currentModel;
    }

    /// <summary>
    /// 切换到指定的VRM模型
    /// </summary>
    public void SwitchModel(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
        {
            Debug.LogError("[VRMLoader] Model name cannot be empty");
            return;
        }

        PlayerPrefs.SetString(SelectedModelPrefsKey, Path.GetFileName(modelPath));
        PlayerPrefs.Save();

        if (File.Exists(modelPath))
        {
            LoadDefaultModelOnly(modelPath);
        }
        else
        {
            Debug.LogError($"[VRMLoader] Model file not found: {modelPath}");
        }
    }
}

public sealed class GltfInstanceDisposer : MonoBehaviour
{
    private UniGLTF.RuntimeGltfInstance inst;

    public void Bind(UniGLTF.RuntimeGltfInstance i)
    {
        inst = i;
    }

    private void OnDestroy()
    {
        try { inst?.Dispose(); } catch { }
    }
}
