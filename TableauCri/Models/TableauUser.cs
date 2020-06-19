using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableauCri.Models
{
    public class TableauUser
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("fullName")]
        public string FullName { get; set; }

        [JsonProperty("siteRole")]
        public string SiteRole { get; set; }

        [JsonProperty("lastLogin")]
        public DateTime LastLogin { get; set; }

        [JsonProperty("externalAuthUserId")]
        public string ExternalAuthUserId { get; set; }

        [JsonProperty("domain")]
        public TableauDomain Domain { get; set; }

        /// <summary>
        /// sAMAccountName@DomainFQDN, e.g. AD userPrincipalName style login name
        /// </summary>
        public string UserPrincipalName => $"{Name ?? ""}@{Domain?.Name ?? ""}";

        /// <summary>
        /// DomainFQDN\sAMAccountName or just sAMAccountName if Domain.Name is null/empty.  FQDN notwithstanding,
        /// 'down-level logon name' is Microsoft's official name for DOMAIN\username style login name.
        /// </summary>
        public string DownLevelLogonName => $"{Domain?.Name ?? ""}\\{Name ?? ""}".TrimStart('\\');

        public string SiteId { get; set; }
        public string SiteName { get; set; }
        public string ApiVersion { get; set; }

        public string Url => $"api/{ApiVersion}/sites/{SiteId}/users/{Id}";

        public string ToRequestString()
        {
            var json = new JObject();
            json["name"] = Name;
            json["siteRole"] = SiteRole;
            return json.ToString();
        }
    }
}
