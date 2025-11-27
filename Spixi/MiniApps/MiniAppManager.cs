using IXICore;
using IXICore.Meta;
using IXICore.Streaming;
using IXICore.Utils;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace SPIXI.MiniApps
{
    class MiniAppManager
    {
        string appsPath = "Apps";
        public string tmpPath { get; private set; } = "Tmp";

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
                    if (app.id == "")
                    {
                        Logging.error("App id is empty for {0}", app_info_path);
                        continue;
                    }

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

                httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, MustRevalidate = true };

                using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest);
                headResponse.EnsureSuccessStatusCode();

                long contentLength = headResponse.Content.Headers.ContentLength ?? 0;
                if (contentLength > maxSizeBytes)
                {
                    Logging.error("App content size exceeds limit: " + contentLength + " bytes");
                    return null;
                }

                byte[] data = await httpClient.GetByteArrayAsync(url);

                string content = Encoding.UTF8.GetString(data);
                string[] app_info = content.Replace("\r\n", "\n").Split('\n');

                var app = new MiniApp(app_info, url);
                if (app.id == "")
                {
                    return null;
                }
                return app;
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

        public string installFromUrl(MiniApp fetchedAppInfo)
        {
            // Check for contentUrl first
            if (string.IsNullOrWhiteSpace(fetchedAppInfo.contentUrl) || !Uri.TryCreate(fetchedAppInfo.contentUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                Logging.error("Invalid or insecure app content URL: " + fetchedAppInfo.contentUrl);
                return null;
            }

            string app_name = "";

            string file_name = fetchedAppInfo.contentUrl.Split('/').Last();
            string source_app_file_path = Path.Combine(tmpPath, file_name);
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, MustRevalidate = true };
                    
                    File.WriteAllBytes(source_app_file_path, client.GetByteArrayAsync(fetchedAppInfo.contentUrl).Result);
                    fetchedAppInfo.contentSize = new FileInfo(source_app_file_path).Length;
                    string file_checksum = Crypto.sha256OfFile(source_app_file_path);

                    if (file_checksum != fetchedAppInfo.checksum)
                    {
                        throw new InvalidOperationException($"Checksum mismatch for downloaded app file. Expected {fetchedAppInfo.checksum} got {file_checksum}");
                    }
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

            app_name = installFromPath(source_app_file_path, fetchedAppInfo.contentUrl);
            if (File.Exists(source_app_file_path))
            {
                File.Delete(source_app_file_path);
            }

            return app_name;
        }

        public MiniApp extractAppInfo(string source_path, string? url = null)
        {
            string app_name = "";

            try
            {
                using (ZipArchive archive = ZipFile.Open(source_path, ZipArchiveMode.Read))
                {
                    // extract the app to tmp location
                    var entry = archive.GetEntry("appinfo.spixi");
                    if (entry == null)
                    {
                        return null;
                    }
                    var app_info_path = Path.Combine(tmpPath, "appinfo.spixi.tmp");
                    entry.ExtractToFile(app_info_path, true);

                    entry = archive.GetEntry("icon.png");
                    if (entry == null)
                    {
                        return null;
                    }
                    var icon_png_path = Path.Combine(tmpPath, "icon.png");
                    entry.ExtractToFile(icon_png_path, true);

                    // read app info
                    MiniApp app = new MiniApp(File.ReadAllLines(app_info_path), url);
                    File.Delete(app_info_path);
                    if (app.id == "")
                    {
                        return null;
                    }

                    if (appList.ContainsKey(app.id))
                    {
                        if (UpdateVerify.compareVersionsWithSuffix(appList[app.id].version, app.version) > 0)
                        {
                            Logging.warn("Newer version of app {0} already installed.", app.id);
                            return null;
                        }
                    }

                    app.contentSize = new FileInfo(source_path).Length;
                    app.checksum = Crypto.sha256OfFile(source_path);
                    app.image = icon_png_path;
                    app_name = app.name;

                    return app;
                }
            }
            catch (Exception e)
            {
                Logging.error("Error extracting app info: " + e);

                return null;
            }
        }

        public string installFromPath(string source_path, string? url = null)
        {
            string app_name = "";

            string source_app_path = Path.Combine(tmpPath, source_path.Split('/').Last().Split('\\').Last() + ".dir");
            string target_app_path = "";
            try
            {
                using (ZipArchive archive = ZipFile.Open(source_path, ZipArchiveMode.Read))
                {
                    if (Directory.Exists(source_app_path))
                    {
                        Directory.Delete(source_app_path, true);
                    }
                    Directory.CreateDirectory(source_app_path);

                    // extract the app to tmp location
                    archive.ExtractToDirectory(source_app_path);

                    // read app info
                    MiniApp app = new MiniApp(File.ReadAllLines(Path.Combine(source_app_path, "appinfo.spixi")), url);
                    if (app.id == "")
                    {
                        return null;
                    }
                    
                    if (appList.ContainsKey(app.id))
                    {
                        if (UpdateVerify.compareVersionsWithSuffix(appList[app.id].version, app.version) > 0)
                        {
                            Logging.warn("Newer version of app {0} already installed.", app.id);

                            if (Directory.Exists(source_app_path))
                            {
                                Directory.Delete(source_app_path, true);
                            }
                            return null;
                        }
                        else
                        {
                            Logging.warn("Older version of app {0} already installed, updating...", app.id);
                            remove(app.id);
                        }
                    }

                    app.contentSize = new FileInfo(source_path).Length;
                    app.checksum = Crypto.sha256OfFile(source_path);
                    app.url = url;
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

                    app.writeAppInfoFile(Path.Combine(target_app_path, "appinfo.spixi"));
                }
            }
            catch (Exception e)
            {
                Logging.error("Error installing app: " + e);

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

        public string getAppInfo(string app_id)
        {
            MiniApp mini_app = getApp(app_id);
            if (mini_app != null)
            {
                return $"{app_id}||{mini_app.url}||{mini_app.name}"; // TODO pack this information better
            }
            return app_id;
        }

        public Dictionary<string, MiniApp> getInstalledApps()
        {
            return appList;
        }

        public MiniAppPage getAppPage(Address sender_address, byte[] session_id)
        {
            lock (appPages)
            {
                if (appPages.ContainsKey(session_id))
                {
                    if (appPages[session_id].hasUser(sender_address))
                    {
                        return appPages[session_id];
                    }
                }
                return null;
            }
        }


        public MiniAppPage getAppPageByProtocol(Address sender_address, byte[] protocol_id)
        {
            lock (appPages)
            {
                foreach (var kv in appPages)
                {
                    var page = kv.Value;
                    if (page.hasUser(sender_address)
                        && getApp(page.appId).hasProtocol(protocol_id))
                    {
                        return page;
                    }
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
                    return getAppPage(sender_address, pages.First().sessionId);
                }
                return null;
            }
        }

        public MiniAppPage getAppPage(Address sender_address)
        {
            lock (appPages)
            {
                var pages = appPages.Values.Where(x => x.hasUser(sender_address));
                if (pages.Any())
                {
                    return getAppPage(sender_address, pages.First().sessionId);
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

        public MiniAppPage acceptAppRequest(Address sender_address, byte[] session_id)
        {
            MiniAppPage app_page = getAppPage(sender_address, session_id);
            if (app_page != null)
            {
                app_page.accepted = true;
                StreamProcessor.sendAppRequestAccept(FriendList.getFriend(sender_address), session_id);
            }
            return app_page;
        }

        public void rejectAppRequest(Address sender_address, byte[] session_id)
        {
            MiniAppPage app_page = getAppPage(sender_address, session_id);
            if (app_page != null)
            {
                if (removeAppPage(session_id))
                {
                    StreamProcessor.sendAppRequestReject(FriendList.getFriend(sender_address), session_id);
                }
            }
        }

    }
}
