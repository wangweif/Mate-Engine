using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Text;

namespace MATE_ENGINE___Scripts.Tools
{
    public static class VersionInfoManager
    {
        /// <summary>
        /// 从version.md文件中提取所有版本信息（纯文本格式）
        /// </summary>
        public static List<VersionInfo> GetAllVersionInfo()
        {
            List<VersionInfo> versions = new List<VersionInfo>();
            
            try
            {
                // 读取版本文件
                string fileContent = ReadVersionFile();
                if (string.IsNullOrEmpty(fileContent))
                {
                    Debug.LogWarning("未找到version.md文件");
                    return versions; // 返回空列表
                }
                
                // 解析版本文件
                versions = ParseVersionFile(fileContent);
                
                // 按版本号降序排列（最新版本在前）
                if (versions.Count > 0)
                {
                    versions.Sort((a, b) => CompareVersionNumbers(b.version, a.version));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"获取版本信息失败: {e.Message}");
                Debug.LogException(e);
            }
            
            return versions;
        }
        
        /// <summary>
        /// 读取version.md文件
        /// </summary>
        private static string ReadVersionFile()
        {
            // 尝试多个可能的路径
            string[] possiblePaths = {
                Path.Combine(Application.dataPath, "../version.md"),        // 项目根目录
                Path.Combine(Application.dataPath, "../../version.md"),     // 项目上一级目录
                Path.Combine(Application.dataPath, "version.md"),           // Assets目录
                Path.Combine(Application.streamingAssetsPath, "version.md"), // StreamingAssets
                "version.md"                                                // 当前目录
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllText(path, Encoding.UTF8);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"读取文件失败 {path}: {e.Message}");
                    }
                }
            }
            
            // 尝试从Resources加载
            TextAsset textAsset = Resources.Load<TextAsset>("version");
            if (textAsset != null)
            {
                return textAsset.text;
            }
            
            return null;
        }
        
        /// <summary>
        /// 解析版本文件内容
        /// </summary>
        private static List<VersionInfo> ParseVersionFile(string fileContent)
        {
            List<VersionInfo> versions = new List<VersionInfo>();
            
            try
            {
                // 清理内容：移除BOM字符
                fileContent = fileContent.TrimStart('\uFEFF');
                
                // 使用正则表达式匹配版本标题：### [3.5.2] - 2025-11-21
                string versionPattern = @"###\s*\[(\d+(?:\.\d+)*)\]\s*-\s*(\d{4}-\d{2}-\d{2})";
                MatchCollection versionMatches = Regex.Matches(fileContent, versionPattern, RegexOptions.Multiline);
                
                if (versionMatches.Count == 0)
                {
                    Debug.LogWarning("未找到版本标题，尝试其他格式");
                    return versions;
                }
                
                // 处理每个版本
                for (int i = 0; i < versionMatches.Count; i++)
                {
                    Match match = versionMatches[i];
                    string versionNumber = "v" + match.Groups[1].Value.Trim();
                    string date = match.Groups[2].Value.Trim();
                    
                    // 获取该版本的内容（从当前匹配到下一个匹配或文件结尾）
                    int startIndex = match.Index;
                    int endIndex = (i < versionMatches.Count - 1) ? versionMatches[i + 1].Index : fileContent.Length;
                    
                    string versionContent = fileContent.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // 生成纯文本描述
                    string description = GeneratePlainTextDescription(versionNumber, date, versionContent);
                    
                    // 添加到列表
                    versions.Add(new VersionInfo(versionNumber, description));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析版本文件失败: {e.Message}");
            }
            
            return versions;
        }
        
        /// <summary>
        /// 生成纯文本格式的描述
        /// </summary>
        private static string GeneratePlainTextDescription(string version, string date, string content)
        {
            StringBuilder sb = new StringBuilder();
            
            // 标题部分
            sb.AppendLine($"版本 {version}");
            sb.AppendLine($"发布日期: {date}");
            sb.AppendLine();
            
            // 分割成行处理
            string[] lines = content.Split('\n');
            bool inNewFeatures = false;
            bool inImprovements = false;
            bool inFixes = false;
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;
                
                // 跳过版本标题行
                if (trimmedLine.StartsWith("### ["))
                    continue;
                
                // 检测章节
                if (trimmedLine == "#### 新增功能")
                {
                    inNewFeatures = true;
                    inImprovements = false;
                    inFixes = false;
                    sb.AppendLine("新增功能");
                    sb.AppendLine();
                    continue;
                }
                else if (trimmedLine == "#### 功能优化")
                {
                    inNewFeatures = false;
                    inImprovements = true;
                    inFixes = false;
                    sb.AppendLine("功能优化");
                    sb.AppendLine();
                    continue;
                }
                else if (trimmedLine == "#### 修复问题")
                {
                    inNewFeatures = false;
                    inImprovements = false;
                    inFixes = true;
                    sb.AppendLine("修复问题");
                    sb.AppendLine();
                    continue;
                }
                
                // 处理内容行
                if (inNewFeatures || inImprovements || inFixes)
                {
                    // 如果是列表项，移除列表标记
                    if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("• "))
                    {
                        trimmedLine = trimmedLine.Substring(2).Trim();
                    }
                    
                    // 移除可能的加粗标记
                    trimmedLine = trimmedLine.Replace("**", "");
                    
                    // 清理多余空格
                    trimmedLine = Regex.Replace(trimmedLine, @"\s+", " ");
                    
                    sb.AppendLine(trimmedLine);
                }
            }
            
            // 确保章节之间有适当的空行
            string result = sb.ToString().Trim();
            
            // 规范化空行：确保每个章节之间有一个空行
            result = Regex.Replace(result, @"\n{3,}", "\n\n");
            
            return result;
        }
        
        /// <summary>
        /// 比较版本号大小
        /// </summary>
        private static int CompareVersionNumbers(string versionA, string versionB)
        {
            try
            {
                // 移除v前缀
                versionA = versionA.TrimStart('v', 'V');
                versionB = versionB.TrimStart('v', 'V');
                
                string[] partsA = versionA.Split('.');
                string[] partsB = versionB.Split('.');
                
                int maxLength = Mathf.Max(partsA.Length, partsB.Length);
                
                for (int i = 0; i < maxLength; i++)
                {
                    int numA = (i < partsA.Length && int.TryParse(partsA[i], out int a)) ? a : 0;
                    int numB = (i < partsB.Length && int.TryParse(partsB[i], out int b)) ? b : 0;
                    
                    if (numA != numB)
                        return numA.CompareTo(numB);
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}