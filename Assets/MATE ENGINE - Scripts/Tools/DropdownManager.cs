using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DropdownManager : MonoBehaviour
{
    public TMP_Dropdown dropdown;

    void Start()
    {
        // 示例数据
        List<string> options = new List<string>();
        List<string> files = PPTDataManager.GetAllPPTInfoJsonFiles();
        print("files_count:" + files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            options.Add(PPTDataManager.LoadPPTInfoFromJson(files[i]).filename);
        }

        // 清空现有选项
        dropdown.ClearOptions();

        // 添加新选项 - 选项数量会自动根据数据数量确定
        dropdown.AddOptions(options);
    }

    public void ReSetDropdown()
    {
        // 示例数据
        List<string> options = new List<string>();
        List<string> files = PPTDataManager.GetAllPPTInfoJsonFiles();
        print("files_count:" + files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            options.Add(PPTDataManager.LoadPPTInfoFromJson(files[i]).filename);
        }

        // 清空现有选项
        dropdown.ClearOptions();

        // 添加新选项 - 选项数量会自动根据数据数量确定
        dropdown.AddOptions(options);
    }

    public string GetCurrentOptionText()
    {
        // 确保有选项且当前value在有效范围内
        if (dropdown.options != null && dropdown.options.Count > 0 &&
            dropdown.value >= 0 && dropdown.value < dropdown.options.Count)
        {
            return dropdown.options[dropdown.value].text;
        }
        else
        {
            return string.Empty; // 或者返回一个默认值
        }
    }
    /// <summary>
    /// 通过传入的字符串设置当前选择哪个option
    /// </summary>
    /// <param name="optionText">要选择的选项文本</param>
    /// <returns>是否成功设置</returns>
    public bool SetCurrentOptionText(string optionText)
    {
        if (dropdown == null)
        {
            Debug.LogError("Dropdown未赋值！");
            return false;
        }

        if (string.IsNullOrEmpty(optionText))
        {
            Debug.LogWarning("传入的选项文本为空！");
            return false;
        }

        // 查找匹配的选项索引
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (dropdown.options[i].text == optionText)
            {
                dropdown.value = i;
                // 触发onValueChanged事件（如果需要）
                dropdown.onValueChanged?.Invoke(i);
                return true;
            }
        }

        Debug.LogWarning($"未找到匹配的选项: {optionText}");
        return false;
    }
}
