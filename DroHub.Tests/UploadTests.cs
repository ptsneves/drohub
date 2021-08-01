using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.Identity.Data;
using DroHub.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DroHub.Tests {
    public class UploadTests : IClassFixture<TestServerFixture> {
        private readonly TestServerFixture _fixture;
        public UploadTests(TestServerFixture fixture) {
            _fixture = fixture;
        }
        [InlineData("video.webm")]
        [InlineData("sample.mp4")]
        [Theory]
        public async void TestUploadMediaSucceeds(string video_src) {
            var ran = false;
            var last_chunk = 0;
            const int CHUNKS = 30;

            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    Assert.Equal(last_chunk++, chunk);
                    if (chunk < CHUNKS-1) {

                        Assert.True(result.TryGetValue("result", out var v));
                        Assert.Equal("send-next", v);
                        Assert.True(result.TryGetValue("begin", out var r));
                        Assert.Equal(sent_chunk_size*(chunk+1), r);
                    }
                    else {
                        Assert.True(result.TryGetValue("result", out var v));
                        Assert.Equal("ok", v);
                        var last_media_path = _fixture.DbContext.MediaObjects.ToList().Last();
                        var orig_sha256 = TestServerFixture.computeFileSHA256($"{TestServerFixture.TestAssetsPath}/{video_src}");
                        var uploaded_sha256 = TestServerFixture.computeFileSHA256(last_media_path.MediaPath);
                        Assert.Equal(orig_sha256, uploaded_sha256);

                        //Check that preview file is not the uploaded one.
                        Assert.True(MediaObjectAndTagAPI
                            .LocalStorageHelper
                            .doesPreviewExist(last_media_path));

                        var preview_file_path = MediaObjectAndTagAPI.LocalStorageHelper.
                            calculatePreviewFilePath(last_media_path.MediaPath);
                        var preview_sha256 = TestServerFixture.computeFileSHA256(preview_file_path);
                        Assert.NotEqual(preview_sha256, uploaded_sha256);

                        ran = true;
                    }

                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                },
                1,
                video_src);
            Assert.Equal(CHUNKS, last_chunk);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadJPEGSingleChunkSucceeds() {
            var ran = false;
            var last_chunk = 0;
            const int CHUNKS = 1;

            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    Assert.Equal(last_chunk++, chunk);
                    Assert.True(result.TryGetValue("result", out var v));
                    Assert.Equal("ok", v);
                    var last_media_path = _fixture.DbContext.MediaObjects.ToList().Last();
                    var orig_sha256 = TestServerFixture.computeFileSHA256(
                        $"{TestServerFixture.TestAssetsPath}/preview-drone-PI040416DA9H110281-1608225545000.jpeg");
                    var uploaded_sha256 = TestServerFixture.computeFileSHA256(last_media_path.MediaPath);
                    Assert.Equal(orig_sha256, uploaded_sha256);
                    ran = true;

                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                },
                1,
                "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                1);
            Assert.Equal(CHUNKS, last_chunk);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadPossibleWhileFlightStillOngoing() {
            const int minutes = 1;
            var ran = false;

            await TelemetryMock.stageThriftDrone(_fixture, true, minutes, TestServerFixture.DEFAULT_USER, TestServerFixture.DEFAULT_PASSWORD,
                TestServerFixture.DEFAULT_ALLOWED_USER_COUNT, TestServerFixture.DEFAULT_ORGANIZATION, TestServerFixture.DEFAULT_DEVICE_SERIAL,
                async (drone_rpc, telemetry_mock, user_name, token) => {
                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                        async () => {
                            Thread.Sleep(2000);
                            const string src = "video.webm";
                            await using var stream = new FileStream($"{TestServerFixture.TestAssetsPath}/{src}", FileMode.Open);
                            var r = await HttpClientHelper.uploadMedia(TestServerFixture.DEFAULT_USER,
                                TestServerFixture.DEFAULT_PASSWORD,
                                new AndroidApplicationController.UploadModel {
                                    File = new FormFile(stream, 0, stream.Length / 30, src,
                                        $"{TestServerFixture.TestAssetsPath}/{src}"),
                                    IsPreview = false, //needs to be because preview files have different paths
                                    DeviceSerialNumber = TestServerFixture.DEFAULT_DEVICE_SERIAL,
                                    UnixCreationTimeMS = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                    AssembledFileSize = stream.Length,
                                    RangeStartBytes = 0
                                });
                            Assert.True(r.TryGetValue("result", out var v));
                            Assert.Equal("send-next", v);
                            ran = true;

                        }, user_name, token);
                },
                () => DroneRPC.generateTelemetry(TestServerFixture.DEFAULT_DEVICE_SERIAL,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadMediaLargeChunkFails() {
            var ran = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    Assert.True(result.TryGetValue("error", out var value));
                    Assert.Equal("Response status code does not indicate success: 413 (Request Entity Too Large).", value);
                    ran = true;
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                },
                1,
                "video.webm",
                1);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadTooSmallChunkFails() {
            var ran = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    Assert.True(result.TryGetValue("error", out var v));
                    Assert.Equal(v, AndroidApplicationController.CHUNK_TOO_SMALL);
                    ran = true;
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                },
                1,
                "video.webm",
                6000000);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadTooSmallFails() {
            var ran = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    Assert.True(result.TryGetValue("error", out var v));
                    Assert.Equal(v, AndroidApplicationController.SIZE_TOO_SMALL);
                    ran = true;
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                },
                1,
                "fake-0byte.webm",
                1);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadMediaBadModelFails() {
            var result = await HttpClientHelper.uploadMedia(TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                new AndroidApplicationController.UploadModel());
            Assert.True(result.ContainsKey("error"));
        }

        [Fact]
        public async void TestUploadMediaOutOfDeviceConnectionFails() {
            var ran = false;
            await _fixture.testUpload(4,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                Assert.True(result.TryGetValue("error", out var v));
                Assert.Equal("Media does not correspond to any known flight", v);
                ran = true;
                return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
            });
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadMediaNonExistentDeviceFoundFails() {
            var src = "video.webm";
            await using var stream = new FileStream($"{TestServerFixture.TestAssetsPath}/{src}", FileMode.Open);
            var r = await HttpClientHelper.uploadMedia(TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                new AndroidApplicationController.UploadModel {
                    File = new FormFile(stream, 0, 4096, src, $"{TestServerFixture.TestAssetsPath}/{src}"),
                    IsPreview = false,
                    DeviceSerialNumber = "Aserial0",
                    UnixCreationTimeMS = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    AssembledFileSize = 4096,
                    RangeStartBytes = 0
                });
            Assert.True(r.TryGetValue("error", out var v));
            Assert.Equal(v, "Device not found");
        }

        [Theory]
        [InlineData("video.webm", false)]
        [InlineData("preview-drone-PI040416DA9H110281-1608225545000.jpeg", true)]
        public async void TestUploadPreviewOnlyAllowsJpeg(string video_src, bool expect_success) {
            var ran = false;

            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                    if (expect_success) {
                        Assert.True(result.TryGetValue("result", out var v));
                        Assert.Equal("send-next", v);
                        ran = true;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                    }
                    else {
                        Assert.True(result.TryGetValue("error", out var v));
                        Assert.Equal(v, AndroidApplicationController.BAD_PREVIEW_FORMAT);
                        ran = true;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                    }
                },
                1,
                video_src, is_preview: true);
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadCannotUploadPreviewOfVideos() {
            var ran = false;
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "video.webm",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                    IsPreview = true
                }
            };

            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, file) => {
                    if (file != file_set[1].Source)
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);

                    Assert.True(result.TryGetValue("error", out var v));
                    Assert.Equal(AndroidApplicationController.FORBIDDEN_PREVIEW, v);
                    ran = true;
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);

                },
                () => Task.CompletedTask,
                file_set);
            Assert.True(ran);
        }



        [Fact]
        public async void TestUploadMediaNotSubscribedDeviceFails() {
            await using var u = await HttpClientHelper.AddUserHelper.addUser(_fixture, TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
            TestServerFixture.DEFAULT_USER, TestServerFixture.DEFAULT_PASSWORD,
                TestServerFixture.DEFAULT_ORGANIZATION, DroHubUser.SUBSCRIBER_POLICY_CLAIM, 99, 99);

            var ran = false;
            await _fixture.testUpload(1,
                    TestServerFixture.DEFAULT_USER,
                    TestServerFixture.DEFAULT_PASSWORD,
                    TestServerFixture.AdminUserEmail,
                    _fixture.AdminPassword,
                    (result, tries, chunk, sent_chunk_size, copy, _) => {
                        Assert.True(result.TryGetValue("error", out var v));
                        Assert.Equal(v, "Response status code does not indicate success: 401 (Unauthorized).");
                        ran = true;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                    }
                );
            Assert.True(ran);
        }

        [Fact]
        public async void TestUploadMediaChunkOverwriteSkips() {
            var ran1 = false;
            var ran2 = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, attempt, chunk, sent_chunk_size, copy, _) => {
                    if (attempt == 0) {
                        ran1 = true;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.SKIP_RUN);
                    }

                    ran2 = true;
                    Assert.True(result.TryGetValue("result", out var v));
                    Assert.Equal("send-next", v);
                    Assert.True(result.TryGetValue("begin", out var r));
                    Assert.Equal(sent_chunk_size*(chunk+1), r);
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                }, 2);

            Assert.True(ran1);
            Assert.True(ran2);
        }

        [Fact]
        public async void TestUploadMediaOverwriteFails() {
            var ran1 = false;
            var ran2 = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                switch (tries) {
                    case 0: {
                        ran1 = true;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                    }
                    case 1: {
                        Assert.True(result.TryGetValue("error", out var v));
                        Assert.Equal(v, "File already exists");
                        ran2 = true;
                        break;
                    }
                    default:
                        Assert.True(false);
                        break;
                }

                return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
            }, 2);
            Assert.True(ran1);
            Assert.True(ran2);
        }

        [Fact]
        public async void TestNotAllowedFormatFails() {
            var ran = false;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, _) => {
                Assert.True(result.TryGetValue("error", out var v));
                Assert.Equal(v, "Format not allowed");
                ran = true;
                return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
            }, 1, "video-janus.mjr");
            Assert.True(ran);
        }

        [InlineData(false, 0)]
        [InlineData(true, 10)]
        [Theory]
        public async void TestMultipleFilesWithSameTimestampNotAllowed(bool expect_success, int timestamp_spread_millis) {
            var ran = false;
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "20200222_123525.jpeg",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                    IsPreview = true
                },
            };
            var old_media_list = _fixture.DbContext
                .MediaObjects
                .ToList();

            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, file) => {
                    if (file == file_set[1].Source && !expect_success) {
                            Assert.True(result.TryGetValue("error", out var v));
                            Assert.Equal(AndroidApplicationController.FORBIDDEN_PREVIEW, v);
                            ran = true;
                            return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);
                    }

                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                },
                () => {
                    var media_list = _fixture.DbContext.MediaObjects
                        .ToList();
                    if (expect_success)
                        Assert.Equal(old_media_list.Count + (file_set.Count()) , media_list.Count);
                    else {
                        Assert.Equal(old_media_list.Count + (file_set.Count()-1) , media_list.Count);
                    }

                    ran = true;
                    return Task.CompletedTask;
                },
                file_set,
                timestamp_srclist_spread_millis: timestamp_spread_millis);
            Assert.True(ran);
        }

        [Fact]
        public async void TestNotAllowedPreviewUploadAfterNonPreviewUpload() {
            var ran = false;
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                    IsPreview = true
                }
            };
            int recorded_chunk = -1;
            await _fixture.testUpload(1,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword,
                (result, tries, chunk, sent_chunk_size, copy, file) => {
                    if (chunk > recorded_chunk) {
                        recorded_chunk = chunk;
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                    }

                    Assert.True(result.TryGetValue("error", out var v));
                    Assert.Equal(AndroidApplicationController.FORBIDDEN_PREVIEW, v);
                    ran = true;
                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.STOP_UPLOAD);

                },
                () => Task.CompletedTask,
                file_set);
            Assert.True(ran);
        }


    }
}