using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PPT.Host
{
    /// <summary>
    /// PPT Host 主程序 - TCP 服务器
    /// </summary>
    class Program
    {
        private static PowerPointController _pptController;
        private static TcpListener _listener;
        private static TcpClient _client;
        private static StreamWriter _writer;
        private static bool _isRunning = true;
        private const int PORT = 45678;

        [STAThread]  // PowerPoint COM 需要 STA 线程模型
        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  PPT Host - PowerPoint 控制服务");
            Console.WriteLine("  监听端口: " + PORT);
            Console.WriteLine("===========================================");
            Console.WriteLine();

            // 解析命令行参数
            PPTApplicationType appType = ParseApplicationType(args);
            
            // 显示系统安装情况
            Console.WriteLine(PPTApplicationDetector.GetInstallationInfo());
            Console.WriteLine();
            
            // 显示将使用的应用类型
            if (appType == PPTApplicationType.Auto)
            {
                try
                {
                    var detected = PPTApplicationDetector.DetectBestAvailable();
                    Console.WriteLine($"自动选择: {PPTApplicationDetector.GetDisplayName(detected)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[错误] {ex.Message}");
                    Console.WriteLine("请按任意键退出...");
                    Console.ReadKey();
                    return;
                }
            }
            else
            {
                Console.WriteLine($"指定使用: {PPTApplicationDetector.GetDisplayName(appType)}");
            }
            Console.WriteLine();

            _pptController = new PowerPointController(appType);
            
            // 订阅 PPT 事件
            _pptController.SlideChanged += OnSlideChanged;
            _pptController.PresentationClosed += OnPresentationClosed;

            try
            {
                StartTcpServer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 服务器异常: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>
        /// 启动 TCP 服务器
        /// </summary>
        static void StartTcpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, PORT);
            _listener.Start();
            Console.WriteLine($"[服务器] 已启动,等待 Unity 连接...");

            while (_isRunning)
            {
                try
                {
                    // 等待客户端连接
                    _client = _listener.AcceptTcpClient();
                    Console.WriteLine("[服务器] Unity 已连接");

                    NetworkStream stream = _client.GetStream();
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    // 发送欢迎消息
                    SendResponse("OK|PPT Host Ready");

                    // 处理命令循环
                    string command;
                    while (_isRunning && (command = reader.ReadLine()) != null)
                    {
                        Console.WriteLine($"[收到命令] {command}");
                        ProcessCommand(command);
                    }

                    Console.WriteLine("[服务器] Unity 已断开");
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Console.WriteLine($"[错误] 连接异常: {ex.Message}");
                    }
                }
                finally
                {
                    _client?.Close();
                }
            }
        }

        /// <summary>
        /// 处理命令
        /// </summary>
        static void ProcessCommand(string command)
        {
            try
            {
                string[] parts = command.Split('|');
                string cmd = parts[0].ToUpper();

                switch (cmd)
                {
                    case "OPEN":
                        if (parts.Length > 1)
                        {
                            string filePath = parts[1];
                            if (File.Exists(filePath))
                            {
                                _pptController.OpenPresentation(filePath);
                                int totalSlides = _pptController.GetTotalSlides();
                                SendResponse($"OK|Opened|{totalSlides}");
                            }
                            else
                            {
                                SendResponse($"ERROR|文件不存在: {filePath}");
                            }
                        }
                        else
                        {
                            SendResponse("ERROR|缺少文件路径参数");
                        }
                        break;

                    case "NEXT":
                        _pptController.NextSlide();
                        SendResponse($"OK|{_pptController.GetCurrentSlide()}");
                        break;

                    case "PREV":
                        _pptController.PreviousSlide();
                        SendResponse($"OK|{_pptController.GetCurrentSlide()}");
                        break;

                    case "GOTO":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int slideNumber))
                        {
                            _pptController.GoToSlide(slideNumber);
                            SendResponse($"OK|{slideNumber}");
                        }
                        else
                        {
                            SendResponse("ERROR|无效的幻灯片编号");
                        }
                        break;

                    case "GET_PAGE":
                        int current = _pptController.GetCurrentSlide();
                        int total = _pptController.GetTotalSlides();
                        SendResponse($"OK|{current}|{total}");
                        break;

                    case "CLOSE":
                        _pptController.ClosePresentation();
                        SendResponse("OK|Closed");
                        break;

                    case "SHUTDOWN":
                        SendResponse("OK|Shutting down");
                        _isRunning = false;
                        break;

                    case "PING":
                        SendResponse("OK|PONG");
                        break;

                    default:
                        SendResponse($"ERROR|未知命令: {cmd}");
                        break;
                }
            }
            catch (Exception ex)
            {
                SendResponse($"ERROR|{ex.Message}");
            }
        }

        /// <summary>
        /// 发送响应到 Unity
        /// </summary>
        static void SendResponse(string message)
        {
            try
            {
                _writer?.WriteLine(message);
                Console.WriteLine($"[发送响应] {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 发送响应失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 幻灯片切换事件回调
        /// </summary>
        static void OnSlideChanged(int slideNumber)
        {
            SendResponse($"EVENT|SLIDE_CHANGED|{slideNumber}");
        }

        /// <summary>
        /// 演示文稿关闭事件回调
        /// </summary>
        static void OnPresentationClosed()
        {
            SendResponse("EVENT|PRESENTATION_CLOSED");
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        static void Cleanup()
        {
            Console.WriteLine("[服务器] 正在关闭...");
            
            _pptController?.Dispose();
            _client?.Close();
            _listener?.Stop();
            
            Console.WriteLine("[服务器] 已关闭");
        }

        /// <summary>
        /// 解析命令行参数,获取应用类型
        /// </summary>
        static PPTApplicationType ParseApplicationType(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(6).ToLower();
                    switch (value)
                    {
                        case "wps":
                            return PPTApplicationType.WPS;
                        case "office":
                        case "powerpoint":
                            return PPTApplicationType.Office;
                        case "auto":
                            return PPTApplicationType.Auto;
                        default:
                            Console.WriteLine($"[警告] 未知的应用类型参数: {value},将使用自动检测");
                            break;
                    }
                }
            }
            
            return PPTApplicationType.Auto;
        }
    }
}
