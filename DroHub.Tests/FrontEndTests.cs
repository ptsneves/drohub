using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Tests.TestInfrastructure;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Model.Builders;
using Ductus.FluentDocker.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DroHub.Tests {
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

            await testWithFile((media_list, token_data, copy) => {
                if (copy == 0)
                    return;

                writeNewTempTestData(VUE_TEST_NAME, temp_test_data => {
                    temp_test_data.cookie = token_data.LoginCookie;
                    temp_test_data.mediaIdList = new JArray(media_list);
                    temp_test_data.propsData.crossSiteForgeryToken = token_data.CrossSiteForgeryToken;
                });

                runVueTestContainer(VUE_TEST_NAME, true, true);

                Assert.Equal(1, _fixture.DbContext.MediaObjectTags.Count(t =>
                    t.TagName == "Marked for Drone removal"
                    && t.MediaPath ==
                    MediaObjectAndTagAPI.LocalStorageHelper.convertToBackEndFilePath(media_list.First())));

                Assert.False(_fixture.DbContext.MediaObjects.Any(t =>
                    t.MediaPath ==  MediaObjectAndTagAPI.LocalStorageHelper.convertToBackEndFilePath(media_list
                        .Last())));
            }, 1);
        }

        [Fact]
        public async void TestVueAddTags() {
            const string VUE_TEST_NAME = "GalleryAddTagModal";
            var tag_list_truth = new List<string> {"my new test tag", "tag2"};

            await testWithFile((media_list, token_data, copy) => {
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
                         && t.MediaPath == MediaObjectAndTagAPI.LocalStorageHelper.convertToBackEndFilePath(media_list.First())));
            });
        }

        private async Task testWithFile(Action<List<string>, HttpClientHelper.GalleryPageData, int> test, int
        file_copies = 1) {
            var old_media_list = _fixture.DbContext
                .MediaObjects
                .ToList();

            await _fixture.testUpload(1, TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                (result, run, chunk, chunk_size, copy) => {
                    if (result.ContainsKey("error"))
                        throw new Exception(result["error"]);

                    if (result["result"] != "ok")
                        return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);

                    var media_list = _fixture.DbContext.MediaObjects
                        .Select(m => MediaObjectAndTagAPI.LocalStorageHelper.convertToFrontEndFilePath(m))
                        .ToList();
                    media_list.Reverse();


                    Assert.Equal(old_media_list.Count + file_copies , media_list.Count);
                    var token_data = HttpClientHelper.getGalleryDataData(TestServerFixture.AdminUserEmail,
                        _fixture.AdminPassword).GetAwaiter().GetResult();

                    test(media_list, token_data, copy);

                    return Task.FromResult(TestServerFixture.UploadTestReturnEnum.CONTINUE);
                },
                copies: file_copies);
        }

        private static void writeNewTempTestData(string test_name, Action<dynamic> temp_data_func) {
            if (File.Exists(TEMP_DATA_PATH))
                File.Delete(TEMP_DATA_PATH);

            dynamic temp_test_data = new Dictionary<string,ExpandoObject>();
            temp_test_data[test_name] = new ExpandoObject();
            temp_test_data[test_name].propsData = new ExpandoObject();
            temp_data_func(temp_test_data[test_name]);
            var output = JsonConvert.SerializeObject(temp_test_data, Formatting.Indented);
            File.WriteAllText(TEMP_DATA_PATH, output);
        }

        [Fact]
        public void TestTimeLabel() {
            runVueTestContainer("TimeLabel", true, true);
        }

        [Fact]
        public void TestGalleryTimeLine() {
            const string VUE_TEST_NAME = "GalleryTimeLine";
            var token_data = HttpClientHelper.getGalleryDataData(TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword).GetAwaiter().GetResult();

            writeNewTempTestData(VUE_TEST_NAME, o => {
                o.propsData.crossSiteForgeryToken = token_data.CrossSiteForgeryToken;
            });

            runVueTestContainer(VUE_TEST_NAME, true, true);
        }
    }
}