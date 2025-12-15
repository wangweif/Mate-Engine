using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VoiceCommand
{
    public string name;
    public List<string> keywords;
    public string description;
    public string command;
}

[Serializable]
public class ApiConfig
{
    public string llm_api_url;
    public int timeout_seconds;
}

[Serializable]
public class VoiceCommandConfig
{
    public List<VoiceCommand> commands;
    public ApiConfig api_config;
}

[Serializable]
public class CommandRequest
{
    public string user_command;
    public VoiceCommandConfig command_config;
}

[Serializable]
public class CommandResponse
{
    public string action;
    public string cmd_command;
}
