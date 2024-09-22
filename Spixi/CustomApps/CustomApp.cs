using IXICore;

namespace SPIXI.CustomApps
{
    enum CustomAppCapabilities
    {
        SingleUser,
        MultiUser,
        Authentication,
        TransactionSigning,
        RegisteredNamesManagement,
        Storage
    }

    class CustomApp
    {
        public string id = "";
        public string publisher = "";
        public string name = "";
        public string version = "";
        public byte[] publicKey = null;
        public byte[] signature = null;
        public Dictionary<CustomAppCapabilities, bool> capabilities = null;

        public CustomApp(string[] app_info)
        {
            foreach(string command in app_info)
            {
                int cmd_sep_index = command.IndexOf('=');
                if(cmd_sep_index == -1)
                {
                    continue;
                }

                string key = command.Substring(0, cmd_sep_index).Trim(new char[] { ' ', '\t', '\r', '\n' });
                string value = command.Substring(cmd_sep_index + 1).Trim(new char[] { ' ', '\t', '\r', '\n' });

                if (key.StartsWith(";"))
                {
                    continue;
                }

                int caVersion = 0;
                switch(key)
                {
                    case "caVersion":
                        caVersion = Int32.Parse(value);
                        break;

                    case "id":
                        id = value;
                        break;

                    case "publisher":
                        publisher = value;
                        break;

                    case "name":
                        name = value;
                        break;

                    case "version":
                        version = value;
                        break;

                    case "publicKey":
                        publicKey = Crypto.stringToHash(value);
                        break;

                    case "signature":
                        signature = Crypto.stringToHash(value);
                        break;

                    case "capabilities":
                        capabilities = parseCapabilities(value);
                        break;
                }
            }
        }

        private Dictionary<CustomAppCapabilities, bool> parseCapabilities(string value)
        {
            var capArr = value.Split(',');
            var caps = new Dictionary<CustomAppCapabilities, bool>();
            foreach (var cap in capArr)
            {
                var trimmedCap = cap.Trim();
                switch (trimmedCap)
                {
                    case "singleUser":
                        caps.Add(CustomAppCapabilities.SingleUser, true);
                        break;

                    case "multiUser":
                        caps.Add(CustomAppCapabilities.MultiUser, true);
                        break;

                    case "authentication":
                        caps.Add(CustomAppCapabilities.Authentication, true);
                        break;

                    case "transactionSigning":
                        caps.Add(CustomAppCapabilities.TransactionSigning, true);
                        break;

                    case "registeredNamesManagement":
                        caps.Add(CustomAppCapabilities.RegisteredNamesManagement, true);
                        break;
                }
            }
            return caps;
        }

        public bool hasCapability(CustomAppCapabilities capability)
        {
            if (capabilities != null && capabilities.ContainsKey(capability))
            {
                return true;
            }
            return false;
        }

        public string getCapabilitiesAsString()
        {
            string str = "";
            if (capabilities == null)
            {
                return "";
            }

            foreach (var cap in capabilities)
            {
                if (str != "")
                {
                    str += ", ";
                }
                str += cap.Key.ToString();
            }
            return str;
        }
    }
}
