using System;
using System.IO;
using DroHub.Helpers;
using Xunit;

namespace DroHub.Tests {
    public class MJRPostProcessorUnitTests {
        private static string DroHubTestsPath => Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "../../../");
        private static string TestAssetsPath => Path.Join(DroHubTestsPath, "TestAssets");

        [Fact]
        public async void TestMJRConverterSimple() {
            var preview_dst_path = Path.Join(TestAssetsPath, "preview-video-janus.webm");
            var dst_path = Path.Join(TestAssetsPath, "video-janus.webm");
            Assert.False(File.Exists(dst_path));
            Assert.False(File.Exists(preview_dst_path));
            try {
                var r = await MJRPostProcessor.RunConvert(TestAssetsPath, true, "preview-", null);
                Assert.Equal(r.result_path, dst_path);
                Assert.True(File.Exists(dst_path));
                Assert.True(File.Exists(preview_dst_path));
                //Number of ticks of manually retrieved
                Assert.Equal(new DateTime(637316971188390000), File.GetCreationTimeUtc(dst_path));
            }
            finally {
                File.Delete(dst_path);
                File.Delete(preview_dst_path);
            }
        }
    }
}