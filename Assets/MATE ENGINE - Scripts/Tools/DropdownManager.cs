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
}
