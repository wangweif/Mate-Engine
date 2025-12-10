using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 封装科大讯飞 TTS/STT 的 WebSocket 调用
/// </summary>
public class XunFeiSpeechService
{
    readonly string appId;
    readonly string apiKey;
    readonly string apiSecret;
    readonly string ttsHost;
    readonly string ttsPath;
    readonly string sttHost;
    readonly string sttPath;

    public XunFeiSpeechService(
        string appId = "8b33cd3e",
        string apiKey = "ed60705846cfdeae8aa91d05532371b3",
        string apiSecret = "ZGE2OGMyOWI5NmNkMjM4ODhiMmMxOTNk",
        string ttsHost = "ws-api.xfyun.cn",
        string ttsPath = "/v2/tts",
        string sttHost = "iat-api.xfyun.cn",
        string sttPath = "/v2/iat")
    {
        this.appId = appId;
        this.apiKey = apiKey;
        this.apiSecret = apiSecret;
        this.ttsHost = ttsHost;
        this.ttsPath = ttsPath;
        this.sttHost = sttHost;
        this.sttPath = sttPath;
    }

    string BuildSignedUrl(string host, string path)
    {
        string date = DateTime.UtcNow.ToString("r");
        string signatureOrigin = $"host: {host}\ndate: {date}\nGET {path} HTTP/1.1";
        byte[] signatureSha = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret))
            .ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin));
        string signature = Convert.ToBase64String(signatureSha);
        string authorization = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signature}\"";
        string authorizationB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));

        string url = $"wss://{host}{path}?authorization={Uri.EscapeDataString(authorizationB64)}&date={Uri.EscapeDataString(date)}&host={Uri.EscapeDataString(host)}";
        return url;
    }

    public async Task<byte[]> RequestTtsAsync(string text, CancellationToken cancellationToken, string voice = "x4_yezi", int speed = 45)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("text为空");

        string wsUrl = BuildSignedUrl(ttsHost, ttsPath);
        List<byte> audioBuffer = new List<byte>();

        using (ClientWebSocket ws = new ClientWebSocket())
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);

            string payload = $"{{\"common\":{{\"app_id\":\"{appId}\"}},\"business\":{{\"aue\":\"lame\",\"auf\":\"audio/L16;rate=16000\",\"vcn\":\"{voice}\",\"tte\":\"utf8\",\"speed\":{speed}}},\"data\":{{\"status\":2,\"text\":\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\"}}}}";
            ArraySegment<byte> payloadBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload));
            await ws.SendAsync(payloadBytes, WebSocketMessageType.Text, true, cancellationToken);

            byte[] buffer = new byte[4096];
            List<byte> messageBuffer = new List<byte>(4096);
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // 处理分片消息，直到 EndOfMessage
                messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();
                if (!message.Contains("\"code\":0") && message.Contains("\"code\":"))
                {
                    throw new Exception($"讯飞TTS返回错误: {message}");
                }

                Match audioMatch = Regex.Match(message, "\"audio\"\\s*:\\s*\"([^\"]+)\"");
                if (audioMatch.Success)
                {
                    byte[] chunk = Convert.FromBase64String(audioMatch.Groups[1].Value);
                    audioBuffer.AddRange(chunk);
                }

                if (message.Contains("\"status\":2"))
                {
                    break;
                }
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken);
            }
        }

        return audioBuffer.ToArray();
    }

    public async Task<string> RequestSttAsync(byte[] pcmBytes, CancellationToken cancellationToken)
    {
        if (pcmBytes == null || pcmBytes.Length == 0) throw new ArgumentException("PCM数据为空");

        string wsUrl = BuildSignedUrl(sttHost, sttPath);
        StringBuilder textBuilder = new StringBuilder();
        List<byte> messageBuffer = new List<byte>(4096);

        using (ClientWebSocket ws = new ClientWebSocket())
        {
            await ws.ConnectAsync(new Uri(wsUrl), cancellationToken);

            string audioBase64 = Convert.ToBase64String(pcmBytes);
            string payload = $"{{\"common\":{{\"app_id\":\"{appId}\"}},\"business\":{{\"domain\":\"iat\",\"language\":\"zh_cn\",\"accent\":\"mandarin\",\"vinfo\":1,\"dwa\":\"wpgs\"}},\"data\":{{\"status\":2,\"format\":\"audio/L16;rate=16000\",\"encoding\":\"raw\",\"audio\":\"{audioBase64}\"}}}}";
            ArraySegment<byte> payloadBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload));
            await ws.SendAsync(payloadBytes, WebSocketMessageType.Text, true, cancellationToken);

            byte[] buffer = new byte[4096];
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // 处理分片消息
                messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                string message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                messageBuffer.Clear();
                if (!message.Contains("\"code\":0") && message.Contains("\"code\":"))
                {
                    throw new Exception($"讯飞STT返回错误: {message}");
                }

                foreach (Match m in Regex.Matches(message, "\"w\"\\s*:\\s*\"([^\"]+)\""))
                {
                    textBuilder.Append(m.Groups[1].Value);
                }

                if (message.Contains("\"status\":2"))
                {
                    break;
                }
            }

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken);
            }
        }

        return textBuilder.ToString();
    }

    public static byte[] AudioClipToPcm16(AudioClip clip)
    {
        if (clip == null) return Array.Empty<byte>();

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] pcmData = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            pcmData[i * 2] = (byte)(sample & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcmData;
    }
}

