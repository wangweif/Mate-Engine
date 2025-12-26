using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Office = Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace PPT.Host
{
    /// <summary>
    /// PowerPoint COM 控制器 - 封装所有 PowerPoint 操作
    /// 所有对 PowerPoint COM 的调用都在单独的 STA 线程中执行，避免在非 STA 线程直接创建或调用 COM 对象导致阻塞或死锁。
    /// </summary>
    public class PowerPointController : IDisposable
    {
        private PowerPoint.Application _app;
        private PowerPoint.Presentation _presentation;
        private PowerPoint.SlideShowWindow _slideShowWindow;

        private readonly StaTaskRunner _sta;
        public event Action<int> SlideChanged;
        public event Action PresentationClosed;

        private bool _isDisposed = false;

        public PowerPointController()
        {
            _sta = new StaTaskRunner();
        }

        /// <summary>
        /// 打开 PowerPoint 演示文稿并开始放映
        /// 注意：所有 COM 操作在 STA 线程内部执行并在返回前同步完成（保持行为与原来的同步逻辑一致）
        /// </summary>
        public void OpenPresentation(string filePath)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(PowerPointController));

            try
            {
                Console.WriteLine($"[PPT] 步骤1: 开始打开 PPT - {filePath}");

                _sta.Invoke(() =>
                {
                    Console.WriteLine("[PPT] 步骤2: (STA) 创建 PowerPoint 应用程序实例...");
                    // 尝试获取已运行的实例，若不存在则创建新实例
                    try
                    {
                        // 如果 PowerPoint 已经运行，优先使用 GetActiveObject 避免再次创建实例
                        object existing = null;
                        try
                        {
                            existing = Marshal.GetActiveObject("PowerPoint.Application");
                        }
                        catch (COMException)
                        {
                            existing = null;
                        }

                        if (existing != null)
                        {
                            _app = (PowerPoint.Application)existing;
                        }
                        else
                        {
                            _app = new PowerPoint.Application();
                        }

                        _app.Visible = Office.MsoTriState.msoTrue;
                        Console.WriteLine("[PPT] 步骤2: PowerPoint 应用程序实例创建成功");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PPT] 创建 PowerPoint 实例失败: {ex.Message}");
                        throw;
                    }

                    Console.WriteLine("[PPT] 步骤3: (STA) 打开演示文稿文件...");
                    _presentation = _app.Presentations.Open(
                        filePath,
                        Office.MsoTriState.msoFalse,  // ReadOnly
                        Office.MsoTriState.msoFalse,  // Untitled
                        Office.MsoTriState.msoTrue    // WithWindow
                    );
                    Console.WriteLine("[PPT] 步骤3: 演示文稿文件打开成功");

                    Console.WriteLine("[PPT] 步骤4: (STA) 订阅事件...");
                    // 先取消订阅,避免重复订阅
                    _app.SlideShowNextSlide -= OnSlideShowNextSlide;
                    _app.SlideShowEnd -= OnSlideShowEnd;
                    // 重新订阅
                    _app.SlideShowNextSlide += OnSlideShowNextSlide;
                    _app.SlideShowEnd += OnSlideShowEnd;
                    Console.WriteLine("[PPT] 步骤4: 事件订阅成功");

                    Console.WriteLine("[PPT] 步骤5: (STA) 开始放映...");
                    _presentation.SlideShowSettings.Run();
                    Console.WriteLine("[PPT] 步骤5: 放映已启动");

                    Console.WriteLine("[PPT] 步骤6: (STA) 获取放映窗口...");
                    // SlideShowWindows 是 1-based 索引
                    _slideShowWindow = _app.SlideShowWindows.Count >= 1 ? _app.SlideShowWindows[1] : null;
                    Console.WriteLine("[PPT] 步骤6: 放映窗口获取成功");
                });

                Console.WriteLine($"[PPT] ✅ 成功打开: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] ❌ 打开失败: {ex.Message}");
                Console.WriteLine($"[PPT] 错误详情: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 下一张幻灯片
        /// </summary>
        public void NextSlide()
        {
            EnsureNotDisposed();

            try
            {
                _sta.Invoke(() =>
                {
                    if (_slideShowWindow != null)
                    {
                        _slideShowWindow.View.Next();
                        Console.WriteLine($"[PPT] 下一张 -> {_slideShowWindow.View.CurrentShowPosition}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 下一张失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 上一张幻灯片
        /// </summary>
        public void PreviousSlide()
        {
            EnsureNotDisposed();

            try
            {
                _sta.Invoke(() =>
                {
                    if (_slideShowWindow != null)
                    {
                        _slideShowWindow.View.Previous();
                        Console.WriteLine($"[PPT] 上一张 -> {_slideShowWindow.View.CurrentShowPosition}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 上一张失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 跳转到指定幻灯片
        /// </summary>
        public void GoToSlide(int slideNumber)
        {
            EnsureNotDisposed();

            try
            {
                _sta.Invoke(() =>
                {
                    if (_slideShowWindow != null)
                    {
                        _slideShowWindow.View.GotoSlide(slideNumber);
                        Console.WriteLine($"[PPT] 跳转到第 {slideNumber} 张");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 跳转失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取当前幻灯片编号
        /// </summary>
        public int GetCurrentSlide()
        {
            EnsureNotDisposed();

            try
            {
                return _sta.Invoke(() =>
                {
                    if (_slideShowWindow != null)
                    {
                        return _slideShowWindow.View.CurrentShowPosition;
                    }
                    return 0;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 获取页码失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 获取总幻灯片数
        /// </summary>
        public int GetTotalSlides()
        {
            EnsureNotDisposed();

            try
            {
                return _sta.Invoke(() =>
                {
                    if (_presentation != null)
                    {
                        return _presentation.Slides.Count;
                    }
                    return 0;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 获取总页数失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 关闭演示文稿
        /// </summary>
        public void ClosePresentation()
        {
            if (_isDisposed) return;

            try
            {
                _sta.Invoke(() =>
                {
                    try
                    {
                        if (_presentation != null)
                        {
                            _presentation.Close();
                            Console.WriteLine("[PPT] 演示文稿已关闭");
                        }

                        if (_app != null)
                        {
                            _app.Quit();
                            Console.WriteLine("[PPT] PowerPoint 已退出");
                        }
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"[PPT] ClosePresentation 内部错误: {exInner.Message}");
                    }
                    finally
                    {
                        ReleaseComObjects();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 关闭失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 幻灯片切换事件处理（在 STA 线程上被触发）
        /// </summary>
        private void OnSlideShowNextSlide(PowerPoint.SlideShowWindow Wn)
        {
            try
            {
                int currentSlide = Wn.View.CurrentShowPosition;
                Console.WriteLine($"[PPT] 幻灯片切换事件: 第 {currentSlide} 张");
                SlideChanged?.Invoke(currentSlide);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PPT] 幻灯片切换事件处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 放映结束事件处理（在 STA 线程上被触发）
        /// </summary>
        private void OnSlideShowEnd(PowerPoint.Presentation Pres)
        {
            Console.WriteLine("[PPT] 放映结束");
            PresentationClosed?.Invoke();
            
            // 放映结束后自动清理资源
            // 注意：PowerPoint 不允许在事件处理程序中调用 Quit()
            // 需要延迟到事件处理完成后执行
            Console.WriteLine("[PPT] 放映结束,准备延迟清理资源...");
            
            // 先取消事件订阅（这个可以在事件处理程序中执行）
            if (_app != null)
            {
                try
                {
                    _app.SlideShowNextSlide -= OnSlideShowNextSlide;
                    _app.SlideShowEnd -= OnSlideShowEnd;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PPT] 取消事件订阅时出错: {ex.Message}");
                }
            }
            
            // 异步延迟执行清理操作（在事件处理程序完成后）
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // 等待一小段时间确保事件处理完成
                Thread.Sleep(200);
                
                Console.WriteLine("[PPT] 开始执行延迟清理...");
                try
                {
                    _sta.Invoke(() =>
                    {
                        try
                        {
                            // 关闭演示文稿
                            if (_presentation != null)
                            {
                                try
                                {
                                    _presentation.Close();
                                    Console.WriteLine("[PPT] 演示文稿已关闭");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[PPT] 关闭演示文稿时出错: {ex.Message}");
                                }
                            }

                            // 退出PowerPoint
                            if (_app != null)
                            {
                                try
                                {
                                    _app.Quit();
                                    Console.WriteLine("[PPT] PowerPoint 已退出");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[PPT] 退出PowerPoint时出错: {ex.Message}");
                                }
                            }
                        }
                        finally
                        {
                            // 释放COM对象
                            ReleaseComObjects();
                        }
                    });
                    Console.WriteLine("[PPT] 延迟清理完成");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PPT] 延迟清理失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 释放 COM 对象（必须在 STA 线程上释放）
        /// </summary>
        private void ReleaseComObjects()
        {
            // 该方法仅在 STA 线程调用（已由调用方保证）
            try
            {
                if (_slideShowWindow != null)
                {
                    try { Marshal.ReleaseComObject(_slideShowWindow); } catch { }
                    _slideShowWindow = null;
                }

                if (_presentation != null)
                {
                    try { Marshal.ReleaseComObject(_presentation); } catch { }
                    _presentation = null;
                }

                if (_app != null)
                {
                    try { Marshal.ReleaseComObject(_app); } catch { }
                    _app = null;
                }
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                ClosePresentation();
                _sta.Dispose();
                _isDisposed = true;
            }
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(PowerPointController));
        }

        /// <summary>
        /// 简单的 STA 线程任务运行器：将所有委托排入队列并在 STA 线程上执行。
        /// 提供同步 Invoke 返回值支持。
        /// </summary>
        private sealed class StaTaskRunner : IDisposable
        {
            private readonly Thread _thread;
            private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

            public StaTaskRunner()
            {
                _thread = new Thread(Run) { IsBackground = true };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            private void Run()
            {
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // 单个任务异常由任务内负责处理，避免整个线程退出
                    }
                }
            }

            // 同步执行，无返回值
            public void Invoke(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                using (var ev = new ManualResetEventSlim(false))
                {
                    Exception captured = null;
                    _queue.Add(() =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            captured = ex;
                        }
                        finally
                        {
                            ev.Set();
                        }
                    });
                    ev.Wait();
                    if (captured != null) throw new TargetInvocationException("STA task failed", captured);
                }
            }

            // 同步执行，有返回值
            public T Invoke<T>(Func<T> func)
            {
                if (func == null) throw new ArgumentNullException(nameof(func));
                using (var ev = new ManualResetEventSlim(false))
                {
                    Exception captured = null;
                    T result = default;
                    _queue.Add(() =>
                    {
                        try
                        {
                            result = func();
                        }
                        catch (Exception ex)
                        {
                            captured = ex;
                        }
                        finally
                        {
                            ev.Set();
                        }
                    });
                    ev.Wait();
                    if (captured != null) throw new TargetInvocationException("STA task failed", captured);
                    return result;
                }
            }

            public void Dispose()
            {
                _queue.CompleteAdding();
                if (!_thread.Join(5000))
                {
                    try { _thread.Interrupt(); } catch { }
                }
                _queue.Dispose();
            }
        }
    }
}