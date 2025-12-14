namespace SPIXI.MiniApps.ActionResponseModels
{
    public class AuthResponse : MiniAppActionResponse
    {
        public string challenge;
        public string publicKey;
        public string signature;
    }
}
