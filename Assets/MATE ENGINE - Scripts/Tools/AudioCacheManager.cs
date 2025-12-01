using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;

public class AudioCacheManager : MonoBehaviour
{
    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
    private AudioSource audioSource;
    private string currentPlayingKey;
    private float pauseTime;

    // 音频文件存储路径
    private string cacheDirectory;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        cacheDirectory = Path.Combine(Application.persistentDataPath, "TTSCache");
        if (!Directory.Exists(cacheDirectory))
            Directory.CreateDirectory(cacheDirectory);

        Debug.Log($"🎵 音频缓存目录: {cacheDirectory}");
    }

    /// <summary>
    /// 获取或创建音频剪辑
    /// </summary>
    public IEnumerator GetOrCreateAudio(string text, string key, System.Action<AudioClip> onComplete)
    {
        // 检查内存缓存
        if (audioCache.ContainsKey(key))
        {
            Debug.Log($"🎵 从内存缓存加载音频: {key}");
            onComplete?.Invoke(audioCache[key]);
            yield break;
        }

        // 检查磁盘缓存
        string filePath = Path.Combine(cacheDirectory, $"{key}.wav");
        if (File.Exists(filePath))
        {
            Debug.Log($"🎵 从磁盘缓存加载音频: {key}");
            yield return StartCoroutine(LoadAudioFile(filePath, key, onComplete));
            yield break;
        }

        // 生成新音频
        Debug.Log($"🎵 生成新音频: {key}");
        yield return StartCoroutine(GenerateAndCacheAudio(text, key, filePath, onComplete));
    }

    /// <summary>
    /// 加载音频文件
    /// </summary>
    private IEnumerator LoadAudioFile(string filePath, string key, System.Action<AudioClip> onComplete)
    {
        string url = "file://" + filePath;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    audioCache[key] = clip;
                    Debug.Log($"✅ 音频加载成功: {key}, 长度: {clip.length}秒");
                    onComplete?.Invoke(clip);
                }
                else
                {
                    Debug.LogError($"❌ 音频剪辑为空: {key}");
                    onComplete?.Invoke(null);
                }
            }
            else
            {
                Debug.LogError($"❌ 加载音频文件失败: {www.error}, 路径: {filePath}");
                onComplete?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// 生成并缓存音频
    /// </summary>
    private IEnumerator GenerateAndCacheAudio(string text, string key, string filePath, System.Action<AudioClip> onComplete)
    {
        // 转义文本用于PowerShell
        string escapedText = EscapeForPowerShell(text);

        // 创建PowerShell脚本
        string script = $@"
Add-Type -AssemblyName System.Speech
try {{
    $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
    $synth.SetOutputToWaveFile('{filePath}')
    $synth.Speak('{escapedText}')
    $synth.Dispose()
    Write-Output 'AUDIO_GENERATED'
    exit 0
}} catch {{
    Write-Error $_.Exception.Message
    exit 1
}}
";

        // 执行PowerShell脚本
        bool success = false;
        yield return StartCoroutine(ExecutePowerShellScript(script, (result) => {
            success = result;
        }));

        if (success)
        {
            Debug.Log($"✅ 音频生成成功: {key}");
            // 加载生成的音频文件
            yield return StartCoroutine(LoadAudioFile(filePath, key, onComplete));
        }
        else
        {
            Debug.LogError($"❌ 音频生成失败: {key}");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>
    /// 执行PowerShell脚本
    /// </summary>
    private IEnumerator ExecutePowerShellScript(string script, System.Action<bool> onComplete)
    {
        string tempFile = Path.GetTempFileName() + ".ps1";
        File.WriteAllText(tempFile, script, Encoding.UTF8);

        Debug.Log($"📝 创建PowerShell脚本: {tempFile}");

        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "powershell.exe";
        process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempFile}\"";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        bool processCompleted = false;
        bool processSuccess = false;

        Task.Run(() =>
        {
            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(30000); // 30秒超时

                if (process.ExitCode == 0 && output.Contains("AUDIO_GENERATED"))
                {
                    processSuccess = true;
                    Debug.Log($"✅ PowerShell脚本执行成功");
                }
                else
                {
                    Debug.LogError($"❌ PowerShell脚本执行失败，退出代码: {process.ExitCode}");
                    Debug.LogError($"错误输出: {error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 执行PowerShell脚本异常: {e.Message}");
            }
            finally
            {
                processCompleted = true;

                // 清理临时文件
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        });

        // 等待进程完成
        while (!processCompleted)
        {
            yield return null;
        }

        onComplete?.Invoke(processSuccess);
    }

    /// <summary>
    /// 为PowerShell转义文本
    /// </summary>
    private string EscapeForPowerShell(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("'", "''")           // 单引号转义为两个单引号
            .Replace("\r", "")           // 移除回车
            .Replace("\n", " ")          // 换行替换为空格
            .Replace("`", "``")          // 反引号转义
            .Replace("$", "`$");         // 变量符号转义
    }

    // 播放控制方法
    public void PlayAudio(string key, AudioClip clip, float startTime = 0f)
    {
        currentPlayingKey = key;
        audioSource.clip = clip;
        audioSource.time = Mathf.Clamp(startTime, 0f, clip.length);
        audioSource.Play();
        Debug.Log($"▶️ 播放音频: {key}, 开始时间: {startTime:F2}s");
    }

    public float PauseAudio()
    {
        if (audioSource.isPlaying)
        {
            pauseTime = audioSource.time;
            audioSource.Pause();
            Debug.Log($"⏸️ 暂停音频, 位置: {pauseTime:F2}s");
            return pauseTime;
        }
        return 0f;
    }

    public void ResumeAudio()
    {
        if (audioSource.clip != null)
        {
            audioSource.time = pauseTime;
            audioSource.Play();
            Debug.Log($"▶️ 恢复音频, 位置: {pauseTime:F2}s");
        }
    }

    public void StopAudio()
    {
        audioSource.Stop();
        currentPlayingKey = null;
        pauseTime = 0f;
        Debug.Log("⏹️ 停止音频");
    }

    public float GetCurrentTime()
    {
        return audioSource.isPlaying ? audioSource.time : pauseTime;
    }

    public float GetClipLength(string key)
    {
        return audioCache.ContainsKey(key) ? audioCache[key].length : 0f;
    }

    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }

    public string GetCurrentKey()
    {
        return currentPlayingKey;
    }

    /// <summary>
    /// 清理缓存
    /// </summary>
    public void ClearCache()
    {
        // 清理内存缓存
        audioCache.Clear();

        // 清理磁盘缓存
        if (Directory.Exists(cacheDirectory))
        {
            try
            {
                Directory.Delete(cacheDirectory, true);
                Directory.CreateDirectory(cacheDirectory);
                Debug.Log("🧹 音频缓存已清理");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 清理缓存失败: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 获取缓存大小
    /// </summary>
    public long GetCacheSize()
    {
        if (!Directory.Exists(cacheDirectory))
            return 0;

        long size = 0;
        var files = Directory.GetFiles(cacheDirectory, "*.wav");
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            size += info.Length;
        }
        return size;
    }
}