using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Threading;
using Debug = UnityEngine.Debug;
using System;

public class SmartWindowsTTS : MonoBehaviour
{
    private bool isAvailable = false;
    private string statusMessage = "未初始化";
    private bool isSpeaking = false;
    private Process currentSpeechProcess;

    void Awake()
    {
        Log("🔊 TTS系统初始化中...");
        CheckTTSAvailability();
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
        // 清理资源
        if (currentSpeechProcess != null && !currentSpeechProcess.HasExited)
        {
            currentSpeechProcess.Kill();
            currentSpeechProcess = null;
        }
    }

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

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-ExecutionPolicy Bypass -File \"{tempFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = psi;
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

    /// <summary>
    /// 同步播放语音（阻塞当前线程，等待播放完成）
    /// </summary>
    public bool Speak(string text)
    {
        if (!isAvailable)
        {
            Debug.LogError($"❌ TTS 不可用: {statusMessage}");
            return false;
        }

        if (isSpeaking)
        {
            Debug.LogWarning("⚠ TTS 正在播放中，请等待完成");
            return false;
        }

        if (string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("⚠ 朗读文本为空");
            return false;
        }

        isSpeaking = true;
        bool success = false;

        try
        {
            Debug.Log($"🗣️ 开始朗读: {text}");

            // 在后台线程中执行语音播放
            Thread thread = new Thread(() =>
            {
                success = ExecuteSpeech(text);
            });

            thread.Start();

            // 等待线程完成（阻塞当前线程）
            thread.Join();

            Debug.Log(success ? $"✅ 朗读完成: {text}" : $"❌ 朗读失败: {text}");
            return success;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 朗读过程异常: {e.Message}");
            return false;
        }
        finally
        {
            isSpeaking = false;
        }
    }

    /// <summary>
    /// 在后台线程中执行语音播放
    /// </summary>
    private bool ExecuteSpeech(string text)
    {
        try
        {
            // 创建简单的PowerShell脚本，避免复杂的转义
            string escapedText = EscapeForPowerShell(text);

            string script = $@"
Add-Type -AssemblyName System.Speech
try {{
    $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
    $synth.Volume = 100
    $synth.Rate = 0
    $synth.Speak('{escapedText}') | Out-Null
    exit 0
}} catch {{
    Write-Error ""TTS Error: $($_.Exception.Message)""
    exit 1
}}
            ";

            string tempFile = Path.GetTempFileName() + ".ps1";
            File.WriteAllText(tempFile, script, System.Text.Encoding.UTF8);

            Log($"📝 创建临时脚本: {tempFile}");
            Log($"📝 脚本内容预览: {script.Substring(0, Math.Min(100, script.Length))}...");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{tempFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();

                currentSpeechProcess = process;

                // 读取输出和错误信息
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit(30000); // 30秒超时
                currentSpeechProcess = null;

                Log($"📝 PowerShell退出代码: {process.ExitCode}");

                if (process.ExitCode != 0)
                {
                    LogError($"⚠ PowerShell脚本执行失败");
                    LogError($"错误输出: {error}");
                    LogError($"标准输出: {output}");
                    return false;
                }

                Log($"✅ PowerShell脚本执行成功");
            }

            // 清理临时文件
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    LogWarning($"⚠ 无法删除临时文件: {ex.Message}");
                }
            }

            return true;
        }
        catch (System.Exception e)
        {
            LogError($"❌ 语音播放失败: {e.Message}");
            LogError($"详细信息: {e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 为PowerShell转义文本
    /// </summary>
    private string EscapeForPowerShell(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // PowerShell单引号字符串中的转义规则
        return text
            .Replace("'", "''")           // 单引号转义为两个单引号
            .Replace("\r", "")           // 移除回车
            .Replace("\n", " ")          // 换行替换为空格
            .Replace("`", "``")          // 反引号转义
            .Replace("$", "`$");         // 变量符号转义
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

        if (isSpeaking)
        {
            LogWarning("⚠ TTS 正在播放中，请等待完成");
            yield break;
        }

        if (string.IsNullOrEmpty(text))
        {
            LogWarning("⚠ 朗读文本为空");
            yield break;
        }

        isSpeaking = true;
        bool success = false;
        bool isCompleted = false;

        // 启动后台播放线程
        Thread thread = new Thread(() =>
        {
            success = ExecuteSpeech(text);
            isCompleted = true;
        });
        thread.Start();

        // 等待线程完成，但不阻塞Unity主线程
        while (thread.IsAlive)
        {
            yield return null; // 每帧检查一次
        }

        isSpeaking = false;

        if (success)
        {
            Log($"✅ 协程朗读完成: {text}");
        }
        else
        {
            LogError($"❌ 协程朗读失败: {text}");
        }

        // 返回成功状态供调用者使用
        if (!success)
        {
            yield return false;
        }
    }

    /// <summary>
    /// 异步播放语音（推荐在Unity主线程中使用）
    /// </summary>
    public void SpeakAsync(string text, System.Action<bool> onComplete = null)
    {
        StartCoroutine(SpeakAsyncCoroutine(text, onComplete));
    }

    /// <summary>
    /// 异步播放协程
    /// </summary>
    private IEnumerator SpeakAsyncCoroutine(string text, System.Action<bool> onComplete)
    {
        if (!isAvailable)
        {
            Debug.LogError($"❌ TTS 不可用: {statusMessage}");
            onComplete?.Invoke(false);
            yield break;
        }

        if (isSpeaking)
        {
            Debug.LogWarning("⚠ TTS 正在播放中，请等待完成");
            onComplete?.Invoke(false);
            yield break;
        }

        isSpeaking = true;
        bool success = false;

        // 在后台线程中执行语音播放
        Thread thread = new Thread(() =>
        {
            success = ExecuteSpeech(text);
        });
        thread.Start();

        // 等待线程完成，但不阻塞Unity主线程
        while (thread.IsAlive)
        {
            yield return null; // 每帧检查一次
        }

        isSpeaking = false;
        onComplete?.Invoke(success);

        if (success)
        {
            Debug.Log($"✅ 异步朗读完成: {text}");
        }
    }

    /// <summary>
    /// 强制停止当前语音播放
    /// </summary>
    public void StopSpeaking()
    {
        if (currentSpeechProcess != null && !currentSpeechProcess.HasExited)
        {
            currentSpeechProcess.Kill();
            currentSpeechProcess = null;
            Debug.Log("⏹️ 强制停止语音播放");
        }

        isSpeaking = false;
    }

    /// <summary>
    /// 转义文本中的特殊字符
    /// </summary>
    private string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 转义PowerShell特殊字符
        return text
            .Replace("'", "''")     // 单引号转义
            .Replace("`", "``")     // 反引号转义
            .Replace("$", "`$")     // 变量符号转义
            .Replace("\"", "`\"")   // 双引号转义
            .Replace("\r", "")      // 移除回车符
            .Replace("\n", " ");    // 换行符替换为空格
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

    // 公共属性
    public bool IsAvailable() => isAvailable;
    public string GetStatus() => statusMessage;
    public bool IsSpeaking() => isSpeaking;

    private bool IsWindowsPlatform()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.WindowsEditor;
    }
}