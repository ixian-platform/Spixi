using IXICore;
using IXICore.Meta;
using IXICore.Utils;
using System.Text;

namespace SPIXI.MiniApps
{
    class MiniAppDataCache
    {
        public Dictionary<string, byte[]> data = new();
        public long firstRequestWrite = 0;
        public long lastRequestWrite = 0;
    }

    public class MiniAppStorage
    {
        string appsStoragePath = "AppsStorage";
        Dictionary<string, Dictionary<string, MiniAppDataCache>> appDataCache = new();
        public bool running = false;
        Thread storageThread;

        public MiniAppStorage(string baseAppPath)
        {
            appsStoragePath = Path.Combine(baseAppPath, "AppsStorage");
            if (!Directory.Exists(appsStoragePath))
            {
                Directory.CreateDirectory(appsStoragePath);
            }
            running = true;

            storageThread = new Thread(storageLoop);
            storageThread.IsBackground = true;
            storageThread.Start();
        }

        private void storageLoop()
        {
            while (running)
            {
                try
                {
                    Dictionary<string, Dictionary<string, MiniAppDataCache>> appDataCacheCopy = new(appDataCache);
                    foreach (var appCache in appDataCacheCopy)
                    {
                        foreach (var tableCache in appCache.Value)
                        {
                            if (tableCache.Value.firstRequestWrite == 0)
                            {
                                continue;
                            }

                            if (Clock.getTimestampMillis() - tableCache.Value.firstRequestWrite < 1000
                                && Clock.getTimestampMillis() - tableCache.Value.lastRequestWrite < 200)
                            {
                                continue;
                            }

                            writeStorageData(appCache.Key, tableCache.Key);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logging.error("Exception in MiniAppStorage: " + e);
                }
                Thread.Sleep(1000);
            }
        }

        private MiniAppDataCache getStorageCache(string appId, string table)
        {
            lock (appDataCache)
            {
                MiniAppDataCache madc;
                if (appDataCache.ContainsKey(appId))
                {
                    if (appDataCache[appId].ContainsKey(table))
                    {
                        return appDataCache[appId][table];
                    }
                }
                else
                {
                    appDataCache[appId] = new Dictionary<string, MiniAppDataCache>();
                }

                madc = new();
                appDataCache[appId][table] = madc;

                string appStoragePath = Path.Combine(appsStoragePath, appId);
                string tableStoragePath = Path.Combine(appStoragePath, table);

                lock (madc)
                {
                    if (!Directory.Exists(appStoragePath))
                    {
                        return madc;
                    }

                    if (!File.Exists(tableStoragePath))
                    {
                        return madc;
                    }

                    using (var fs = File.Open(tableStoragePath, FileMode.Open))
                    {
                        using (var br = new BinaryReader(fs))
                        {
                            br.ReadBytes(1); // version
                            while (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                try
                                {
                                    var key = UTF8Encoding.UTF8.GetString(br.ReadBytes((int)br.ReadIxiVarUInt()));
                                    var value = br.ReadBytes((int)br.ReadIxiVarUInt());
                                    madc.data.Add(key, value);
                                }
                                catch (Exception e)
                                {
                                    Logging.error("" + e);
                                }
                            }
                        }
                    }
                }

                return madc;
            }
        }

        private void writeStorageData(string appId, string table)
        {
            var madc = getStorageCache(appId, table);
            lock (madc)
            {
                madc.firstRequestWrite = 0;
                madc.lastRequestWrite = 0;

                string appStoragePath = Path.Combine(appsStoragePath, appId);
                if (!Directory.Exists(appStoragePath))
                {
                    Directory.CreateDirectory(appStoragePath);
                }
                using (var fs = File.Open(Path.Combine(appStoragePath, table), FileMode.Create))
                {
                    fs.WriteByte(0);
                    foreach (var entry in madc.data)
                    {
                        fs.Write(IxiUtils.GetIxiBytes(UTF8Encoding.UTF8.GetBytes(entry.Key)));
                        fs.Write(IxiUtils.GetIxiBytes(entry.Value));
                    }
                }
            }
        }

        public byte[]? getStorageData(string appId, string table, string key)
        {
            var madc = getStorageCache(appId, table);
            lock (madc)
            {
                if (madc.data.ContainsKey(key))
                {
                    return madc.data[key];
                }
            }
            return null;
        }

        public void setStorageData(string appId, string table, string key, byte[] value)
        {
            var madc = getStorageCache(appId, table);
            lock (madc)
            {
                if (madc.data.ContainsKey(key))
                {
                    if (value == null)
                    {
                        madc.data.Remove(key);
                        if (madc.firstRequestWrite == 0)
                        {
                            madc.firstRequestWrite = Clock.getTimestampMillis();
                        }
                        madc.lastRequestWrite = Clock.getTimestampMillis();
                        return;
                    }
                }
                madc.data[key] = value;
                if (madc.firstRequestWrite == 0)
                {
                    madc.firstRequestWrite = Clock.getTimestampMillis();
                }
                madc.lastRequestWrite = Clock.getTimestampMillis();
            }
        }
    }
}
