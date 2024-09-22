using IXICore;
using IXICore.Meta;
using IXICore.Utils;
using System.IO.Compression;

namespace SPIXI.CustomApps
{
    class CustomAppManager
    {
        string appsPath = "Apps";
        string tmpPath = "Tmp";

        Dictionary<string, CustomApp> appList = new Dictionary<string, CustomApp>();

        private Dictionary<byte[], CustomAppPage> appPages = new Dictionary<byte[], CustomAppPage>(new ByteArrayComparer());

        bool started = false;

        public CustomAppManager(string base_app_path)
        {
            appsPath = Path.Combine(base_app_path, "html", "Apps");
            if (!Directory.Exists(appsPath))
            {
                Directory.CreateDirectory(appsPath);
            }

            tmpPath = Path.Combine(appsPath, "Tmp");
            if (!Directory.Exists(tmpPath))
            {
                Directory.CreateDirectory(tmpPath);
            }

        }

        public void start()
        {
            if(started)
            {
                Logging.warn("Custom App Manager already started.");
                return;
            }
            started = true;

            lock (appList)
            {
                foreach (var path in Directory.EnumerateDirectories(appsPath))
                {
                    string app_info_path = Path.Combine(path, "appinfo.spixi");
                    if (!File.Exists(app_info_path))
                    {
                        continue;
                    }
                    CustomApp app = new CustomApp(File.ReadAllLines(app_info_path));
                    appList.Add(app.id, app);
                }
            }
        }

        public void stop()
        {
            if (!started)
            {
                Logging.warn("Custom App Manager already stopped.");
                return;
            }

            started = false;

            lock (appList)
            {
                // TODO maybe stop all apps
                appList.Clear();
            }
        }

        public string install(string url)
        {
            string file_name = Path.GetRandomFileName();
            string source_app_file_path = Path.Combine(tmpPath, file_name);
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    File.WriteAllBytes(source_app_file_path, client.GetByteArrayAsync(url).Result);
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured while downloading file: " + e);
                    if(File.Exists(source_app_file_path))
                    {
                        File.Delete(source_app_file_path);
                    }
                    return null;
                }
            }
            string app_name = "";

            string source_app_path = Path.Combine(tmpPath, file_name + ".dir");
            string target_app_path = "";
            try
            {
                using (ZipArchive archive = ZipFile.Open(source_app_file_path, ZipArchiveMode.Read))
                {
                    if (Directory.Exists(source_app_path))
                    {
                        Directory.Delete(source_app_path, true);
                    }
                    Directory.CreateDirectory(source_app_path);

                    // extract the app to tmp location
                    archive.ExtractToDirectory(source_app_path);

                    // read app info
                    CustomApp app = new CustomApp(File.ReadAllLines(Path.Combine(source_app_path, "appinfo.spixi")));

                    if (appList.ContainsKey(app.id))
                    {
                        // TODO except when updating - version check
                        Logging.warn("App {0} already installed.", app.id);
                        if (File.Exists(source_app_file_path))
                        {
                            File.Delete(source_app_file_path);
                        }

                        if (Directory.Exists(source_app_path))
                        {
                            Directory.Delete(source_app_path, true);
                        }
                        return null;
                    }

                    app_name = app.name;

                    // TODO sig check

                    target_app_path = Path.Combine(appsPath, app.id);

                    // move to apps directory
                    Directory.Move(source_app_path, target_app_path);

                    lock (appList)
                    {
                        // add app to the list
                        appList.Add(app.id, app);
                    }
                }
                File.Delete(source_app_file_path);
            }
            catch (Exception e)
            {
                Logging.error("Error installing app: " + e);

                if (File.Exists(source_app_file_path))
                {
                    File.Delete(source_app_file_path);
                }

                if (Directory.Exists(source_app_path))
                {
                    Directory.Delete(source_app_path, true);
                }

                if (target_app_path != "" && Directory.Exists(target_app_path))
                {
                    Directory.Delete(target_app_path, true);
                }

                return null;
            }

            return app_name;
        }

        public bool remove(string app_id)
        {
            lock (appList)
            {
                if (!appList.ContainsKey(app_id))
                {
                    return false;
                }
                CustomApp app = appList[app_id];
                Directory.Delete(Path.Combine(appsPath, app.id), true);
                appList.Remove(app_id);
                return true;
            }
        }

        public CustomApp getApp(string app_id)
        {
            lock(appList)
            {
                if (appList.ContainsKey(app_id))
                {
                    return appList[app_id];
                }
                return null;
            }
        }

        public string getAppEntryPoint(string app_id)
        {
            if(getApp(app_id) != null)
            {
                return Path.Combine(appsPath, app_id, "app", "index.html");
            }
            return null;
        }

        public string getAppIconPath(string app_id)
        {
            if (getApp(app_id) != null)
            {
                string path = Path.Combine(appsPath, app_id, "icon.png");
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        public Dictionary<string, CustomApp> getInstalledApps()
        {
            return appList;
        }



        public CustomAppPage getAppPage(byte[] session_id)
        {
            lock (appPages)
            {
                if (appPages.ContainsKey(session_id))
                {
                    return appPages[session_id];
                }
                return null;
            }
        }

        public CustomAppPage getAppPage(Address sender_address, string app_id)
        {
            lock (appPages)
            {
                var pages = appPages.Values.Where(x => x.appId.SequenceEqual(app_id) && x.hasUser(sender_address));
                if (pages.Any())
                {
                    return getAppPage(pages.First().sessionId);
                }
                return null;
            }
        }

        public Dictionary<byte[], CustomAppPage> getAppPages()
        {
            return appPages;
        }

        public void addAppPage(CustomAppPage page)
        {
            lock (appPages)
            {
                appPages.Add(page.sessionId, page);
            }
        }

        public bool removeAppPage(byte[] session_id)
        {
            lock (appPages)
            {
                return appPages.Remove(session_id);
            }
        }

        public CustomAppPage acceptAppRequest(byte[] session_id)
        {
            CustomAppPage app_page = getAppPage(session_id);
            if (app_page != null)
            {
                app_page.accepted = true;
                StreamProcessor.sendAppRequestAccept(FriendList.getFriend(app_page.requestedByAddress), session_id);
            }
            return app_page;
        }

        public void rejectAppRequest(byte[] session_id)
        {
            CustomAppPage app_page = getAppPage(session_id);
            if (app_page != null)
            {
                if (removeAppPage(session_id))
                {
                    StreamProcessor.sendAppRequestReject(FriendList.getFriend(app_page.requestedByAddress), session_id);
                }
            }
        }

    }
}
