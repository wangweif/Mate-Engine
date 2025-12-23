using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[System.Serializable]
public class PPTInfo
{
    public string filename;
    public string file_path;
    public string[] desc;

    public bool is_uploaded;
    
    // 新增字段
    public int pageCount = 0; // PPT页数
    public int configStatus = 0; // 配置状态: 0=未配置, 1=进行中, 2=已配置, 3=失败
}