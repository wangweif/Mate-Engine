using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class PPTDataManager
{
    /// <summary>
    /// 将 PPTInfo 对象保存为 JSON 文件到项目根目录
    /// </summary>
    /// <param name="pptInfo">要保存的 PPTInfo 对象</param>
    /// <param name="fileName">JSON 文件名（不包含路径）</param>
    /// <returns>是否保存成功</returns>
    public static bool SavePPTInfoToJson(PPTInfo pptInfo, string fileName = "ppt_data.json")
    {
        try
        {
            // 验证输入参数
            if (pptInfo == null)
            {
                Debug.LogError("PPTInfo 对象为 null，无法保存");
                return false;
            }

            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("文件名为空，无法保存");
                return false;
            }

            // 将 PPTInfo 对象序列化为 JSON 字符串
            string jsonData = JsonUtility.ToJson(pptInfo, true);

            // 获取项目根目录路径
            string projectRootPath = GetProjectRootPath();

            // 构建完整的文件路径
            string fullFilePath = Path.Combine(projectRootPath, fileName);

            // 确保目录存在
            string directory = Path.GetDirectoryName(fullFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 将 JSON 数据写入文件
            File.WriteAllText(fullFilePath, jsonData, Encoding.UTF8);

            Debug.Log($"PPTInfo 数据已成功保存到: {fullFilePath}");
            Debug.Log($"JSON 内容:\n{jsonData}");

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存 PPTInfo 到 JSON 失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 从项目根目录加载 JSON 文件并转换为 PPTInfo 对象
    /// </summary>
    /// <param name="fileName">JSON 文件名（不包含路径）</param>
    /// <returns>加载的 PPTInfo 对象，失败返回 null</returns>
    public static PPTInfo LoadPPTInfoFromJson(string fileName = "ppt_data.json")
    {
        try
        {
            // 获取项目根目录路径
            string projectRootPath = GetProjectRootPath();

            // 构建完整的文件路径
            string fullFilePath = Path.Combine(projectRootPath, fileName);

            // 检查文件是否存在
            if (!File.Exists(fullFilePath))
            {
                Debug.LogWarning($"JSON 文件不存在: {fullFilePath}");
                return null;
            }

            // 读取 JSON 文件内容
            string jsonData = File.ReadAllText(fullFilePath, Encoding.UTF8);

            // 将 JSON 数据反序列化为 PPTInfo 对象
            PPTInfo pptInfo = JsonUtility.FromJson<PPTInfo>(jsonData);

            Debug.Log($"PPTInfo 数据已从 {fullFilePath} 成功加载");

            return pptInfo;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"从 JSON 加载 PPTInfo 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取项目根目录路径（包含 Assets 文件夹的目录）
    /// </summary>
    /// <returns>项目根目录路径</returns>
    private static string GetProjectRootPath()
    {
        // Application.dataPath 返回 Assets 文件夹的路径
        // 获取其父目录即为项目根目录
        string assetsPath = Application.dataPath;
        string projectRoot = Directory.GetParent(assetsPath).FullName;
        projectRoot = Path.Combine(projectRoot, "pptinfo");

        return projectRoot;
    }

    /// <summary>
    /// 获取项目根目录中所有 PPTInfo JSON 文件
    /// </summary>
    /// <returns>JSON 文件路径列表</returns>
    public static List<string> GetAllPPTInfoJsonFiles()
    {
        List<string> jsonFiles = new List<string>();

        try
        {
            string projectRootPath = GetProjectRootPath();

            // 查找所有 .json 文件
            string[] files = Directory.GetFiles(projectRootPath, "*.json", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                // // 可以添加更精确的过滤条件
                // if (file.ToLower().Contains("ppt") || file.ToLower().Contains("info"))
                // {
                //     jsonFiles.Add(file);
                // }
                jsonFiles.Add(file);
            }

            Debug.Log($"找到 {jsonFiles.Count} 个 PPTInfo JSON 文件");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"查找 PPTInfo JSON 文件失败: {e.Message}");
        }

        return jsonFiles;
    }

    /// <summary>
    /// 删除项目根目录中的 PPTInfo JSON 文件
    /// </summary>
    /// <param name="fileName">要删除的文件名</param>
    /// <returns>是否删除成功</returns>
    public static bool DeletePPTInfoJsonFile(string fileName = "ppt_data.json")
    {
        try
        {
            string projectRootPath = GetProjectRootPath();
            string fullFilePath = Path.Combine(projectRootPath, fileName);

            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
                Debug.Log($"已删除文件: {fullFilePath}");
                return true;
            }
            else
            {
                Debug.LogWarning($"文件不存在，无法删除: {fullFilePath}");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"删除文件失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取 JSON 文件的完整路径
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>完整文件路径</returns>
    public static string GetJsonFilePath(string fileName = "ppt_data.json")
    {
        string projectRootPath = GetProjectRootPath();
        return Path.Combine(projectRootPath, fileName);
    }

    /// <summary>
    /// 检查 JSON 文件是否存在
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否存在</returns>
    public static bool JsonFileExists(string fileName = "ppt_data.json")
    {
        string filePath = GetJsonFilePath(fileName);
        return File.Exists(filePath);
    }
}