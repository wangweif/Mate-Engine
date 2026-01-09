"""
PPT Host - Python 版本

Unity 与 PowerPoint 之间的通信服务
监听 TCP 端口 45678,接收 Unity 命令并控制 PowerPoint
"""
import sys
import signal
import argparse
import pythoncom
from ppt_controller import PowerPointController
from tcp_server import TCPServer
from ppt_detector import PPTApplicationType
from logger_config import setup_logger

# 初始化日志
logger = setup_logger("PPTHost.Main")


class PPTHost:
    """PPT Host 主程序"""
    
    def __init__(self, app_type: PPTApplicationType = PPTApplicationType.AUTO):
        """
        初始化
        
        Args:
            app_type: 应用类型 (AUTO/WPS/OFFICE)
        """
        self.app_type = app_type
        self.ppt_controller = PowerPointController(app_type)
        self.tcp_server = TCPServer()
        
        # 设置 PowerPoint 事件处理函数
        self.ppt_controller.set_event_handlers(
            on_slide_changed=self._on_slide_changed,
            on_presentation_closed=self._on_presentation_closed
        )
    
    def _on_slide_changed(self, slide_number: int):
        """幻灯片切换事件处理"""
        logger.info(f"幻灯片切换到第 {slide_number} 张")
        self.tcp_server.send_event(f"SLIDE_CHANGED|{slide_number}")
    
    def _on_presentation_closed(self):
        """演示结束事件处理"""
        logger.info("演示已结束")
        self.tcp_server.send_event("PRESENTATION_CLOSED")
    
    def process_command(self, command: str) -> str:
        """
        处理客户端命令
        
        Args:
            command: 命令字符串
            
        Returns:
            响应字符串
        """
        try:
            # 移除 BOM 字符
            command = command.lstrip('\ufeff')
            
            # 解析命令
            parts = command.split('|')
            cmd = parts[0].upper()
            
            # 处理各种命令
            if cmd == "OPEN":
                return self._handle_open(parts)
            elif cmd == "NEXT":
                return self._handle_next()
            elif cmd == "PREV":
                return self._handle_previous()
            elif cmd == "GOTO":
                return self._handle_goto(parts)
            elif cmd == "GET_PAGE":
                return self._handle_get_page()
            elif cmd == "CLOSE":
                return self._handle_close()
            elif cmd == "PING":
                return "OK|PONG"
            elif cmd == "SHUTDOWN":
                return "OK|Shutting down"
            else:
                return f"ERROR|Unknown command: {cmd}"
        
        except Exception as e:
            logger.error(f"处理命令失败: {e}", exc_info=True)
            return f"ERROR|{e}"
    
    def _handle_open(self, parts: list) -> str:
        """处理 OPEN 命令"""
        if len(parts) < 2:
            return "ERROR|Missing file path"
        
        file_path = parts[1]
        total_slides = self.ppt_controller.open_presentation(file_path)
        return f"OK|Opened|{total_slides}"
    
    def _handle_next(self) -> str:
        """处理 NEXT 命令"""
        current = self.ppt_controller.next_slide()
        return f"OK|{current}"
    
    def _handle_previous(self) -> str:
        """处理 PREV 命令"""
        current = self.ppt_controller.previous_slide()
        return f"OK|{current}"
    
    def _handle_goto(self, parts: list) -> str:
        """处理 GOTO 命令"""
        if len(parts) < 2:
            return "ERROR|Missing slide number"
        
        slide_number = int(parts[1])
        current = self.ppt_controller.goto_slide(slide_number)
        return f"OK|{current}"
    
    def _handle_get_page(self) -> str:
        """处理 GET_PAGE 命令"""
        current = self.ppt_controller.get_current_slide()
        total = self.ppt_controller.get_total_slides()
        return f"OK|{current}|{total}"
    
    def _handle_close(self) -> str:
        """处理 CLOSE 命令"""
        self.ppt_controller.close_presentation()
        return "OK|Closed"
    
    def run(self):
        """运行主程序"""
        # 设置信号处理器
        def signal_handler(sig, frame):
            logger.info("收到中断信号,正在退出...")
            self.tcp_server.is_running = False
            sys.exit(0)
        
        signal.signal(signal.SIGINT, signal_handler)
        
        logger.info("=" * 50)
        logger.info("  PPT Host - Python 版本")
        logger.info("  监听端口: 45678")
        logger.info("=" * 50)
        
        # 检测已安装的演示软件
        from ppt_detector import PPTApplicationDetector
        try:
            PPTApplicationDetector.print_detection_info(self.app_type)
        except Exception as e:
            logger.error(f"检测失败: {e}")
        
        # 初始化 COM
        pythoncom.CoInitialize()
        logger.info("COM 已初始化")
        
        try:
            # 启动 TCP 服务器
            self.tcp_server.start(self.process_command)
        
        except KeyboardInterrupt:
            logger.info("收到中断信号")
        
        except Exception as e:
            logger.error(f"运行出错: {e}", exc_info=True)
        
        finally:
            # 清理资源
            logger.info("正在清理资源...")
            
            try:
                self.ppt_controller.close_presentation()
            except:
                pass
            
            try:
                self.tcp_server.stop()
            except:
                pass
            
            # 清理 COM
            try:
                pythoncom.CoUninitialize()
                logger.info("COM 已清理")
            except:
                pass
            
            logger.info("已退出")


def parse_arguments():
    """解析命令行参数"""
    parser = argparse.ArgumentParser(
        description='PPT Host - Unity 与 PowerPoint 通信服务',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  python main.py              # 自动检测 (优先 Office)
  python main.py --app=office # 强制使用 Office PowerPoint
  python main.py --app=wps    # 强制使用 WPS 演示
        """
    )
    
    parser.add_argument(
        '--app',
        type=str,
        choices=['auto', 'office', 'wps'],
        default='auto',
        help='指定使用的应用程序 (默认: auto)'
    )
    
    return parser.parse_args()


def main():
    """程序入口"""
    # 解析命令行参数
    args = parse_arguments()
    
    # 转换为枚举类型
    app_type_map = {
        'auto': PPTApplicationType.AUTO,
        'office': PPTApplicationType.OFFICE,
        'wps': PPTApplicationType.WPS
    }
    app_type = app_type_map[args.app]
    
    # 创建并运行 Host
    host = PPTHost(app_type)
    host.run()


if __name__ == "__main__":
    main()
