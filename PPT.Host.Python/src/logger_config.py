"""
日志配置模块

提供统一的日志配置,支持控制台和文件输出
"""
import logging
import sys
import os
from pathlib import Path
from logging.handlers import RotatingFileHandler


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
    
    return logger

