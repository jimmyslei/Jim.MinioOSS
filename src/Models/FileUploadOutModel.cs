using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class FileUploadOutModel
{
    /// <summary>
    /// 上传成功与否
    /// </summary>
    public bool success { get; set; }
    /// <summary>
    /// 是否需要合并分片
    /// </summary>
    public bool merge { get; set; }
}
