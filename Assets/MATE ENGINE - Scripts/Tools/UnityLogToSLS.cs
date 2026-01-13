using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.Api.LogService;
using Aliyun.Api.LogService.Domain.Log;
using Aliyun.Api.LogService.Infrastructure.Protocol;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MATE_ENGINE___Scripts.Tools
{
    /// <summary>
    /// SLS配置数据结构
    /// </summary>
    [Serializable]
    public class SLSConfig
    {
        public string accessKeyId;
        public string accessKeySecret;
    }

    public class UnityLogToSLS : MonoBehaviour
    {
        // 日志服务的服务接入点
        private static string endpoint = "cn-beijing.log.aliyuncs.com";
        // AccessKey ID和AccessKey Secret(从配置文件读取)
        private static string accessKeyId = "";
        private static string accessKeySecret = "";
        // Project名称
        private static string project = "digitalperson";
        // Logstore名称
        private static string logstore = "dp";
        // 创建日志服务Client
        private static ILogServiceClient client;

        [Header("日志上传配置")]
        [Tooltip("是否启用日志上传到SLS")]
        public bool enableLogUpload = true;

        [Tooltip("是否上传普通日志(Log)")]
        public bool uploadLog = true;

        [Tooltip("是否上传警告日志(Warning)")]
        public bool uploadWarning = true;

        [Tooltip("是否上传错误日志(Error - 由Debug.LogError主动调用)")]
        public bool uploadError = true;

        [Tooltip("是否上传异常日志(Exception - 未捕获的运行时异常)")]
        public bool uploadException = true;

        [Tooltip("批量上传的日志数量阈值")]
        public int batchSize = 10;

        [Tooltip("批量上传的时间间隔(秒)")]
        public float uploadInterval = 5f;

        [Tooltip("是否在控制台显示SLS上传状态")]
        public bool showUploadStatus = true;

        // 日志队列
        private static Queue<LogInfo> logQueue = new Queue<LogInfo>();
        private static readonly object queueLock = new object();

        // 上传定时器
        private float uploadTimer;

        /// <summary>
        /// 从配置文件加载AccessKey
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                // 从Resources文件夹加载配置文件
                TextAsset configAsset = Resources.Load<TextAsset>("SLSConfig");
                if (configAsset != null)
                {
                    // 解析JSON
                    var config = JsonUtility.FromJson<SLSConfig>(configAsset.text);
                    accessKeyId = config.accessKeyId;
                    accessKeySecret = config.accessKeySecret;

                    // 创建客户端
                    client = BuildSimpleClient();
                }
                else
                {
                    UnityEngine.Debug.LogError("[UnityLogToSLS] 未找到 SLSConfig.json 配置文件,请确保文件在 Assets/Resources 文件夹下");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[UnityLogToSLS] 加载配置文件失败: {e.Message}");
            }
        }

        private void Awake()
        {
            // 在Awake时加载配置
            LoadConfig();
        }

        private void OnEnable()
        {
            // 只有在打包后的应用程序中才启用日志上传,编辑器模式下不上传
            if (enableLogUpload && !Application.isEditor)
            {
                // 注册Unity日志回调
                Application.logMessageReceived += HandleLog;
            }
        }

        private void OnDisable()
        {
            // 取消注册Unity日志回调
            Application.logMessageReceived -= HandleLog;

            // 上传剩余的日志
            if (enableLogUpload)
            {
                StartCoroutine(FlushRemainingLogs());
            }
        }

        /// <summary>
        /// 处理Unity日志消息
        /// </summary>
        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            // 根据日志类型判断是否上传
            bool shouldUpload = false;
            string logLevel = "";
            string logType = "";

            switch (type)
            {
                case LogType.Log:
                    shouldUpload = uploadLog;
                    logLevel = "INFO";
                    logType = "Log";
                    break;
                case LogType.Warning:
                    shouldUpload = uploadWarning;
                    logLevel = "WARNING";
                    logType = "Warning";
                    break;
                case LogType.Error:
                    shouldUpload = uploadError;
                    logLevel = "ERROR";
                    logType = "Error";
                    break;
                case LogType.Exception:
                    shouldUpload = uploadException;
                    logLevel = "ERROR";
                    logType = "Exception";
                    break;
            }

            if (!shouldUpload)
                return;

            // 创建日志条目
            var logInfo = new LogInfo
            {
                Time = DateTimeOffset.Now,
                Contents = new Dictionary<String, String>
                {
                    {"LogLevel", logLevel},
                    {"LogType", logType},
                    {"Message", logString},
                    {"Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")},
                    {"DeviceId", GetUUID()},
                    {"DeviceModel", SystemInfo.deviceModel + SystemInfo.deviceName}
                }
            };

            // 添加到队列
            lock (queueLock)
            {
                logQueue.Enqueue(logInfo);
            }
        }

        /// <summary>
        /// 获取本机IP地址
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // 忽略异常,返回空字符串
            }
            return "Unknown";
        }

        private void Update()
        {
            if (!enableLogUpload)
                return;

            uploadTimer += Time.deltaTime;

            // 检查是否需要批量上传
            int currentQueueCount;
            lock (queueLock)
            {
                currentQueueCount = logQueue.Count;
            }

            // 当队列达到批次大小或时间间隔到达时上传
            if (currentQueueCount >= batchSize || uploadTimer >= uploadInterval)
            {
                if (currentQueueCount > 0)
                {
                    StartCoroutine(UploadLogsCoroutine());
                    uploadTimer = 0f;
                }
            }
        }

        /// <summary>
        /// 上传日志到SLS的协程
        /// </summary>
        private IEnumerator UploadLogsCoroutine()
        {
            List<LogInfo> logsToUpload = new List<LogInfo>();

            // 从队列中取出日志
            lock (queueLock)
            {
                while (logQueue.Count > 0 && logsToUpload.Count < batchSize)
                {
                    logsToUpload.Add(logQueue.Dequeue());
                }
            }

            if (logsToUpload.Count == 0)
                yield break;

            bool isCompleted = false;
            Exception error = null;

            // 在后台线程执行异步操作
            Task.Run(async () =>
            {
                try
                {
                    await UploadLogsToSLS(logsToUpload);
                }
                catch (Exception e)
                {
                    error = e;
                }
                finally
                {
                    isCompleted = true;
                }
            });

            // 等待操作完成
            yield return new WaitUntil(() => isCompleted);

            if (error != null)
            {
                if (showUploadStatus)
                {
                    Debug.LogError($"[UnityLogToSLS] 上传日志失败: {error.Message}");
                }
            }
            else if (showUploadStatus)
            {
                //Debug.Log($"[UnityLogToSLS] 成功上传 {logsToUpload.Count} 条日志到SLS");
            }
        }

        /// <summary>
        /// 刷新剩余日志
        /// </summary>
        private IEnumerator FlushRemainingLogs()
        {
            int remainingCount;
            lock (queueLock)
            {
                remainingCount = logQueue.Count;
            }

            if (remainingCount > 0)
            {
                yield return UploadLogsCoroutine();
            }
        }

        /// <summary>
        /// 上传日志到SLS
        /// </summary>
        private static async Task UploadLogsToSLS(List<LogInfo> logs)
        {
            if (logs == null || logs.Count == 0)
                return;

            var response = await client.PostLogStoreLogsAsync(logstore, new LogGroupInfo
            {
                Topic = "UnityLogs",
                Source = System.Net.Dns.GetHostName(),
                LogTags = new Dictionary<String, String>
                {
                    {"Environment", "Unity"},
                    {"Platform", Application.platform.ToString()}
                },
                Logs = logs
            });

            check(response);
        }

        /// <summary>
        /// 构建SLS客户端
        /// </summary>
        public static ILogServiceClient BuildSimpleClient()
            => LogServiceClientBuilders.HttpBuilder
                .Endpoint(endpoint, project)
                .Credential(accessKeyId, accessKeySecret)
                .Build();

        /// <summary>
        /// 检查响应是否成功
        /// </summary>
        public static void check(IResponse res)
        {
            if (!res.IsSuccess)
            {
                throw new ApplicationException(res.Error.ErrorMessage);
            }
        }

        /// <summary>
        /// 手动上传单条日志
        /// </summary>
        public void UploadSingleLog(string message, string logLevel = "INFO", string stackTrace = "")
        {
            var logInfo = new LogInfo
            {
                Time = DateTimeOffset.Now,
                Contents = new Dictionary<String, String>
                {
                    {"LogLevel", logLevel},
                    {"Message", message},
                    {"StackTrace", stackTrace},
                    {"Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")},
                    {"Source", "Manual"}
                }
            };

            lock (queueLock)
            {
                logQueue.Enqueue(logInfo);
            }
        }
        
        public static string GetUUID()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"wmic csproduct get uuid\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                
                    // 使用正则表达式提取UUID
                    string pattern = @"[A-Fa-f0-9]{8}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{4}-[A-Fa-f0-9]{12}";
                    Match match = Regex.Match(output, pattern);
                
                    if (match.Success)
                    {
                        // 去掉所有"-"分隔符
                        return match.Value.Replace("-", "");
                    }
                }
            }
            catch
            {
                // 发生异常时返回空字符串
            }
        
            return string.Empty;
        }
    }
}