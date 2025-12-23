using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using TMPro;

namespace MATE_ENGINE___Scripts.Tools
{
    public class AutoDesc : MonoBehaviour
    {
        private string filePath;
        private string baseUrl = "http://192.168.8.88:7899";

        // 跟踪请求状态
        private bool isRequestInProgress = false;
        private UnityWebRequest currentRequest = null;
        
        // 演讲稿生成完成的回调事件
        public System.Action<string[]> OnSpeechGenerated;
        
        // 定义返回数据的结构
        [System.Serializable]
        public class ApiResponse
        {
            public string content;
        }

        // 取消当前请求
        private void CancelCurrentRequest()
        {
            if (currentRequest != null && !currentRequest.isDone)
            {
                currentRequest.Abort();
                currentRequest.Dispose();
                currentRequest = null;
                Debug.Log("演讲稿生成请求已取消");
            }
            isRequestInProgress = false;
        }

        public void StartGetDescProcess(string filename)
        {
            // 如果已有请求在进行中，不允许重复请求
            if (isRequestInProgress)
            {
                Debug.LogWarning("已有演讲稿生成请求在进行中，请等待完成");
                return;
            }
            isRequestInProgress = true;
            StartCoroutine(GetDescFromHTTP(filename));
        }
        
        IEnumerator GetDescFromHTTP(string filename)
        {
            string fileName = filename;
            fileName = Path.ChangeExtension(fileName, ".json");
            PPTInfo pptInfo = PPTDataManager.LoadPPTInfoFromJson(fileName);
            filePath = pptInfo.file_path;
            // 上传PPT文件并获取演讲稿
            yield return StartCoroutine(UploadPPTFile(filePath));
            
            // 请求完成，重置状态并启用控件
            isRequestInProgress = false;
        }
        
        IEnumerator UploadPPTFile(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                Debug.LogError($"文件不存在: {filePath}");
                yield break;
            }            
            string url = $"{baseUrl}/ppt";
            
            // 读取文件数据
            byte[] fileData = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);
            
            // 创建表单数据
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            
            // 添加文件数据
            formData.Add(new MultipartFormFileSection("file", fileData, fileName, "application/vnd.openxmlformats-officedocument.presentationml.presentation"));
            
            currentRequest = UnityWebRequest.Post(url, formData);
            
            // 设置超时时间（秒）
            currentRequest.timeout = 300;
            
            // 发送请求
            yield return currentRequest.SendWebRequest();

            // 检查GameObject是否仍然存在（可能在请求过程中被销毁）
            if (this == null || currentRequest == null)
            {
                yield break;
            }

            if (currentRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("演讲稿生成成功！");
                string descText = currentRequest.downloadHandler.text;

                string[] descArray = toStringArray(descText);

                // string desc = string.Join(Environment.NewLine, descArray);
                
                // 触发回调事件
                OnSpeechGenerated?.Invoke(descArray);
            }
            else
            {
                Debug.LogError($"演讲稿生成失败: {currentRequest.error}");
                OnSpeechGenerated?.Invoke(new string[] { "" });
            }

            // 清理请求对象
            if (currentRequest != null)
            {
                currentRequest.Dispose();
                currentRequest = null;
            }
        }

        public string[] toStringArray(string input)
        {
            input = RemoveOuterQuotes(input);
            return SplitByMultipleNewLines(input);
        }
        /// <summary>
        /// 去掉字符串最外层的一对引号和think标签
        /// </summary>
        private string RemoveOuterQuotes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
    
            string result = input;
    
            // 1. 移除外层引号
            if (result.Length >= 2)
            {
                if ((result[0] == '"' && result[^1] == '"') || 
                    (result[0] == '\'' && result[^1] == '\''))
                {
                    result = result.Substring(1, result.Length - 2);
                }
            }
    
            // 2. 使用正则表达式移除 <think> 标签及内容
            result = Regex.Replace(result, @"<think>[\s\S]*?</think>", "", RegexOptions.IgnoreCase);
    
            // 3. 清理可能残留的空白字符
            result = result.Trim();
    
            return result;
        }

        /// <summary>
        /// 使用正则表达式按一个或多个连续的换行符分割（支持\n、\r\n、\r）
        /// </summary>
        private string[] SplitByMultipleNewLines(string input)
        {
            string[] result = Regex.Split(input, @"[\r\n]+");
            result = result.Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            return result;
        }
    }
}