using IXICore;

namespace SPIXI.MiniApps
{
    public class MiniAppStorage
    {
        string appsStoragePath = "AppsStorage";
        public MiniAppStorage(string baseAppPath)
        {
            appsStoragePath = Path.Combine(baseAppPath, "AppsStorage");
            if (!Directory.Exists(appsStoragePath))
            {
                Directory.CreateDirectory(appsStoragePath);
            }
        }

        public byte[]? getStorageData(string appId, string key)
        {
            string appStoragePath = Path.Combine(appsStoragePath, appId);
            if (!File.Exists(appStoragePath))
            {
                return null;
            }

            var storageData = File.ReadAllLines(appStoragePath);
            foreach (var line in storageData)
            {
                var lineKey = line.Substring(0, line.IndexOf('=')).Trim();
                if (lineKey == key)
                {
                    return Crypto.stringToHash(line.Substring(line.IndexOf('=') + 1));
                }
            }
            return null;
        }

        public void setStorageData(string appId, string key, byte[] value)
        {
            string appStoragePath = Path.Combine(appsStoragePath, appId);
            int lineCount = 0;
            bool found = false;
            List<string> storageData = new();
            if (File.Exists(appStoragePath))
            {
                storageData = File.ReadAllLines(appStoragePath).ToList();
                foreach (var line in storageData)
                {
                    var lineKey = line.Substring(0, line.IndexOf('=')).Trim();
                    if (lineKey == key)
                    {
                        found = true;
                        break;
                    }
                    lineCount++;
                }

                if (found)
                {
                    // update
                    if (value != null)
                    {
                        storageData[lineCount] = key + "=" + Crypto.hashToString(value);
                    }
                    else
                    {
                        storageData.RemoveAt(lineCount);
                    }
                    File.WriteAllLines(appStoragePath, storageData);
                    return;
                }
            }
            
            if (value != null)
            {
                // create
                storageData.Add(key + "=" + Crypto.hashToString(value));
                File.WriteAllLines(appStoragePath, storageData);
            }
        }
    }
}
