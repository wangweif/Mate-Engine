# PPT Host - Python 版本

Unity 与 PowerPoint 之间的通信服务,使用 Python 和 win32com 实现。

## 功能特性

- ✅ TCP 服务器 (端口 45678)
- ✅ 完整的通信协议 (与 C# 版本兼容)
- ✅ PowerPoint 控制 (打开/翻页/跳转/关闭)
- ✅ 事件监听 (幻灯片切换、演示结束)
- ✅ 自动资源清理和进程管理
- ✅ 支持 Office PowerPoint 和 WPS
- ✅ 无需 DCOM 配置
- ✅ 无需管理员权限

## 快速开始

### 1. 安装依赖

```powershell
pip install pywin32 psutil
```

### 2. 运行程序

```powershell
cd src
python main.py
```

### 3. 打包成 exe

```powershell
python build.py
```

打包后的文件位于 `dist/PPT.Host.exe`

## 通信协议

| 命令 | 格式 | 响应 |
|------|------|------|
| OPEN | `OPEN\|<文件路径>` | `OK\|Opened\|<总页数>` |
| NEXT | `NEXT` | `OK\|<当前页>` |
| PREV | `PREV` | `OK\|<当前页>` |
| GOTO | `GOTO\|<页码>` | `OK\|<页码>` |
| GET_PAGE | `GET_PAGE` | `OK\|<当前页>\|<总页数>` |
| CLOSE | `CLOSE` | `OK\|Closed` |
| PING | `PING` | `OK\|PONG` |
| SHUTDOWN | `SHUTDOWN` | `OK\|Shutting down` |

## 事件通知

| 事件 | 格式 |
|------|------|
| 幻灯片切换 | `EVENT\|SLIDE_CHANGED\|<页码>` |
| 演示结束 | `EVENT\|PRESENTATION_CLOSED` |

## 项目结构

```
PPT.Host.Python/
├── src/
│   ├── main.py              # 主程序
│   ├── ppt_controller.py    # PowerPoint 控制器
│   └── tcp_server.py        # TCP 服务器
├── build.py                 # 打包脚本
├── requirements.txt         # 依赖列表
└── README.md               # 本文件
```

## 依赖说明

- **pywin32**: Windows COM 接口
- **psutil**: 进程管理

## 注意事项

1. 需要安装 Microsoft Office 或 WPS Office
2. 首次运行可能需要等待 win32com 生成类型库
3. 关闭时会自动清理 PowerPoint 进程

## 与 C# 版本对比

| 特性 | Python 版本 | C# 版本 |
|------|------------|---------|
| 基本控制 | ✅ | ✅ |
| 事件监听 | ✅ | ✅ |
| DCOM 配置 | 不需要 | 需要 |
| 管理员权限 | 不需要 | 需要 |
| 部署难度 | 简单 | 中等 |
| 文件大小 | ~15MB | ~2MB |

## 许可证

与主项目相同
