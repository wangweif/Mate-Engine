using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using TMPro;
using UnityEngine.UI;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Text;

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

    // 知识库API配置
    private const string RAGFLOW_API_URL = "https://know.baafs.net.cn/v1";
    private const string RAGFLOW_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6ImI1ZWU1NDU3LWIxYmEtNDdmMS1hY2JmLTVlMzc2ZjNlZGIzMiJ9.FBxm0gwpI7WJhFvgNzskfYqN9Ddx_gsHVf-DuBn6aU4";
    private const string KB_ID = "728cfd04d56411f097ac578fc36c86e8";
    private const string KB_NAME = "数字人V3";

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

        if (success)
        {
            // 保存成功后上传文件到知识库
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                StartCoroutine(UploadFileToKnowledgeBase(filePath));
            }
            else
            {
                Debug.LogWarning("文件路径为空或文件不存在，无法上传到知识库");
            }
        }
        else
        {
            Debug.LogError("保存PPT信息失败，跳过上传到知识库");
        }

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

    /// <summary>
    /// 上传文件到知识库
    /// </summary>
    /// <param name="filePath">要上传的文件路径</param>
    /// <param name="parserId">知识库文档解析方式（可选）</param>
    /// <param name="run">是否可用状态，默认为1</param>
    /// <returns>上传结果协程</returns>
    private IEnumerator UploadFileToKnowledgeBase(string filePath, string parserId = null, int run = 1)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogError($"文件不存在或路径为空: {filePath}");
            yield break;
        }

        string url = $"{RAGFLOW_API_URL}/document/upload";
        Debug.Log($"开始上传文件到知识库: {filePath}");

        // 读取文件字节
        byte[] fileBytes = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        // 创建multipart/form-data请求
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        
        // 添加文件
        formData.Add(new MultipartFormFileSection("file", fileBytes, fileName, "application/octet-stream"));
        
        // 添加表单字段
        formData.Add(new MultipartFormDataSection("kb_name", KB_NAME));
        formData.Add(new MultipartFormDataSection("kb_id", KB_ID));
        formData.Add(new MultipartFormDataSection("run", run.ToString()));

        if (!string.IsNullOrEmpty(parserId))
        {
            formData.Add(new MultipartFormDataSection("parser_id", parserId));
        }

        // 创建请求
        using (UnityWebRequest request = UnityWebRequest.Post(url, formData))
        {
            // 设置授权头
            request.SetRequestHeader("authorization", RAGFLOW_TOKEN);

            // 发送请求
            yield return request.SendWebRequest();

            // 处理响应
            string extractedDocId = null; // 在try-catch块外声明，用于存储提取的文档ID
            bool uploadSuccess = false;
            
            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"上传响应: {responseText}");
                    
                    // 尝试解析响应JSON
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        // 使用Newtonsoft.Json解析（更灵活）
                        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        
                        if (responseJson != null && responseJson.ContainsKey("code"))
                        {
                            int code = Convert.ToInt32(responseJson["code"]);
                            if (code == 200 || code == 0)
                            {
                                Debug.Log($"文件上传成功: {fileName}");
                                uploadSuccess = true;
                                
                                // 提取文档ID - data是数组格式
                                extractedDocId = ExtractDocIdFromResponse(responseJson);
                                
                                if (string.IsNullOrEmpty(extractedDocId))
                                {
                                    Debug.LogWarning("无法从上传响应中提取文档ID，跳过解析步骤");
                                }
                            }
                            else
                            {
                                string message = responseJson.ContainsKey("message") ? responseJson["message"].ToString() : "未知错误";
                                Debug.LogWarning($"文件上传返回非成功状态码: {code}, 消息: {message}");
                            }
                        }
                        else
                        {
                            // 如果响应中没有code字段，尝试使用JsonUtility解析
                            var responseJson2 = JsonUtility.FromJson<UploadResponse>(responseText);
                            if (responseJson2 != null)
                            {
                                if (responseJson2.code == 200 || responseJson2.code == 0)
                                {
                                    Debug.Log($"文件上传成功: {fileName}");
                                    uploadSuccess = true;
                                    
                                    // 尝试从响应文本中提取doc_id（如果JsonUtility解析的类中没有id字段）
                                    try
                                    {
                                        var fullResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                                        if (fullResponse != null)
                                        {
                                            extractedDocId = ExtractDocIdFromResponse(fullResponse);
                                        }
                                        
                                        if (string.IsNullOrEmpty(extractedDocId))
                                        {
                                            Debug.LogWarning("无法从上传响应中提取文档ID，跳过解析步骤");
                                        }
                                    }
                                    catch
                                    {
                                        Debug.LogWarning("无法从上传响应中提取文档ID，跳过解析步骤");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"文件上传返回非成功状态码: {responseJson2.code}, 消息: {responseJson2.message}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"无法解析上传响应，但HTTP状态码为200。响应内容: {responseText}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析上传响应失败: {e.Message}");
                    Debug.Log($"响应内容: {request.downloadHandler.text}");
                }
            }
            else
            {
                Debug.LogError($"文件上传失败: {request.error}");
                Debug.Log($"响应状态码: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.Log($"响应内容: {request.downloadHandler.text}");
                }
            }
            
            // 在try-catch块外调用解析方法
            if (uploadSuccess && !string.IsNullOrEmpty(extractedDocId))
            {
                Debug.Log($"获取到文档ID: {extractedDocId}，开始解析文档");
                yield return StartCoroutine(ParseDocumentChunks(extractedDocId, run));
            }
            else
            {
                Debug.LogError($"文件上传失败: {request.error}");
                Debug.Log($"响应状态码: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.Log($"响应内容: {request.downloadHandler.text}");
                }
            }
        }
    }

    /// <summary>
    /// 上传响应数据结构
    /// </summary>
    [Serializable]
    private class UploadResponse
    {
        public int code;
        public string message;
    }

    /// <summary>
    /// 从上传响应中提取文档ID
    /// </summary>
    /// <param name="responseJson">响应JSON对象</param>
    /// <returns>文档ID，如果提取失败返回null</returns>
    private string ExtractDocIdFromResponse(Dictionary<string, object> responseJson)
    {
        if (responseJson == null)
            return null;

        // 方法1: 从data数组中提取（data是数组格式，包含文档对象）
        if (responseJson.ContainsKey("data"))
        {
            var data = responseJson["data"];
            
            // 优先处理JArray格式（Newtonsoft.Json通常返回JArray）
            if (data is Newtonsoft.Json.Linq.JArray jArray && jArray.Count > 0)
            {
                var firstItem = jArray[0];
                if (firstItem is Newtonsoft.Json.Linq.JObject jObj && jObj["id"] != null)
                {
                    return jObj["id"].ToString();
                }
            }
            // 处理数组格式：data是List<object>
            else if (data is List<object> dataList && dataList.Count > 0)
            {
                var firstItem = dataList[0];
                // 处理Dictionary格式
                if (firstItem is Dictionary<string, object> itemDict && itemDict.ContainsKey("id"))
                {
                    return itemDict["id"].ToString();
                }
                // 处理JObject格式（List中可能包含JObject）
                else if (firstItem is Newtonsoft.Json.Linq.JObject jObj && jObj["id"] != null)
                {
                    return jObj["id"].ToString();
                }
            }
            // 处理单个对象格式（如果data是单个对象而不是数组）
            else if (data is Dictionary<string, object> dataDict && dataDict.ContainsKey("id"))
            {
                return dataDict["id"].ToString();
            }
            // 处理JObject格式（单个对象）
            else if (data is Newtonsoft.Json.Linq.JObject dataJObj && dataJObj["id"] != null)
            {
                return dataJObj["id"].ToString();
            }
        }
        
        // 方法2: 如果响应中直接包含id字段
        if (responseJson.ContainsKey("id"))
        {
            return responseJson["id"].ToString();
        }
        
        return null;
    }

    /// <summary>
    /// 解析文档块
    /// </summary>
    /// <param name="docIds">文档ID列表（可以是单个ID字符串或ID数组）</param>
    /// <param name="run">是否可用状态，默认为1</param>
    /// <returns>解析结果协程</returns>
    private IEnumerator ParseDocumentChunks(string docIds, int run = 1)
    {
        // 处理docIds：如果是单个ID，转换为数组格式
        // 根据Python接口，doc_ids应该是一个列表（数组）
        string[] docIdArray;
        if (string.IsNullOrEmpty(docIds))
        {
            Debug.LogError("文档ID为空，无法解析");
            yield break;
        }
        
        if (docIds.Contains(","))
        {
            // 如果是逗号分隔的字符串，分割成数组并去除空格
            docIdArray = docIds.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < docIdArray.Length; i++)
            {
                docIdArray[i] = docIdArray[i].Trim();
            }
        }
        else
        {
            // 单个ID，转换为数组
            docIdArray = new string[] { docIds.Trim() };
        }

        string url = $"{RAGFLOW_API_URL}/document/run";
        Debug.Log($"开始解析文档，文档ID: {docIds}");

        // 构建请求数据
        var requestData = new
        {
            delete = false,
            doc_ids = docIdArray,
            run = run
        };

        string jsonData = JsonConvert.SerializeObject(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        // 创建请求
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("authorization", RAGFLOW_TOKEN);

            // 发送请求
            yield return request.SendWebRequest();

            // 处理响应
            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    Debug.Log($"解析文档响应: {responseText}");
                    
                    // 解析响应JSON
                    if (!string.IsNullOrEmpty(responseText))
                    {
                        var responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        if (responseJson != null)
                        {
                            Debug.Log($"文档解析成功: {docIds}");
                            
                            // 可以在这里处理解析结果
                            if (responseJson.ContainsKey("code"))
                            {
                                int code = Convert.ToInt32(responseJson["code"]);
                                if (code == 200 || code == 0)
                                {
                                    Debug.Log($"文档解析完成: {docIds}");
                                }
                                else
                                {
                                    string message = responseJson.ContainsKey("message") ? responseJson["message"].ToString() : "未知错误";
                                    Debug.LogWarning($"文档解析返回非成功状态码: {code}, 消息: {message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析文档响应失败: {e.Message}");
                    Debug.Log($"响应内容: {request.downloadHandler.text}");
                }
            }
            else
            {
                Debug.LogError($"文档解析失败: {request.error}");
                Debug.Log($"响应状态码: {request.responseCode}");
                if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
                {
                    Debug.Log($"响应内容: {request.downloadHandler.text}");
                }
            }
        }
    }
}