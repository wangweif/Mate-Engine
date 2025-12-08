using UnityEngine;
using System.Collections;
using System.Net.NetworkInformation;

/// <summary>
/// 网络连接检测器 - 启动时检测与指定服务器的连接状态
/// </summary>
public class NetworkConnectivityChecker : MonoBehaviour
{
    [Header("服务器配置")]
    public string serverHost = "192.168.8.88";
    public int pingTimeout = 3000; // Ping超时时间（毫秒）

    [Header("状态")]
    public bool isOnline = false;

    // 事件：当连接状态改变时触发
    public System.Action<bool> OnConnectivityChanged;

    void Start()
    {
        // 启动时检测一次网络连接
        CheckConnectivity();
    }

    /// <summary>
    /// 检测网络连接状态（使用Ping）
    /// </summary>
    void CheckConnectivity()
    {
        try
        {
            System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
            PingReply reply = ping.Send(serverHost, pingTimeout);
            
            isOnline = (reply != null && reply.Status == IPStatus.Success);
            
            if (isOnline)
            {
                Debug.Log($"[NetworkChecker] Ping {serverHost} 成功 - 在线模式 (往返时间: {reply.RoundtripTime}ms)");
            }
            else
            {
                Debug.LogWarning($"[NetworkChecker] Ping {serverHost} 失败 - 离线模式 (状态: {reply?.Status})");
            }
        }
        catch (System.Exception e)
        {
            isOnline = false;
            Debug.LogWarning($"[NetworkChecker] Ping {serverHost} 异常 - 离线模式 (错误: {e.Message})");
        }
        
        // 触发连接状态事件
        OnConnectivityChanged?.Invoke(isOnline);
    }

    /// <summary>
    /// 手动触发一次连接检测
    /// </summary>
    public void CheckNow()
    {
        CheckConnectivity();
    }

    /// <summary>
    /// 获取当前连接状态
    /// </summary>
    public bool IsOnline()
    {
        return isOnline;
    }
}
