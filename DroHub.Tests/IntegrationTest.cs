using System;
using Xunit;
using DroHub.Tests.TestInfrastructure;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Threading;
using DroHub.Areas.DHub.Controllers;
using DroHub.Areas.DHub.Models;
using DroHub.Areas.Identity.Data;
using DroHub.Helpers;
using Ductus.FluentDocker;
using Ductus.FluentDocker.Builders;
using Ductus.FluentDocker.Extensions;
using Ductus.FluentDocker.Model.Builders;
using Microsoft.Extensions.Configuration;

// ReSharper disable StringLiteralTypo

namespace DroHub.Tests
{
    public class IntegrationTest : IClassFixture<DroHubFixture>
    {
        private readonly DroHubFixture _fixture;

        private const int ALLOWED_USER_COUNT = 999;
        private const string DEFAULT_ORGANIZATION = "UN";
        private const string DEFAULT_DEVICE_NAME = "A Name";
        private const string DEFAULT_BASE_TYPE = DroHubUser.SUBSCRIBER_POLICY_CLAIM;
        private const string DEFAULT_DEVICE_SERIAL = "Aserial";
        private const string DEFAULT_USER = "auser@drohub.xyz";
        private const string DEFAULT_PASSWORD = "password1234";
        private const int DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES = 999;
        private const int DEFAULT_ALLOWED_USER_COUNT = 3;

        public IntegrationTest(DroHubFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        public async void TestLoginIsNotHomePageAndAllowsAnonymous() {
            using var http_helper = await HttpClientHelper.createHttpClient(DroHubFixture.SiteUri);
            Assert.NotEqual(new Uri(DroHubFixture.SiteUri, "Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage"),
                http_helper.Response.RequestMessage.RequestUri);
        }

        [InlineData("DHub/DeviceRepository/Dashboard")]
        [InlineData("Identity/Account/Manage")]
        [InlineData("Identity/Account/Manage/AdminPanel")]
        [Theory]
        public async void TestPageRedirectedToLogin(string uri_path) {
            using var http_helper = await HttpClientHelper.createHttpClient(
                new Uri(DroHubFixture.SiteUri + uri_path));
            Assert.NotEqual(new Uri(DroHubFixture.SiteUri, uri_path),
                http_helper.Response.RequestMessage.RequestUri);
        }

        private async Task testLogin(string user, string password, bool expect_login_fail) {
            if (expect_login_fail)
                await Assert.ThrowsAsync<InvalidProgramException>(async () => (await HttpClientHelper.createLoggedInUser(user, password)).Dispose());
            else
                using (await HttpClientHelper.createLoggedInUser(user, password)) { }
        }

        [InlineData(null, EXPECT_FAIL)]
        [InlineData("1", EXPECT_SUCCESS)]
        [Theory]
        public async void TestAdminAccount(string password, bool expect_login_fail) {
            await testLogin("admin@drohub.xyz", password ?? _fixture.AdminPassword, expect_login_fail);
        }

        [Fact]
        public async void TestJanusOnline() {
            await HttpClientHelper.createJanusHandle();
        }

        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, false, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, true, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, true, false)]
        [Theory]
        public async void TestCreateUserSimple(string user_base_role, string new_user_role, bool same_org, bool expect_success) {
            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, user_base_role,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var new_user_org = same_org ? DEFAULT_ORGANIZATION : DEFAULT_ORGANIZATION + "1";
            var t = HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_USER+"1", DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION, new_user_role, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            if (expect_success) {
                await using var u = await t;
            }
            else {
                var threw = false;
                try {
                    await using var u = await t;
                }
                catch (InvalidOperationException) {
                    threw = true;
                }
                catch (HttpRequestException) {
                    threw = true;
                }

                Assert.True(threw);
            }
        }

        [Fact]
        public async void TestCreateSubscriptionAllowedMinutesLimits() {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, (long)TimeSpan.MaxValue.TotalMinutes+1,
                    DEFAULT_ALLOWED_USER_COUNT);
            });

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, 0,
                    DEFAULT_ALLOWED_USER_COUNT);
            });

            await using var u = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, (long)TimeSpan.MaxValue.TotalMinutes,
                DEFAULT_ALLOWED_USER_COUNT);

        }

        [Fact]
        public async void TestCreateSubscriptionWithNoAllowedUsers() {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 0);
            });
        }

        private const bool EXPECT_SUCCESS = true;
        private const bool EXPECT_FAIL = false;
        private const bool SAME_ORG = true;
        private const bool OTHER_ORG = false;

        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, false)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, false)]
        [Theory]
        public async void TestSendInvitation(string agent_role, bool expect_success) {
            // var mail_slurp_helper = new TemMailHelper
            //     ("132892a34e183e7264f2f13a47adf67f26e1381c5fb1401b70d0f3f64046a883", "TestSendInvitation");
            //
            // var timeout = new TimeSpan(0, 0, 5);
            // var match_options = new MatchOptions {
            //     Matches = new List<MatchOption> {
            //         new MatchOption {
            //             Field = MatchOption.FieldEnum.FROM,
            //             Should = MatchOption.ShouldEnum.EQUAL,
            //             Value = "postmaster@drohub.xyz"
            //         },
            //         new MatchOption {
            //             Field = MatchOption.FieldEnum.TO,
            //             Should = MatchOption.ShouldEnum.EQUAL,
            //             Value = mail_slurp_helper.EMAIL_ADDRESS
            //         }
            //     }
            // };
            //
            // await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
            //     DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, agent_role,
            //     DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);
            //
            // var t = HttpClientHelper.sendInvitation(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
            //     new[] {mail_slurp_helper.EMAIL_ADDRESS});
            //
            // if (expect_success) {
            //     await t;
            //     var emails = await mail_slurp_helper.receiveEmail(timeout, match_options, 1);
            //     Assert.True(1 == emails.Count);
            //     await HttpClientHelper.AddUserHelper.excludeUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
            //         mail_slurp_helper.EMAIL_ADDRESS);
            // }
            // else {
            //     await Assert.ThrowsAsync<HttpRequestException>(async () => {
            //         await t;
            //     });
            // }
        }

        [Fact]
        public async void TestSendInvitationMoreThanOnceOnPendingFails() {
            // var mail_slurp_helper = new TemMailHelper
            //     ("132892a34e183e7264f2f13a47adf67f26e1381c5fb1401b70d0f3f64046a883", "TestSendInvitationMoreThanOnceOnPendingFails");
            //
            // var timeout = new TimeSpan(0, 0, 5);
            // var match_options = new MatchOptions {
            //     Matches = new List<MatchOption> {
            //         new MatchOption {
            //             Field = MatchOption.FieldEnum.FROM,
            //             Should = MatchOption.ShouldEnum.EQUAL,
            //             Value = "postmaster@drohub.xyz"
            //         },
            //         new MatchOption {
            //             Field = MatchOption.FieldEnum.TO,
            //             Should = MatchOption.ShouldEnum.EQUAL,
            //             Value = mail_slurp_helper.EMAIL_ADDRESS
            //         }
            //     }
            // };
            //
            // await HttpClientHelper.sendInvitation(_fixture, "admin@drohub.xyz", _fixture.AdminPassword,
            //     new[] {"a@a@b.com"});
            //
            // var t = HttpClientHelper.sendInvitation(_fixture, "admin@drohub.xyz", _fixture.AdminPassword,
            //     new[] {"a@a@b.com"});
            //
            // await Assert.ThrowsAsync<HttpRequestException>(async () => {
            //     await t;
            // });
        }

        [Fact]
        public async void TestSendInvitationInValidEmailFails() {
            await Assert.ThrowsAsync<HttpRequestException>(async () => {
                await HttpClientHelper.sendInvitation("admin@drohub.xyz", _fixture.AdminPassword,
                    new[] {"a@a@b.com"});;
            });
        }

        [InlineData("sdasd")]
        [Theory]
        public async void TestChangePermissionsInvalidInputFails(string victim_target_role) {


        }

        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM,  DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG,DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG,DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG,DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG,DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]


        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]


        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]


        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]


        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [Theory]
        public async void TestChangePermissions(bool same_org, string agent_role, string victim_original_role,
            string victim_target_role, bool expect_success) {

            const string AGENT_USER_EMAIL = DEFAULT_USER;
            const string VICTIM_USER_EMAIL = DEFAULT_USER + "1";
            var victim_user_org = same_org ? DEFAULT_ORGANIZATION : DEFAULT_ORGANIZATION + "1";

            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                AGENT_USER_EMAIL, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, agent_role,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);


            await using var victim_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                VICTIM_USER_EMAIL, DEFAULT_PASSWORD,
                victim_user_org, victim_original_role, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                DEFAULT_ALLOWED_USER_COUNT);

            var t = HttpClientHelper.changePermissions(AGENT_USER_EMAIL, DEFAULT_PASSWORD,
                VICTIM_USER_EMAIL, victim_target_role);

            var victim_user_id = _fixture.DbContext.Users
                .Single(u => u.Email == VICTIM_USER_EMAIL)
                .Id;


            if (expect_success) {
                    await t;
                    var victim_claims = _fixture.DbContext.UserClaims
                        .Where(c => c.UserId == victim_user_id)
                        .ToList();

                    //The other +1 is the subscription name
                    Assert.Equal(victim_claims.Count, DroHubUser.UserClaims[victim_target_role].Count + 1);
            }
            else {
                await Assert.ThrowsAsync<HttpRequestException>( async () =>

                    await t
                );
                var victim_claims = _fixture.DbContext.UserClaims
                    .Where(c => c.UserId == victim_user_id)
                    .ToList();

                //The other +1 is the subscription name
                Assert.Equal(victim_claims.Count, DroHubUser.UserClaims[victim_original_role].Count + 1);
            }
        }

        [Fact]
        public async void TestExcludeSelfUserFails() {
            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DroHubUser.SUBSCRIBER_POLICY_CLAIM,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<HttpRequestException>( async () =>
                await HttpClientHelper.AddUserHelper
                    .excludeUser(DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER)
            );
        }

        [InlineData("")]
        [InlineData("sdasd")]
        [InlineData("sdasd@df@.com")]
        [Theory]
        public async void TestExcludeUserInputValidationFails(string email_to_delete) {
            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DroHubUser.SUBSCRIBER_POLICY_CLAIM,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<HttpRequestException>( async () =>
                await HttpClientHelper.AddUserHelper
                    .excludeUser(DEFAULT_USER, DEFAULT_PASSWORD, email_to_delete)
            );
        }

        [Fact]
        public async void TestDeleteDeviceWithOngoingFlightFails() {

            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);
            var token = await HttpClientHelper.getApplicationToken("admin@drohub.xyz",
                _fixture.AdminPassword);
            var s = await HttpClientHelper.openWebSocket("admin@drohub.xyz", token["result"], DEFAULT_DEVICE_SERIAL);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await HttpClientHelper.CreateDeviceHelper
                .deleteDevice(DEFAULT_DEVICE_SERIAL, "admin@drohub.xyz", _fixture.AdminPassword));

            s.Close();
        }


        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG,DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.ADMIN_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]

        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.SUBSCRIBER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(SAME_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_SUCCESS)]
        [InlineData(OTHER_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.OWNER_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]

        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.PILOT_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        //
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(SAME_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.ADMIN_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.SUBSCRIBER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.OWNER_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.PILOT_POLICY_CLAIM, EXPECT_FAIL)]
        [InlineData(OTHER_ORG, DroHubUser.GUEST_POLICY_CLAIM, DroHubUser.GUEST_POLICY_CLAIM, EXPECT_FAIL)]
        [Theory]
        public async void TestExcludeUserPermissions(bool same_org, string agent_role, string victim_role,
            bool expect_success) {
            await using var agent_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, agent_role,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var new_user_org = same_org ? DEFAULT_ORGANIZATION : DEFAULT_ORGANIZATION + "1";
            await using var to_be_excluded_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER+"1", DEFAULT_PASSWORD,
                new_user_org, victim_role, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT,
                !expect_success);

            var t = HttpClientHelper.AddUserHelper.excludeUser(DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER+"1");
            if (expect_success) {
                try {
                    await t;
                }
                catch(Exception) {
                    await HttpClientHelper.AddUserHelper.excludeUser(_fixture, DEFAULT_USER + "1");
                    throw;
                }
            }
            else {
                await Assert.ThrowsAsync<HttpRequestException>( async () =>
                    await t
                );
            }

        }

        [Fact]
        public async void TestUpdateSubscriptionOnlyOnUserDelete() {
            {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 3);
                await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER+"1", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);
                await using var u3 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER+"2", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 3);
            }
            {
                await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);

                await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                    await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                        DEFAULT_USER+"1", DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                        DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 1);
                });
            }
        }

        [Fact]
        public async void TestCreateUserCountLimit() {
            await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);

            await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER+"1",
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u3 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_USER+"2",
                    DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, 2);
            });
        }

        [Fact]
        public async void TestDoubleUserCreationFails() {
            await using var u1 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await using var u2 = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                    DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE,
                    DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);
            });
        }

        [Fact]
        public async void TestLogout() {
            using var http_client_helper = await HttpClientHelper.createLoggedInUser("admin@drohub.xyz", _fixture.AdminPassword);
            var logout_url = new Uri(DroHubFixture.SiteUri, "Identity/Account/Logout");

            using var response = await http_client_helper.Client.GetAsync(logout_url);
            response.EnsureSuccessStatusCode();
            Assert.Equal(new Uri(DroHubFixture.SiteUri, "/"), response.RequestMessage.RequestUri);
        }

        [Fact]
        public async void TestQueryDeviceInfoOnNonExistingDeviceIsEmpty() {
            var token = (await HttpClientHelper.getApplicationToken("admin@drohub.xyz",
                _fixture.AdminPassword))["result"];
            var device_info = await HttpClientHelper.queryDeviceInfo("admin@drohub.xyz", token,
                DEFAULT_DEVICE_SERIAL);

            Assert.False(device_info.TryGetValue("result", out var _));
            Assert.True(device_info.TryGetValue("error", out var error));
            Assert.Equal("Device does not exist.", error);
        }

        [Fact]
        public async void TestGetApplicationTokenWithInvalidUserFails() {
            var deserialized_object = HttpClientHelper.getApplicationToken(DEFAULT_USER,
                DEFAULT_PASSWORD);
            await Assert.ThrowsAsync<InvalidCredentialException>(async () => await deserialized_object);
        }

        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(null)]
        [Theory]
        public async void TestValidateTokenWithBadVersion(int? test_version) {
            var token = (await HttpClientHelper.getApplicationToken("admin@drohub.xyz",
                _fixture.AdminPassword))["result"];

            var result = await HttpClientHelper.validateToken("admin@drohub.xyz", token, test_version);
            Assert.False(result.TryGetValue("result", out var _));
            Assert.True(result.TryGetValue("error", out var error));
            Assert.Equal(error, AndroidApplicationController.WRONG_API_DESCRIPTION);
        }


        [InlineData(DroHubUser.ADMIN_POLICY_CLAIM)]
        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM)]
        [Theory]
        public async void TestValidateToken(string user_role) {
            await using var s = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION, user_role, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            var result = await HttpClientHelper.validateToken(DEFAULT_USER, token,
                _fixture.Configuration.GetValue<double>(AndroidApplicationController.APPSETTINGS_API_VERSION_KEY));
            Assert.True(result.TryGetValue("result", out var message));
            Assert.Equal("ok", message);
        }

        [Fact]
        public async void TestCreateExistingDeviceFails() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var s = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
            DEFAULT_ORGANIZATION,
                DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            await Assert.ThrowsAsync<InvalidDataException>(async () => {
                await using var f = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                    DEFAULT_PASSWORD, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);
            });
        }

        [Fact]
        public async void TestQueryDeviceInfoWhenDeviceIsInOtherSubscriptionFails() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var s = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION,
                DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_ALLOWED_USER_COUNT);

            var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await Assert.ThrowsAsync<HttpRequestException>(async () => {
                await HttpClientHelper.queryDeviceInfo(DEFAULT_USER, token,
                    DEFAULT_DEVICE_SERIAL);
            });
        }

        [Fact]
        public async void TestQueryDeviceInfoOnNonExistingUser() {
            await Assert.ThrowsAsync<HttpRequestException>(async () => {
                await HttpClientHelper.queryDeviceInfo("asd",
                    "sadsdd",
                    DEFAULT_DEVICE_SERIAL);
            });
        }

        [InlineData("MyAnafi", null)]
        [InlineData(null, null)]
        [Theory]
        public async void TestIncompleteCreateDeviceModelFails(string device_name, string device_serial) {
                await Assert.ThrowsAsync<HttpRequestException>(async () => {
                    await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                        _fixture.AdminPassword, device_name, device_serial);
                });
        }

        [Fact]
        public async void TestNODEVICECreateDeviceFails() {
            await Assert.ThrowsAsync<InvalidDataException>(async () => {
                await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                    _fixture.AdminPassword, DEFAULT_DEVICE_NAME, "NODEVICE");
            });
        }


        [InlineData(DroHubUser.SUBSCRIBER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.OWNER_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.PILOT_POLICY_CLAIM, "MyAnafi", "000000", true)]
        [InlineData(DroHubUser.GUEST_POLICY_CLAIM, "MyAnafi", "000000", false)]
        [Theory]
        public async void TestCreateAndDeleteDevicePermission(string user_base_type,
            string device_name, string device_serial, bool expect_created) {
            await using var user_add = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD,
                DEFAULT_ORGANIZATION, user_base_type, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                DEFAULT_ALLOWED_USER_COUNT);
            {
                if (expect_created) {
                    await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                        DEFAULT_PASSWORD, device_name, device_serial);

                    var devices_list = await HttpClientHelper.getDeviceList(DEFAULT_USER, DEFAULT_PASSWORD);
                    Assert.NotNull(devices_list);
                    devices_list.Single(ds => ds.SerialNumber == device_serial);
                    var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER,
                        DEFAULT_PASSWORD))["result"];
                    var device_info = (await HttpClientHelper.queryDeviceInfo(DEFAULT_USER, token,
                        device_serial));
                    Assert.Equal(device_name, (string)device_info["result"]["name"]);
                    Assert.Equal(device_serial, (string)device_info["result"]["serialNumber"]);
                }
                else {
                    await Assert.ThrowsAsync<InvalidCredentialException>(async () => {
                        await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture,
                            DEFAULT_USER,
                            DEFAULT_PASSWORD, device_name, device_serial);
                    });
                }
            }
            Assert.Null(await HttpClientHelper.getDeviceList(DEFAULT_USER, DEFAULT_PASSWORD));
        }

        [Fact]
        public async void TestConnectionClosedOnNoSerial() {
            using var ws_transport = new TWebSocketClient(DroHubFixture.ThriftUri, WebSocketMessageType.Text, false);
            await Assert.ThrowsAsync<WebSocketException>(async () => await ws_transport.OpenAsync());
        }

        [Fact]
        public async void TestWebSocketWithNonExistingDevice() {
            const string user = "admin@drohub.xyz";
            var password = _fixture.AdminPassword;
            var token = (await HttpClientHelper.getApplicationToken(user, password))["result"];
            await Assert.ThrowsAsync<WebSocketException>(async () =>
                await HttpClientHelper.openWebSocket(user, token, DEFAULT_DEVICE_SERIAL));
        }

        [Fact]
        public async void TestWebSocketWithDeviceNotBelongingToSubscriptionFails() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture,
                DEFAULT_USER, DEFAULT_PASSWORD, DEFAULT_ORGANIZATION,
                DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10, 10);

            var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            await Assert.ThrowsAsync<WebSocketException>(async () =>
                await HttpClientHelper.openWebSocket(DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL));
        }

        [Fact]
        public async void TestWebSocketWithDeviceBelongingToSubscriptionSucceeds() {
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER, DEFAULT_PASSWORD,
                "Administrators", DroHubUser.SUBSCRIBER_POLICY_CLAIM, 10,
                10);

            var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER,
                DEFAULT_PASSWORD))["result"];

            var o = await HttpClientHelper.openWebSocket(DEFAULT_USER, token, DEFAULT_DEVICE_SERIAL);
            o.Close();
        }

        [Fact]
        public async void TestWebSocketFailedAuthentication() {
            var exception_occured = false;
            try {
                await HttpClientHelper.openWebSocket(DEFAULT_USER, DEFAULT_PASSWORD, _fixture
                    .AdminPassword);
            }
            catch (Exception e) {
                Assert.Equal("The server returned status code '401' when status code '101' was expected.", e.Message);
                exception_occured = true;
            }
            Assert.True(exception_occured, "No exception occurred and should have");
        }

        [Fact]
        public async void TestDeviceFlightStartTime() {
            Assert.Null(await HttpClientHelper.getDeviceFlightStartTime(999999, "admin@drohub.xyz",
                _fixture.AdminPassword));
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            var token = (await HttpClientHelper.getApplicationToken("admin@drohub.xyz",
                _fixture.AdminPassword))["result"];

            var device_id = await HttpClientHelper.getDeviceId(DEFAULT_DEVICE_SERIAL,
                "admin@drohub.xyz", _fixture.AdminPassword);
            Assert.Null(await HttpClientHelper.getDeviceFlightStartTime(device_id, "admin@drohub.xyz",
                _fixture.AdminPassword));

            var time_start = DateTime.Now.ToUniversalTime();
            using var f = await HttpClientHelper.openWebSocket("admin@drohub.xyz", token, DEFAULT_DEVICE_SERIAL);
            await Task.Delay(TimeSpan.FromSeconds(5));
            var received = await HttpClientHelper.getDeviceFlightStartTime(device_id, "admin@drohub.xyz",
                _fixture.AdminPassword);

            Assert.True(received.HasValue);
            var time_diff = time_start - HttpClientHelper.UnixEpoch.AddMilliseconds(received.Value);
            Assert.True(time_diff < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async void TestThriftConnectionDroppedAtTimeout() {
            await using var extra_user = await HttpClientHelper.AddUserHelper.addUser(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ORGANIZATION, DEFAULT_BASE_TYPE, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                ALLOWED_USER_COUNT);

            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_DEVICE_NAME, DEFAULT_DEVICE_SERIAL);

            var token = (await HttpClientHelper.getApplicationToken(DEFAULT_USER, DEFAULT_PASSWORD))["result"];
            using var t_web_socket_client = HttpClientHelper.getTWebSocketClient(DEFAULT_USER, token,
                DEFAULT_DEVICE_SERIAL);

            await t_web_socket_client.OpenAsync();
            Assert.True(t_web_socket_client.IsOpen);
            await Task.Delay(DroneMicroServiceManager.ConnectionTimeout + TimeSpan.FromSeconds(1));

            //For some reason the first one does not throw broken pipe. Probably some stupid internal in WebSocket.
            //All the library is crap.
            await t_web_socket_client.WriteAsync(new byte[1]);

            await Assert.ThrowsAsync<IOException>(async () =>
                await t_web_socket_client.WriteAsync(new byte[1]));
        }

        [Fact]
        public async void TestAndroidTests() {
            const string DOCKER_REPO_MOUNT_PATH = "/home/cirrus";
            await using var d = await HttpClientHelper.CreateDeviceHelper.createDevice(_fixture, "admin@drohub.xyz",
                _fixture.AdminPassword, DEFAULT_DEVICE_NAME, "DevSerial");

            var token = await HttpClientHelper.getApplicationToken("admin@drohub.xyz", _fixture.AdminPassword);

            using var test_containers = new Builder()
                .UseContainer()
                .WithEnvironment(
                    $"CODE_DIR={DOCKER_REPO_MOUNT_PATH}",
                    $"RPC_API_PATH={DroHubFixture.RPCAPIPathInRepo}",
                    $"APP_PATH={DroHubFixture.AppPathInRepo}"
                )
                .IsPrivileged()
                .KeepContainer()
                .ReuseIfExists()
                .AsUser("1000:1000")
                .UseImage("ptsneves/airborneprojects:android-test")
                .Mount(DroHubFixture.DroHubPath, DOCKER_REPO_MOUNT_PATH, MountType.ReadWrite)
                .Command(
                    $"-Pandroid.testInstrumentationRunnerArguments.UserName=admin@drohub.xyz",
                    $"-Pandroid.testInstrumentationRunnerArguments.Token={token["result"]}",
                    $"-Pandroid.testInstrumentationRunnerArguments.ValidateTokenUrl={HttpClientHelper.getAndroidActionUrl(HttpClientHelper.ValidateTokenActionName)}")
                .Build()
                .Start();
            test_containers.WaitForStopped();
            Assert.Equal(0, test_containers.GetConfiguration().State.ExitCode);
        }

        [Fact]
        public async void TestThriftDroneDataCorrectness() {
            var tasks = new List<Task>();
            for (var i = 0; i < 1; i++) {
                var serial = DEFAULT_DEVICE_SERIAL + i;
                var t = TelemetryMock.stageThriftDrone(_fixture, false, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES,
                    DEFAULT_USER+i, DEFAULT_PASSWORD, DEFAULT_ALLOWED_USER_COUNT, DEFAULT_ORGANIZATION+i, serial,
                    async (drone_rpc, telemetry_mock, user_name, token) => {
                        await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                            telemetry_mock.WaitForServer, user_name, token);

                        var signal_r_tasks_telemetry = telemetry_mock.getSignalRTasksTelemetry().ToArray();
                        Assert.Equal(telemetry_mock.TelemetryItems.Count, signal_r_tasks_telemetry.Count());
                        foreach (var f in signal_r_tasks_telemetry) {
                            var ds = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(f);
                            Assert.Equal((string) ds.Serial, telemetry_mock.SerialNumber);
                        }

                        var r = await telemetry_mock.getRecordedTelemetry();
                        Assert.Equal(telemetry_mock.TelemetryItems.Count(), r.Count);
                        foreach (var key_value_pair in r) {
                            Assert.Equal(((IDroneTelemetry)(key_value_pair.Value)).Serial, telemetry_mock.SerialNumber);
                        }
                    },
                    () => DroneRPC.generateTelemetry(serial,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                );
                tasks.Add(t);
            }

            try {
                await Task.WhenAll(tasks);
            }
            catch (Exception e) {
                var s = "";
                foreach (var task in tasks) {
                    if (task.IsFaulted) {
                        s += $"Exception:  {task.Exception}   \n";
                    }
                }
                throw new InvalidDataException(s);
            }
        }

        [Fact]
        public async void TestTelemetryWithEmptySerialFails() {
            var t = TelemetryMock.stageThriftDrone(_fixture, false, DEFAULT_ALLOWED_FLIGHT_TIME_MINUTES, DEFAULT_USER,
                DEFAULT_PASSWORD, DEFAULT_ALLOWED_USER_COUNT,
                DEFAULT_ORGANIZATION, DEFAULT_DEVICE_SERIAL, async (drone_rpc, telemetry_mock, user_name, token) => {
                    await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                        telemetry_mock.WaitForServer, user_name, token);

                    var signal_r_tasks_telemetry = telemetry_mock.getSignalRTasksTelemetry().ToArray();
                    Assert.Empty(signal_r_tasks_telemetry);

                    var r = await telemetry_mock.getRecordedTelemetry();
                    Assert.Empty(r.Keys);
                },
                () => DroneRPC.generateTelemetry(null,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await t);
        }

        [Fact]
        public async void TestSubscriptionEnd() {
            const int minutes = 1;
            var tasks = new List<Task>();
            for (var i = 0; i < 1; i++) {
                var serial = DEFAULT_DEVICE_SERIAL + i;
                var t = TelemetryMock.stageThriftDrone(_fixture, true, minutes, DEFAULT_USER+i, DEFAULT_PASSWORD,
                    DEFAULT_ALLOWED_USER_COUNT, DEFAULT_ORGANIZATION+i, serial,
                    async (drone_rpc, telemetry_mock, user_name, token) => {
                        var timer_start = DateTime.Now;
                        await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                            async () => {
                                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes + 0.5f * minutes));
                                await drone_rpc.MonitorConnection(TimeSpan.FromSeconds(5), cts.Token);
                            }, user_name, token);
                        var elapsed_time = DateTime.Now - timer_start;
                        if (elapsed_time < TimeSpan.FromMinutes(minutes))
                            throw new InvalidDataException($"{elapsed_time} > {TimeSpan.FromMinutes(minutes)} FAILED");
                        if (!(elapsed_time.TotalMinutes <
                              TimeSpan.FromMinutes(minutes).TotalMinutes + DroneMicroServiceManager.SubscriptionCheckInterval.TotalMinutes))
                            throw new InvalidDataException(
                                $"{elapsed_time.TotalMinutes} < {TimeSpan.FromMinutes(minutes).TotalSeconds + DroneMicroServiceManager.SubscriptionCheckInterval.TotalSeconds} FAILED");

                        await Assert.ThrowsAsync<WebSocketException>(async () =>
                            await HttpClientHelper.openWebSocket(telemetry_mock.UserName, token,
                                telemetry_mock.SerialNumber));
                    },
                    () => DroneRPC.generateTelemetry(serial,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }

        // ReSharper disable once xUnit1004
        [Fact (Skip = "To be ran manually")]
        public async void TestInterface() {
            const int minutes = 999;
            var tasks = new List<Task>();
            for (var i = 0; i < 1; i++) {
                var t = TelemetryMock.stageThriftDrone(_fixture, true, minutes, "admin@drohub.xyz", _fixture.AdminPassword,
                DEFAULT_ALLOWED_USER_COUNT, "administrators", DEFAULT_DEVICE_SERIAL+i, async (drone_rpc, telemetry_mock, user_name, token) => {
                        await DroneDeviceHelper.mockDrone(_fixture, drone_rpc, telemetry_mock.SerialNumber,
                            async () => {
                                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(minutes + 0.5f * minutes));
                                await drone_rpc.MonitorConnection(TimeSpan.FromSeconds(5), cts.Token);
                            }, user_name, token);
                    },
                () => DroneRPC.generateTelemetry(DEFAULT_DEVICE_SERIAL,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                );
                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }
    }
}
