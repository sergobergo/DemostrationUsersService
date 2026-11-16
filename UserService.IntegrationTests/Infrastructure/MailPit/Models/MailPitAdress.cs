using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace UserService.IntegrationTests.Infrastructure.MailPit.Models
{


        public class MailPitAddress
        {
            [JsonProperty("Address")]
            public string Address {get; set;} = string.Empty;

            [JsonProperty("Name")]
            public string Name { get; set; } = string.Empty;
        }
}
