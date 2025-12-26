using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Text;

public static class KnowledgeBaseManager
{
    // 知识库API配置
    private const string RAGFLOW_API_URL = "https://know.baafs.net.cn/v1";
    private const string RAGFLOW_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6ImI1ZWU1NDU3LWIxYmEtNDdmMS1hY2JmLTVlMzc2ZjNlZGIzMiJ9.FBxm0gwpI7WJhFvgNzskfYqN9Ddx_gsHVf-DuBn6aU4";
    private const string KB_ID = "728cfd04d56411f097ac578fc36c86e8";
    private const string KB_NAME = "数字人V3";

    // 上传和解析结果
    public static bool LastUploadSuccess { get; private set; }

    /// <summary>
    /// 上传文件到知识库
    /// </summary>
    /// <param name="filePath">要上传的文件路径</param>
    /// <param name="parserId">知识库文档解析方式（可选）</param>
    /// <param name="run">是否可用状态，默认为1</param>
    /// <returns>上传结果协程</returns>
    public static IEnumerator UploadFileToKnowledgeBase(string filePath, string parserId = null, int run = 1)
    {
        // 初始化状态
        LastUploadSuccess = false;
        
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
                                LastUploadSuccess = true;
                                
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
                                    LastUploadSuccess = true;
                                    
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
                // 直接调用协程迭代器，而不是使用StartCoroutine
                IEnumerator parseCoroutine = ParseDocumentChunks(extractedDocId, run);
                while (parseCoroutine.MoveNext())
                {
                    yield return parseCoroutine.Current;
                }
            }
            else if (!uploadSuccess)
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
    public static string ExtractDocIdFromResponse(Dictionary<string, object> responseJson)
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
    public static IEnumerator ParseDocumentChunks(string docIds, int run = 1)
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