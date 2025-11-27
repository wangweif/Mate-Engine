using System;
using System.Runtime.InteropServices;
using UnityEngine;

// 定义与Windows API交互所需的结构体[citation:4]
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class OpenFileName
{
    public int structSize = 0;
    public IntPtr dlgOwner = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public String filter = null; // 文件过滤器，例如 "All files\0*.*\0Text files\0*.txt\0"
    public String customFilter = null;
    public int maxCustFilter = 0;
    public int filterIndex = 0;
    public String file = null; // 接收文件名的缓冲区
    public int maxFile = 0; // 缓冲区大小
    public String fileTitle = null; // 接收文件标题的缓冲区
    public int maxFileTitle = 0;
    public String initialDir = null; // 初始目录
    public String title = null; // 对话框标题
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public String defExt = null;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public String templateName = null;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;
}

public static class LocalDialog
{
    // 导入Windows API函数[citation:4]
    [DllImport("Comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
}