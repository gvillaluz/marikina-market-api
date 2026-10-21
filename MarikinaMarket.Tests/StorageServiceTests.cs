using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Model.Internal.MarshallTransformations;
using MarikinaMarket.API.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MarikinaMarket.Tests;

public class StorageServiceTests
{
    // Published MD5 test vectors keep expected digests independent of the implementation.
    [Theory]
    [InlineData("abc", "kAFQmDzST7DWlj99KOF/cg==", null, 365)]
    [InlineData("message digest", "+WtpfXy3k41SWi8xqvFh0A==", 30, 30)]
    public async Task Evidence_upload_sends_content_md5_and_preserves_bytes_and_retention(
        string content, string expectedDigest, int? retentionDays, int expectedDays)
    {
        var before = DateTime.UtcNow;
        var calls = 0;
        var service = CreateService(async request =>
        {
            calls++;
            Assert.Equal(expectedDigest, request.MD5Digest);
            var wireRequest = PutObjectRequestMarshaller.Instance.Marshall(request);
            Assert.Contains(wireRequest.Headers, header =>
                string.Equals(header.Key, "Content-MD5", StringComparison.OrdinalIgnoreCase)
                && header.Value == expectedDigest);
            Assert.Equal("synthetic-evidence-bucket", request.BucketName);
            Assert.Equal("tickets/test/checksum.bin", request.Key);
            Assert.Equal("application/octet-stream", request.ContentType);
            Assert.Equal(ObjectLockMode.Compliance, request.ObjectLockMode);
            Assert.InRange(request.ObjectLockRetainUntilDate!.Value,
                before.AddDays(expectedDays), DateTime.UtcNow.AddDays(expectedDays));
            Assert.False(request.UseChunkEncoding);
            Assert.True(request.DisablePayloadSigning);
            using var uploaded = new MemoryStream();
            await request.InputStream.CopyToAsync(uploaded);
            Assert.Equal(Encoding.UTF8.GetBytes(content), uploaded.ToArray());
            return new PutObjectResponse();
        });
        var file = File(content);

        var result = retentionDays.HasValue
            ? await service.UploadEvidenceAsync(file, "tickets/test/checksum.bin", retentionDays.Value)
            : await service.UploadEvidenceAsync(file, "tickets/test/checksum.bin");

        Assert.Equal("tickets/test/checksum.bin", result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Batch_evidence_upload_checksums_each_file_and_uploads_complete_bytes()
    {
        var expected = new Dictionary<string, (string Content, string Digest)>
        {
            ["tickets/test/first.bin"] = ("abc", "kAFQmDzST7DWlj99KOF/cg=="),
            ["tickets/test/second.bin"] = ("hello", "XUFAKrxLKna5cZ2REBfFkg==")
        };
        var uploadedKeys = new ConcurrentBag<string>();
        var service = CreateService(async request =>
        {
            var file = expected[request.Key];
            Assert.Equal(file.Digest, request.MD5Digest);
            using var uploaded = new MemoryStream();
            await request.InputStream.CopyToAsync(uploaded);
            Assert.Equal(Encoding.UTF8.GetBytes(file.Content), uploaded.ToArray());
            Assert.Equal(ObjectLockMode.Compliance, request.ObjectLockMode);
            uploadedKeys.Add(request.Key);
            return new PutObjectResponse();
        });

        var result = await service.UploadEvidencesAsync(new()
        {
            [File("abc")] = "tickets/test/first.bin",
            [File("hello")] = "tickets/test/second.bin"
        });

        Assert.Equal(expected.Keys.Order(), result.Order());
        Assert.Equal(expected.Keys.Order(), uploadedKeys.Order());
    }

    [Fact]
    public async Task Evidence_upload_propagates_storage_failure()
    {
        var error = new AmazonS3Exception("Synthetic storage failure.");
        var service = CreateService(_ => Task.FromException<PutObjectResponse>(error));

        var actual = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            service.UploadEvidenceAsync(File("abc"), "tickets/test/checksum.bin"));

        Assert.Same(error, actual);
    }

    private static FormFile File(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length,
            "ticket_evidence_files", "checksum-vector.bin")
        {
            Headers = new HeaderDictionary(), ContentType = "application/octet-stream"
        };
    }

    private static StorageService CreateService(Func<PutObjectRequest, Task<PutObjectResponse>> upload)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BackBlazeB2:Endpoint"] = "storage.example.invalid"
        }).Build();
        var service = new StorageService(config);
        var client = ReviewBoundaryProxy.Create<IAmazonS3>((method, args) =>
            method.Name == nameof(IAmazonS3.PutObjectAsync)
                ? upload((PutObjectRequest)args[0]!)
                : throw new InvalidOperationException(method.Name));
        // Substitute only the external S3 boundary; exercise the real upload methods.
        var cache = (ConcurrentDictionary<B2BucketType, (IAmazonS3 Client, string BucketName)>)
            typeof(StorageService).GetField("_clientCache", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(service)!;
        cache[B2BucketType.Evidence] = (client, "synthetic-evidence-bucket");
        return service;
    }
}
