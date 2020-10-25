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
            var dst_path = Path.Join(TestAssetsPath, "video.webm");
            Assert.False(File.Exists(dst_path));
            try {
                await MJRPostProcessor.RunConvert(TestAssetsPath, true, null);
                Assert.True(File.Exists(dst_path));
                //Number of ticks of manually retrieved
                Assert.Equal(new DateTime(637316971188390000), File.GetCreationTimeUtc(dst_path));
            }
            finally {
                File.Delete(dst_path);
            }
        }
    }
}