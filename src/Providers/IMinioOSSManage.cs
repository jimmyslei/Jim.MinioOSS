﻿using Microsoft.AspNetCore.Http;
using Minio.DataModel;
using Minio.DataModel.Notification;
using System;

namespace Jim.MinioOSS;

public interface IMinioOSSManage
{
    /// <summary>
    /// 删除一个未完整上传的对象
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <returns></returns>
    Task<bool> RemoveIncompleteUploadAsync(string bucketName, string objectName);
    /// <summary>
    /// 列出存储桶中未完整上传的对象
    /// </summary>
    /// <param name="bucketName"></param>
    /// <returns></returns>
    Task<List<ItemUploadInfo>> ListIncompleteUploads(string bucketName);

    /// <summary>
    /// 获取存储桶权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <returns></returns>
    Task<PolicyInfo> GetPolicyAsync(string bucketName);

    /// <summary>
    /// 设置存储桶的权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="statements"></param>
    /// <returns></returns>
    Task<bool> SetPolicyAsync(string bucketName, List<StatementItem> statements);
    
    /// <summary>
    /// 移除全部存储桶的权限
    /// 如果要单独移除某个桶的权限，可以使用SetPolicyAsync，并将StatementItem中的IsDelete设置为true
    /// </summary>
    /// <param name="bucketName"></param>
    /// <returns></returns>
    Task<bool> RemovePolicyAsync(string bucketName);

    Task<bool> PolicyExistsAsync(string bucketName, StatementItem statement);
    /// <summary>
    /// 订阅存储桶更改通知
    /// </summary>
    /// <param name="bucketName"></param>
    /// <returns></returns>
    Task<List<MinioNotificationRaw>> ListenBucketNotificationsAsync(string bucketName);

    /// <summary>
    /// 检查存储桶是否存在。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    Task<bool> BucketExistsAsync(string bucketName);

    /// <summary>
    /// 创建一个存储桶。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="location">可选参数。默认是us-east-1。</param>
    /// <returns></returns>
    Task<bool> CreateBucketAsync(string bucketName);

    /// <summary>
    /// 删除一个存储桶
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    Task<bool> RemoveBucketAsync(string bucketName);

    /// <summary>
    /// 列出所有的存储桶。
    /// </summary>
    /// <returns></returns>
    Task<List<Bucket>> ListBucketsAsync();

    /// <summary>
    /// 设置储存桶的访问权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    Task<bool> SetBucketAclAsync(string bucketName, AccessMode mode);

    /// <summary>
    /// 获取储存桶的访问权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <returns></returns>
    Task<AccessMode> GetBucketAclAsync(string bucketName);

    /// <summary>
    /// 判断桶中对象是否存在
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <returns></returns>
    Task<bool> ObjectsExistsAsync(string bucketName, string objectName);

    /// <summary>
    /// 列出存储桶里的对象。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    Task<List<Item>> ListObjectsAsync(string bucketName, string prefix = null);

    /// <summary>
    /// 分片返回对象数据的流
    /// </summary>
    /// <param name="bucketName">存储桶名称</param>
    /// <param name="objectName">存储桶名称</param>
    /// <param name="offset">分片开始位置</param>
    /// <param name="length">分片大小</param>
    /// <param name="callback">处理流的回调函数</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task GetObjectAsync(string bucketName, string objectName, long offset, long length, Action<Stream> callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// 返回对象数据的流。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶名称。</param>
    /// <param name="callback">处理流的回调函数。</param>
    /// <param name="cancellationToken">可选参数。默认是default(CancellationToken)</param>
    /// <returns></returns>
    Task GetObjectAsync(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// 下载并将文件保存到本地文件系统。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <param name="fileName">本地文件路径。</param>
    /// <param name="cancellationToken">可选参数。默认是default(CancellationToken)</param>
    /// <returns></returns>
    Task GetObjectAsync(string bucketName, string objectName, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过Stream上传对象。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <param name="data">要上传的Stream对象。</param>
    /// <param name="cancellationToken">可选参数。默认是default(CancellationToken)</param>
    /// <returns></returns>
    Task<bool> PutObjectAsync(string bucketName, string objectName, Stream data, CancellationToken cancellationToken = default(CancellationToken));

    /// <summary>
    /// 通过文件上传到对象中。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <param name="filePath">要上传的本地文件名。</param>
    /// <param name="cancellationToken">可选参数。默认是default(CancellationToken) </param>
    /// <returns></returns>
    Task<bool> PutObjectAsync(string bucketName, string objectName, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取对象的元数据。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <returns></returns>
    Task<ItemMeta> GetObjectMetadataAsync(string bucketName
        , string objectName
        , string versionID = null
        , string matchEtag = null
        , DateTime? modifiedSince = null);

    /// <summary>
    /// 从objectName指定的对象中将数据拷贝到destObjectName指定的对象。
    /// </summary>
    /// <param name="bucketName">源存储桶名称。</param>
    /// <param name="objectName">源存储桶中的源对象名称。</param>
    /// <param name="destBucketName">目标存储桶名称。</param>
    /// <param name="destObjectName">要创建的目标对象名称,如果为空，默认为源对象名称。</param>
    /// <returns></returns>
    Task<bool> CopyObjectAsync(string bucketName, string objectName, string destBucketName, string destObjectName = null);

    /// <summary>
    /// 删除一个对象。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <returns></returns>
    Task<bool> RemoveObjectAsync(string bucketName, string objectName);

    /// <summary>
    /// 删除多个对象
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectNames"></param>
    /// <returns></returns>
    Task<bool> RemoveObjectAsync(string bucketName, List<string> objectNames);

    ///// <summary>
    ///// 清除Presigned Object缓存
    ///// </summary>
    ///// <param name="bucketName"></param>
    ///// <param name="objectName"></param>
    //Task RemovePresignedUrlCache(string bucketName, string objectName);

    /// <summary>
    /// 生成一个给HTTP GET请求用的presigned URL。浏览器/移动端的客户端可以用这个URL进行下载，即使其所在的存储桶是私有的。这个presigned URL可以设置一个失效时间，默认值是7天。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <param name="expiresInt">失效时间（以秒为单位），默认是7天，不得大于七天。</param>
    /// <returns></returns>
    Task<string> PresignedGetObjectAsync(string bucketName, string objectName, int expiresInt);

    /// <summary>
    /// 生成一个给HTTP PUT请求用的presigned URL。浏览器/移动端的客户端可以用这个URL进行上传，即使其所在的存储桶是私有的。这个presigned URL可以设置一个失效时间，默认值是7天。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <param name="expiresInt">失效时间（以秒为单位），默认是7天，不得大于七天。</param>
    /// <returns></returns>
    Task<string> PresignedPutObjectAsync(string bucketName, string objectName, int expiresInt);

    /// <summary>
    /// 设置文件的访问权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    Task<bool> SetObjectAclAsync(string bucketName, string objectName, AccessMode mode);

    /// <summary>
    /// 获取文件的访问权限
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <returns></returns>
    Task<AccessMode> GetObjectAclAsync(string bucketName, string objectName);

    /// <summary>
    /// 清空object的ACL，使对象ACL继承存储桶的设置
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <returns></returns>
    Task<AccessMode> RemoveObjectAclAsync(string bucketName, string objectName);

    /// <summary>
    /// 分片上传附件
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    Task<FileUploadOutModel> UploadAttachmentChunk(FileChunkModel input);
    /// <summary>
    /// 分片组装.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    Task<FileMergeOutModel?> MergeAttachment(FileMergeModel input);
    /// <summary>
    /// 下载附件
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="filePath">文件路径</param>
    /// <param name="fileName">文件名称</param>
    /// <returns></returns>
    Task<byte[]> DownloadAttachment(string filePath, string fileName);
    /// <summary>
    /// 分片下载
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="filePath">OSS文件路径</param>
    /// <param name="byteStart">分片开始字节</param>
    /// <param name="byteEnd">分片结束字节</param>
    /// <param name="fileName">文件名称</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    Task DownloadAttachmentChunk(HttpContext httpContext, string filePath, string byteStart, string byteEnd, string fileName);
}
