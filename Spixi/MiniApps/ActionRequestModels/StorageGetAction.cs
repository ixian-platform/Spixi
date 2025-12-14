using IXICore;

namespace SPIXI.MiniApps.ActionRequestModels
{
    public class StorageGetAction : MiniAppActionBase
    {
        public string t;
        public string k;

        public StorageGetAction(string t, string k)
        {
            this.t = t;
            this.k = k;
        }
    }
}
