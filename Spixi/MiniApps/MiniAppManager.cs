using IXICore;
using IXICore.Meta;
using IXICore.Utils;
using System.IO.Compression;
using System.Text;

namespace SPIXI.MiniApps
{
    class MiniAppManager
    {
        string appsPath = "Apps";
        string tmpPath = "Tmp";

        Dictionary<string, MiniApp> appList = new Dictionary<string, MiniApp>();

        private Dictionary<byte[], MiniAppPage> appPages = new Dictionary<byte[], MiniAppPage>(new ByteArrayComparer());
        private static readonly HttpClient httpClient = new HttpClient();

        bool started = false;

        public MiniAppManager(string base_app_path)
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
                Logging.warn("Spixi Mini App Manager already started.");
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
                    MiniApp app = new MiniApp(File.ReadAllLines(app_info_path));
                    appList.Add(app.id, app);
                }
            }
        }

        public void stop()
        {
            if (!started)
            {
                Logging.warn("Spixi Mini App Manager already stopped.");
                return;
            }

            started = false;

            lock (appList)
            {
                // TODO maybe stop all apps
                appList.Clear();
            }
        }

        public async Task<MiniApp?> fetch(string url, long maxSizeBytes = 1 * 1024 * 1024) // 1 MB default limit
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                Logging.error("Invalid or insecure app URL: " + url);
                return null;
            }

            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest);
                headResponse.EnsureSuccessStatusCode();

                long contentLength = headResponse.Content.Headers.ContentLength ?? 0;
                if (contentLength > maxSizeBytes)
                {
                    Logging.error("App content size exceeds limit: " + contentLength + " bytes");
                    return null;
                }

                byte[] data = await httpClient.GetByteArrayAsync(url);
                if (data.Length != contentLength)
                {
                    Logging.error("Downloaded app data size mismatch: expected " + contentLength + " bytes, but got " + data.Length + " bytes");
                    return null;
                }

                string content = Encoding.UTF8.GetString(data);
                string[] app_info = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                return new MiniApp(app_info);
            }
            catch (HttpRequestException e)
            {
                Logging.error("HTTP request exception occurred: " + e.Message);
            }
            catch (Exception e)
            {
                Logging.error("Exception occurred while downloading app data: " + e.Message);
            }

            return null;
        }
        public string install(MiniApp fetchedAppInfo)
        {
            // Check for contentUrl first
            if (string.IsNullOrWhiteSpace(fetchedAppInfo.contentUrl) || !Uri.TryCreate(fetchedAppInfo.contentUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                Logging.error("Invalid or insecure app content URL: " + fetchedAppInfo.contentUrl);
                return null;
            }

            string app_name = "";

            string file_name = Path.GetRandomFileName();
            string source_app_file_path = Path.Combine(tmpPath, file_name);
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    File.WriteAllBytes(source_app_file_path, client.GetByteArrayAsync(fetchedAppInfo.contentUrl).Result);
                    fetchedAppInfo.contentSize = new FileInfo(source_app_file_path).Length;
                }
                catch (Exception e)
                {
                    Logging.error("Exception occured while downloading file: " + e);
                    if (File.Exists(source_app_file_path))
                    {
                        File.Delete(source_app_file_path);
                    }
                    return null;
                }
            }

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
                    string app_info_path = Path.Combine(source_app_path, "appinfo.spixi");

                    fetchedAppInfo.writeAppInfoFile(app_info_path);


                    MiniApp app = new MiniApp(File.ReadAllLines(app_info_path));

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
                    MiniApp app = new MiniApp(File.ReadAllLines(Path.Combine(source_app_path, "appinfo.spixi")));

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
                MiniApp app = appList[app_id];
                Directory.Delete(Path.Combine(appsPath, app.id), true);
                appList.Remove(app_id);
                return true;
            }
        }

        public MiniApp getApp(string app_id)
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

        public string getAppInstallURL(string app_id)
        {
            MiniApp mini_app = getApp(app_id);
            if (mini_app != null)
            {
                return mini_app.url;
            }
            return null;
        }

        public string getAppName(string app_id)
        {
            MiniApp mini_app = getApp(app_id);
            if (mini_app != null)
            {
                return mini_app.name;
            }
            return null;
        }

        public Dictionary<string, MiniApp> getInstalledApps()
        {
            return appList;
        }

        public MiniAppPage getAppPage(byte[] session_id)
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

        public MiniAppPage getAppPage(Address sender_address, string app_id)
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

        public Dictionary<byte[], MiniAppPage> getAppPages()
        {
            return appPages;
        }

        public void addAppPage(MiniAppPage page)
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

        public MiniAppPage acceptAppRequest(byte[] session_id)
        {
            MiniAppPage app_page = getAppPage(session_id);
            if (app_page != null)
            {
                app_page.accepted = true;
                StreamProcessor.sendAppRequestAccept(FriendList.getFriend(app_page.requestedByAddress), session_id);
            }
            return app_page;
        }

        public void rejectAppRequest(byte[] session_id)
        {
            MiniAppPage app_page = getAppPage(session_id);
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
