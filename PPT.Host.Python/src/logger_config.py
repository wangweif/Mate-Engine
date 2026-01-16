"""
日志配置模块

提供统一的日志配置,支持控制台、文件和 SLS 输出
"""
import logging
import sys
import os
from pathlib import Path
from logging.handlers import RotatingFileHandler
from typing import Optional, Dict

# 加载 .env 文件
_env_loaded = False
try:
    from dotenv import load_dotenv
    
    # 确定 .env 文件的搜索路径
    env_paths = []
    
    # 1. 如果是打包后的 exe,优先从可执行文件所在目录加载
    if getattr(sys, 'frozen', False):
        # 打包后:可执行文件所在目录
        exe_dir = Path(sys.executable).parent
        env_paths.append(exe_dir / ".env")
    else:
        # 开发环境:项目根目录 (src 的父目录)
        env_paths.append(Path(__file__).parent.parent / ".env")
    
    # 2. 当前工作目录 (无论开发还是打包都检查)
    env_paths.append(Path.cwd() / ".env")
    
    # 3. 尝试加载第一个存在的 .env 文件
    for env_path in env_paths:
        if env_path.exists():
            load_dotenv(env_path, override=True)
            _env_loaded = True
            break
            
except ImportError:
    pass
except Exception:
    pass


# 全局 SLS Handler 单例
_sls_handler = None
_sls_handler_initialized = False


def _get_log_directory() -> Path:
    """
    获取日志目录路径
    
    在开发环境中,返回项目根目录下的 logs 文件夹
    在打包后的 exe 中,返回可执行文件所在目录下的 logs 文件夹
    
    Returns:
        日志目录路径
    """
    # 检查是否是打包后的 exe
    if getattr(sys, 'frozen', False):
        # 打包后:使用可执行文件所在目录
        exe_dir = Path(sys.executable).parent
        log_dir = exe_dir / "logs"
    else:
        # 开发环境:使用项目根目录
        log_dir = Path(__file__).parent.parent / "logs"
    
    return log_dir


def _get_sls_config() -> Optional[Dict[str, str]]:
    """
    从环境变量获取 SLS 配置
    
    Returns:
        SLS 配置字典,如果未启用则返回 None
    """
    # 检查是否启用 SLS
    if os.getenv("SLS_ENABLED", "false").lower() != "true":
        return None
    
    # 读取必需的配置项
    config = {
        "access_key_id": os.getenv("SLS_ACCESS_KEY_ID", ""),
        "access_key_secret": os.getenv("SLS_ACCESS_KEY_SECRET", ""),
        "endpoint": os.getenv("SLS_ENDPOINT", ""),
        "project": os.getenv("SLS_PROJECT", ""),
        "logstore": os.getenv("SLS_LOGSTORE", ""),
    }
    
    # 可选配置项
    config["topic"] = os.getenv("SLS_TOPIC", "")
    config["source"] = os.getenv("SLS_SOURCE", "PPTHost")
    config["min_level"] = os.getenv("SLS_MIN_LEVEL", "INFO")
    config["batch_size"] = int(os.getenv("SLS_BATCH_SIZE", "10"))
    config["flush_interval"] = float(os.getenv("SLS_FLUSH_INTERVAL", "5.0"))
    
    # 验证必需配置
    required_keys = ["access_key_id", "access_key_secret", "endpoint", "project", "logstore"]
    if not all(config[key] for key in required_keys):
        return None
    
    return config


def _get_or_create_sls_handler(formatter: logging.Formatter):
    """
    获取或创建全局 SLS Handler 单例
    
    Args:
        formatter: 日志格式化器
        
    Returns:
        SLS Handler 实例,如果未启用或初始化失败则返回 None
    """
    global _sls_handler, _sls_handler_initialized
    
    # 如果已经尝试过初始化,直接返回结果
    if _sls_handler_initialized:
        return _sls_handler
    
    # 标记为已初始化(无论成功与否)
    _sls_handler_initialized = True
    
    try:
        sls_config = _get_sls_config()
        if not sls_config:
            return None
        
        from sls_handler import SLSHandler
        
        # 解析日志级别
        min_level_str = sls_config.get("min_level", "INFO")
        min_level = getattr(logging, min_level_str.upper(), logging.INFO)
        
        # 创建 SLS Handler
        _sls_handler = SLSHandler(
            access_key_id=sls_config["access_key_id"],
            access_key_secret=sls_config["access_key_secret"],
            endpoint=sls_config["endpoint"],
            project=sls_config["project"],
            logstore=sls_config["logstore"],
            topic=sls_config.get("topic", ""),
            source=sls_config.get("source", "PPTHost"),
            batch_size=sls_config.get("batch_size", 10),
            flush_interval=sls_config.get("flush_interval", 5.0)
        )
        _sls_handler.setLevel(min_level)
        _sls_handler.setFormatter(formatter)
        
        return _sls_handler
    except Exception:
        # SLS 初始化失败,返回 None
        return None


def setup_logger(name: str = "PPTHost", log_level: int = logging.INFO) -> logging.Logger:
    """
    配置并返回日志记录器
    
    Args:
        name: 日志记录器名称
        log_level: 日志级别
        
    Returns:
        配置好的日志记录器
    """
    logger = logging.getLogger(name)
    
    # 避免重复配置
    if logger.handlers:
        return logger
    
    logger.setLevel(log_level)
    
    # 日志格式
    formatter = logging.Formatter(
        fmt='[%(asctime)s] [%(levelname)s] [%(name)s] %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    # 控制台处理器
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(log_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)
    
    # 文件处理器
    try:
        log_dir = _get_log_directory()
        log_dir.mkdir(exist_ok=True)
        
        log_file = log_dir / "ppt_host.log"
        file_handler = RotatingFileHandler(
            log_file,
            maxBytes=10 * 1024 * 1024,  # 10MB
            backupCount=5,
            encoding='utf-8'
        )
        file_handler.setLevel(log_level)
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)
        
        # 输出日志文件路径
        logger.info(f"日志文件: {log_file.absolute()}")
    except Exception as e:
        # 如果创建日志文件失败,只输出到控制台
        logger.warning(f"无法创建日志文件: {e},日志将仅输出到控制台")
    
    # SLS 处理器 (可选,使用全局单例)
    try:
        sls_handler = _get_or_create_sls_handler(formatter)
        if sls_handler:
            logger.addHandler(sls_handler)
            
            # 只在第一个 logger 初始化时输出 SLS 信息
            if name == "PPTHost.Main":
                sls_config = _get_sls_config()
                if sls_config:
                    logger.info(f"SLS 日志已启用: {sls_config['project']}/{sls_config['logstore']}")
    except Exception as e:
        # SLS 初始化失败不影响其他日志输出
        logger.warning(f"SLS 日志初始化失败: {e},将仅使用本地日志")
    
    return logger
