using IXICore;
using IXICore.Utils;
using System.Text;
using System.Text.Unicode;

namespace SPIXI.MiniApps
{
    public enum MiniAppCapabilities
    {
        SingleUser,
        MultiUser,
        Authentication,
        TransactionSigning,
        RegisteredNamesManagement,
        Storage
    }

    public class MiniApp
    {
        public string id = "";
        public string publisher = "";
        public string name = "";
        public string description = "";
        public string version = "";
        public string image = "";
        public string url = "";
        public string contentUrl = "";
        public long contentSize = 0;
        public string checksum = "";
  
        public Dictionary<MiniAppCapabilities, bool> capabilities = null;
        public int minUsers = 1;
        public int maxUsers = 1;

        public byte[] publicKey = null;
        public byte[] signature = null;

        public Dictionary<byte[], string> protocols = null;

        public MiniApp(string[] app_info, string? app_url = null)
        {
            foreach (string command in app_info)
            {
                int cmd_sep_index = command.IndexOf('=');
                if (cmd_sep_index == -1)
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
                switch (key)
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

                    case "description":
                        description = value;
                        break;

                    case "version":
                        version = value;
                        break;

                    case "image":
                        image = value;
                        break;

                    case "url":
                        url = value;
                        break;

                    case "contentUrl":
                        contentUrl = value;
                        break;

                    case "contentSize":
                        if (long.TryParse(value, out long size))
                        {
                            contentSize = size;
                        }
                        break;

                    case "checksum":
                        checksum = value;
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

                    case "protocols":
                        protocols = parseProtocols(value);
                        break;

                    case "minUsers":
                        if (int.TryParse(value, out int minUsers))
                        {
                            this.minUsers = minUsers;
                        }
                        break;

                    case "maxUsers":
                        if (int.TryParse(value, out int maxUsers))
                        {
                            this.maxUsers = maxUsers;
                        }
                        break;
                }
            }

            // If an app url is provided, this app metadata is likely from a remote source
            if (app_url != null)
            {
                // Attempt to resolve relative URLs
                if (!contentUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    int last_index = app_url.LastIndexOf('/');
                    if (last_index != -1)
                    {
                        contentUrl = app_url.Substring(0, last_index + 1) + contentUrl;
                    }
                    
                }

                if (!image.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    int last_index = app_url.LastIndexOf('/');
                    if (last_index != -1)
                    {
                        image = app_url.Substring(0, last_index + 1) + image;
                    }                  
                }
            }

        }

        private Dictionary<MiniAppCapabilities, bool> parseCapabilities(string value)
        {
            var capArr = value.Split(',');
            var caps = new Dictionary<MiniAppCapabilities, bool>();
            foreach (var cap in capArr)
            {
                var trimmedCap = cap.Trim().ToLower();
                switch (trimmedCap)
                {
                    case "singleuser":
                        caps.Add(MiniAppCapabilities.SingleUser, true);
                        break;

                    case "multiuser":
                        caps.Add(MiniAppCapabilities.MultiUser, true);
                        break;

                    case "authentication":
                        caps.Add(MiniAppCapabilities.Authentication, true);
                        break;

                    case "transactionsigning":
                        caps.Add(MiniAppCapabilities.TransactionSigning, true);
                        break;

                    case "registerednamesmanagement":
                        caps.Add(MiniAppCapabilities.RegisteredNamesManagement, true);
                        break;
                }
            }
            return caps;
        }

        private Dictionary<byte[], string> parseProtocols(string value)
        {
            var protoArr = value.Split(',');
            var protos = new Dictionary<byte[], string>(new ByteArrayComparer());
            foreach (var proto in protoArr)
            {
                var trimmedProto = proto.Trim().ToLower();
                protos.Add(CryptoManager.lib.sha3_512Trunc(UTF8Encoding.UTF8.GetBytes(trimmedProto)), value);
            }
            return protos;
        }

        public bool hasCapability(MiniAppCapabilities capability)
        {
            if (capabilities != null && capabilities.ContainsKey(capability))
            {
                return true;
            }
            return false;
        }

        public bool hasProtocol(byte[] protocolId)
        {
            if (protocols != null && protocols.ContainsKey(protocolId))
            {
                return true;
            }
            return false;
        }

        public string getProtocolName(byte[] protocolId)
        {
            if (protocols != null && protocols.ContainsKey(protocolId))
            {
                return protocols[protocolId];
            }
            return null;
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

        public void writeAppInfoFile(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"caVersion = 0");
            sb.AppendLine($"id = {id}");
            sb.AppendLine($"publisher = {publisher}");
            sb.AppendLine($"name = {name}");
            sb.AppendLine($"description = {description}");
            sb.AppendLine($"version = {version}");
            sb.AppendLine($"image = {image}");
            sb.AppendLine($"url = {url}");
            sb.AppendLine($"contentUrl = {contentUrl}");
            sb.AppendLine($"contentSize = {contentSize}");
            sb.AppendLine($"checksum = {checksum}");
            var capabilities_str = getCapabilitiesAsString();
            sb.AppendLine($"capabilities = {capabilities_str}");
            sb.AppendLine($"minUsers = {minUsers}");
            sb.AppendLine($"maxUsers = {maxUsers}");

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
