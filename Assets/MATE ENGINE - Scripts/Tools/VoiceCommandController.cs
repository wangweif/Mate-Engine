using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Windows.Speech;
using System;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public class VoiceControlDemo : MonoBehaviour
{
    [Header("基本设置")]
    public GameObject model;      // 桌宠对象
    public MenuActions menuActions;
    public string wakeWord = "小智小智"; // 唤醒词
    public string configFile = "VoiceCommandConfig.json";

    [Header("语音助手设置")]
    public bool enableVoiceAssistant = true;
    public float commandTimeout = 30f;
    public float wakeWordTimeout = 10f; // 唤醒后等待命令的超时时间

    [Header("语音回应设置")]
    public bool enableVoiceResponse = true; // 是否启用语音回应
    public AudioSource audioSource; // 用于播放音频
    
    [Header("预录音频文件")]
    public AudioClip wakeResponseAudio; // 唤醒回应音频 "我在,请说"
    public AudioClip listeningAudio; // 监听提示音频 (可选)
    public AudioClip processingAudio; // 处理提示音频 (可选)
    public AudioClip executingAudio; // 执行提示音频 (可选)
    public AudioClip errorAudio; // 错误提示音频 (可选)
    
    [Header("备用文字(用于日志)")]
    public string wakeResponseText = "我在,请说"; // 唤醒回应文字
    public string listeningText = "正在听..."; // 监听提示
    public string processingText = "正在处理..."; // 处理提示
    public string executingText = "正在执行命令"; // 执行提示
    public string errorText = "抱歉,出错了"; // 错误提示

    private KeywordRecognizer wakeWordRecognizer;
    private KeywordRecognizer commandRecognizer;
    private VoiceCommandConfig commandConfig;
    private bool isListeningForCommands = false;
    private bool isWaitingForWakeWord = false;
    private bool isProcessingCommand = false; // 防止重复触发
    private Coroutine wakeWordTimeoutCoroutine = null;
    
    // 讯飞语音服务
    private XunFeiSpeechService xunFeiSpeechService;
    
    // 语音录制相关
    private AudioClip recordingClip;
    private string microphoneDevice;
    private bool isRecording = false;
    private const int sampleRate = 16000;
    private const int maxRecordingLength = 60; // 最大录制60秒


    void Start()
    {
        // 加载命令配置
        LoadCommandConfig();

        // 初始化唤醒词识别
        InitializeWakeWordRecognition();
        
        // 初始化讯飞语音服务
        xunFeiSpeechService = new XunFeiSpeechService();

        // 开始监听唤醒词
        if (enableVoiceAssistant)
        {
            StartWakeWordListening();
        }
    }

    private void InitializeWakeWordRecognition()
    {
        // 创建唤醒词识别器
        List<string> wakeWords = new List<string> { wakeWord };
        wakeWordRecognizer = new KeywordRecognizer(wakeWords.ToArray());
        wakeWordRecognizer.OnPhraseRecognized += OnWakeWordRecognized;

        Debug.Log($"初始化唤醒词监听: {wakeWord}");
    }

    private void StartWakeWordListening()
    {
        if (wakeWordRecognizer != null && !isWaitingForWakeWord)
        {
            wakeWordRecognizer.Start();
            isWaitingForWakeWord = true;
            Debug.Log("开始监听唤醒词...");
        }
    }

    private void StopWakeWordListening()
    {
        if (wakeWordRecognizer != null && isWaitingForWakeWord)
        {
            wakeWordRecognizer.Stop();
            isWaitingForWakeWord = false;
            Debug.Log("停止监听唤醒词");
        }
    }

    private void LoadCommandConfig()
    {
        try
        {
            string configPath = Path.Combine(Application.streamingAssetsPath, configFile);
            if (File.Exists(configPath))
            {
                string jsonContent = File.ReadAllText(configPath);
                commandConfig = JsonUtility.FromJson<VoiceCommandConfig>(jsonContent);
                Debug.Log("命令配置加载成功");
            }
            else
            {
                Debug.LogWarning($"配置文件不存在: {configPath}，使用默认配置");
                CreateDefaultConfig();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"加载配置文件失败: {e.Message}");
            CreateDefaultConfig();
        }
    }

    private void CreateDefaultConfig()
    {
        commandConfig = new VoiceCommandConfig
        {
            commands = new List<VoiceCommand>
            {
                new VoiceCommand { name = "打开记事本", keywords = new List<string>{"记事本", "notepad"}, description = "打开记事本", command = "notepad" },
                new VoiceCommand { name = "截图", keywords = new List<string>{"截图", "screenshot"}, description = "截取屏幕", command = "explorer ms-screenclip:" },
                new VoiceCommand { name = "关机", keywords = new List<string>{"关机", "关电脑", "shutdown"}, description = "关闭计算机（可指定延时时间，单位为秒）", command = "shutdown /s /t 0" },
                new VoiceCommand { name = "重启", keywords = new List<string>{"重启", "重新启动", "restart"}, description = "重新启动计算机（可指定延时时间，单位为秒）", command = "shutdown /r /t 0" }
            },
            api_config = new ApiConfig { llm_api_url = "http://192.168.8.88:8000/v1/chat/completions", timeout_seconds = 30 }
        };
    }

    void OnWakeWordRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"识别到唤醒词: {args.text} (置信度: {args.confidence})");

        // 如果正在处理指令,忽略新的唤醒
        if (isProcessingCommand)
        {
            Debug.LogWarning("正在处理指令中,忽略重复唤醒");
            return;
        }

        // 停止唤醒词监听
        StopWakeWordListening();

        // 设置处理状态
        isProcessingCommand = true;

        // 播放唤醒回应,然后开始语音识别
        StartCoroutine(PlayWakeResponseAndListen());
    }

    /// <summary>
    /// 播放唤醒回应并开始监听
    /// </summary>
    private IEnumerator PlayWakeResponseAndListen()
    {
        if (enableVoiceResponse && wakeResponseAudio != null && audioSource != null)
        {
            Debug.Log($"播放唤醒回应: {wakeResponseText}");
            
            // 确保AudioSource不循环播放
            audioSource.loop = false;
            
            // 停止当前播放(如果有)
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
            
            // 播放预录音频
            audioSource.clip = wakeResponseAudio;
            audioSource.Play();
            
            // 等待音频播放完成
            yield return new WaitForSeconds(wakeResponseAudio.length);
            
            // 短暂停顿
            yield return new WaitForSeconds(0.3f);
        }
        else if (enableVoiceResponse)
        {
            Debug.LogWarning("唤醒回应音频未配置,跳过语音回应");
        }

        // 开始语音识别
        StartCoroutine(StartVoiceToText());
    }



    private IEnumerator StartVoiceToText()
    {
        Debug.Log("请说出您的指令...");

        // 开始录制音频
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("没有找到麦克风设备");
            yield return new WaitForSeconds(1f);
            isProcessingCommand = false; // 重置处理状态
            StartWakeWordListening();
            yield break;
        }

        microphoneDevice = Microphone.devices[0];
        isRecording = true;
        recordingClip = Microphone.Start(microphoneDevice, false, maxRecordingLength, sampleRate);

        // 等待用户说话或超时
        float startTime = Time.time;
        float timeout = wakeWordTimeout;
        float lastSoundTime = Time.time;
        const float silenceThreshold = 1.5f; // 静音1.5秒后自动停止

        // 简单的VAD检测：检测是否有声音
        bool hasDetectedSound = false;
        float[] samples = new float[sampleRate / 10]; // 100ms的样本

        while (Time.time - startTime < timeout && isRecording)
        {
            yield return new WaitForSeconds(0.1f);

            if (recordingClip == null) continue;

            // 检查是否有声音活动
            int position = Microphone.GetPosition(microphoneDevice);
            if (position > 0)
            {
                int readLength = Mathf.Min(samples.Length, position);
                recordingClip.GetData(samples, Mathf.Max(0, position - readLength));

                // 计算音频能量
                float energy = 0f;
                for (int i = 0; i < readLength; i++)
                {
                    energy += Mathf.Abs(samples[i]);
                }
                energy /= readLength;

                if (energy > 0.01f) // 能量阈值
                {
                    hasDetectedSound = true;
                    lastSoundTime = Time.time;
                }
            }

            // 如果检测到声音后，静音超过阈值，自动停止
            if (hasDetectedSound && Time.time - lastSoundTime > silenceThreshold)
            {
                Debug.Log("检测到静音，停止录制");
                break;
            }
        }

        // 停止录制
        if (isRecording && recordingClip != null)
        {
            int position = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            isRecording = false;

            if (position <= 0)
            {
                Debug.LogWarning("录音时间太短，无法转文字");
                if (recordingClip != null)
                {
                    Destroy(recordingClip);
                    recordingClip = null;
                }
                yield return new WaitForSeconds(1f);
                isProcessingCommand = false; // 重置处理状态
                StartWakeWordListening();
                yield break;
            }

            // 裁剪音频片段
            float[] allSamples = new float[recordingClip.samples * recordingClip.channels];
            recordingClip.GetData(allSamples, 0);

            int clipLength = position * recordingClip.channels;
            float[] trimmedSamples = new float[clipLength];
            Array.Copy(allSamples, trimmedSamples, clipLength);

            AudioClip trimmedClip = AudioClip.Create("RecordedAudio", clipLength / recordingClip.channels, recordingClip.channels, recordingClip.frequency, false);
            trimmedClip.SetData(trimmedSamples, 0);

            // 清理原始录音
            Destroy(recordingClip);
            recordingClip = trimmedClip;

            Debug.Log($"录音结束，开始转文字... (时长: {trimmedClip.length:F2}秒)");

            // 调用讯飞STT转文字
            string transcribedText = "";
            bool transcribeCompleted = false;
            bool transcribeSuccess = false;

            Task<string> transcribeTask = TranscribeAudioWithResult(trimmedClip);
            StartCoroutine(WaitForTranscribeTask(transcribeTask, (text, success) =>
            {
                transcribedText = text;
                transcribeCompleted = true;
                transcribeSuccess = success;
            }));

            while (!transcribeCompleted)
            {
                yield return null;
            }

            // 清理音频
            if (trimmedClip != null)
            {
                Destroy(trimmedClip);
            }

            // 处理识别结果
            if (transcribeSuccess && !string.IsNullOrEmpty(transcribedText.Trim()))
            {
                Debug.Log($"识别到的完整指令: {transcribedText.Trim()}");
                // 发送用户指令和命令列表给大模型
                SendCommandToLLM(transcribedText.Trim());
            }
            else
            {
                Debug.Log("没有识别到指令，返回待机状态");
            }
        }
        else
        {
            Debug.Log("语音识别超时，返回待机状态");
            if (recordingClip != null)
            {
                Microphone.End(microphoneDevice);
                Destroy(recordingClip);
                recordingClip = null;
            }
            isRecording = false;
        }

        // 重新开始监听唤醒词
        yield return new WaitForSeconds(1f);
        isProcessingCommand = false; // 重置处理状态
        StartWakeWordListening();
    }

    /// <summary>
    /// 使用讯飞STT转文字
    /// </summary>
    async Task<string> TranscribeAudioWithResult(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return "";
        }

        if (xunFeiSpeechService == null)
        {
            Debug.LogError("讯飞语音服务未初始化，无法转文字");
            return "";
        }

        try
        {
            byte[] pcmData = XunFeiSpeechService.AudioClipToPcm16(audioClip);
            if (pcmData == null || pcmData.Length == 0)
            {
                Debug.LogWarning("音频数据为空");
                return "";
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                string transcribedText = await xunFeiSpeechService.RequestSttAsync(pcmData, cts.Token);
                return transcribedText;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("讯飞转文字任务被取消");
            return "";
        }
        catch (Exception e)
        {
            Debug.LogError($"讯飞转文字失败: {e.Message}");
            return "";
        }
    }

    /// <summary>
    /// 等待转文字任务完成
    /// </summary>
    IEnumerator WaitForTranscribeTask(Task<string> task, System.Action<string, bool> onComplete)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsCompletedSuccessfully)
        {
            onComplete?.Invoke(task.Result, true);
        }
        else
        {
            Debug.LogError($"转文字任务失败: {task.Exception}");
            onComplete?.Invoke("", false);
        }
    }

    private void SendCommandToLLM(string userCommand)
    {
        Debug.Log($"将用户指令发送到LLM: {userCommand}");

        // 发送命令到大模型API（异步）
        StartCoroutine(SendToLLMAPI(userCommand, (response) => {
            if (response != null && !string.IsNullOrEmpty(response.cmd_command))
            {
                Debug.Log($"执行命令: {response.cmd_command}");

                // 执行CMD命令
                StartCoroutine(ExecuteCMDCommand(response.cmd_command));
            }
            else
            {
                Debug.LogError("LLM响应无效或执行失败");
            }
        }));
    }



    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public bool stream = false;
        public ResponseFormat response_format;
    }

    [Serializable]
    private class ResponseFormat
    {
        public string type;
        public JsonSchemaWrapper json_schema;
    }

    [Serializable]
    private class JsonSchemaWrapper
    {
        public string name;
        public SchemaDefinition schema;
    }

    [Serializable]
    private class SchemaDefinition
    {
        public string type;
        public CmdResponseProperties properties;
        public List<string> required;
        public bool additionalProperties;
    }

    [Serializable]
    private class CmdResponseProperties
    {
        public PropertyItem action;
        public PropertyItem cmd_command;
    }

    [Serializable]
    private class PropertyItem
    {
        public string type;
        public string description;
    }

    private IEnumerator SendToLLMAPI(string userCommand, System.Action<CommandResponse> callback)
    {
        if (commandConfig == null)
        {
            Debug.LogError("命令配置未加载");
            callback(null);
            yield break;
        }

        string promptText = BuildPromptText();

        var responseFormat = new ResponseFormat
        {
            type = "json_schema",
            json_schema = new JsonSchemaWrapper
            {
                name = "cmd_response",
                schema = new SchemaDefinition
                {
                    type = "object",
                    properties = new CmdResponseProperties
                    {
                        action = new PropertyItem { type = "string", description = "操作类型" },
                        cmd_command = new PropertyItem { type = "string", description = "具体的CMD命令" }
                    },
                    required = new List<string> { "action", "cmd_command" },
                    additionalProperties = false
                }
            }
        };

        // 创建JSON请求体
        ChatRequest chatRequest = new ChatRequest
        {
            model = "Qwen3:8B",
            messages = new List<ChatMessage>
            {
                new ChatMessage { role = "system", content = promptText },
                new ChatMessage { role = "user", content = userCommand }
            },
            stream = false,
            response_format = responseFormat
        };

        string jsonRequest = JsonUtility.ToJson(chatRequest);

        Debug.Log($"发送API请求: {jsonRequest}");

        using (UnityWebRequest webRequest = new UnityWebRequest(commandConfig.api_config.llm_api_url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonRequest);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.timeout = commandConfig.api_config.timeout_seconds;

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string textResponse = webRequest.downloadHandler.text;
                JObject jsonResponse = JObject.Parse(textResponse);
                Debug.Log($"API响应: {textResponse}");

                try
                {
                    // 解析响应内容为命令
                    CommandResponse commandResponse = ParseCommandFromContent(jsonResponse["choices"][0]["message"]["content"].ToString());
                    callback(commandResponse);
                }
                catch (Exception e)
                {
                    Debug.LogError($"解析API响应失败: {e.Message}");
                    // 如果JSON解析失败，尝试直接解析为命令响应
                    callback(ParseCommandFromContent(jsonResponse["choices"][0]["message"]["content"].ToString()));
                }
            }
            else
            {
                string errorBody = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : "";
                Debug.LogError($"API请求失败: {webRequest.error}, 状态码: {webRequest.responseCode}, 响应: {errorBody}");
                callback(null);
            }
        }
    }

    private string BuildPromptText()
    {
        // 构建提示词，告诉大模型如何处理语音命令
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("你是一个语音助手命令解析器。请根据用户的语音指令，生成对应的Windows CMD命令。");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("支持的命令类型：");

        foreach (var cmd in commandConfig.commands)
        {
            promptBuilder.AppendLine($"- {cmd.name}: {cmd.description}");
            promptBuilder.AppendLine($"- 命令:{cmd.command}");
        }

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("请以JSON格式返回结果：{action: \"操作类型\", cmd_command: \"具体的CMD命令\"}");

        return promptBuilder.ToString();
    }

    private CommandResponse ParseCommandFromContent(string content)
    {
        try
        {
            // 清理响应内容（类似AutoDesc.cs的处理方式）
            // content = RemoveOuterQuotes(content);

            // 尝试解析JSON格式的响应
            CommandResponse response = JsonConvert.DeserializeObject<CommandResponse>(content);
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"解析命令内容失败: {e.Message}");

            // 如果JSON解析失败，尝试从文本中提取CMD命令
            CommandResponse fallbackResponse = new CommandResponse();

            // 查找可能的CMD命令模式
            if (content.Contains("notepad") || content.ToLower().Contains("记事本"))
            {
                fallbackResponse.cmd_command = "notepad";
                fallbackResponse.action = "open_notepad";
            }
            else if (content.Contains("calc") || content.ToLower().Contains("计算器"))
            {
                fallbackResponse.cmd_command = "calc";
                fallbackResponse.action = "open_calculator";
            }
            else if (content.Contains("snip") || content.ToLower().Contains("截图"))
            {
                fallbackResponse.cmd_command = "explorer ms-screenclip:";
                fallbackResponse.action = "screenshot";
            }
            else
            {
                fallbackResponse.action = "unknown";
                fallbackResponse.cmd_command = "";
            }

            return fallbackResponse;
        }
    }

    // 参考AutoDesc.cs的字符串处理方法
    private string RemoveOuterQuotes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        string result = input;

        // 移除外层引号
        if (result.Length >= 2)
        {
            if ((result[0] == '\"' && result[result.Length - 1] == '\"') ||
                (result[0] == '\'' && result[result.Length - 1] == '\''))
            {
                result = result.Substring(1, result.Length - 2);
            }
        }

        // 清理可能残留的空白字符
        result = result.Trim();

        return result;
    }

    private IEnumerator ExecuteCMDCommand(string command)
    {
        Debug.Log($"执行CMD命令: {command}");

        try
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process process = new Process { StartInfo = processInfo };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Debug.Log($"命令执行成功: {output}");
            }
            else
            {
                Debug.LogError($"命令执行失败 (退出码: {process.ExitCode}): {error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"执行命令异常: {e.Message}");
        }

        yield return null;
    }

    private void StopCommandListening()
    {
        isListeningForCommands = false;
        Debug.Log("停止命令监听");
    }



    void OnDestroy()
    {
        // 停止并释放资源
        if (wakeWordRecognizer != null)
        {
            if (isWaitingForWakeWord)
                wakeWordRecognizer.Stop();
            wakeWordRecognizer.Dispose();
        }

        // 停止录音
        if (isRecording && recordingClip != null)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;
        }

        // 清理音频资源
        if (recordingClip != null)
        {
            Destroy(recordingClip);
            recordingClip = null;
        }

        // 停止超时协程
        if (wakeWordTimeoutCoroutine != null)
        {
            StopCoroutine(wakeWordTimeoutCoroutine);
        }
    }
}