"""
PowerPoint 控制器模块

使用 win32com 控制 PowerPoint 应用程序,支持:
- 打开和控制 PPT 演示
- 监听幻灯片切换和演示结束事件
- 自动清理资源和进程
"""
import os
import gc
import time
import threading
from typing import Optional, Callable

# 修复 PyInstaller 打包后 win32com 的问题
import tempfile
import win32com
if not hasattr(win32com, '__path__'):
    gen_py_dir = os.path.join(tempfile.gettempdir(), "gen_py")
    os.makedirs(gen_py_dir, exist_ok=True)
    win32com.__path__ = [gen_py_dir]

import win32com.client
import pythoncom
from logger_config import setup_logger

# 初始化日志
logger = setup_logger("PPTHost.Controller")


class PowerPointController:
    """
    PowerPoint COM 控制器
    
    负责创建 PowerPoint 实例、打开演示文稿、控制幻灯片翻页,
    并监听用户在 PowerPoint 窗口中的操作事件。
    """
    
    def __init__(self, app_type: 'PPTApplicationType' = None):
        """
        初始化控制器
        
        Args:
            app_type: 应用类型,如果为 None 则自动检测
        """
        from ppt_detector import PPTApplicationType
        
        # COM 对象 (分离 app 和 app_events)
        self.app: Optional[object] = None
        self.app_events: Optional[object] = None  # 事件代理对象
        self.presentation: Optional[object] = None
        self.slideshow_window: Optional[object] = None
        self.ppt_process_id: Optional[int] = None
        
        # 应用类型
        self.app_type = app_type if app_type else PPTApplicationType.AUTO
        
        # 事件回调
        self.on_slide_changed: Optional[Callable[[int], None]] = None
        self.on_presentation_closed: Optional[Callable[[], None]] = None
        
        # 关闭标志 (用于事件回调中设置标志位)
        self._need_close = False
    
    def set_event_handlers(self, 
                          on_slide_changed: Optional[Callable[[int], None]] = None,
                          on_presentation_closed: Optional[Callable[[], None]] = None):
        """
        设置事件处理函数
        
        Args:
            on_slide_changed: 幻灯片切换回调,参数为页码
            on_presentation_closed: 演示结束回调,无参数
        """
        self.on_slide_changed = on_slide_changed
        self.on_presentation_closed = on_presentation_closed
    
    def open_presentation(self, file_path: str) -> int:
        """
        打开 PowerPoint 演示文稿并开始放映
        
        Args:
            file_path: PPT 文件路径
            
        Returns:
            总幻灯片数
            
        Raises:
            FileNotFoundError: 文件不存在
            Exception: 打开失败
        """
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"文件不存在: {file_path}")
        
        try:
            logger.info(f"正在打开: {file_path}")
            
            # 创建 PowerPoint 实例并订阅事件
            self._create_powerpoint_instance()
            
            # 记录进程 ID
            self._record_process_id()
            
            # 打开文件
            self.presentation = self.app.Presentations.Open(
                FileName=file_path,
                ReadOnly=1,  # 只读模式
                Untitled=0,
                WithWindow=True
            )
            logger.info(f"文件已打开,共 {self.presentation.Slides.Count} 张幻灯片")
            
            # 开始放映
            self.presentation.SlideShowSettings.Run()
            logger.info("幻灯片放映已启动")
            
            # 获取放映窗口
            if self.app.SlideShowWindows.Count > 0:
                self.slideshow_window = self.app.SlideShowWindows(1)
                logger.info("放映窗口已获取")
                
                # 验证 View 对象
                try:
                    _ = self.slideshow_window.View
                    logger.debug("View 对象验证成功")
                except Exception as e:
                    logger.warning(f"View 对象访问失败: {e}")
            
            return self.presentation.Slides.Count
            
        except Exception as e:
            logger.error(f"打开失败: {e}")
            self.close_presentation()
            raise
    
    def _create_powerpoint_instance(self):
        """创建 PowerPoint 实例并订阅事件"""
        from ppt_detector import PPTApplicationDetector, PPTApplicationType
        
        # 获取 ProgID
        try:
            prog_id = PPTApplicationDetector.get_prog_id(self.app_type)
            app_name = PPTApplicationDetector.get_display_name(self.app_type)
            logger.info(f"使用 {app_name} ({prog_id})")
        except RuntimeError as e:
            logger.error(f"检测失败: {e}")
            raise
        
        try:
            # 定义事件处理类
            controller_ref = self
            
            class PPTAppEvents:
                """PowerPoint 应用程序事件处理器"""
                
                def OnSlideShowNextSlide(self, Wn):
                    """幻灯片切换事件 - 只设置标志位,不执行 Close/Quit"""
                    try:
                        current = Wn.View.Slide.SlideIndex
                        logger.info(f"幻灯片切换到第 {current} 张")
                        if controller_ref.on_slide_changed:
                            controller_ref.on_slide_changed(current)
                    except Exception as e:
                        logger.error(f"处理切换事件失败: {e}")
                
                def OnSlideShowEnd(self, Pres):
                    """演示结束事件 - 只设置标志位,不执行 Close/Quit"""
                    logger.info("演示已结束")
                    controller_ref._need_close = True
                    if controller_ref.on_presentation_closed:
                        controller_ref.on_presentation_closed()
            
            # 使用 gencache 确保类型信息
            from win32com.client import gencache
            self.app = gencache.EnsureDispatch(prog_id)
            
            # 包装事件 (保存到 app_events,不覆盖 app)
            try:
                self.app_events = win32com.client.DispatchWithEvents(self.app, PPTAppEvents)
                logger.info("PowerPoint 实例已创建并订阅事件")
            except Exception as e:
                logger.warning(f"事件订阅失败: {e},将继续运行但无事件支持")
            
        except Exception as e:
            # 降级到普通 Dispatch
            self.app = win32com.client.Dispatch(prog_id)
            logger.warning(f"PowerPoint 实例已创建 (无事件): {e}")
        
        self.app.Visible = True
    
    def _record_process_id(self):
        """记录 PowerPoint 进程 ID,用于后续强制终止"""
        try:
            import psutil
            
            # 查找 PowerPoint 进程
            for proc in psutil.process_iter(['pid', 'name']):
                try:
                    if 'POWERPNT.EXE' in proc.info['name'].upper():
                        self.ppt_process_id = proc.info['pid']
                        logger.info(f"记录进程 ID: {self.ppt_process_id}")
                        break
                except:
                    continue
                    
        except ImportError:
            logger.warning("未安装 psutil,无法记录进程 ID")
        except Exception as e:
            logger.warning(f"无法获取进程 ID: {e}")
    
    def unsubscribe_events(self):
        """取消订阅 PowerPoint 事件"""
        if self.app_events is None:
            return
        
        try:
            logger.info("正在取消订阅事件...")
            
            # 1) 断开 Python 引用
            self.app_events = None
            
            # 2) 强制垃圾回收,触发 COM 释放
            gc.collect()
            
            # 3) 清理 COM 消息泵里待处理事件
            try:
                pythoncom.PumpWaitingMessages()
            except Exception:
                pass
            
            logger.info("事件订阅已取消")
            
        except Exception as e:
            logger.error(f"取消订阅事件失败: {e}")
    
    def next_slide(self) -> int:
        """
        下一张幻灯片
        
        Returns:
            当前幻灯片编号
        """
        if not self.slideshow_window:
            return 0
        
        try:
            self.slideshow_window.View.Next()
            current = self._get_current_slide_index()
            logger.info(f"下一张 -> {current}")
            return current
        except Exception as e:
            logger.error(f"下一张失败: {e}")
            raise
    
    def previous_slide(self) -> int:
        """
        上一张幻灯片
        
        Returns:
            当前幻灯片编号
        """
        if not self.slideshow_window:
            return 0
        
        try:
            self.slideshow_window.View.Previous()
            current = self._get_current_slide_index()
            logger.info(f"上一张 -> {current}")
            return current
        except Exception as e:
            logger.error(f"上一张失败: {e}")
            raise
    
    def goto_slide(self, slide_number: int) -> int:
        """
        跳转到指定幻灯片
        
        Args:
            slide_number: 幻灯片编号 (1-based)
            
        Returns:
            当前幻灯片编号
        """
        if not self.slideshow_window:
            return 0
        
        try:
            self.slideshow_window.View.GotoSlide(slide_number)
            logger.info(f"跳转到第 {slide_number} 张")
            return slide_number
        except Exception as e:
            logger.error(f"跳转失败: {e}")
            raise
    
    def get_current_slide(self) -> int:
        """
        获取当前幻灯片编号
        
        Returns:
            当前幻灯片编号
        """
        if not self.slideshow_window:
            return 0
        
        try:
            return self._get_current_slide_index()
        except:
            return 0
    
    def get_total_slides(self) -> int:
        """
        获取总幻灯片数
        
        Returns:
            总幻灯片数
        """
        if not self.presentation:
            return 0
        
        try:
            return self.presentation.Slides.Count
        except:
            return 0
    
    def _get_current_slide_index(self) -> int:
        """
        获取当前幻灯片索引
        
        优先使用 Slide.SlideIndex,如果失败则使用 CurrentShowPosition
        """
        try:
            current = self.slideshow_window.View.CurrentShowPosition
            
            # 如果返回 0,尝试使用 Slide.SlideIndex
            if current == 0:
                try:
                    current = self.slideshow_window.View.Slide.SlideIndex
                    logger.debug(f"使用 Slide.SlideIndex 获取页码: {current}")
                except:
                    pass
            
            return current
        except:
            return 0
    
    def close_presentation(self):
        """关闭演示文稿并释放所有资源"""
        logger.info("正在关闭...")
        
        try:
            # 1) 先取消事件订阅 (避免 Quit/Close 时又触发事件回调)
            self.unsubscribe_events()
            
            # 2) 关闭演示文稿
            if self.presentation:
                try:
                    self.presentation.Close()
                    logger.info("演示文稿已关闭")
                except Exception as e:
                    logger.error(f"关闭演示文稿时出错: {e}")
            
            # 3) 退出 PowerPoint (同步执行,确保完成)
            if self.app:
                try:
                    self.app.Quit()
                    logger.info("PowerPoint 已退出")
                except Exception as e:
                    logger.error(f"退出 PowerPoint 时出错: {e}")
        
        finally:
            # 4) 释放 COM 对象
            self._release_com_objects()
            
            # 5) 强制垃圾回收
            self._force_garbage_collection()
            
            # 6) 强制终止进程 (如果需要)
            # self._kill_process_if_needed()
            
            # 7) 重置关闭标志
            self._need_close = False
            
            logger.info("资源已释放")
    
    
    def _release_com_objects(self):
        """释放 COM 对象 (参考 C# 的 ReleaseComObjects 方法)"""
        try:
            # 按照从内到外的顺序释放
            if self.slideshow_window:
                try:
                    del self.slideshow_window
                except:
                    pass
                self.slideshow_window = None
            
            if self.presentation:
                try:
                    del self.presentation
                except:
                    pass
                self.presentation = None
            
            # 释放事件代理对象
            if self.app_events:
                try:
                    del self.app_events
                except:
                    pass
                self.app_events = None
            
            if self.app:
                try:
                    del self.app
                except:
                    pass
                self.app = None
                
        except Exception as e:
            logger.error(f"释放 COM 对象时出错: {e}")
    
    def _force_garbage_collection(self):
        """强制垃圾回收 (参考 C# 的双重 GC.Collect)"""
        gc.collect()
        time.sleep(0.1)
        gc.collect()
        logger.debug("COM 对象已释放,等待进程退出...")
    
    def _kill_process_if_needed(self):
        """检查并强制终止 PowerPoint 进程"""
        if not self.ppt_process_id:
            return
        
        try:
            # 等待进程自然退出
            time.sleep(1.0)
            
            # 检查进程是否还在运行
            try:
                import psutil
                if psutil.pid_exists(self.ppt_process_id):
                    proc = psutil.Process(self.ppt_process_id)
                    if 'POWERPNT.EXE' in proc.name().upper():
                        proc.kill()
                        logger.info(f"强制终止进程 {self.ppt_process_id}")
                    else:
                        logger.warning(f"进程 {self.ppt_process_id} 已被其他程序占用")
                else:
                    logger.info(f"进程 {self.ppt_process_id} 已自然退出")
            except ImportError:
                # 降级使用 win32api
                self._kill_process_with_win32api()
            
            self.ppt_process_id = None
            
        except Exception as e:
            logger.error(f"终止进程时出错: {e}")
    
    def _kill_process_with_win32api(self):
        """使用 win32api 终止进程"""
        try:
            import win32api
            import win32con
            
            handle = win32api.OpenProcess(
                win32con.PROCESS_TERMINATE, 
                False, 
                self.ppt_process_id
            )
            if handle:
                win32api.TerminateProcess(handle, 0)
                win32api.CloseHandle(handle)
                logger.info(f"强制终止进程 {self.ppt_process_id}")
        except:
            logger.info(f"进程 {self.ppt_process_id} 已自然退出")
