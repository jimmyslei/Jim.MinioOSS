using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class FileChunkModel
{
    /// <summary>
    /// 分片文件
    /// </summary>
    public IFormFile file { get; set; }

    /// <summary>
    /// 完整文件的MD5
    /// </summary>
    public string md5 { get; set; }

    /// <summary>
    /// 当前分片序号
    /// </summary>
    public int chunkNumber { get; set; }

    /// <summary>
    /// 总的分片数
    /// </summary>
    public int chunkCount { get; set; }

}
