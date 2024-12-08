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
            var storageData = File.ReadAllLines(appStoragePath);
            foreach (var line in storageData)
            {
                var lineKey = line.Substring(0, line.IndexOf('=')).Trim();
                if (lineKey == key)
                {
                    return Crypto.stringToHash(line.Substring(line.IndexOf('=')));
                }
            }
            return null;
        }

        public void setStorageData(string appId, string key, byte[] value)
        {
            string appStoragePath = Path.Combine(appsStoragePath, appId);
            var storageData = File.ReadAllLines(appStoragePath);
            int lineCount = 0;
            bool found = false;
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
                    storageData[lineCount] = Crypto.hashToString(value);
                } else
                {
                    var storageDataList = storageData.ToList();
                    storageDataList.RemoveAt(lineCount);
                    storageData = storageDataList.ToArray();
                }
                File.WriteAllLines(appStoragePath, storageData);
            } else if (value != null)
            {
                // create
                storageData.Append(Crypto.hashToString(value));
                File.WriteAllLines(appStoragePath, storageData);
            }
        }
    }
}
