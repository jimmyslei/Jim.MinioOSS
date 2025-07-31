using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeDetective;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Notification;
using Minio.DataModel.Result;
using Minio.Exceptions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Web;

namespace Jim.MinioOSS;

internal sealed class MinioOSSManage: IMinioOSSManage
{
    private readonly IMinioClient _client = null;
    private readonly string _defaultPolicyVersion = "2024-10-22";
    private readonly MinioOSSOptions _options;
    private readonly ILogger<MinioOSSManage> _logger;

    public MinioOSSManage(IMinioClient client, IOptions<MinioOSSOptions> options,ILogger<MinioOSSManage> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public IMinioClient Context
    {
        get
        {
            return _client;
        }
    }

    private static string GetMimeType(byte[] byteArray)
    {
        var inspector = new ContentInspectorBuilder()
        {
            Definitions = MimeDetective.Definitions.Default.All()
        }.Build();
        var results = inspector.Inspect(byteArray);
        var resultsByMimeType = results.ByMimeType();
        return resultsByMimeType.FirstOrDefault()?.MimeType ?? "application/octet-stream";
    }

    private string FormatObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || objectName == "/")
        {
            throw new ArgumentNullException(nameof(objectName));
        }
        if (objectName.StartsWith('/'))
        {
            return objectName.TrimStart('/');
        }
        return objectName;
    }

    private async Task<string> PresignedObjectAsync(string bucketName
            , string objectName
            , int expiresInt
            , PresignedObjectType type
            , Func<string, string, int, Task<string>> PresignedFunc)
    {
        try
        {
            if (string.IsNullOrEmpty(bucketName))
            {
                throw new ArgumentNullException(nameof(bucketName));
            }
            objectName = FormatObjectName(objectName);
            if (expiresInt <= 0)
            {
                throw new Exception("ExpiresIn time can not less than 0.");
            }
            if (expiresInt > 7 * 24 * 3600)
            {
                throw new Exception("ExpiresIn time no more than 7 days.");
            }
            string presignedUrl = await PresignedFunc(bucketName, objectName, expiresInt);
            if (string.IsNullOrEmpty(presignedUrl))
            {
                throw new Exception("Presigned object url failed.");
            }
            return presignedUrl;

        }
        catch (Exception ex)
        {
            throw new Exception($"Presigned {(type == PresignedObjectType.Get ? "get" : "put")} url for object '{objectName}' from {bucketName} failed. {ex.Message}", ex);
        }
    }


    #region Minio自有方法

    /// <summary>
    /// 删除一个未完整上传的对象。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="objectName">存储桶里的对象名称。</param>
    /// <returns></returns>
    public async Task<bool> RemoveIncompleteUploadAsync(string bucketName, string objectName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        RemoveIncompleteUploadArgs args = new RemoveIncompleteUploadArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);
        await _client.RemoveIncompleteUploadAsync(args);
        return true;
    }

    /// <summary>
    /// 列出存储桶中未完整上传的对象。
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    public async Task<List<ItemUploadInfo>> ListIncompleteUploads(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        ListIncompleteUploadsArgs args = new ListIncompleteUploadsArgs()
            .WithBucket(bucketName);
        var uploads = _client.ListIncompleteUploadsEnumAsync(args);

        List<ItemUploadInfo> result = new List<ItemUploadInfo>();
        var listUpload = await uploads.ToListAsync();
        listUpload.ForEach(item =>
        {
            result.Add(new ItemUploadInfo()
            {
                Key = item.Key,
                Initiated = item.Initiated,
                UploadId = item.UploadId,
            });
        });
        
        return await Task.FromResult(result);
    }

    /// <summary>
    /// 获取存储桶的权限
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    public async Task<PolicyInfo> GetPolicyAsync(string bucketName)
    {
        try
        {
            if (string.IsNullOrEmpty(bucketName))
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            var args = new GetPolicyArgs()
                .WithBucket(bucketName);
            string policyJson = await _client.GetPolicyAsync(args);
            if (string.IsNullOrEmpty(policyJson))
            {
                throw new Exception("Result policy json is null.");
            }
            return JsonConvert.DeserializeObject<PolicyInfo>(policyJson);
        }
        catch (MinioException ex)
        {
            if (!string.IsNullOrEmpty(ex.Message) && ex.Message.ToLower().Contains("the bucket policy does not exist"))
            {
                return new PolicyInfo()
                {
                    Version = _defaultPolicyVersion,
                    Statement = new List<StatementItem>()
                };
            }
            else
            {
                throw;
            }
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// 设置存储桶的权限
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <param name="statements">权限条目</param>
    /// <returns></returns>
    public async Task<bool> SetPolicyAsync(string bucketName, List<StatementItem> statements)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        if (statements == null || statements.Count == 0)
        {
            throw new ArgumentNullException(nameof(PolicyInfo));
        }

        List<StatementItem> oldStatements = null;
        List<StatementItem> addStatements = statements;
        List<StatementItem> tempStatements = new List<StatementItem>();
        //获取原有的
        PolicyInfo info = await GetPolicyAsync(bucketName);
        if (info.Statement == null)
        {
            info.Statement = new List<StatementItem>();
        }
        else
        {
            oldStatements = UnpackResource(info.Statement);
        }
        //解析要添加的条目，将包含多条Resource的条目解析为仅包含一条条目的数据
        statements = UnpackResource(statements);
        //验证要添加的数据
        foreach (var addItem in statements)
        {
            if (!addItem.Effect.Equals("Allow", StringComparison.OrdinalIgnoreCase)
                && !addItem.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Add statement effect only support 'Allow' or 'Deny'.");
            }
            if (addItem.Action == null || addItem.Action.Count == 0)
            {
                throw new Exception("Add statement action can not null");
            }
            if (addItem.Resource == null || addItem.Resource.Count == 0)
            {
                throw new Exception("Add statement resource can not null");
            }
            if (addItem.Principal == null || addItem.Principal.AWS == null || addItem.Principal.AWS.Count == 0)
            {
                addItem.Principal = new Principal()
                {
                    AWS = new List<string>()
                        {
                            "*"
                        }
                };
            }
        }
        if (oldStatements == null || oldStatements.Count == 0)
        {
            //没有Policy数据的情况，新建，修改或删除
            foreach (var addItem in statements)
            {
                //跳过删除
                if (addItem.IsDelete)
                {
                    continue;
                }
                tempStatements.Add(addItem);
            }
        }
        else
        {
            foreach (var addItem in addStatements)
            {
                foreach (var oldItem in oldStatements)
                {
                    //判断已经存在的条目是否包含现有要添加的条目
                    //如果存在条目，则更新；不存在条目，添加进去
                    if ((IsRootResource(bucketName, oldItem.Resource[0]) && IsRootResource(bucketName, addItem.Resource[0]))
                    || oldItem.Resource[0].Equals(addItem.Resource[0], StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        oldItem.IsDelete = true;  //就记录标识为删除，不重新添加到待添加列表中
                    }
                }
                if (!addItem.IsDelete)
                {
                    tempStatements.Add(addItem);
                }
            }
            foreach (var oldItem in oldStatements)
            {
                if (!oldItem.IsDelete)
                {
                    tempStatements.Add(oldItem);
                }
            }
        }
        //reset info
        info.Version = _defaultPolicyVersion;
        info.Statement = tempStatements;

        string policyJson = JsonConvert.SerializeObject(info);
        await _client.SetPolicyAsync(new SetPolicyArgs()
            .WithBucket(bucketName)
            .WithPolicy(policyJson));
        return true;
    }

    /// <summary>
    /// 移除全部存储桶的权限
    /// 如果要单独移除某个桶的权限，可以使用SetPolicyAsync，并将StatementItem中的IsDelete设置为true
    /// </summary>
    /// <param name="bucketName">存储桶名称。</param>
    /// <returns></returns>
    public async Task<bool> RemovePolicyAsync(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        var args = new RemovePolicyArgs().WithBucket(bucketName);
        await _client.RemovePolicyAsync(args);
        return true;
    }

    public async Task<bool> PolicyExistsAsync(string bucketName, StatementItem statement)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        if (statement == null
            || string.IsNullOrEmpty(statement.Effect)
            || (statement.Action == null || statement.Action.Count == 0)
            || (statement.Resource == null || statement.Resource.Count == 0))
        {
            throw new ArgumentNullException(nameof(StatementItem));
        }
        PolicyInfo info = await GetPolicyAsync(bucketName);
        if (info.Statement == null || info.Statement.Count == 0)
        {
            return false;
        }
        if (statement.Resource.Count > 1)
        {
            throw new Exception("Only support one resource.");
        }
        foreach (var item in info.Statement)
        {
            bool result = true;
            bool findSource = false;
            if (item.Resource.Count == 1)
            {
                if ((IsRootResource(bucketName, item.Resource[0]) && IsRootResource(bucketName, statement.Resource[0]))
                    || item.Resource[0].Equals(statement.Resource[0]))
                {
                    findSource = true;
                }
            }
            else
            {
                foreach (var sourceitem in item.Resource)
                {
                    if (sourceitem.Equals(statement.Resource[0])
                        && item.Effect.Equals(statement.Effect, StringComparison.OrdinalIgnoreCase))
                    {
                        findSource = true;
                    }
                }
            }
            if (!findSource) continue;
            //验证规则
            if (!item.Effect.Equals(statement.Effect))
            {
                //访问权限
                continue;
            }
            if (item.Action.Count < statement.Action.Count)
            {
                //动作，如果存在的条目数量少于要验证的，false
                continue;
            }
            foreach (var actionItem in statement.Action)
            {
                //验证action
                if (!item.Action.Any(p => p.Equals(actionItem, StringComparison.OrdinalIgnoreCase)))
                {
                    result = false;
                }
            }
            if (result)
            {
                return result;
            }
        }
        return false;
    }

    public async Task<List<MinioNotificationRaw>> ListenBucketNotificationsAsync(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        var events = new List<EventType> { EventType.ObjectCreatedAll };
        var args = new ListenBucketNotificationsArgs().WithBucket(bucketName).WithEvents(events).WithPrefix(null).WithSuffix(null);

        List<MinioNotificationRaw> result = new List<MinioNotificationRaw>();
        bool isFinish = false;
        var observable = _client.ListenBucketNotificationsAsync(args);
        observable.Subscribe(
            item =>
            {
                result.Add(new MinioNotificationRaw(item.Json));
            },
            ex =>
            {
                isFinish = true;
            },
            () =>
            {
                isFinish = true;
            });

        while (!isFinish)
        {
            Thread.Sleep(0);
        }
        return await Task.FromResult(result);
    }

    #endregion

    #region Bucket

    public async Task<bool> BucketExistsAsync(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        var args = new BucketExistsArgs().WithBucket(bucketName);
        return await _client.BucketExistsAsync(args);
    }

    public async Task<bool> CreateBucketAsync(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        bool found = await BucketExistsAsync(bucketName);
        if (found)
        {
            throw new Exception($"Bucket '{bucketName}' already exists.");
        }
        else
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs()
                    .WithBucket(bucketName)
                    .WithLocation(_options.Region));
            return true;
        }
    }

    public async Task<List<Bucket>> ListBucketsAsync()
    {
        ListAllMyBucketsResult list = await _client.ListBucketsAsync(); // minio.ListBucketsAsync(); 
        if (list == null || list.Buckets == null)
        {
            throw new Exception("List buckets failed, result obj is null");
        }
        List<Bucket> result = new List<Bucket>();
        foreach (var item in list.Buckets)
        {
            result.Add(new Bucket()
            {
                Name = item.Name,
                //Location = _options.Region,
                CreationDate = item.CreationDate,
                //Owner = new Owner()
                //{
                //    Id = _options.AccessKey,
                //    Name = _options.AccessKey,
                //}
            });
        }
        return result;
    }

    public async Task<bool> RemoveBucketAsync(string bucketName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        bool found = await BucketExistsAsync(bucketName);
        if (!found)
        {
            return true;
        }
        else
        {
            await _client.RemoveBucketAsync(new RemoveBucketArgs().WithBucket(bucketName));
            return true;
        }
    }

    public Task<bool> SetBucketAclAsync(string bucketName, AccessMode mode)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        List<StatementItem> statementItems = new List<StatementItem>();
        switch (mode)
        {
            case AccessMode.Private:
                {
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Deny",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:GetObject",
                                "s3:ListBucket",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                "arn:aws:s3:::*",
                            },
                        IsDelete = false
                    });

                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.PublicRead:
                {
                    //允许列出和下载
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Allow",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:GetObject",
                                "s3:ListBucket"
                            },
                        Resource = new List<string>()
                            {
                                "arn:aws:s3:::*",
                            },
                        IsDelete = false
                    });
                    //禁止删除和修改
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Deny",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                "arn:aws:s3:::*",
                            },
                        IsDelete = false
                    });
                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.PublicReadWrite:
                {
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Allow",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:GetObject",
                                "s3:ListBucket",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                "arn:aws:s3:::*",
                            },
                        IsDelete = false
                    });
                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.Default:
            default:
                {
                    return this.RemovePolicyAsync(bucketName);
                }
        }
    }

    public async Task<AccessMode> GetBucketAclAsync(string bucketName)
    {
        bool FindAction(List<string> actions, string input)
        {
            if (actions != null && actions.Count > 0 && actions.Exists(p => p.Equals(input, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        PolicyInfo info = await GetPolicyAsync(bucketName);
        if (info == null)
        {
            return AccessMode.Default;
        }
        if (info.Statement == null || info.Statement.Count == 0)
        {
            return AccessMode.Private;
        }
        List<StatementItem> statements = UnpackResource(info.Statement);

        bool isPublicRead = false;
        bool isPublicWrite = false;
        foreach (var item in statements)
        {
            if (!IsRootResource(bucketName, item.Resource[0]))
            {
                continue;
            }
            if (item.Action == null || item.Action.Count == 0)
            {
                continue;
            }
            if (item.Effect.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                if (FindAction(item.Action, "*"))
                {
                    return AccessMode.PublicReadWrite;
                }
                if (FindAction(item.Action, "s3:GetObject"))
                {
                    isPublicRead = true;
                }
                if (FindAction(item.Action, "s3:PutObject"))
                {
                    isPublicWrite = true;
                }
            }
            if (isPublicRead && isPublicWrite)
            {
                return AccessMode.PublicReadWrite;
            }
        }
        //结果
        if (isPublicRead && !isPublicWrite)
        {
            return AccessMode.PublicRead;
        }
        else if (isPublicRead && isPublicWrite)
        {
            return AccessMode.PublicReadWrite;
        }
        else if (!isPublicRead && isPublicWrite)
        {
            return AccessMode.Private;
        }
        else
        {
            return AccessMode.Private;
        }
    }

    #endregion

    #region Object

    public async Task<bool> ObjectsExistsAsync(string bucketName, string objectName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        try
        {
            var result = await GetObjectMetadataAsync(bucketName, objectName);
            return result != null;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Item>> ListObjectsAsync(string bucketName, string prefix = null)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        var observable = _client.ListObjectsEnumAsync(
            new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true));
        List<Item> result = new List<Item>();
        var subscription = await observable.ToListAsync();
        subscription.ForEach(item =>
            {
                result.Add(new Item()
                {
                    Key = item.Key,
                    LastModified = item.LastModified,
                    ETag = item.ETag,
                    Size = item.Size,
                    IsDir = item.IsDir,
                    ContentType = item.ContentType,
                    Expires = item.Expires,
                    VersionId = item.VersionId,
                    UserMetadata = item.UserMetadata,
                    IsLatest = item.IsLatest,
                });
            });

        return await Task.FromResult(result);
    }

    public async Task GetObjectAsync(string bucketName, string objectName,long offset, long length, Action<Stream> callback, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        GetObjectArgs args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithOffsetAndLength(offset,length)
            .WithCallbackStream((stream) =>
            {
                callback(stream);
            });
        _ = await _client.GetObjectAsync(args, cancellationToken);
    }

    public async Task GetObjectAsync(string bucketName, string objectName, Action<Stream> callback, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        GetObjectArgs args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream((stream) =>
            {
                callback(stream);
            });
        _ = await _client.GetObjectAsync(args, cancellationToken);
    }

    public async Task GetObjectAsync(string bucketName, string objectName, string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        string fullPath = Path.GetFullPath(fileName);
        string parentPath = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentPath) && !Directory.Exists(parentPath))
        {
            Directory.CreateDirectory(parentPath);
        }
        objectName = FormatObjectName(objectName);
        GetObjectArgs args = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithCallbackStream((stream) =>
            {
                using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                    stream.Dispose();
                    fs.Close();
                }
            });
        _ = await _client.GetObjectAsync(args, cancellationToken);
    }

    public async Task<bool> PutObjectAsync(string bucketName, string objectName, Stream data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        string contentType = "application/octet-stream";
        if (data is FileStream fileStream)
        {
            string fileName = fileStream.Name;
            if (!string.IsNullOrEmpty(fileName))
            {
                new FileExtensionContentTypeProvider().TryGetContentType(fileName, out contentType);
            }
        }
        else
        {
            new FileExtensionContentTypeProvider().TryGetContentType(objectName, out contentType);
        }
        if (string.IsNullOrEmpty(contentType))
        {
            contentType = "application/octet-stream";
        }
        PutObjectArgs args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(data.Length)
            .WithContentType(contentType);
        var result = await _client.PutObjectAsync(args, cancellationToken);
        if (result.Size > 0)
            return true;
        return false;
    }

    public async Task<bool> PutObjectAsync(string bucketName, string objectName, string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        if (!File.Exists(filePath))
        {
            throw new Exception("File not exist.");
        }
        string fileName = Path.GetFileName(filePath);
        string contentType = null;
        if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out contentType))
        {
            contentType = "application/octet-stream";
        }
        PutObjectArgs args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithFileName(filePath)
            .WithContentType(contentType);
        var result = await _client.PutObjectAsync(args, cancellationToken);
        if (result.Size > 0)
            return true;
        return false;
    }

    public async Task<ItemMeta> GetObjectMetadataAsync(string bucketName
        , string objectName
        , string versionID = null
        , string matchEtag = null
        , DateTime? modifiedSince = null)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        StatObjectArgs args = new StatObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithVersionId(versionID)
            .WithMatchETag(matchEtag);
        if (modifiedSince.HasValue)
        {
            args = args.WithModifiedSince(modifiedSince.Value);
        }
        ObjectStat statObject = await _client.StatObjectAsync(args);

        return new ItemMeta()
        {
            ObjectName = statObject.ObjectName,
            Size = statObject.Size,
            LastModified = statObject.LastModified,
            ETag = statObject.ETag,
            ContentType = statObject.ContentType,
            IsEnableHttps = _options.IsEnableHttps,
            MetaData = statObject.MetaData,
        };
    }

    public async Task<bool> CopyObjectAsync(string bucketName, string objectName, string destBucketName = null, string destObjectName = null)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        if (string.IsNullOrEmpty(destBucketName))
        {
            destBucketName = bucketName;
        }
        destObjectName = FormatObjectName(destObjectName);
        CopySourceObjectArgs cpSrcArgs = new CopySourceObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);
        CopyObjectArgs args = new CopyObjectArgs()
            .WithBucket(destBucketName)
            .WithObject(destObjectName)
            .WithCopyObjectSource(cpSrcArgs);
        await _client.CopyObjectAsync(args);
        return true;
    }

    public async Task<bool> RemoveObjectAsync(string bucketName, string objectName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        RemoveObjectArgs args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);
        await _client.RemoveObjectAsync(args);
        return true;
    }

    public async Task<bool> RemoveObjectAsync(string bucketName, List<string> objectNames)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        if (objectNames == null || objectNames.Count == 0)
        {
            throw new ArgumentNullException(nameof(objectNames));
        }
        List<string> delObjects = new List<string>();
        foreach (var item in objectNames)
        {
            delObjects.Add(FormatObjectName(item));
        }
        RemoveObjectsArgs args = new RemoveObjectsArgs()
            .WithBucket(bucketName)
            .WithObjects(delObjects);
        IList<DeleteError> observable = await _client.RemoveObjectsAsync(args);
        List<string> removeFailed = new List<string>();
        var subscription = observable.ToList();
        subscription.ForEach(err =>
           {
               removeFailed.Add(err.Key);
           });
        
        if (removeFailed.Count > 0)
        {
            if (removeFailed.Count == delObjects.Count)
            {
                throw new Exception("Remove all object failed.");
            }
            else
            {
                throw new Exception($"Remove objects '{string.Join(",", removeFailed)}' from {bucketName} failed.");
            }
        }
        return true;
    }

    /// <summary>
    /// 生成一个临时连接
    /// </summary>
    /// <param name="bucketName"></param>
    /// <param name="objectName"></param>
    /// <param name="expiresInt"></param>
    /// <returns></returns>
    public Task<string> PresignedGetObjectAsync(string bucketName, string objectName, int expiresInt)
    {
        return PresignedObjectAsync(bucketName
            , objectName
            , expiresInt
            , PresignedObjectType.Get
            , async (bucketName, objectName, expiresInt) =>
            {
                objectName = FormatObjectName(objectName);
                //生成URL
                AccessMode accessMode = await this.GetObjectAclAsync(bucketName, objectName);
                if (accessMode == AccessMode.PublicRead || accessMode == AccessMode.PublicReadWrite)
                {
                    return $"{(_options.IsEnableHttps ? "https" : "http")}://{_options.Endpoint}/{bucketName}{(objectName.StartsWith("/") ? objectName : $"/{objectName}")}";
                }
                else
                {
                    PresignedGetObjectArgs args = new PresignedGetObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName)
                        .WithExpiry(expiresInt);
                    return await _client.PresignedGetObjectAsync(args);
                }
            });
    }

    public Task<string> PresignedPutObjectAsync(string bucketName, string objectName, int expiresInt)
    {
        return PresignedObjectAsync(bucketName
            , objectName
            , expiresInt
            , PresignedObjectType.Put
            , async (bucketName, objectName, expiresInt) =>
            {
                objectName = FormatObjectName(objectName);
                //生成URL
                PresignedPutObjectArgs args = new PresignedPutObjectArgs()
                        .WithBucket(bucketName)
                        .WithObject(objectName)
                        .WithExpiry(expiresInt);
                return await _client.PresignedPutObjectAsync(args);
            });
    }

    /// <summary>
    /// 将应用程序详细信息添加到User-Agent。
    /// </summary>
    /// <param name="appName">执行API请求的应用程序的名称</param>
    /// <param name="appVersion">执行API请求的应用程序的版本</param>
    /// <returns></returns>
    public Task SetAppInfo(string appName, string appVersion)
    {
        if (string.IsNullOrEmpty(appName))
        {
            throw new ArgumentNullException(nameof(appName));
        }
        if (string.IsNullOrEmpty(appVersion))
        {
            throw new ArgumentNullException(nameof(appVersion));
        }
        _client.SetAppInfo(appName, appVersion);
        return Task.FromResult(true);
    }

    public Task<bool> SetObjectAclAsync(string bucketName, string objectName, AccessMode mode)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        if (!objectName.StartsWith(bucketName))
        {
            objectName = $"{bucketName}/{objectName}";
        }
        List<StatementItem> statementItems = new List<StatementItem>();
        switch (mode)
        {
            case AccessMode.Private:
                {
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Deny",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:GetObject",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                $"arn:aws:s3:::{objectName}",
                            },
                        IsDelete = false
                    });
                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.PublicRead:
                {
                    //允许列出和下载
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Allow",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:GetObject"
                            },
                        Resource = new List<string>()
                            {
                                $"arn:aws:s3:::{objectName}",
                            },
                        IsDelete = false
                    });
                    //禁止删除和修改
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Deny",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                $"arn:aws:s3:::{objectName}",
                            },
                        IsDelete = false
                    });
                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.PublicReadWrite:
                {
                    statementItems.Add(new StatementItem()
                    {
                        Effect = "Allow",
                        Principal = new Principal()
                        {
                            AWS = new List<string>()
                                {
                                    "*"
                                }
                        },
                        Action = new List<string>()
                            {
                                "s3:DeleteObject",
                                "s3:GetObject",
                                "s3:PutObject"
                            },
                        Resource = new List<string>()
                            {
                                $"arn:aws:s3:::{objectName}",
                            },
                        IsDelete = false
                    });
                    return this.SetPolicyAsync(bucketName, statementItems);
                }
            case AccessMode.Default:
            default:
                {
                    throw new ArgumentNullException($"Unsupport access mode '{mode}'");
                }
        }
    }

    public async Task<AccessMode> GetObjectAclAsync(string bucketName, string objectName)
    {
        bool FindAction(List<string> actions, string input)
        {
            if (actions != null && actions.Count > 0 && actions.Exists(p => p.Equals(input, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        if (!objectName.StartsWith(bucketName))
        {
            objectName = $"{bucketName}/{objectName}";
        }
        //获取存储桶默认权限
        AccessMode bucketMode = await GetBucketAclAsync(bucketName);
        PolicyInfo info = await GetPolicyAsync(bucketName);
        if (info == null || info.Statement == null || info.Statement.Count == 0)
        {
            return bucketMode;
        }
        List<StatementItem> statements = UnpackResource(info.Statement);

        bool isPublicRead = false;
        bool isPublicWrite = false;
        switch (bucketMode)
        {
            case AccessMode.PublicRead:
                {
                    isPublicRead = true;
                    isPublicWrite = false;
                    break;
                }
            case AccessMode.PublicReadWrite:
                {
                    isPublicRead = true;
                    isPublicWrite = true;
                    break;
                }
            case AccessMode.Default:
            case AccessMode.Private:
            default:
                {
                    isPublicRead = false;
                    isPublicWrite = false;
                    break;
                }
        }

        foreach (var item in statements)
        {
            if (!item.Resource[0].Equals($"arn:aws:s3:::{objectName}")
                && !item.Resource[0].Equals($"{objectName}"))
            {
                continue;
            }
            if (item.Action == null || item.Action.Count == 0)
            {
                continue;
            }
            if (item.Effect.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                if (FindAction(item.Action, "*"))
                {
                    return AccessMode.PublicReadWrite;
                }
                if (FindAction(item.Action, "s3:GetObject"))
                {
                    isPublicRead = true;
                }
                if (FindAction(item.Action, "s3:PutObject"))
                {
                    isPublicWrite = true;
                }
            }
            else if (item.Effect.Equals("Deny", StringComparison.OrdinalIgnoreCase))
            {
                if (FindAction(item.Action, "*"))
                {
                    return AccessMode.Private;
                }
                if (FindAction(item.Action, "s3:GetObject"))
                {
                    isPublicRead = false;
                }
                if (FindAction(item.Action, "s3:PutObject"))
                {
                    isPublicWrite = false;
                }
            }
        }
        //结果
        if (isPublicRead && !isPublicWrite)
        {
            return AccessMode.PublicRead;
        }
        else if (isPublicRead && isPublicWrite)
        {
            return AccessMode.PublicReadWrite;
        }
        else if (!isPublicRead && isPublicWrite)
        {
            return AccessMode.Private;
        }
        else
        {
            return AccessMode.Private;
        }
    }

    public async Task<AccessMode> RemoveObjectAclAsync(string bucketName, string objectName)
    {
        if (string.IsNullOrEmpty(bucketName))
        {
            throw new ArgumentNullException(nameof(bucketName));
        }
        objectName = FormatObjectName(objectName);
        if (!objectName.StartsWith(bucketName))
        {
            objectName = $"{bucketName}/{objectName}";
        }
        PolicyInfo info = await GetPolicyAsync(bucketName);
        if (info == null || info.Statement == null || info.Statement.Count == 0)
        {
            return await GetObjectAclAsync(bucketName, objectName);
        }
        List<StatementItem> statements = UnpackResource(info.Statement);
        bool hasUpdate = false;
        foreach (var item in statements)
        {
            if (item.Resource[0].Equals($"arn:aws:s3:::{objectName}")
                || item.Resource[0].Equals($"{objectName}"))
            {
                hasUpdate = true;
                item.IsDelete = true;
            }
        }
        if (hasUpdate)
        {
            if (!await SetPolicyAsync(bucketName, statements))
            {
                throw new Exception("Save new policy info failed when remove object acl.");
            }
        }
        return await GetObjectAclAsync(bucketName, objectName);
    }

    #region private

    private List<StatementItem> UnpackResource(List<StatementItem> source)
    {
        List<StatementItem> dest = new List<StatementItem>();
        if (source == null || source.Count == 0)
        {
            return dest;
        }
        foreach (var item in source)
        {
            if (item.Resource == null || item.Resource.Count == 0)
            {
                continue;
            }
            else if (item.Resource.Count > 0)
            {
                foreach (var resourceItem in item.Resource)
                {
                    StatementItem newItem = new StatementItem()
                    {
                        Effect = item.Effect,
                        Principal = item.Principal,
                        Action = item.Action,
                        Resource = new List<string>()
                            {
                                resourceItem
                            },
                        IsDelete = item.IsDelete
                    };
                    dest.Add(newItem);
                }
            }
            else
            {
                dest.Add(item);
            }
        }
        return dest;
    }

    private bool IsRootResource(string bucketName, string resource)
    {
        if (resource.StartsWith("*", StringComparison.OrdinalIgnoreCase)
            || resource.StartsWith("arn:aws:s3:::*", StringComparison.OrdinalIgnoreCase)
            || resource.StartsWith($"arn:aws:s3:::{bucketName}*", StringComparison.OrdinalIgnoreCase)
            || resource.StartsWith($"arn:aws:s3:::{bucketName}/*", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    #endregion

    #endregion


    #region 分片上传文件

    /// <summary>
    /// 分片上传附件.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<FileUploadOutModel> UploadAttachmentChunk(FileChunkModel input)
    {
        // 碎片临时文件存储路径
        // Directory.GetCurrentDirectory()
        string directoryPath = Path.Combine("TempFile", input.md5);
        try
        {
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await input.file.OpenReadStream().CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (string.IsNullOrEmpty(input.md5))
            {
                throw new Exception("md5 not allow null");
            }
            if (input.chunkNumber < 1)
            {
                throw new Exception("chunkNumber must greater than 0");
            }
            if (input.chunkNumber > input.chunkCount)
            {
                throw new Exception("totalChunks must greater than chunkNumber");
            }

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // 碎片文件名称
            string chunkFileName = string.Format("{0}{1}{2}{3}", input.md5, "-", input.chunkNumber, ".chunk");
            string chunkFilePath = Path.Combine(directoryPath, chunkFileName);
            if (!File.Exists(chunkFilePath))
            {
                using (var streamLocal = File.Create(chunkFilePath))
                {
                    using (var ms = new MemoryStream(bytes))
                    {
                        await ms.CopyToAsync(streamLocal);
                    }
                }
            }

            return new FileUploadOutModel { success = true, merge = input.chunkNumber == input.chunkCount };
        }
        catch (Exception ex)
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
            throw new Exception("file upload fail");
        }
    }

    /// <summary>
    /// 分片组装.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async Task<FileMergeOutModel?> MergeAttachment(FileMergeModel input)
    {
        var fileLength = 0L; // 文件长度
        try
        {
            input.type ??= "";
            if (string.IsNullOrEmpty(input.md5))
            {
                throw new Exception("md5 not allow null");
            }
            if (string.IsNullOrEmpty(input.fileName))
            {
                throw new Exception("fileName not allow null");
            }
            var tempDirectoryPath = "TempFile";
            var timeNow = DateTime.Now;
            // 新文件名称
            var saveFileName = string.Format("{0}_{1}{2}", timeNow.ToString("yyyyMMdd"), Guid.NewGuid(), Path.GetExtension(input.fileName));
            // 碎片临时文件存储路径 
            var directoryPath = Path.Combine(tempDirectoryPath, input.md5);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            var chunkFiles = Directory.GetFiles(directoryPath, "*.chunk");
            if (chunkFiles.Length != input.chunkCount)
            {
                throw new Exception("error. chunkFiles.Length != input.totalChunks");
            }
            List<byte> byteSource = new List<byte>();
            var fs = new FileStream(Path.Combine(directoryPath, saveFileName), FileMode.Create);
            fs.Seek(0, SeekOrigin.Begin);
            fs.SetLength(0);
            for (var index = 1; index <= chunkFiles.Length; index++)
            {
                var chunkFileName = string.Join("", new string[] { input.md5, "-", index.ToString(), ".chunk" });
                var chunkFilePath = Path.Combine(directoryPath, chunkFileName);
                if (!File.Exists(chunkFilePath))
                {
                    throw new Exception("chunk file not exist! " + chunkFileName);
                }
                var bytes = File.ReadAllBytes(chunkFilePath);
                if (bytes != null)
                {
                    fileLength += bytes.Length;
                    fs.Write(bytes, 0, bytes.Length);
                }
                bytes = null;
            }
            fs.Flush();
            fs.Close();
            fs.Dispose();
            
            var mergedFilePath = Path.Combine(directoryPath, saveFileName);
            var uploadPath = string.Empty; // 上传路径

            if (string.IsNullOrEmpty(input.type))
            {
                uploadPath = $"base/{timeNow.ToString("yyyyMMdd")}/{input.md5}";
            }
            else
            {
                uploadPath = $"{input.type}/{timeNow.ToString("yyyyMMdd")}/{input.md5}";
            }
            
            // 在后台线程上传文件，不阻塞当前请求
            _ = Task.Run(async () =>
            {
                try
                {
                    // 存储桶是否存在
                    var bucketExist = await BucketExistsAsync(_options.BucketName);
                    if (!bucketExist) await CreateBucketAsync(_options.BucketName);
                    
                    // 上传文件
                    using (Stream stream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var ossObj = await PutObjectAsync(_options.BucketName, uploadPath, stream);
                        if (!ossObj)
                        {
                            // 记录日志，上传失败
                            _logger.LogError("File upload failed. Path: {Path}", uploadPath);
                            throw new Exception($"file upload failed. path:{uploadPath}");
                        }
                    }
                    // 清理临时文件
                    if (Directory.Exists(directoryPath))
                        Directory.Delete(directoryPath, true);
                }
                catch (Exception ex)
                {
                    // 记录异常日志
                    _logger.LogError(ex, "Background file upload failed. Path: {Path}", uploadPath);
                    throw new Exception("Background file upload failed: " + ex.Message);
                }
            });

            var contentType = "";
            new FileExtensionContentTypeProvider().TryGetContentType(input.fileName, out contentType);
            if (string.IsNullOrEmpty(contentType)) contentType = "application/octet-stream";
            return new FileMergeOutModel()
            {
                //id = Guid.NewGuid().ToString(),
                name = input.fileName,
                mimeType = contentType,
                size = fileLength,
                md5 = input.md5,
                path = uploadPath,
            };
        }
        catch (Exception ex)
        {
            throw new Exception("file upload failed " + ex);
        }
    }

    /// <summary>
    /// 下载附件
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="filePath">OSS文件路径</param>
    /// <param name="fileName">文件名</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<byte[]> DownloadAttachment(string filePath,string fileName)
    {
        byte[]? bytes = null;
        var objectName = FormatObjectName(filePath);
        var exists = await ObjectsExistsAsync(_options.BucketName, objectName);
        if (!exists) throw new Exception("file not find");
        await GetObjectAsync(_options.BucketName, objectName, (stream) =>
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            //httpContext.Response.ContentType = "application/octet-stream;charset=UTF-8"; //fileStreamResult.ContentType;
        });
        
        if (bytes == null)
        {
            throw new Exception("file not find");
        }

        return bytes;
        //httpContext.Response.Headers.Append("Content-Disposition", "attachment;filename=" + HttpUtility.UrlEncode(fileName, Encoding.UTF8));
        //httpContext.Response.Headers.Append("Content-Length", bytes.Length.ToString());
        //await httpContext.Response.Body.WriteAsync(bytes);
        //await httpContext.Response.Body.FlushAsync();
        //httpContext.Response.Body.Close();
    }

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
    public async Task DownloadAttachmentChunk(HttpContext httpContext, string filePath,string byteStart,string byteEnd, string fileName)
    {
        var objectName = FormatObjectName(filePath);
        var objectInfo = await GetObjectMetadataAsync(_options.BucketName, objectName);
        var exists = objectInfo != null;
        if (!exists) throw new Exception("file not find");
        // 计算文件分片
        long startByte = 0; // 开始下载位置
        long fileSize = objectInfo.Size;
        long endByte = fileSize - 1; // 结束下载位置

        // 类型一：bytes=-2343 后端转换为 0-2343
        if (string.IsNullOrEmpty(byteStart)) endByte = long.Parse(byteEnd);
        // 类型二：bytes=2343- 后端转换为 2343-最后
        if (string.IsNullOrEmpty(byteEnd)) startByte = long.Parse(byteStart);
        else
        { // 类型三：bytes=22-2343
            startByte = long.Parse(byteStart);
            endByte = long.Parse(byteEnd);
        }
        // 要下载的长度
        // 确保返回的 contentLength 不会超过文件的实际剩余大小
        long contentLength = Math.Min(endByte - startByte + 1, fileSize - startByte);

        byte[]? bytes = null;
        await GetObjectAsync(_options.BucketName, objectName, startByte, contentLength, (stream) =>
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            httpContext.Response.ContentType = "application/octet-stream;charset=UTF-8";
        });

        if (bytes == null)
        {
            throw new Exception("file not find");
        }
        httpContext.Response.Headers.Append("Accept-Ranges", "bytes");
        httpContext.Response.Headers.Append("Content-Range", "bytes " + startByte + "-" + endByte + "/" + fileSize);
        httpContext.Response.Headers.Append("Content-Disposition", "attachment;filename=" + HttpUtility.UrlEncode(fileName, Encoding.UTF8));
        httpContext.Response.Headers.Append("Content-Length", bytes.Length.ToString());
        await httpContext.Response.Body.WriteAsync(bytes);
        await httpContext.Response.Body.FlushAsync();
        httpContext.Response.Body.Close();
    }

    #endregion


}
