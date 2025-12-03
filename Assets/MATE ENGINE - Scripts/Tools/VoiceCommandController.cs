using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Windows.Speech;
using System;

public class VoiceControlDemo : MonoBehaviour
{
    [Header("基本设置")]
    public GameObject model;      // 桌宠对象
    public MenuActions menuActions;
    public string wakeWord = "小智小智"; // 唤醒词

    private KeywordRecognizer recognizer;

    void Start()
    {
        // 初始化语音识别
        List<string> keywords = new List<string> { wakeWord };

        recognizer = new KeywordRecognizer(keywords.ToArray());
        recognizer.OnPhraseRecognized += OnVoiceCommand;
        recognizer.Stop();
    }
    private void Update()
    {
        if (HiddenManager.IsModelHidden)
        {
            if (!recognizer.IsRunning)
            {
                recognizer.Start();
            }
        }
    }

    void OnVoiceCommand(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"识别到: {args.text} (置信度: {args.confidence})");
        if (args.text.Equals(wakeWord))
        {
            ShowModel();
        }
    }

    void ShowModel()
    {
        try
        {
            model.SetActive(true);
            menuActions.enabled = true;
            HiddenManager.IsModelHidden = false;
            recognizer.Stop();
            print("暂停识别关键词！");
        }
        catch(Exception e)
        {
            Debug.LogError("ShowModel执行失败！错误信息:"+e);
        }
    }

    void OnDestroy()
    {
        if (recognizer != null)
        {
            recognizer.Stop();
            recognizer.Dispose();
        }
    }
}