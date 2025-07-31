using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class FileMergeModel
{
    /// <summary>
    /// 文件md5
    /// </summary>
    public string md5 { get; set; }

    /// <summary>
    /// 总的分片数
    /// </summary>
    public int chunkCount { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string fileName { get; set; }

    /// <summary>
    /// 文件类型，用于判断存放不同的目录
    /// </summary>
    public string type { get; set; } = "";
}
