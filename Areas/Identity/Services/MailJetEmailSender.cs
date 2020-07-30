using System;
using System.Configuration;
using System.Threading.Tasks;
using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace DroHub.Areas.Identity.Services {

    public static class AmazonSESEmailSenderExtensions {
        public static void AddMailJetEmailSenderExtensions (this IServiceCollection services, IConfiguration configuration) {
            services.AddTransient<IEmailSender, MailJetEmailSender>();
            services.Configure<MailJetEmailSender.Options>(
                configuration.GetSection(MailJetEmailSender.ConfigurationKey));
        }
    }

    public class MailJetEmailSender : IEmailSender {
        public const string ConfigurationKey = "MailJetEmailSender";
        public class Options {
            public string APIKey { get; set; }
            public string APISecret { get; set; }

            public string FromEmailAddress { get; set; }
        }

        readonly Options _options;
        private readonly ILogger<MailJetEmailSender> _logger;
        private readonly MailjetClient _client;

        public MailJetEmailSender(ILogger<MailJetEmailSender> logger,
            IOptionsMonitor<Options> options) {
            _logger = logger;
            _options = options.CurrentValue;
            _client = new MailjetClient(_options.APIKey, _options.APISecret) {
                Version = ApiVersion.V3_1,
            };
        }

        public class MailJetEmailSenderException : InvalidOperationException {
            internal MailJetEmailSenderException(string error) : base(error) {}
        }

        public async Task SendEmailAsync(string email, string subject, string html) {


            var request = new MailjetRequest {
                    Resource = Send.Resource,
                }
                .Property(Send.Messages, new JArray { new JObject {
                        {
                            "From",
                            new JObject {
                                {"Email", _options.FromEmailAddress},
                                {"Name", "DROHUB.xyz"}
                            }
                        }, {
                            "To",
                            new JArray {
                                new JObject {
                                    {
                                        "Email",
                                        email
                                    }, {
                                        "Name",
                                        email
                                    }
                                }
                            }
                        }, {
                            "Subject",
                            subject
                        }, {
                            "HTMLPart",
                            html
                        }
                    }
                });

            var response = await _client.PostAsync(request);
            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("Failed to send email: {error_message}", response.GetErrorMessage());
                throw new MailJetEmailSenderException(response.GetErrorMessage());
            }
        }
    }
}
