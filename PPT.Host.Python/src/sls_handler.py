"""
阿里云 SLS 日志处理器

提供自定义的 logging.Handler,支持批量缓冲和异步发送日志到阿里云 SLS
"""
import logging
import time
import threading
import queue
from typing import Optional, List
from aliyun.log import LogClient, PutLogsRequest, LogItem


class SLSHandler(logging.Handler):
    """
    阿里云 SLS 日志处理器
    
    特性:
    - 批量缓冲: 减少网络请求
    - 异步发送: 独立线程处理,不阻塞主逻辑
    - 自动重试: 网络异常时自动重试
    - 优雅关闭: 确保缓冲日志全部发送
    """
    
    def __init__(
        self,
        access_key_id: str,
        access_key_secret: str,
        endpoint: str,
        project: str,
        logstore: str,
        topic: str = "",
        source: str = "PPTHost",
        batch_size: int = 10,
        flush_interval: float = 5.0,
        max_retries: int = 3
    ):
        """
        初始化 SLS Handler
        
        Args:
            access_key_id: 阿里云 AK ID
            access_key_secret: 阿里云 AK Secret
            endpoint: SLS 服务端点
            project: SLS 项目名
            logstore: SLS 日志库名
            topic: 日志主题 (可选)
            source: 日志来源 (默认 "PPTHost")
            batch_size: 批量发送大小 (默认 10 条)
            flush_interval: 刷新间隔秒数 (默认 5 秒)
            max_retries: 最大重试次数 (默认 3 次)
        """
        super().__init__()
        
        # SLS 配置
        self.client = LogClient(endpoint, access_key_id, access_key_secret)
        self.project = project
        self.logstore = logstore
        self.topic = topic
        self.source = source
        self.batch_size = batch_size
        self.flush_interval = flush_interval
        self.max_retries = max_retries
        
        # 缓冲队列
        self.log_queue: queue.Queue = queue.Queue()
        self.buffer: List[LogItem] = []
        self.last_flush_time = time.time()
        
        # 线程控制
        self.is_running = True
        self.worker_thread = threading.Thread(target=self._worker_thread, daemon=True)
        self.worker_thread.start()
        
        # 统计信息
        self.sent_count = 0
        self.error_count = 0
    
    def emit(self, record: logging.LogRecord):
        """
        发送日志记录
        
        Args:
            record: 日志记录对象
        """
        try:
            from device_info import get_cached_device_id, get_device_info
            device_id = get_cached_device_id()
            device_info = get_device_info()
            
            log_level_map = {
                "DEBUG": "INFO",
                "INFO": "INFO",
                "WARNING": "WARNING",
                "ERROR": "ERROR",
                "CRITICAL": "ERROR"
            }
            log_level = log_level_map.get(record.levelname, "INFO")
            log_type = record.name if record.name else record.module
            
            from datetime import datetime
            timestamp = datetime.fromtimestamp(record.created).strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
            
            device_model = f"{device_info.get('hostname', 'Unknown')} ({device_info.get('os', 'Unknown')} {device_info.get('os_release', '')})"
            
            log_item = LogItem(timestamp=int(record.created))
            log_item.set_contents([
                ("LogLevel", log_level),
                ("LogType", log_type),
                ("Message", self.format(record)),
                ("Timestamp", timestamp),
                ("DeviceId", device_id),
                ("DeviceModel", device_model),
            ])
            
            # 添加异常信息 (如果有)
            if record.exc_info:
                exception_text = self.formatter.formatException(record.exc_info) if self.formatter else str(record.exc_info)
                log_item.push_back("StackTrace", exception_text)
            
            # 放入队列
            self.log_queue.put(log_item, block=False)
            
        except Exception:
            # 避免日志处理器本身抛出异常
            self.handleError(record)
    
    def _worker_thread(self):
        """后台工作线程,负责批量发送日志"""
        while self.is_running or not self.log_queue.empty():
            try:
                # 从队列获取日志 (带超时)
                try:
                    log_item = self.log_queue.get(timeout=0.5)
                    self.buffer.append(log_item)
                except queue.Empty:
                    pass
                
                # 检查是否需要发送
                current_time = time.time()
                should_flush = (
                    len(self.buffer) >= self.batch_size or
                    (self.buffer and current_time - self.last_flush_time >= self.flush_interval)
                )
                
                if should_flush:
                    self._send_logs()
                    self.last_flush_time = current_time
                
            except Exception as e:
                # 记录错误但继续运行
                self.error_count += 1
                # 使用标准错误输出,避免循环
                import sys
                print(f"[SLSHandler] Worker thread error: {e}", file=sys.stderr)
    
    def _send_logs(self):
        """批量发送日志到 SLS"""
        if not self.buffer:
            return
        
        # 获取当前缓冲区
        logs_to_send = self.buffer[:]
        self.buffer.clear()
        
        # 重试发送
        for attempt in range(self.max_retries):
            try:
                request = PutLogsRequest(
                    self.project,
                    self.logstore,
                    topic=self.topic,
                    source=self.source,
                    logitems=logs_to_send,
                    compress=True  # 启用压缩
                )
                self.client.put_logs(request)
                self.sent_count += len(logs_to_send)
                return  # 成功发送,退出
                
            except Exception as e:
                if attempt < self.max_retries - 1:
                    # 等待后重试
                    time.sleep(0.5 * (attempt + 1))
                else:
                    # 最后一次重试失败,记录错误
                    self.error_count += 1
                    import sys
                    print(f"[SLSHandler] Failed to send {len(logs_to_send)} logs after {self.max_retries} attempts: {e}", file=sys.stderr)
    
    def flush(self):
        """立即刷新缓冲区"""
        if self.buffer:
            self._send_logs()
    
    def close(self):
        """
        关闭处理器,尝试发送剩余日志
        
        由于工作线程是 daemon 线程,主程序退出时会自动终止
        """
        # 停止工作线程
        self.is_running = False
        
        # 尝试发送剩余日志(不阻塞)
        try:
            if self.buffer:
                self._send_logs()
        except:
            pass
        
        # 调用父类关闭
        super().close()
    
    def get_stats(self) -> dict:
        """
        获取统计信息
        
        Returns:
            统计信息字典
        """
        return {
            "sent_count": self.sent_count,
            "error_count": self.error_count,
            "queue_size": self.log_queue.qsize(),
            "buffer_size": len(self.buffer)
        }
