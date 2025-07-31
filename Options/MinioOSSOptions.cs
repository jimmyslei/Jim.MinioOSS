using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

public class MinioOSSOptions
{
    ///// <summary>
    ///// 枚举，OOS提供商
    ///// </summary>
    //public OSSProvider Provider { get; set; } = OSSProvider.Minio;

    /// <summary>
    /// 节点
    /// </summary>
    /// <remarks>
    /// </remarks>
    public string Endpoint { get; set; }

    /// <summary>
    /// AccessKey
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// SecretKey
    /// </summary>
    public string SecretKey { get; set; }

    /// <summary>
    /// 桶名
    /// </summary>
    public string BucketName { get; set; }

    private string _region = "us-east-1";
    /// <summary>
    /// 地域
    /// </summary>
    public string Region
    {
        get
        {
            return _region;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                _region = "us-east-1";
            }
            else
            {
                _region = value;
            }
        }
    }

    /// <summary>
    /// 是否启用HTTPS
    /// </summary>
    public bool IsEnableHttps { get; set; } = false;

    ///// <summary>
    ///// 是否启用缓存，默认缓存在MemeryCache中（可使用自行实现的缓存替代默认缓存）
    ///// 在使用之前请评估当前应用的缓存能力能否顶住当前请求
    ///// </summary>
    //public bool IsEnableCache { get; set; } = false;
}