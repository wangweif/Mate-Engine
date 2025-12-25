using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MateEngine.PPT
{
    /// <summary>
    /// PPT 服务 - 管理与 PPT Host 的通信
    /// </summary>
    public class PPTService : MonoBehaviour
    {
        public static PPTService Instance { get; private set; }

        [Header("连接设置")]
        [SerializeField] private string hostAddress = "127.0.0.1";
        [SerializeField] private int hostPort = 45678;
        [SerializeField] private string hostExePath = "PPTHost/PPT.Host.exe";

        [Header("状态")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private int currentSlide = 0;
        [SerializeField] private int totalSlides = 0;

        // 事件
        public event Action<int> OnSlideChanged;
        public event Action OnPresentationClosed;
        public event Action<string> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;

        // TCP 客户端
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;
        private Process _hostProcess;

        // 重连
        private bool _shouldReconnect = true;
        private float _reconnectDelay = 2f;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            StartCoroutine(ConnectToHost());
        }

        void OnDestroy()
        {
            _shouldReconnect = false;
            Disconnect();
            StopHostProcess();
        }

        void OnApplicationQuit()
        {
            _shouldReconnect = false;
            SendCommand("SHUTDOWN");
            Disconnect();
        }

        /// <summary>
        /// 连接到 PPT Host
        /// </summary>
        private IEnumerator ConnectToHost()
        {
            while (_shouldReconnect)
            {
                if (!isConnected)
                {
                    UnityEngine.Debug.Log("[PPT] 尝试连接到 PPT Host...");

                    // 检查 Host 进程是否运行
                    if (!IsHostProcessRunning())
                    {
                        UnityEngine.Debug.Log("[PPT] Host 未运行,正在启动...");
                        StartHostProcess();
                        yield return new WaitForSeconds(2f); // 等待 Host 启动
                    }

                    // 尝试连接
                    bool connected = TryConnect();
                    
                    if (connected)
                    {
                        // 开始监听消息
                        StartCoroutine(ListenForMessages());
                    }
                }

                yield return new WaitForSeconds(_reconnectDelay);
            }
        }

        /// <summary>
        /// 尝试连接到 TCP 服务器
        /// </summary>
        private bool TryConnect()
        {
            try
            {
                // 连接到 TCP 服务器
                _client = new TcpClient();
                _client.Connect(hostAddress, hostPort);

                NetworkStream stream = _client.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                isConnected = true;
                UnityEngine.Debug.Log("[PPT] 已连接到 PPT Host");
                OnConnected?.Invoke();

                // 读取欢迎消息
                string welcome = _reader.ReadLine();
                UnityEngine.Debug.Log($"[PPT] {welcome}");

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PPT] 连接失败: {ex.Message}");
                OnError?.Invoke($"连接失败: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 监听来自 Host 的消息
        /// </summary>
        private IEnumerator ListenForMessages()
        {
            while (isConnected && _client != null && _client.Connected)
            {
                if (_reader != null)
                {
                    try
                    {
                        if (_client.Available > 0)
                        {
                            string message = _reader.ReadLine();
                            if (!string.IsNullOrEmpty(message))
                            {
                                ProcessMessage(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[PPT] 读取消息失败: {ex.Message}");
                        Disconnect();
                        break;
                    }
                }

                yield return null;
            }

            UnityEngine.Debug.Log("[PPT] 停止监听消息");
        }

        /// <summary>
        /// 处理来自 Host 的消息
        /// </summary>
        private void ProcessMessage(string message)
        {
            UnityEngine.Debug.Log($"[PPT] 收到: {message}");

            string[] parts = message.Split('|');
            string type = parts[0];

            switch (type)
            {
                case "OK":
                    // 命令成功响应
                    if (parts.Length > 1 && parts[1] == "Opened" && parts.Length > 2)
                    {
                        totalSlides = int.Parse(parts[2]);
                        currentSlide = 1;
                        OnSlideChanged?.Invoke(currentSlide);
                    }
                    // 处理翻页命令返回的页码 (OK|页码)
                    else if (parts.Length > 1 && int.TryParse(parts[1], out int newSlide))
                    {
                        currentSlide = newSlide;
                        OnSlideChanged?.Invoke(currentSlide);
                        UnityEngine.Debug.Log($"[PPT] 翻页成功,当前页: {currentSlide}/{totalSlides}");
                    }
                    break;

                case "ERROR":
                    string errorMsg = parts.Length > 1 ? parts[1] : "未知错误";
                    UnityEngine.Debug.LogError($"[PPT] 错误: {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    break;

                case "EVENT":
                    if (parts.Length > 1)
                    {
                        string eventType = parts[1];
                        switch (eventType)
                        {
                            case "SLIDE_CHANGED":
                                if (parts.Length > 2 && int.TryParse(parts[2], out int slideNum))
                                {
                                    currentSlide = slideNum;
                                    OnSlideChanged?.Invoke(currentSlide);
                                }
                                break;

                            case "PRESENTATION_CLOSED":
                                OnPresentationClosed?.Invoke();
                                break;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 发送命令到 Host
        /// </summary>
        private void SendCommand(string command)
        {
            if (!isConnected || _writer == null)
            {
                UnityEngine.Debug.LogWarning("[PPT] 未连接,无法发送命令");
                return;
            }

            try
            {
                _writer.WriteLine(command);
                UnityEngine.Debug.Log($"[PPT] 发送: {command}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PPT] 发送命令失败: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        private void Disconnect()
        {
            isConnected = false;

            try
            {
                _reader?.Close();
                _writer?.Close();
                _client?.Close();
            }
            catch { }

            _reader = null;
            _writer = null;
            _client = null;

            UnityEngine.Debug.Log("[PPT] 已断开连接");
            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// 启动 Host 进程
        /// </summary>
        private void StartHostProcess()
        {
            try
            {
                string exePath = Path.Combine(Application.dataPath, "..", hostExePath);
                
                if (!File.Exists(exePath))
                {
                    UnityEngine.Debug.LogError($"[PPT] Host 程序不存在: {exePath}");
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    CreateNoWindow = true,  // 隐藏控制台窗口
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                _hostProcess = Process.Start(startInfo);
                UnityEngine.Debug.Log($"[PPT] Host 进程已以管理员身份启动 (PID: {_hostProcess.Id})");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 用户取消了 UAC 提示
                UnityEngine.Debug.LogError($"[PPT] 启动 Host 失败 (可能用户取消了管理员权限请求): {ex.Message}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PPT] 启动 Host 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止 Host 进程
        /// </summary>
        private void StopHostProcess()
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                try
                {
                    _hostProcess.Kill();
                    _hostProcess.WaitForExit(2000);
                    UnityEngine.Debug.Log("[PPT] Host 进程已停止");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[PPT] 停止 Host 进程失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查 Host 进程是否运行
        /// </summary>
        private bool IsHostProcessRunning()
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                return true;
            }

            // 检查是否有其他 PPT.Host.exe 进程在运行
            Process[] processes = Process.GetProcessesByName("PPT.Host");
            return processes.Length > 0;
        }

        // ==================== 公共 API ====================

        /// <summary>
        /// 打开演示文稿
        /// </summary>
        public void OpenPresentation(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OnError?.Invoke($"文件不存在: {filePath}");
                return;
            }

            SendCommand($"OPEN|{filePath}");
        }

        /// <summary>
        /// 下一张幻灯片
        /// </summary>
        public void NextSlide()
        {
            SendCommand("NEXT");
        }

        /// <summary>
        /// 上一张幻灯片
        /// </summary>
        public void PreviousSlide()
        {
            SendCommand("PREV");
        }

        /// <summary>
        /// 跳转到指定幻灯片
        /// </summary>
        public void GoToSlide(int slideNumber)
        {
            SendCommand($"GOTO|{slideNumber}");
        }

        /// <summary>
        /// 获取当前幻灯片编号
        /// </summary>
        public int GetCurrentSlide()
        {
            return currentSlide;
        }

        /// <summary>
        /// 获取总幻灯片数
        /// </summary>
        public int GetTotalSlides()
        {
            return totalSlides;
        }

        /// <summary>
        /// 关闭演示文稿
        /// </summary>
        public void ClosePresentation()
        {
            SendCommand("CLOSE");
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected()
        {
            return isConnected;
        }
    }
}
