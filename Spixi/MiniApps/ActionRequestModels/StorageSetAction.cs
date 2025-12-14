using IXICore;

namespace SPIXI.MiniApps.ActionRequestModels
{
    public class StorageSetAction : MiniAppActionBase
    {
        public string t;
        public string k;
        public string v;

        public StorageSetAction(string t, string k, string v)
        {
            this.t = t;
            this.k = k;
            this.v = v;
        }
    }
}
