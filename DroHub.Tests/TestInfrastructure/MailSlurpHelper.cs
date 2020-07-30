using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mailslurp.Api;
using mailslurp.Model;

namespace DroHub.Tests.TestInfrastructure {
    public class MailSlurpHelper {
        private readonly mailslurp.Client.Configuration _configuration;
        private readonly Guid _inbox_id;
        public readonly string EMAIL_ADDRESS;

        public MailSlurpHelper(string api_key, string inbox_name) {
            _configuration = new mailslurp.Client.Configuration {
                BasePath = "https://api.mailslurp.com"
            };
            _configuration.ApiKey.Add("x-api-key", api_key);
            var api_instance = new InboxControllerApi(_configuration);

            var inbox_projection = api_instance
                .GetAllInboxes()
                .Content
                .SingleOrDefault(i => i.Name == inbox_name);

            if (inbox_projection == null) {
                var inbox = api_instance.CreateInbox(name: inbox_name);
                _inbox_id = inbox.Id;
                EMAIL_ADDRESS = inbox.EmailAddress;
            }
            else {
                _inbox_id = inbox_projection.Id;
                EMAIL_ADDRESS = $"{inbox_projection.Id}@mailslurp.com";
            }
        }

        public Task<List<EmailPreview>> receiveEmail(TimeSpan timeout, MatchOptions match_options, int? count) {
            var wait_for_instance = new WaitForControllerApi(_configuration);
            return wait_for_instance.WaitForMatchingEmailAsync(match_options, count, _inbox_id,
                (long?) timeout.TotalMilliseconds, true);
        }
    }
}