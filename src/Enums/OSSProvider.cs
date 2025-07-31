using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jim.MinioOSS;

internal enum OSSProvider
{
    /// <summary>
    /// 无效
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Minio自建对象储存
    /// </summary>
    Minio = 1,

    ///// <summary>
    ///// 阿里云OSS
    ///// </summary>
    //Aliyun = 2,

    ///// <summary>
    ///// 腾讯云OSS
    ///// </summary>
    //QCloud = 3,

    ///// <summary>
    ///// 七牛云 OSS
    ///// </summary>
    //Qiniu = 4,

    ///// <summary>
    ///// 华为云 OBS
    ///// </summary>
    //HuaweiCloud = 5,

    ///// <summary>
    ///// 百度云 BOS
    ///// </summary>
    //BaiduCloud = 6,
    ///// <summary>
    ///// 天翼云 OOS
    ///// </summary>
    //Ctyun = 7
}
