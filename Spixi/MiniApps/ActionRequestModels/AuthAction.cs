namespace SPIXI.MiniApps.ActionRequestModels
{
    public class AuthData
    {
        public string challenge;
    }

    public class AuthAction : MiniAppActionBase
    {
        public AuthData data;
    }
}
