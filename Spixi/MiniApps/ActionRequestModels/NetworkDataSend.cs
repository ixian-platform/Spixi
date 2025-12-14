using IXICore;

namespace SPIXI.MiniApps.ActionRequestModels
{
    public class NetworkDataSendAction : MiniAppActionBase
    {
        public string d;
        public Address? r;
        public string? pid;

        public NetworkDataSendAction(string d, string? r, string? pid)
        {
            this.d = d;
            this.r = r != null ? new Address(r) : null;
            this.pid = pid;
        }
    }
}
