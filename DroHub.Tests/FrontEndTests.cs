using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Helpers;
using DroHub.Tests.TestInfrastructure;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Model.Builders;
using Ductus.FluentDocker.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DroHub.Tests {
    [Collection("Docker")]
    public class FrontEndTests : IClassFixture<TestServerFixture> {
        private readonly TestServerFixture _fixture;

        private static readonly string TEMP_DATA_PATH = Path.Join(TestServerFixture.DroHubPath,
            TestServerFixture.FrontEndPathInRepo,
            "tests",
            "temporary-test-data.json");

        private static IContainerService runVueTestContainer(string command, bool assert_exit_0, bool rm_on_success) {
            const string DOCKER_REPO_MOUNT_PATH = "/home/cirrus";
            using var test_containers = new Builder()
                .UseContainer()
                .IsPrivileged()
                .KeepContainer()
                .ReuseIfExists()
                .AsUser("1000:1000")
                .Command(command)
                .UseNetwork("host")
                .UseImage("ptsneves/airborneprojects:vue-test")
                .Mount(TestServerFixture.DroHubPath, DOCKER_REPO_MOUNT_PATH, MountType.ReadWrite)
                .Build()
                .Start();
            test_containers.WaitForStopped();

            if (assert_exit_0)
                Assert.Equal(0, test_containers.GetConfiguration().State.ExitCode);

            if (rm_on_success)
                test_containers.Remove((true));

            return test_containers;
        }

        public FrontEndTests(TestServerFixture fixture) {
            _fixture = fixture;
        }


        [Fact]
        public async void TestGalleryDeleteFilesModal() {
            const string VUE_TEST_NAME = "GalleryDeleteFilesModal";
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "video.webm",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "20200222_123525.jpeg",
                    IsPreview = false
                },
            };

            await testWithFile((media_list, token_data) => {
                writeNewTempTestData(VUE_TEST_NAME, temp_test_data => {
                    temp_test_data.cookie = token_data.LoginCookie;
                    temp_test_data.mediaIdList = new JArray(media_list);
                    temp_test_data.propsData.crossSiteForgeryToken = token_data.CrossSiteForgeryToken;
                });

                runVueTestContainer(VUE_TEST_NAME, true, true);

                Assert.Equal(1, _fixture.DbContext.MediaObjectTags.Count(t =>
                    t.TagName == "Marked for Drone removal"
                    && t.MediaPath ==
                    LocalStorageHelper.convertToBackEndFilePath(media_list.First())));

                Assert.False(_fixture.DbContext.MediaObjects.Any(t =>
                    t.MediaPath ==  LocalStorageHelper.convertToBackEndFilePath(media_list
                        .Last())));
                return Task.CompletedTask;
            }, file_set);
        }

        [Fact]
        public async void TestVueAddTags() {
            const string VUE_TEST_NAME = "GalleryAddTagModal";
            var tag_list_truth = new List<string> {"my new test tag", "tag2"};
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "video.webm",
                    IsPreview = false
                },
            };
            await testWithFile((media_list, token_data) => {
                writeNewTempTestData(VUE_TEST_NAME, temp_test_data => {
                    temp_test_data.cookie = token_data.LoginCookie;
                    temp_test_data.tagsToAdd = new JArray(tag_list_truth);
                    temp_test_data.mediaIdList = new JArray(media_list.Last());
                    temp_test_data.useTimeStamp = false;
                    temp_test_data.propsData.crossSiteForgeryToken = token_data.CrossSiteForgeryToken;
                });

                runVueTestContainer(VUE_TEST_NAME, true, true);
                Assert.Equal(1, _fixture.DbContext.MediaObjectTags.Count(
                    t => t.TagName == tag_list_truth[0]
                         && t.MediaPath == LocalStorageHelper.convertToBackEndFilePath(media_list.First())));

                return Task.CompletedTask;
            }, file_set);
        }

        [Fact]
        public async void TestGalleryTimeLine() {
            const string VUE_TEST_NAME = "GalleryTimeLine";
            var file_set = new[] {
                new TestServerFixture.FileToBeUploaded {
                    Source = "video.webm",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "20200222_123525.jpeg",
                    IsPreview = false
                },
                new TestServerFixture.FileToBeUploaded {
                    Source = "preview-drone-PI040416DA9H110281-1608225545000.jpeg",
                    IsPreview = true
                },
            };
            await testWithFile((media_list, gallery_page_data) => {
                writeNewTempTestData(VUE_TEST_NAME, temp_test_data => {
                    var media_list_data = media_list
                        .Where(media =>
                            File.Exists(LocalStorageHelper.convertToBackEndFilePath(media)))
                        .Select(i => new JObject {
                        {"sha256", TestServerFixture.computeFileSHA256(
                            LocalStorageHelper.convertToBackEndFilePath(i))},
                        {"file", i}
                    });

                    var preview_media_list_data = media_list
                        .Where(media =>
                            LocalStorageHelper.doesPreviewFileExist(
                                LocalStorageHelper.convertToBackEndFilePath(media)))
                        .Select(i => new JObject {
                        {"sha256", TestServerFixture.computeFileSHA256(
                            LocalStorageHelper.convertToBackEndFilePath(
                                LocalStorageHelper.calculatePreviewFilePath(i))
                            )},
                        {"file", i}
                    });

                    temp_test_data.cookie = gallery_page_data.LoginCookie;
                    temp_test_data.mediaIdList = new JArray(media_list_data);
                    temp_test_data.previewMediaIdList = new JArray(preview_media_list_data);
                    temp_test_data.propsData.crossSiteForgeryToken = gallery_page_data.CrossSiteForgeryToken;
                    temp_test_data.propsData.galleryModelJson = TestServerFixture.getHtmlDOM(gallery_page_data.Content)
                        .QuerySelector("gallery-timeline")
                        .Attributes.First(e => e.Name == "gallery-model-json")
                        .Value;
                });

                runVueTestContainer(VUE_TEST_NAME, true, true);
                return Task.CompletedTask;
            }, file_set);
        }

        private async Task testWithFile(Func<List<string>, HttpClientHelper.GalleryPageData, Task> test,
            IEnumerable<TestServerFixture.FileToBeUploaded> src_list, int file_copies = 1) {
            var old_media_list = _fixture.DbContext
                .MediaObjects
                .ToList();

            var ran = false;
            await _fixture.testUpload(1, TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                (result, run, chunk, chunk_size, copy,file) =>
                    Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE),
                async () => {
                    var media_list = _fixture.DbContext.MediaObjects
                        .Select(m => LocalStorageHelper.convertToFrontEndFilePath(m))
                        .ToList();
                    media_list.Reverse();


                    Assert.Equal(old_media_list.Count + (file_copies * src_list.Count()) , media_list.Count);
                    var gallery_page_data = await HttpClientHelper.getGalleryDataData(TestServerFixture.AdminUserEmail,
                        _fixture.AdminPassword);

                    await test(media_list, gallery_page_data);
                    ran = true;
                },
                src_list,
                copies: file_copies,
                timestamp_srclist_spread_millis: 10);
            Assert.True(ran);
        }

        private static void writeNewTempTestData(string test_name, Action<dynamic> temp_data_func) {
            if (File.Exists(TEMP_DATA_PATH))
                File.Delete(TEMP_DATA_PATH);

            dynamic temp_test_data = new Dictionary<string,ExpandoObject>();
            temp_test_data[test_name] = new ExpandoObject();
            temp_test_data[test_name].siteURI = TestServerFixture.SiteUri.ToString();
            temp_test_data[test_name].propsData = new ExpandoObject();
            temp_data_func(temp_test_data[test_name]);
            var output = JsonConvert.SerializeObject(temp_test_data, Formatting.Indented);
            File.WriteAllText(TEMP_DATA_PATH, output);
        }

        [Fact]
        public void TestTimeLabel() {
            runVueTestContainer("TimeLabel", true, true);
        }
    }
}