namespace SPIXI.CustomApps.ActionRequestModels
{
    public class AuthData
    {
        public string challenge;
    }

    public class AuthAction : CustomAppActionBase
    {
        public AuthData data;
    }
}
