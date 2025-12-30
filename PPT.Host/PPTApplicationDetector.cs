using System;
using System.Runtime.InteropServices;

namespace PPT.Host
{
    /// <summary>
    /// 应用类型
    /// </summary>
    public enum PPTApplicationType
    {
        Auto,
        WPS,
        Office
    }

    /// <summary>
    /// PowerPoint 应用程序检测器
    /// 用于检测系统中已安装的演示软件(WPS/Office)
    /// </summary>
    public static class PPTApplicationDetector
    {
        private const string WPS_PROG_ID = "KWPP.Application";
        private const string OFFICE_PROG_ID = "PowerPoint.Application";

        /// <summary>
        /// 检测 WPS 演示是否已安装
        /// </summary>
        public static bool IsWPSInstalled()
        {
            return IsProgIDAvailable(WPS_PROG_ID);
        }

        /// <summary>
        /// 检测 Office PowerPoint 是否已安装
        /// </summary>
        public static bool IsOfficeInstalled()
        {
            return IsProgIDAvailable(OFFICE_PROG_ID);
        }

        /// <summary>
        /// 自动检测最佳可用应用(优先 Office,其次 WPS)
        /// </summary>
        public static PPTApplicationType DetectBestAvailable()
        {
            if (IsOfficeInstalled())
            {
                return PPTApplicationType.Office;
            }
            else if (IsWPSInstalled())
            {
                return PPTApplicationType.WPS;
            }
            else
            {
                throw new InvalidOperationException("未检测到 WPS 或 Office PowerPoint,请至少安装其中一个");
            }
        }

        /// <summary>
        /// 获取指定应用类型的 ProgID
        /// </summary>
        public static string GetProgID(PPTApplicationType type)
        {
            switch (type)
            {
                case PPTApplicationType.Auto:
                    var bestType = DetectBestAvailable();
                    return GetProgID(bestType);

                case PPTApplicationType.WPS:
                    if (!IsWPSInstalled())
                    {
                        throw new InvalidOperationException("WPS 演示未安装");
                    }
                    return WPS_PROG_ID;

                case PPTApplicationType.Office:
                    if (!IsOfficeInstalled())
                    {
                        throw new InvalidOperationException("Office PowerPoint 未安装");
                    }
                    return OFFICE_PROG_ID;

                default:
                    throw new ArgumentException($"未知的应用类型: {type}");
            }
        }

        /// <summary>
        /// 获取应用类型的显示名称
        /// </summary>
        public static string GetDisplayName(PPTApplicationType type)
        {
            switch (type)
            {
                case PPTApplicationType.Auto:
                    return "自动检测";
                case PPTApplicationType.WPS:
                    return "WPS 演示";
                case PPTApplicationType.Office:
                    return "Office PowerPoint";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// 检测指定的 ProgID 是否可用
        /// </summary>
        private static bool IsProgIDAvailable(string progId)
        {
            try
            {
                Type type = Type.GetTypeFromProgID(progId, false);
                return type != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取系统安装情况的详细信息
        /// </summary>
        public static string GetInstallationInfo()
        {
            bool wpsInstalled = IsWPSInstalled();
            bool officeInstalled = IsOfficeInstalled();

            string info = "系统检测结果:\n";
            info += $"  - WPS 演示: {(wpsInstalled ? "已安装" : "未安装")}\n";
            info += $"  - Office PowerPoint: {(officeInstalled ? "已安装" : "未安装")}";

            return info;
        }
    }
}
