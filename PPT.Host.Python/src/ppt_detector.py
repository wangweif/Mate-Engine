"""
PowerPoint 应用程序检测器

用于检测系统中已安装的演示软件 (WPS/Office)
"""
import winreg
from enum import Enum
from typing import Optional
from logger_config import setup_logger

# 初始化日志
logger = setup_logger("PPTHost.Detector")


class PPTApplicationType(Enum):
    """应用类型枚举"""
    AUTO = "auto"
    WPS = "wps"
    OFFICE = "office"


class PPTApplicationDetector:
    """
    PowerPoint 应用程序检测器
    
    检测系统中已安装的演示软件,优先使用 Office PowerPoint
    """
    
    WPS_PROG_ID = "KWPP.Application"
    OFFICE_PROG_ID = "PowerPoint.Application"
    
    @staticmethod
    def is_prog_id_available(prog_id: str) -> bool:
        """
        检查指定的 ProgID 是否可用
        
        Args:
            prog_id: COM ProgID
            
        Returns:
            是否可用
        """
        try:
            # 尝试打开注册表键
            key_path = f"SOFTWARE\\Classes\\{prog_id}\\CLSID"
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, key_path, 0, winreg.KEY_READ):
                return True
        except WindowsError:
            try:
                # 尝试 HKEY_CURRENT_USER
                with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path, 0, winreg.KEY_READ):
                    return True
            except WindowsError:
                return False
    
    @classmethod
    def is_wps_installed(cls) -> bool:
        """
        检测 WPS 演示是否已安装
        
        Returns:
            是否已安装
        """
        return cls.is_prog_id_available(cls.WPS_PROG_ID)
    
    @classmethod
    def is_office_installed(cls) -> bool:
        """
        检测 Office PowerPoint 是否已安装
        
        Returns:
            是否已安装
        """
        return cls.is_prog_id_available(cls.OFFICE_PROG_ID)
    
    @classmethod
    def detect_best_available(cls) -> PPTApplicationType:
        """
        自动检测最佳可用应用 (优先 Office,其次 WPS)
        
        Returns:
            最佳可用应用类型
            
        Raises:
            RuntimeError: 未检测到任何可用应用
        """
        if cls.is_office_installed():
            return PPTApplicationType.OFFICE
        elif cls.is_wps_installed():
            return PPTApplicationType.WPS
        else:
            raise RuntimeError("未检测到 WPS 或 Office PowerPoint,请至少安装其中一个")
    
    @classmethod
    def get_prog_id(cls, app_type: PPTApplicationType) -> str:
        """
        获取指定应用类型的 ProgID
        
        Args:
            app_type: 应用类型
            
        Returns:
            ProgID 字符串
            
        Raises:
            RuntimeError: 指定应用未安装
        """
        if app_type == PPTApplicationType.AUTO:
            best_type = cls.detect_best_available()
            return cls.get_prog_id(best_type)
        
        elif app_type == PPTApplicationType.WPS:
            if not cls.is_wps_installed():
                raise RuntimeError("WPS 演示未安装")
            return cls.WPS_PROG_ID
        
        elif app_type == PPTApplicationType.OFFICE:
            if not cls.is_office_installed():
                raise RuntimeError("Office PowerPoint 未安装")
            return cls.OFFICE_PROG_ID
        
        else:
            raise ValueError(f"未知的应用类型: {app_type}")
    
    @classmethod
    def get_display_name(cls, app_type: PPTApplicationType) -> str:
        """
        获取应用类型的显示名称
        
        Args:
            app_type: 应用类型
            
        Returns:
            显示名称
        """
        if app_type == PPTApplicationType.AUTO:
            return "自动检测"
        elif app_type == PPTApplicationType.WPS:
            return "WPS 演示"
        elif app_type == PPTApplicationType.OFFICE:
            return "Office PowerPoint"
        else:
            return "未知"
    
    @classmethod
    def print_detection_info(cls):
        """打印检测信息"""
        logger.info("正在检测已安装的演示软件...")
        
        office_installed = cls.is_office_installed()
        wps_installed = cls.is_wps_installed()
        
        logger.info(f"Office PowerPoint: {'已安装 ✓' if office_installed else '未安装 ✗'}")
        logger.info(f"WPS 演示: {'已安装 ✓' if wps_installed else '未安装 ✗'}")
        
        if office_installed or wps_installed:
            best = cls.detect_best_available()
            logger.info(f"将使用: {cls.get_display_name(best)}")
        else:
            logger.error("错误: 未检测到任何可用的演示软件")
