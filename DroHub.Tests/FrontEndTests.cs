using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using DroHub.Areas.DHub.API;
using DroHub.Tests.TestInfrastructure;
using Ductus.FluentDocker.Extensions;
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

        public FrontEndTests(TestServerFixture fixture) {
            _fixture = fixture;
        }

         [Fact]
        public async void TestVueAddTags() {
            const string VUE_TEST_NAME = "GalleryAddTagModal";
            var tag_list_truth = new List<string> {"my new test tag", "tag2"};

            var login_cookie = (await HttpClientHelper
                .createLoggedInUser(TestServerFixture.AdminUserEmail, _fixture.AdminPassword)).loginCookies;

            var old_media_list = _fixture.DbContext
                .MediaObjects
                .ToList();


            await _fixture.testUpload(1, TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                TestServerFixture.AdminUserEmail, _fixture.AdminPassword,
                (objects, i, arg3, arg4) => {
                    if (objects["result"] != "ok")
                        return TestServerFixture.UploadTestReturnEnum.CONTINUE;

                    var media_list = _fixture.DbContext.MediaObjects
                        .Select(m => MediaObjectAndTagAPI.LocalStorageHelper.convertToFrontEndFilePath(m))
                        .ToList();
                    media_list.Reverse();
                    Assert.Equal(old_media_list.Count + 1 , media_list.Count);
                    var token = HttpClientHelper.getCrossSiteAntiForgeryToken(TestServerFixture.AdminUserEmail,
                        _fixture.AdminPassword).GetAwaiter().GetResult();


                    dynamic temp_test_data = new ExpandoObject();
                    temp_test_data.cookie = login_cookie;
                    temp_test_data.tagsToAdd = new JArray(tag_list_truth);
                    temp_test_data.mediaIdList = new JArray(media_list.First());
                    temp_test_data.useTimeStamp = false;
                    temp_test_data.propsData.crossSiteForgeryToken = token;

                    writeNewTempTestData(VUE_TEST_NAME, temp_test_data);

                    var test_containers = _fixture.runVueTestContainer(VUE_TEST_NAME);

                    Assert.Equal(0, test_containers.GetConfiguration().State.ExitCode);
                    test_containers.Remove((true));
                    Assert.Equal(1, _fixture.DbContext.MediaObjectTags.Count(
                        t => t.TagName == tag_list_truth[0]
                        && t.MediaPath == MediaObjectAndTagAPI.LocalStorageHelper.convertToBackEndFilePath(media_list.First())));

                    return TestServerFixture.UploadTestReturnEnum.CONTINUE;
                });
        }

        private static void writeNewTempTestData(string test_name, dynamic data) {
            if (File.Exists(TEMP_DATA_PATH))
                File.Delete(TEMP_DATA_PATH);

            dynamic temp_test_data = new ExpandoObject();
            temp_test_data.propsData = new ExpandoObject();
            temp_test_data[test_name] = data;
            var output = JsonConvert.SerializeObject(temp_test_data, Formatting.Indented);
            File.WriteAllText(TEMP_DATA_PATH, output);
        }

        [Fact]
        public void TestTimeLabel() {
            var test_container = _fixture.runVueTestContainer("TimeLabel");
            test_container.WaitForStopped();
            Assert.Equal(0, test_container.GetConfiguration().State.ExitCode);
            test_container.Remove((true));
        }

        [Fact]
        public void TestGalleryTimeLine() {
            const string VUE_TEST_NAME = "GalleryTimeLine";
            var token = HttpClientHelper.getCrossSiteAntiForgeryToken(TestServerFixture.AdminUserEmail,
                _fixture.AdminPassword).GetAwaiter().GetResult();

            dynamic temp_test_data = new ExpandoObject();
            temp_test_data.propsData.crossSiteForgeryToken = token;
            writeNewTempTestData(VUE_TEST_NAME, temp_test_data);

            var test_container = _fixture.runVueTestContainer(VUE_TEST_NAME);
            Assert.Equal(0, test_container.GetConfiguration().State.ExitCode);
            test_container.Remove((true));
        }
    }
}