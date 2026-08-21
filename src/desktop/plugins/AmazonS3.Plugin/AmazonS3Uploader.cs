#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Amazon.Runtime;
using Amazon.S3;
using ShareX.AmazonS3.Plugin.Multipart;
using System.Collections.Specialized;
using System.Globalization;
using System.Security.Cryptography;
using XerahS.Common;
using XerahS.Uploaders;
using XerahS.Uploaders.Multipart;

namespace ShareX.AmazonS3.Plugin;

/// <summary>
/// Amazon S3 uploader - supports basic S3 uploads with AWS V4 signing and multipart uploads for large files.
/// </summary>
public class AmazonS3Uploader : FileUploader
{
    private const string DefaultRegion = "us-east-1";
    private readonly S3ConfigModel _config;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string? _sessionToken;
    private CancellationTokenSource? _multipartCancellationTokenSource;

    public static List<AmazonS3Endpoint> Endpoints { get; } = new List<AmazonS3Endpoint>
    {
        new AmazonS3Endpoint("Asia Pacific (Hong Kong)", "s3.ap-east-1.amazonaws.com", "ap-east-1"),
        new AmazonS3Endpoint("Asia Pacific (Mumbai)", "s3.ap-south-1.amazonaws.com", "ap-south-1"),
        new AmazonS3Endpoint("Asia Pacific (Seoul)", "s3.ap-northeast-2.amazonaws.com", "ap-northeast-2"),
        new AmazonS3Endpoint("Asia Pacific (Singapore)", "s3.ap-southeast-1.amazonaws.com", "ap-southeast-1"),
        new AmazonS3Endpoint("Asia Pacific (Sydney)", "s3.ap-southeast-2.amazonaws.com", "ap-southeast-2"),
        new AmazonS3Endpoint("Asia Pacific (Tokyo)", "s3.ap-northeast-1.amazonaws.com", "ap-northeast-1"),
        new AmazonS3Endpoint("Canada (Central)", "s3.ca-central-1.amazonaws.com", "ca-central-1"),
        new AmazonS3Endpoint("China (Beijing)", "s3.cn-north-1.amazonaws.com.cn", "cn-north-1"),
        new AmazonS3Endpoint("China (Ningxia)", "s3.cn-northwest-1.amazonaws.com.cn", "cn-northwest-1"),
        new AmazonS3Endpoint("EU (Frankfurt)", "s3.eu-central-1.amazonaws.com", "eu-central-1"),
        new AmazonS3Endpoint("EU (Ireland)", "s3.eu-west-1.amazonaws.com", "eu-west-1"),
        new AmazonS3Endpoint("EU (London)", "s3.eu-west-2.amazonaws.com", "eu-west-2"),
        new AmazonS3Endpoint("EU (Paris)", "s3.eu-west-3.amazonaws.com", "eu-west-3"),
        new AmazonS3Endpoint("EU (Stockholm)", "s3.eu-north-1.amazonaws.com", "eu-north-1"),
        new AmazonS3Endpoint("Middle East (Bahrain)", "s3.me-south-1.amazonaws.com", "me-south-1"),
        new AmazonS3Endpoint("South America (Sao Paulo)", "s3.sa-east-1.amazonaws.com", "sa-east-1"),
        new AmazonS3Endpoint("US East (N. Virginia)", "s3.amazonaws.com", "us-east-1"),
        new AmazonS3Endpoint("US East (Ohio)", "s3.us-east-2.amazonaws.com", "us-east-2"),
        new AmazonS3Endpoint("US West (N. California)", "s3.us-west-1.amazonaws.com", "us-west-1"),
        new AmazonS3Endpoint("US West (Oregon)", "s3.us-west-2.amazonaws.com", "us-west-2"),
        new AmazonS3Endpoint("DreamObjects", "objects-us-east-1.dream.io"),
        new AmazonS3Endpoint("DigitalOcean (Amsterdam)", "ams3.digitaloceanspaces.com", "ams3"),
        new AmazonS3Endpoint("DigitalOcean (New York)", "nyc3.digitaloceanspaces.com", "nyc3"),
        new AmazonS3Endpoint("DigitalOcean (San Francisco)", "sfo2.digitaloceanspaces.com", "sfo2"),
        new AmazonS3Endpoint("DigitalOcean (Singapore)", "sgp1.digitaloceanspaces.com", "sgp1"),
        new AmazonS3Endpoint("Wasabi", "s3.wasabisys.com")
    };

    public AmazonS3Uploader(S3ConfigModel config, string accessKeyId, string secretAccessKey, string? sessionToken = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _sessionToken = sessionToken;
    }

    public override UploadResult? UploadFile(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (ShouldUseMultipart(fileInfo.Length))
            {
                return UploadMultipart(filePath, Path.GetFileName(filePath));
            }
        }

        return base.UploadFile(filePath);
    }

    public override void StopUpload()
    {
        base.StopUpload();

        try
        {
            _multipartCancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public override UploadResult Upload(Stream stream, string fileName)
    {
        if (stream is FileStream fileStream && File.Exists(fileStream.Name) && ShouldUseMultipart(stream.Length))
        {
            return UploadMultipart(fileStream.Name, fileName);
        }

        return UploadSinglePut(stream, fileName);
    }

    private UploadResult UploadSinglePut(Stream stream, string fileName)
    {
        bool isPathStyleRequest = _config.UsePathStyleUrl || _config.BucketName.Contains(".");

        string scheme = _config.Endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? string.Empty : "https://";
        string endpoint = _config.Endpoint;
        string host = isPathStyleRequest ? endpoint : $"{_config.BucketName}.{endpoint}";
        string region = GetRegion();
        string contentType = MimeTypes.GetMimeTypeFromFileName(fileName);

        string hashedPayload = _config.SignedPayload
            ? ComputeSHA256Hash(stream)
            : "UNSIGNED-PAYLOAD";

        (string uploadPath, string resultUrl, _) = CreateUploadContext(fileName);
        OnEarlyURLCopyRequested(resultUrl);

        NameValueCollection headers = new NameValueCollection
        {
            ["Host"] = host,
            ["Content-Length"] = stream.Length.ToString(CultureInfo.InvariantCulture),
            ["Content-Type"] = contentType,
            ["x-amz-storage-class"] = GetStorageClassHeaderValue(_config.StorageClass)
        };

        if (_config.SetPublicACL)
        {
            headers["x-amz-acl"] = "public-read";
        }

        string canonicalUri = uploadPath;
        if (isPathStyleRequest)
        {
            canonicalUri = URLHelpers.CombineURL(_config.BucketName, canonicalUri);
        }

        canonicalUri = URLHelpers.AddSlash(canonicalUri, SlashType.Prefix);
        canonicalUri = URLHelpers.URLEncode(canonicalUri, true);

        AwsS3Signer.Sign(headers, "PUT", canonicalUri, string.Empty, region, _accessKeyId, _secretAccessKey, _sessionToken, hashedPayload);

        headers.Remove("Host");
        headers.Remove("Content-Type");

        string url = URLHelpers.CombineURL(scheme + host, canonicalUri);
        url = URLHelpers.FixPrefix(url);

        SendRequest(XerahS.Uploaders.HttpMethod.PUT, url, stream, contentType, null, headers);

        if (LastResponseInfo?.IsSuccess == true)
        {
            return new UploadResult
            {
                IsSuccess = true,
                URL = resultUrl
            };
        }

        Errors.Add("Upload to Amazon S3 failed.");
        return new UploadResult
        {
            IsSuccess = false
        };
    }

    private UploadResult UploadMultipart(string filePath, string fileName)
    {
        UploadResult result = new UploadResult();

        if (!File.Exists(filePath))
        {
            result.Response = $"Upload file not found: {filePath}";
            Errors.Add(result.Response);
            return result;
        }

        FileInfo fileInfo = new FileInfo(filePath);
        if (!ShouldUseMultipart(fileInfo.Length))
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return UploadSinglePut(stream, fileName);
        }

        (string uploadPath, string resultUrl, string contentType) = CreateUploadContext(fileName);
        OnEarlyURLCopyRequested(resultUrl);

        IsUploading = true;
        StopUploadRequested = false;

        using CancellationTokenSource multipartCancellationTokenSource = new CancellationTokenSource();
        _multipartCancellationTokenSource = multipartCancellationTokenSource;

        ProgressManager progressManager = new ProgressManager(fileInfo.Length);
        long reportedBytes = 0;
        object progressSync = new object();

        try
        {
            using IAmazonS3 client = CreateS3Client();

            S3MultipartUploadOptions options = new S3MultipartUploadOptions
            {
                BucketName = _config.BucketName,
                ObjectKey = uploadPath,
                URL = resultUrl,
                ContentType = contentType,
                PartSizeBytes = _config.MultipartPartSizeBytes,
                MaxConcurrency = _config.MultipartMaxConcurrency,
                RetryPolicy = new XerahS.Uploaders.Multipart.RetryPolicy(),
                StorageClass = _config.StorageClass,
                SetPublicAcl = _config.SetPublicACL
            };

            options.Validate();

            Progress<MultipartUploadProgress> progressReporter = new Progress<MultipartUploadProgress>(snapshot =>
            {
                long delta;

                lock (progressSync)
                {
                    delta = snapshot.BytesUploaded - reportedBytes;
                    reportedBytes = snapshot.BytesUploaded;

                    if (delta > 0 && AllowReportProgress && progressManager.UpdateProgress(delta))
                    {
                        OnProgressChanged(progressManager);
                    }
                }
            });

            MultipartUploadResult multipartResult = Task.Run(
                () => new S3MultipartUploader(client).UploadAsync(filePath, options, progressReporter, multipartCancellationTokenSource.Token),
                multipartCancellationTokenSource.Token).GetAwaiter().GetResult();

            return new UploadResult
            {
                IsSuccess = multipartResult.IsSuccess,
                Response = multipartResult.ETag,
                URL = multipartResult.URL ?? resultUrl
            };
        }
        catch (OperationCanceledException)
        {
            result.Response = "Amazon S3 multipart upload was canceled.";
            DebugHelper.WriteLine(result.Response);
            return result;
        }
        catch (MultipartUploadException ex)
        {
            DebugHelper.WriteException(ex, "Amazon S3 multipart upload failed.");
            Errors.Add(ex.Message);
            result.Response = ex.Message;
            return result;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Amazon S3 multipart upload failed.");
            Errors.Add(ex.Message);
            result.Response = ex.Message;
            return result;
        }
        finally
        {
            _multipartCancellationTokenSource = null;
            IsUploading = false;
        }
    }

    private string GetRegion()
    {
        if (!string.IsNullOrEmpty(_config.Region))
        {
            return _config.Region;
        }

        string url = _config.Endpoint;

        if (url.Contains("//"))
        {
            url = url.Split(new[] { "//" }, StringSplitOptions.None)[1];
        }

        if (url.EndsWith("/"))
        {
            url = url.Substring(0, url.Length - 1);
        }

        if (!url.Contains(".amazonaws.com"))
        {
            return DefaultRegion;
        }

        string serviceAndRegion = url.Split(new[] { ".amazonaws.com" }, StringSplitOptions.None)[0];
        if (serviceAndRegion.StartsWith("s3-"))
        {
            serviceAndRegion = "s3." + serviceAndRegion.Substring(3);
        }

        int separatorIndex = serviceAndRegion.LastIndexOf('.');
        if (separatorIndex == -1)
        {
            return DefaultRegion;
        }

        return serviceAndRegion.Substring(separatorIndex + 1);
    }

    private string GetUploadPath(string fileName)
    {
        string path = NameParser.Parse(NameParserType.FilePath, _config.ObjectPrefix).Trim('/');

        bool removeExt = false;
        if (_config.RemoveExtensionImage && IsImageFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionVideo && IsVideoFile(fileName)) removeExt = true;
        else if (_config.RemoveExtensionText && IsTextFile(fileName)) removeExt = true;

        if (removeExt)
        {
            fileName = Path.GetFileNameWithoutExtension(fileName);
        }

        return URLHelpers.CombineURL(path, fileName);
    }

    private (string UploadPath, string ResultUrl, string ContentType) CreateUploadContext(string fileName)
    {
        string uploadPath = GetUploadPath(fileName);
        return (uploadPath, GenerateURL(uploadPath), MimeTypes.GetMimeTypeFromFileName(fileName));
    }

    private string GenerateURL(string uploadPath)
    {
        if (!string.IsNullOrEmpty(_config.Endpoint) && !string.IsNullOrEmpty(_config.BucketName))
        {
            uploadPath = URLHelpers.URLEncode(uploadPath, true);

            string url;

            if (_config.UseCustomCNAME && !string.IsNullOrEmpty(_config.CustomDomain))
            {
                ShareXCustomUploaderSyntaxParser parser = new ShareXCustomUploaderSyntaxParser();
                string parsedDomain = parser.Parse(_config.CustomDomain);
                url = URLHelpers.CombineURL(parsedDomain, uploadPath);
            }
            else
            {
                url = URLHelpers.CombineURL(_config.Endpoint, _config.BucketName, uploadPath);
            }

            return URLHelpers.FixPrefix(url);
        }

        return string.Empty;
    }

    private bool IsImageFile(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".webp" }.Contains(ext);
    }

    private bool IsVideoFile(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".mp4", ".avi", ".mov", ".mkv", ".flv", ".wmv", ".webm" }.Contains(ext);
    }

    private bool IsTextFile(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return new[] { ".txt", ".log", ".json", ".xml", ".md", ".html", ".css", ".js" }.Contains(ext);
    }

    private bool ShouldUseMultipart(long streamLength)
    {
        return streamLength > 0 && streamLength >= _config.MultipartThresholdBytes;
    }

    private IAmazonS3 CreateS3Client()
    {
        AWSCredentials credentials = string.IsNullOrWhiteSpace(_sessionToken)
            ? new BasicAWSCredentials(_accessKeyId, _secretAccessKey)
            : new SessionAWSCredentials(_accessKeyId, _secretAccessKey, _sessionToken);

        AmazonS3Config config = new AmazonS3Config
        {
            ServiceURL = GetServiceUrl(),
            AuthenticationRegion = GetRegion(),
            ForcePathStyle = _config.UsePathStyleUrl || _config.BucketName.Contains("."),
            UseHttp = _config.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        };

        return new AmazonS3Client(credentials, config);
    }

    private string GetServiceUrl()
    {
        string endpoint = _config.Endpoint.Trim().TrimEnd('/');

        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return "https://" + endpoint;
    }

    private string ComputeSHA256Hash(Stream stream)
    {
        long position = stream.Position;
        stream.Seek(0, SeekOrigin.Begin);
        byte[] hash = SHA256.HashData(stream);
        stream.Seek(position, SeekOrigin.Begin);
        return BytesToHex(hash);
    }

    private static string GetStorageClassHeaderValue(S3StorageClass storageClass)
    {
        return storageClass switch
        {
            S3StorageClass.Standard => "STANDARD",
            S3StorageClass.StandardInfrequentAccess => "STANDARD_IA",
            S3StorageClass.OneZoneInfrequentAccess => "ONEZONE_IA",
            S3StorageClass.Glacier => "GLACIER",
            S3StorageClass.DeepArchive => "DEEP_ARCHIVE",
            _ => "STANDARD"
        };
    }

    private static string BytesToHex(byte[] bytes)
    {
        return Convert.ToHexStringLower(bytes);
    }
}

public class AmazonS3Endpoint
{
    public string Name { get; set; }
    public string Endpoint { get; set; }
    public string Region { get; set; }

    public AmazonS3Endpoint(string name, string endpoint, string region = "")
    {
        Name = name;
        Endpoint = endpoint;
        Region = region;
    }
}
