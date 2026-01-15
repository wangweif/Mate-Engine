"""
设备信息工具模块

提供获取设备唯一标识等功能
"""
import platform
import uuid
import hashlib
import subprocess
from typing import Optional


def get_device_id() -> str:
    """
    获取设备唯一标识
    优先使用 PowerShell Get-CimInstance 获取系统 UUID
    
    Returns:
        设备唯一标识字符串
    """
    try:
        # 使用 PowerShell Get-CimInstance 获取 UUID (兼容性更好)
        cmd = "powershell -Command \"Get-CimInstance Win32_ComputerSystemProduct | Select-Object -ExpandProperty UUID\""
        creationflags = 0x08000000 if platform.system() == "Windows" else 0
        
        output = subprocess.check_output(
            cmd, 
            shell=True, 
            creationflags=creationflags,
            stderr=subprocess.DEVNULL
        ).decode().strip()
        
        # 移除 UUID 中的连字符
        uuid_str = output.replace('-', '')
        if uuid_str:
            return uuid_str
    except Exception:
        pass


def get_device_info() -> dict:
    """
    获取设备详细信息
    
    Returns:
        包含设备信息的字典
    """
    return {
        "device_id": get_device_id(),
        "hostname": platform.node(),
        "os": platform.system(),
        "os_version": platform.version(),
        "os_release": platform.release(),
        "machine": platform.machine(),
        "processor": platform.processor(),
    }


# 缓存设备 ID,避免重复计算
_cached_device_id: Optional[str] = None


def get_cached_device_id() -> str:
    """
    获取缓存的设备 ID
    
    Returns:
        设备唯一标识
    """
    global _cached_device_id
    if _cached_device_id is None:
        _cached_device_id = get_device_id()
    return _cached_device_id


if __name__ == "__main__":
    # 测试代码
    print("设备信息:")
    print("-" * 50)
    info = get_device_info()
    for key, value in info.items():
        print(f"{key:15s}: {value}")
