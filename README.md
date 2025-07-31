# Jim.MinioOSS

[![NuGet](https://img.shields.io/nuget/v/Jim.MinioOSS.svg)](https://www.nuget.org/packages/Jim.MinioOSS/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6.0%7C7.0%7C8.0-blue.svg)](https://dotnet.microsoft.com/)

Jim.MinioOSS 是一个功能强大的 .NET 对象存储库，基于 MinIO SDK 构建，提供了简单易用的 API 来管理 MinIO/S3 兼容的对象存储服务。支持分片上传、断点续传、权限管理、预签名 URL 等高级功能。

## ✨ 特性

- 🚀 **多框架支持**: 支持 .NET 6.0、7.0、8.0
- 🔧 **简单易用**: 简洁的 API 设计，易于集成
- 📦 **完整功能**: 支持所有 MinIO 核心功能
- 🔄 **分片上传**: 支持大文件分片上传和断点续传
- 🔐 **权限管理**: 细粒度的存储桶和对象权限控制
- 🔗 **预签名 URL**: 支持临时访问 URL 生成
- 📊 **元数据管理**: 完整的对象元数据操作
- 🎯 **强类型**: 完全强类型的 API 设计
- 🔍 **缓存支持**: 可选的缓存机制

## 📦 安装

### NuGet 包管理器

```bash
dotnet add package Jim.MinioOSS
```

### Package Manager Console

```powershell
Install-Package Jim.MinioOSS
```

## 🚀 快速开始

### 1. 配置 appsettings.json

```json
{
  "OSS": {
    "Endpoint": "127.0.0.1:9000",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key",
    "BucketName": "your-bucket-name",
    "IsEnableHttps": false,
    "Region": "us-east-1"
  }
}
```

### 2. 注册服务

在 `Program.cs` 或 `Startup.cs` 中：

```csharp
// ASP.NET Core
builder.Services.AddMinioOSS();

// 或者手动配置
builder.Services.AddMinioOSS(options =>
{
    options.Endpoint = "127.0.0.1:9000";
    options.AccessKey = "your-access-key";
    options.SecretKey = "your-secret-key";
    options.BucketName = "your-bucket-name";
    options.IsEnableHttps = false;
});
```

### 3. 使用服务

```csharp
public class FileService
{
    private readonly IMinioOSSManage _oss;

    public FileService(IMinioOSSManage oss)
    {
        _oss = oss;
    }

    public async Task UploadFileAsync(string filePath, string objectName)
    {
        await _oss.PutObjectAsync("my-bucket", objectName, filePath);
    }

    public async Task<byte[]> DownloadFileAsync(string objectName)
    {
        using var memoryStream = new MemoryStream();
        await _oss.GetObjectAsync("my-bucket", objectName, stream =>
        {
            stream.CopyTo(memoryStream);
        });
        return memoryStream.ToArray();
    }
}
```

## 📋 API 文档

### 存储桶操作

#### 创建存储桶

```csharp
await _oss.CreateBucketAsync("my-bucket");
```

#### 检查存储桶是否存在

```csharp
bool exists = await _oss.BucketExistsAsync("my-bucket");
```

#### 列出所有存储桶

```csharp
var buckets = await _oss.ListBucketsAsync();
foreach (var bucket in buckets)
{
    Console.WriteLine($"Bucket: {bucket.Name}, Created: {bucket.CreationDate}");
}
```

#### 删除存储桶

```csharp
await _oss.RemoveBucketAsync("my-bucket");
```

#### 设置存储桶权限

```csharp
// 设置为公共读
await _oss.SetBucketAclAsync("my-bucket", AccessMode.PublicRead);

// 设置为私有
await _oss.SetBucketAclAsync("my-bucket", AccessMode.Private);
```

### 对象操作

#### 上传对象

**从文件上传：**

```csharp
await _oss.PutObjectAsync("my-bucket", "file.txt", "/path/to/file.txt");
```

**从流上传：**

```csharp
using var stream = File.OpenRead("/path/to/file.txt");
await _oss.PutObjectAsync("my-bucket", "file.txt", stream);
```

#### 下载对象

**下载到文件：**

```csharp
await _oss.GetObjectAsync("my-bucket", "file.txt", "/path/to/save/file.txt");
```

**下载到流：**

```csharp
using var memoryStream = new MemoryStream();
await _oss.GetObjectAsync("my-bucket", "file.txt", stream =>
{
    stream.CopyTo(memoryStream);
});
```

#### 分片下载

```csharp
// 下载文件的一部分（分片下载）
await _oss.GetObjectAsync("my-bucket", "large-file.zip", 0, 1024 * 1024, stream =>
{
    // 处理前1MB的数据
});
```

#### 列出对象

```csharp
// 列出存储桶中的所有对象
var objects = await _oss.ListObjectsAsync("my-bucket");

// 按前缀过滤
var objects = await _oss.ListObjectsAsync("my-bucket", "folder/");
```

#### 删除对象

```csharp
// 删除单个对象
await _oss.RemoveObjectAsync("my-bucket", "file.txt");

// 批量删除
await _oss.RemoveObjectAsync("my-bucket", new List<string> { "file1.txt", "file2.txt" });
```

#### 复制对象

```csharp
// 在同一存储桶内复制
await _oss.CopyObjectAsync("my-bucket", "source.txt", "my-bucket", "dest.txt");

// 跨存储桶复制
await _oss.CopyObjectAsync("source-bucket", "file.txt", "dest-bucket", "file.txt");
```

### 对象元数据

#### 获取对象元数据

```csharp
var metadata = await _oss.GetObjectMetadataAsync("my-bucket", "file.txt");
Console.WriteLine($"Size: {metadata.Size}");
Console.WriteLine($"Content-Type: {metadata.ContentType}");
Console.WriteLine($"Last-Modified: {metadata.LastModified}");
Console.WriteLine($"ETag: {metadata.ETag}");
```

#### 设置对象权限

```csharp
// 设置为公共读
await _oss.SetObjectAclAsync("my-bucket", "file.txt", AccessMode.PublicRead);

// 获取对象权限
var acl = await _oss.GetObjectAclAsync("my-bucket", "file.txt");
```

### 预签名 URL

#### 生成下载 URL

```csharp
// 生成有效期为1小时的下载URL
string url = await _oss.PresignedGetObjectAsync("my-bucket", "file.txt", 3600);
```

#### 生成上传 URL

```csharp
// 生成有效期为1小时的上传URL
string url = await _oss.PresignedPutObjectAsync("my-bucket", "file.txt", 3600);
```

### 分片上传

#### 分片上传文件

```csharp
// 上传分片
var chunkModel = new FileChunkModel
{
    file = formFile, // IFormFile
    md5 = "file-md5-hash",
    chunkNumber = 1,
    chunkCount = 10
};

var result = await _oss.UploadAttachmentChunk(chunkModel);
```

#### 合并分片

```csharp
var mergeModel = new FileMergeModel
{
    // 配置合并参数
    fileName = "large-file.zip",
    md5 = "file-md5-hash",
    totalChunks = 10
};

var result = await _oss.MergeAttachment(mergeModel);
```

### 权限管理

#### 存储桶策略

```csharp
// 获取存储桶策略
var policy = await _oss.GetPolicyAsync("my-bucket");

// 设置存储桶策略
var statements = new List<StatementItem>
{
    new StatementItem
    {
        Effect = "Allow",
        Principal = new Principal { AWS = "*" },
        Action = new List<string> { "s3:GetObject" },
        Resource = new List<string> { "arn:aws:s3:::my-bucket/*" }
    }
};

await _oss.SetPolicyAsync("my-bucket", statements);

// 移除存储桶策略
await _oss.RemovePolicyAsync("my-bucket");
```

### 事件通知

```csharp
// 监听存储桶事件通知
var notifications = await _oss.ListenBucketNotificationsAsync("my-bucket");
foreach (var notification in notifications)
{
    Console.WriteLine($"Event: {notification.EventName}");
    Console.WriteLine($"Key: {notification.S3.Object.Key}");
}
```

### 断点续传

#### 列出未完成的传输

```csharp
var incompleteUploads = await _oss.ListIncompleteUploads("my-bucket");
foreach (var upload in incompleteUploads)
{
    Console.WriteLine($"Upload ID: {upload.UploadId}");
    Console.WriteLine($"Key: {upload.Key}");
    Console.WriteLine($"Initiated: {upload.Initiated}");
}
```

#### 取消未完成的上传

```csharp
await _oss.RemoveIncompleteUploadAsync("my-bucket", "large-file.zip");
```

## 🏗️ 高级配置

### 自定义 MinIO 客户端

```csharp
services.AddMinio(configureClient => configureClient
    .WithEndpoint("play.min.io")
    .WithSSL(true)
    .WithCredentials("Q3AM3UQ867SPQQA43P2F", "zuf+tfteSlswRu7BJ86wekitnifILbZam1KYY3TG")
    .WithRegion("us-east-1")
    .Build());
```

### 使用环境变量配置

```bash
export MINIO_ENDPOINT=127.0.0.1:9000
export MINIO_ACCESS_KEY=your-access-key
export MINIO_SECRET_KEY=your-secret-key
export MINIO_BUCKET_NAME=your-bucket-name
```

### 多环境配置

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "OSS": {
    "Endpoint": "127.0.0.1:9000",
    "AccessKey": "dev-access-key",
    "SecretKey": "dev-secret-key",
    "BucketName": "dev-bucket",
    "IsEnableHttps": false
  },
  "Production": {
    "OSS": {
      "Endpoint": "minio.example.com",
      "AccessKey": "prod-access-key",
      "SecretKey": "prod-secret-key",
      "BucketName": "prod-bucket",
      "IsEnableHttps": true
    }
  }
}
```

## 🧪 测试

### 单元测试示例

```csharp
[TestClass]
public class MinioOSSTests
{
    private IMinioOSSManage _oss;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddMinioOSS(options =>
        {
            options.Endpoint = "127.0.0.1:9000";
            options.AccessKey = "test-access-key";
            options.SecretKey = "test-secret-key";
            options.BucketName = "test-bucket";
            options.IsEnableHttps = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        _oss = serviceProvider.GetRequiredService<IMinioOSSManage>();
    }

    [TestMethod]
    public async Task UploadAndDownloadFile_ShouldWork()
    {
        // Arrange
        var testFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(testFile, "Hello, MinIO!");

        // Act
        await _oss.PutObjectAsync("test-bucket", "test.txt", testFile);
        var downloadedContent = await _oss.GetObjectAsync("test-bucket", "test.txt");

        // Assert
        Assert.IsNotNull(downloadedContent);
    }
}
```

## 🐛 故障排除

### 常见问题

#### 1. 连接超时

```csharp
// 增加超时时间
services.AddMinio(configureClient => configureClient
    .WithEndpoint("your-endpoint")
    .WithSSL(false)
    .WithCredentials("access-key", "secret-key")
    .WithTimeout(60000) // 60秒超时
    .Build());
```

#### 2. 权限错误

确保：

- AccessKey 和 SecretKey 正确
- 用户具有相应的存储桶权限
- 存储桶策略配置正确

#### 3. 大文件上传失败

对于大文件上传：

- 使用分片上传功能
- 检查网络连接稳定性
- 增加客户端超时时间

### 调试日志

```csharp
// 启用详细日志
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 开源。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📞 支持

如有问题，请通过以下方式联系：

- 提交 [GitHub Issue](https://github.com/your-repo/issues)
- 发送邮件至：your-email@example.com

## 🙏 致谢

- [MinIO](https://min.io/) - 提供优秀的对象存储服务
- [.NET Community](https://dotnet.microsoft.com/) - 强大的 .NET 生态系统
