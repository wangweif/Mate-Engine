"""
打包脚本
使用 PyInstaller 将 Python 程序打包成 exe
"""
import os
import sys
import subprocess


def build():
    """打包程序"""
    print("=" * 50)
    print("  开始打包 PPT Host")
    print("=" * 50)
    
    # 确保安装了 PyInstaller
    try:
        import PyInstaller
    except ImportError:
        print("[错误] 未安装 PyInstaller")
        print("[提示] 请运行: pip install pyinstaller")
        return False
    
    # 打包参数
    args = [
        'pyinstaller',
        '--onefile',                    # 打包成单个文件
        '--name=PPT.Host',              # 输出文件名
        '--console',                    # 显示控制台窗口
        '--clean',                      # 清理临时文件
        '--noconfirm',                  # 不询问确认
        'src/main.py',                  # 主程序入口
    ]
    
    # 执行打包
    print(f"[打包] 命令: {' '.join(args)}")
    result = subprocess.run(' '.join(args), shell=True, cwd=os.path.dirname(os.path.abspath(__file__)) or '.')
    
    
    if result.returncode == 0:
        print("\n" + "=" * 50)
        print("  ✅ 打包成功!")
        print("  输出文件: dist/PPT.Host.exe")
        print("=" * 50)
        return True
    else:
        print("\n" + "=" * 50)
        print("  ❌ 打包失败!")
        print("=" * 50)
        return False


if __name__ == "__main__":
    success = build()
    sys.exit(0 if success else 1)
