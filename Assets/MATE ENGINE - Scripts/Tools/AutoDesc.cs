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

namespace MATE_ENGINE___Scripts.Tools
{
    public class AutoDesc : MonoBehaviour
    {
        [SerializeField] 
        private InputField inputField;
        [SerializeField]
        private DropdownManager dropdown;

        private string filePath;
        private string baseUrl = "http://192.168.8.88:7899"; 
        
        // 定义返回数据的结构
        [System.Serializable]
        public class ApiResponse
        {
            public string content;
        }

        // 调用这个方法开始整个流程
        public void StartGetDescProcess()
        {
            StartCoroutine(GetDescFromHTTP());
        }
        
        IEnumerator GetDescFromHTTP()
        {
            string fileName = dropdown.GetCurrentOptionText();
            fileName = Path.ChangeExtension(fileName, ".json");
            PPTInfo pptInfo = PPTDataManager.LoadPPTInfoFromJson(fileName);
            filePath = pptInfo.file_path;
            // 上传PPT文件并获取演讲稿
            yield return StartCoroutine(UploadPPTFile(filePath));
            
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
            
            using (UnityWebRequest request = UnityWebRequest.Post(url, formData))
            {
                // 设置超时时间（秒）
                request.timeout = 300;
                
                // 发送请求
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("演讲稿生成成功！");
                    string descText = request.downloadHandler.text;

                    string[] descArray = toStringArray(descText);

                    string desc = string.Join(Environment.NewLine, descArray);
                    inputField.text = desc;
                }
                else
                {
                    Debug.LogError($"上传失败: {request.error}");
                    Debug.LogError($"状态码: {request.responseCode}");
                    Debug.LogError($"错误详情: {request.downloadHandler?.text}");
                    
                    // 显示错误信息
                    if (inputField != null)
                    {
                        inputField.text = $"错误: {request.error}\n状态码: {request.responseCode}";
                    }
                }
            }
        }

        public string[] toStringArray(string input)
        {
            input = RemoveOuterQuotes(input);
            return SplitByMultipleNewLines(input);
        }
        /// <summary>
        /// 去掉字符串最外层的一对引号
        /// </summary>
        private string RemoveOuterQuotes(string input)
        {
            if (input.Length >= 2)
            {
                // 检查是否以引号开头和结尾
                if ((input[0] == '"' && input[^1] == '"') || 
                    (input[0] == '\'' && input[^1] == '\''))
                {
                    // 去掉第一个和最后一个字符（引号）
                    return input.Substring(1, input.Length - 2);
                }
            }
            return input;
        }

        /// <summary>
        /// 使用正则表达式按一个或多个连续的换行符分割（支持\n、\r\n、\r）
        /// </summary>
        private string[] SplitByMultipleNewLines(string input)
        {
            
            string[] result = null;
            result = input.Split(new string[] { "\\n", "\n", "\\r", "\r", "\\r\\n", "\r\n" },
                StringSplitOptions.RemoveEmptyEntries);
            result = result.Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            return result;
        }
    }
}