using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using TMPro;
using UnityEngine.UI;
using System.IO;
using Newtonsoft.Json;

public class CommitManager : MonoBehaviour
{
    public PPTInfo PPTInfo;
    public string text = "";
    [SerializeField]
    private InputField inputField;
    //[SerializeField]
    //private TextMeshProUGUI filePathObj;

    [SerializeField]
    private DropdownManager dropdown;
    public string filePath = "";
    public string fileName = "";
    public string jsonConfigPath = "";
    public PPTInfo pptInfo;

    // Windows API 导入
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // 窗口显示命令
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    public void Start()
    {
        fileName = dropdown.GetCurrentOptionText();
        jsonConfigPath = Path.ChangeExtension(fileName,".json");
        string jsonFilePath = FindJsonFilePath();
        string jsonContent = File.ReadAllText(jsonFilePath);
        pptInfo = JsonUtility.FromJson<PPTInfo>(jsonContent);
        filePath = pptInfo.file_path;
        // 将desc数组用换行符连接并放入InputField
        if (pptInfo.desc != null && pptInfo.desc.Length > 0)
        {
            // 使用 Environment.NewLine 作为换行符（跨平台兼容）
            string descText = string.Join(Environment.NewLine, pptInfo.desc);
            inputField.text = descText;
        }
        else
        {
            inputField.text = ""; // 如果没有desc内容，清空输入框
        }
        FindAndSetupInputField();
        EnsureUnityWindowOnTop(); // 启动时确保Unity窗口在前台
    }

    private void FindAndSetupInputField()
    {
        GameObject inputObj = GameObject.Find("InputField (Legacy)");

        if (inputObj != null)
        {
            Debug.Log("找到 InputField (Legacy) 对象: " + inputObj.name);

            inputField = inputObj.GetComponent<InputField>();

            if (inputField != null)
            {
                Debug.Log("成功获取 InputField 组件");
            }
            else
            {
                Debug.LogError("在 InputField (Legacy) 对象上找不到 InputField 组件！");

                // 检查对象上有什么组件
                Component[] components = inputObj.GetComponents<Component>();
                foreach (Component comp in components)
                {
                    Debug.Log("找到组件: " + comp.GetType().Name);
                }
            }
        }
        else
        {
            Debug.LogError("找不到名为 'InputField (Legacy)' 的游戏对象！");

            // 列出场景中所有对象，帮助调试
            Debug.Log("场景中的对象列表:");
            foreach (GameObject obj in FindObjectsOfType<GameObject>())
            {
                if (obj.name.Contains("Input"))
                {
                    Debug.Log("找到可能相关的对象: " + obj.name);
                }
            }
        }
    }

    public void Update()
    {
        if(fileName != dropdown.GetCurrentOptionText())
        {
            fileName = dropdown.GetCurrentOptionText();
            try
            {
                jsonConfigPath = Path.ChangeExtension(fileName, ".json");
                string jsonFilePath = FindJsonFilePath();
                string jsonContent = File.ReadAllText(jsonFilePath);
                pptInfo = JsonUtility.FromJson<PPTInfo>(jsonContent);
                filePath = pptInfo.file_path;
                // 将desc数组用换行符连接并放入InputField
                if (pptInfo.desc != null && pptInfo.desc.Length > 0)
                {
                    // 使用 Environment.NewLine 作为换行符（跨平台兼容）
                    string descText = string.Join(Environment.NewLine, pptInfo.desc);
                    inputField.text = descText;
                }
                else
                {
                    inputField.text = ""; // 如果没有desc内容，清空输入框
                }
            }
            catch (Exception e)
            {
                print(e.InnerException);
            }
        }
        
        if (inputField != null && !string.IsNullOrEmpty(inputField.text))
        {
            text = inputField.text;
        }
    }

    public void onclick()
    {
        EnsureUnityWindowOnTop(); // 点击按钮时确保窗口在前台
        OpenFileDialog();
    }

    /// <summary>
    /// 确保Unity窗口处于前台和置顶状态
    /// </summary>
    private void EnsureUnityWindowOnTop()
    {
        try
        {
            IntPtr unityWindowHandle = GetUnityWindowHandle();

            if (unityWindowHandle != IntPtr.Zero)
            {
                // 恢复窗口（如果最小化）
                ShowWindow(unityWindowHandle, SW_RESTORE);

                // 设置为前景窗口
                SetForegroundWindow(unityWindowHandle);

                // 置顶窗口
                BringWindowToTop(unityWindowHandle);

                //Debug.Log("Unity窗口已置顶，句柄: " + unityWindowHandle);
            }
            else
            {
                Debug.LogWarning("无法获取Unity窗口句柄");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("置顶窗口时出错: " + e.Message);
        }
    }

    /// <summary>
    /// 获取Unity窗口句柄
    /// </summary>
    private IntPtr GetUnityWindowHandle()
    {
        IntPtr handle = IntPtr.Zero;

        // 方法1：获取活动窗口
        handle = GetActiveWindow();

        // 方法2：如果方法1无效，尝试获取前景窗口
        if (handle == IntPtr.Zero)
        {
            handle = GetForegroundWindow();
        }

        // 方法3：如果仍然无效，尝试通过进程获取
        if (handle == IntPtr.Zero)
        {
            handle = GetWindowHandleByProcess();
        }

        //Debug.Log("获取到的窗口句柄: " + handle);
        return handle;
    }

    /// <summary>
    /// 通过进程信息获取窗口句柄（备选方案）
    /// </summary>
    private IntPtr GetWindowHandleByProcess()
    {
        try
        {
            System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            return currentProcess.MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public void OpenFileDialog()
    {
        // 在打开对话框前再次检查 inputField
        if (inputField == null)
        {
            Debug.LogError("inputField 为 null，无法获取文本内容！");
            FindAndSetupInputField(); // 重新尝试查找
            return;
        }

        // 确保Unity窗口在前台
        EnsureUnityWindowOnTop();

        // 等待一帧，确保窗口置顶操作完成
        StartCoroutine(OpenFileDialogCoroutine());
    }

    private IEnumerator OpenFileDialogCoroutine()
    {
        // 等待一帧，确保SetForegroundWindow生效
        yield return new WaitForEndOfFrame();

        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);

        // 关键修正：使用 dlgOwner 而不是 hwndOwner
        ofn.dlgOwner = GetUnityWindowHandle();

        ofn.filter = "All Files\0*.*\0\0";
        ofn.file = new string(new char[256]);
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string(new char[64]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.title = "选择文件";
        ofn.initialDir = UnityEngine.Application.streamingAssetsPath.Replace('/', '\\');

        // 标志位说明：
        // 0x00080000 = OFN_EXPLORER (使用新式对话框)
        // 0x00001000 = OFN_ENABLESIZING (允许调整大小)
        // 0x00000800 = OFN_PATHMUSTEXIST (路径必须存在)
        // 0x00000008 = OFN_NOCHANGEDIR (不改变当前目录)
        // 0x00040000 = OFN_FORCESHOWHIDDEN (强制显示隐藏文件)
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00040000;

        //Debug.Log("正在打开文件对话框，父窗口句柄: " + ofn.dlgOwner);

        if (LocalDialog.GetOpenFileName(ofn))
        {
            string selectedFilePath = ofn.file;
            //Debug.Log("选中的文件: " + selectedFilePath);
            //filePathObj.text = selectedFilePath;
            string newFileName = Path.GetFileName(selectedFilePath);
            string newFilePath = selectedFilePath;
            PPTInfo newInfo = new PPTInfo();
            newInfo.filename = newFileName; 
            newInfo.file_path = newFilePath;
            newInfo.desc = new string[] {""};
            PPTDataManager.SavePPTInfoToJson(newInfo, Path.ChangeExtension(newFileName, ".json"));
            dropdown.ReSetDropdown();
            dropdown.SetCurrentOptionText(newFileName);
            // 文件选择完成后再次确保Unity窗口在前台
            EnsureUnityWindowOnTop();
        }
        else
        {
            //Debug.Log("用户取消了文件选择");
            // 取消选择后也要确保Unity窗口在前台
            EnsureUnityWindowOnTop();
        }
    }

    public void submit()
    {
        string jsonName = Path.ChangeExtension(fileName,".json");
        string[] strings = text.Split('\n');
        pptInfo.desc = strings;
        bool success = PPTDataManager.SavePPTInfoToJson(pptInfo, jsonName);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            // 当应用获得焦点时，确保窗口置顶
            EnsureUnityWindowOnTop();
        }
    }

    // 调试方法：测试窗口句柄获取
    [ContextMenu("测试窗口句柄获取")]
    public void TestWindowHandle()
    {
        IntPtr handle = GetUnityWindowHandle();
        //Debug.Log("测试获取窗口句柄: " + handle);

        if (handle != IntPtr.Zero)
        {
            //Debug.Log("句柄有效，尝试置顶...");
            EnsureUnityWindowOnTop();
        }
    }

    private string FindJsonFilePath()
    {
        // 尝试在StreamingAssets中查找
        string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "pptinfo", jsonConfigPath);
        if (File.Exists(streamingAssetsPath))
        {
            return streamingAssetsPath;
        }

        // 尝试在项目根目录查找
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string rootPath = Path.Combine(projectRoot, "pptinfo", jsonConfigPath);
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        // 尝试在Assets文件夹中查找
        string assetsPath = Path.Combine(Application.dataPath, "pptinfo", jsonConfigPath);
        if (File.Exists(assetsPath))
        {
            return assetsPath;
        }

        return null;
    }
}