using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Debug = UnityEngine.Debug;

public class SmartWindowsTTS : MonoBehaviour
{
    private AudioCacheManager audioCache;
    private bool isAvailable = false;
    private string statusMessage = "未初始化";

    // 音频缓存变量
    private Dictionary<string, string> textToKeyMap = new Dictionary<string, string>();
    private Dictionary<string, float> resumePositions = new Dictionary<string, float>();

    // 配置选项
    [Header("TTS 配置")]
    public int maxCacheSizeMB = 100; // 最大缓存大小(MB)
    public bool autoClearCache = true; // 自动清理缓存

    void Awake()
    {
        // 初始化音频缓存管理器
        InitializeAudioCache();

        Debug.Log("🔊 TTS系统初始化中...");
        CheckTTSAvailability();
    }

    /// <summary>
    /// 初始化音频缓存
    /// </summary>
    private void InitializeAudioCache()
    {
        audioCache = FindObjectOfType<AudioCacheManager>();
        if (audioCache == null)
        {
            GameObject obj = new GameObject("AudioCacheManager");
            audioCache = obj.AddComponent<AudioCacheManager>();
            DontDestroyOnLoad(obj);
        }
        Debug.Log("🎵 音频缓存系统已初始化");
    }

    /// <summary>
    /// 日志输出方法
    /// </summary>
    private void Log(string message)
    {
        Debug.Log($"[SmartWindowsTTS] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[SmartWindowsTTS] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SmartWindowsTTS] {message}");
    }

    void OnDestroy()
    {
        // 自动清理缓存
        if (autoClearCache && audioCache != null)
        {
            long cacheSize = audioCache.GetCacheSize();
            float cacheSizeMB = cacheSize / (1024f * 1024f);
            if (cacheSizeMB > maxCacheSizeMB)
            {
                Debug.Log($"🧹 自动清理缓存，当前大小: {cacheSizeMB:F2}MB");
                audioCache.ClearCache();
            }
        }
    }

    /// <summary>
    /// 检查TTS可用性
    /// </summary>
    private void CheckTTSAvailability()
    {
        if (!IsWindowsPlatform())
        {
            statusMessage = "仅支持 Windows 平台";
            LogWarning($"⚠ {statusMessage}");
            return;
        }

        try
        {
            string testScript = @"
                try {
                    Add-Type -AssemblyName System.Speech -ErrorAction Stop
                    $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
                    if ($synth) {
                        Write-Output 'TTS_AVAILABLE'
                        $voices = $synth.GetInstalledVoices()
                        Write-Output (""VOICE_COUNT:{0}"" -f $voices.Count)
                        foreach ($voice in $voices) {
                            Write-Output (""VOICE:{0}"" -f $voice.VoiceInfo.Name)
                        }
                    }
                }
                catch {
                    Write-Output 'TTS_UNAVAILABLE'
                    Write-Output $_.Exception.Message
                }
            ";

            string tempFile = Path.GetTempFileName() + ".ps1";
            File.WriteAllText(tempFile, testScript);

            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{tempFile}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(5000);

                if (output.Contains("TTS_AVAILABLE"))
                {
                    isAvailable = true;
                    int voiceCount = 0;

                    foreach (string line in output.Split('\n'))
                    {
                        if (line.StartsWith("VOICE_COUNT:"))
                        {
                            string countStr = line.Substring("VOICE_COUNT:".Length).Trim();
                            int.TryParse(countStr, out voiceCount);
                        }
                        else if (line.StartsWith("VOICE:"))
                        {
                            string voiceName = line.Substring("VOICE:".Length).Trim();
                            Log($"🔊 找到语音: {voiceName}");
                        }
                    }

                    statusMessage = $"TTS 初始化成功，找到 {voiceCount} 个语音引擎";
                    Log($"✅ {statusMessage}");
                }
                else
                {
                    statusMessage = $"TTS 不可用: {error}";
                    LogError($"❌ {statusMessage}");
                }

                // 清理临时文件
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (System.Exception e)
        {
            statusMessage = $"TTS 检查失败: {e.Message}";
            LogError($"❌ {statusMessage}");
        }
    }

    // ==================== 音频缓存方法 ====================

    /// <summary>
    /// 使用音频缓存播放语音（协程版本）
    /// </summary>
    public IEnumerator Speak(string text, System.Action<bool> onComplete = null)
    {
        if (!isAvailable)
        {
            LogError($"❌ TTS 不可用: {statusMessage}");
            onComplete?.Invoke(false);
            yield break;
        }

        if (string.IsNullOrEmpty(text))
        {
            LogWarning("⚠ 朗读文本为空");
            onComplete?.Invoke(false);
            yield break;
        }

        string key = GenerateAudioKey(text);
        textToKeyMap[key] = text;

        Log($"🗣️ 准备播放语音: {text.Substring(0, Mathf.Min(50, text.Length))}...");

        // 检查是否有恢复位置
        float resumeTime = resumePositions.ContainsKey(key) ? resumePositions[key] : 0f;

        yield return StartCoroutine(audioCache.GetOrCreateAudio(text, key, (audioClip) => {
            if (audioClip != null)
            {
                audioCache.PlayAudio(key, audioClip, resumeTime);

                // 如果是从恢复位置播放，清除恢复位置
                if (resumeTime > 0)
                {
                    resumePositions.Remove(key);
                    Log($"▶️ 从位置 {resumeTime:F2}s 恢复播放");
                }

                onComplete?.Invoke(true);
            }
            else
            {
                LogError($"❌ 无法获取音频剪辑: {key}");
                onComplete?.Invoke(false);
            }
        }));
    }

    /// <summary>
    /// 协程同步播放语音（在协程中使用，会等待播放完成）
    /// </summary>
    public IEnumerator SpeakCoroutine(string text)
    {
        if (!isAvailable)
        {
            LogError($"❌ TTS 不可用: {statusMessage}");
            yield break;
        }

        if (string.IsNullOrEmpty(text))
        {
            LogWarning("⚠ 朗读文本为空");
            yield break;
        }

        bool speakCompleted = false;
        bool speakSuccess = false;

        // 使用回调方式跟踪说话状态
        Speak(text, (success) => {
            speakCompleted = true;
            speakSuccess = success;
        });

        // 等待说话完成
        while (!speakCompleted)
        {
            yield return null;
        }

        if (speakSuccess)
        {
            Log($"✅ 协程朗读完成: {text}");
        }
        else
        {
            LogError($"❌ 协程朗读失败: {text}");
        }
    }

    /// <summary>
    /// 异步播放语音
    /// </summary>
    public void SpeakAsync(string text, System.Action<bool> onComplete = null)
    {
        StartCoroutine(Speak(text, onComplete));
    }

    /// <summary>
    /// 暂停语音播放
    /// </summary>
    public float PauseSpeaking()
    {
        if (audioCache != null && audioCache.IsPlaying())
        {
            string currentKey = audioCache.GetCurrentKey();
            float pauseTime = audioCache.PauseAudio();

            // 保存恢复位置
            if (!string.IsNullOrEmpty(currentKey))
            {
                resumePositions[currentKey] = pauseTime;
                Log($"⏸️ 保存恢复位置: {currentKey} -> {pauseTime:F2}s");
            }

            return pauseTime;
        }
        return 0f;
    }

    /// <summary>
    /// 恢复语音播放
    /// </summary>
    public void ResumeSpeaking()
    {
        if (audioCache != null)
        {
            audioCache.ResumeAudio();
            Log("▶️ 恢复音频播放");
        }
    }

    /// <summary>
    /// 停止语音播放
    /// </summary>
    public void StopSpeaking()
    {
        if (audioCache != null)
        {
            audioCache.StopAudio();
            resumePositions.Clear();
            Log("⏹️ 停止音频播放");
        }
    }

    /// <summary>
    /// 生成音频键值
    /// </summary>
    public string GenerateAudioKey(string text)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 获取当前播放时间
    /// </summary>
    public float GetCurrentAudioTime()
    {
        if (audioCache != null)
        {
            return audioCache.GetCurrentTime();
        }
        return 0f;
    }

    /// <summary>
    /// 获取音频长度
    /// </summary>
    public float GetAudioLength(string text)
    {
        if (audioCache != null)
        {
            string key = GenerateAudioKey(text);
            return audioCache.GetClipLength(key);
        }
        return EstimateSpeechDuration(text);
    }

    /// <summary>
    /// 检查是否正在播放
    /// </summary>
    public bool IsSpeaking()
    {
        if (audioCache != null)
        {
            return audioCache.IsPlaying();
        }
        return false;
    }

    /// <summary>
    /// 检查是否已暂停
    /// </summary>
    public bool IsPaused()
    {
        if (audioCache != null)
        {
            return !audioCache.IsPlaying() && GetCurrentAudioTime() > 0;
        }
        return false;
    }

    /// <summary>
    /// 清理音频缓存
    /// </summary>
    public void ClearAudioCache()
    {
        if (audioCache != null)
        {
            audioCache.ClearCache();
            resumePositions.Clear();
            textToKeyMap.Clear();
            Log("🧹 音频缓存已清理");
        }
    }

    /// <summary>
    /// 获取缓存大小（MB）
    /// </summary>
    public float GetCacheSizeMB()
    {
        if (audioCache != null)
        {
            long bytes = audioCache.GetCacheSize();
            return bytes / (1024f * 1024f);
        }
        return 0f;
    }

    /// <summary>
    /// 估算语音播放时间（秒）
    /// </summary>
    public float EstimateSpeechDuration(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        // 简单估算：中文字符约0.4秒/字，英文字符约0.1秒/字
        int chineseChars = 0;
        int englishChars = 0;

        foreach (char c in text)
        {
            if (c >= 0x4E00 && c <= 0x9FFF) // 中文字符范围
                chineseChars++;
            else if (char.IsLetterOrDigit(c))
                englishChars++;
        }

        return chineseChars * 0.4f + englishChars * 0.1f + 1.0f; // 基础1秒
    }

    /// <summary>
    /// 检查TTS是否可用
    /// </summary>
    public bool IsAvailable()
    {
        return isAvailable;
    }

    /// <summary>
    /// 获取状态信息
    /// </summary>
    public string GetStatus()
    {
        return statusMessage;
    }

    /// <summary>
    /// 检查是否为Windows平台
    /// </summary>
    private bool IsWindowsPlatform()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.WindowsEditor;
    }

    /// <summary>
    /// 获取当前播放进度（0-1）
    /// </summary>
    public float GetPlaybackProgress()
    {
        if (audioCache != null && audioCache.IsPlaying())
        {
            string currentKey = audioCache.GetCurrentKey();
            if (!string.IsNullOrEmpty(currentKey))
            {
                float currentTime = GetCurrentAudioTime();
                float totalTime = audioCache.GetClipLength(currentKey);
                if (totalTime > 0)
                {
                    return currentTime / totalTime;
                }
            }
        }
        return 0f;
    }

    /// <summary>
    /// 获取当前播放的文本
    /// </summary>
    public string GetCurrentText()
    {
        if (audioCache != null)
        {
            string currentKey = audioCache.GetCurrentKey();
            if (!string.IsNullOrEmpty(currentKey) && textToKeyMap.ContainsKey(currentKey))
            {
                return textToKeyMap[currentKey];
            }
        }
        return null;
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    public void SetVolume(float volume)
    {
        if (audioCache != null)
        {
            // 这里需要为AudioCacheManager添加音量控制
            // audioCache.SetVolume(volume);
        }
    }

    /// <summary>
    /// 设置播放速率
    /// </summary>
    public void SetRate(float rate)
    {
        if (audioCache != null)
        {
            // 这里需要为AudioCacheManager添加速率控制
            // audioCache.SetRate(rate);
        }
    }
    /// <summary>
    /// 等待当前语音播放完成
    /// </summary>
    public IEnumerator WaitForSpeechComplete()
    {
        if (audioCache != null && audioCache.IsPlaying())
        {
            // 等待直到播放停止
            while (audioCache.IsPlaying())
            {
                yield return null;
            }

            // 额外等待一小段时间确保完全结束
            yield return new WaitForSeconds(0.1f);
            Debug.Log("✅ 语音播放完全完成");
        }
    }
}