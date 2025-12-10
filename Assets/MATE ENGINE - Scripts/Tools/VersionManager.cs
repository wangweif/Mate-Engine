using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MATE_ENGINE___Scripts.Tools
{
    public class VersionManager : MonoBehaviour
    {
        public TMP_Dropdown dropdown;
        public InputField desc;
        private int descVersion;
        private List<VersionInfo> versionInfos;

        private void Start()
        {
            List<string> options = new List<string>();
            versionInfos = VersionInfoManager.GetAllVersionInfo();
            print("version_count:" + versionInfos.Count);
            for (var i = 0; i < versionInfos.Count; i++)
            {
                options.Add(versionInfos[i].version);
            }

            // 清空现有选项
            dropdown.ClearOptions();

            // 添加新选项 - 选项数量会自动根据数据数量确定
            dropdown.AddOptions(options);
            dropdown.value = 0;
            desc.text = versionInfos[0].description;
            descVersion = 0;
        }

        void Update()
        {
            if (descVersion != dropdown.value)
            {
                desc.text = versionInfos[dropdown.value].description;
            }
        }

    }
}