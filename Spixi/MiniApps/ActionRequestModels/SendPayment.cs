using IXICore;
using IXICore.Activity;
using Newtonsoft.Json;

namespace SPIXI.MiniApps.ActionRequestModels
{
    public class SendPayment : MiniAppActionBase
    {
        [JsonConverter(typeof(AddressIxiNumberDictConverter))]
        public IDictionary<Address, IxiNumber> recipients;

        public SendPayment(IDictionary<Address, IxiNumber> recipients)
        {
            this.recipients = recipients;
        }
    }
}
