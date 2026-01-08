"""
TCP 服务器模块

监听端口 45678,接收 Unity 客户端命令并处理
"""
import socket
import threading
import pythoncom
from typing import Callable, Optional


class TCPServer:
    """
    TCP 服务器
    
    负责监听客户端连接、接收命令、发送响应和事件通知。
    在接收循环中处理 Windows 消息泵,以支持 COM 事件。
    """
    
    def __init__(self, host: str = "127.0.0.1", port: int = 45678):
        """
        初始化 TCP 服务器
        
        Args:
            host: 监听地址
            port: 监听端口
        """
        self.host = host
        self.port = port
        self.server_socket: Optional[socket.socket] = None
        self.client_socket: Optional[socket.socket] = None
        self.is_running = False
        self.command_handler: Optional[Callable[[str], str]] = None
    
    def start(self, command_handler: Callable[[str], str]):
        """
        启动服务器并等待客户端连接
        
        Args:
            command_handler: 命令处理函数,接收命令字符串,返回响应字符串
        """
        self.command_handler = command_handler
        self.is_running = True
        
        try:
            # 创建服务器 socket
            self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            # 设置超时,以便可以响应 Ctrl+C
            self.server_socket.settimeout(1.0)
            self.server_socket.bind((self.host, self.port))
            self.server_socket.listen(1)
            
            print(f"[服务器] 已启动,监听 {self.host}:{self.port}")
            print("[服务器] 等待 Unity 连接... (按 Ctrl+C 退出)")
            
            # 等待客户端连接 (带超时循环)
            while self.is_running:
                try:
                    self.client_socket, client_address = self.server_socket.accept()
                    print(f"[服务器] Unity 已连接: {client_address}")
                    
                    # 发送就绪消息
                    self.send_response("OK|PPT Host Ready")
                    
                    # 处理客户端命令
                    self._handle_client()
                    break
                    
                except socket.timeout:
                    # 超时是正常的,继续循环
                    continue
                except KeyboardInterrupt:
                    print("\n[服务器] 收到中断信号")
                    break
            
        except Exception as e:
            print(f"[服务器] 启动失败: {e}")
        finally:
            self.stop()
    
    def _handle_client(self):
        """
        处理客户端命令
        
        在接收循环中调用 PumpWaitingMessages() 处理 Windows 消息,
        以支持 PowerPoint COM 事件。
        """
        buffer = ""
        
        # 设置 socket 超时,以便可以处理消息泵
        self.client_socket.settimeout(0.1)
        
        while self.is_running:
            try:
                # 处理 Windows 消息 (用于 COM 事件)
                pythoncom.PumpWaitingMessages()
                
                # 接收数据
                try:
                    data = self.client_socket.recv(4096)
                    if not data:
                        break
                    
                    # 解码并添加到缓冲区
                    buffer += data.decode('utf-8')
                
                except socket.timeout:
                    # 超时是正常的,继续循环处理消息
                    continue
                except Exception as e:
                    # 其他错误则退出
                    if self.is_running:
                        print(f"[服务器] 接收数据异常: {e}")
                    break
                
                # 处理完整的命令行
                while '\n' in buffer:
                    line, buffer = buffer.split('\n', 1)
                    command = line.strip()
                    
                    if command:
                        print(f"[收到命令] {command}")
                        
                        # 调用命令处理函数
                        if self.command_handler:
                            response = self.command_handler(command)
                            if response:
                                self.send_response(response)
                        
                        # 检查是否需要关闭
                        if command.upper() == "SHUTDOWN":
                            self.is_running = False
                            break
            
            except Exception as e:
                print(f"[服务器] 处理命令时出错: {e}")
                break
        
        print("[服务器] Unity 已断开")
    
    def send_response(self, message: str):
        """
        发送响应消息到客户端
        
        Args:
            message: 响应消息
        """
        if self.client_socket:
            try:
                self.client_socket.sendall((message + '\n').encode('utf-8'))
                print(f"[发送响应] {message}")
            except Exception as e:
                print(f"[服务器] 发送响应失败: {e}")
    
    def send_event(self, event: str):
        """
        发送事件通知到客户端
        
        Args:
            event: 事件消息
        """
        self.send_response(f"EVENT|{event}")
    
    def stop(self):
        """停止服务器并关闭连接"""
        print("[服务器] 正在关闭...")
        self.is_running = False
        
        if self.client_socket:
            try:
                self.client_socket.close()
            except:
                pass
            self.client_socket = None
        
        if self.server_socket:
            try:
                self.server_socket.close()
            except:
                pass
            self.server_socket = None
        
        print("[服务器] 已关闭")
