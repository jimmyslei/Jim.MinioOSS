using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class FileMergeOutModel
{
    ///// <summary>
    ///// ID
    ///// </summary>
    //public string? id { get; set; }

    /// <summary>
    /// 文件名.
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    /// 文件长度，字节数.
    /// </summary>
    public long size { get; set; }

    /// <summary>
    /// 文件内容类型，MIME-Type.
    /// </summary>
    public string? mimeType { get; set; }

    /// <summary>
    /// 文件MD5值.
    /// </summary>
    public string? md5 { get; set; }
    /// <summary>
    /// 文件路径
    /// </summary>
    public string path { get; set; }

    /// <summary>
    /// 访问路径
    /// </summary>
    public string url
    {
        get
        {
            //var domain = App.Configuration["OTTOMS_APP:Domain"];
            //return $"{domain}/FileService/Attachment/Download/{id}";
            return "";
        }
    }

    /// <summary>
    /// 文件类型，Type.
    /// </summary>
    public string? type
    {
        get
        {
            return mimeType?.Split('/').FirstOrDefault();
        }
    }

    /// <summary>
    /// 文件扩展名.
    /// </summary>
    public string? ext
    {
        get
        {
            return name?.Split('.').LastOrDefault();
        }
    }
}
