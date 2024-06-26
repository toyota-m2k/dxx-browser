using Common;
using DxxBrowser.driver.dmm;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DxxBrowser.driver.ipondo {
    public class IpondoDriver : DxxDriverBaseStoragePathSupport {
        public override string Name => "1Pondo";

        public override string ID => "ipondoDBBasedDriver";

        public override IDxxLinkExtractor LinkExtractor { get; } = new Extractor();
        public override IDxxStorageManager StorageManager { get; }

        public IpondoDriver() {
            StorageManager = new Storage(this);
        }

        private static Regex idRegex = new Regex(@".*/movies/(?<id>[0-9_]+)/.*");
        public override string ReserveFilePath(Uri uri) {
            var id = idRegex.Match(uri.ToString()).Groups["id"].Value;
            return Path.Combine(StoragePath, $"{id}.mp4");
        }

        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("1pondo.tv") || uri.Host.Contains("pacopacomama.com") || uri.Host.Contains("muramura.tv") || uri.Host.Contains("10musume.com");
        }
        public override string GetNameFromUri(Uri uri, string defName = "") {
            return DxxUrl.GetFileName(uri);
        }

        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }

        private string HandlingUrl = null;
        private Regex jsonRegex = new Regex(@".*/movie_details/movie_id/(?<id>[0-9_]+)\.json");
        private Regex listJsonRegex = new Regex(@".*/movie_lists/.*\.json");
        public override void HandleLinkedResource(string url, string refererUrl, string refererTitle) {
            if (url.EndsWith(".json")) {
                Console.WriteLine(url);
            }
            if (IsSupported(url)) {
                if (url.EndsWith(".json")) {
                    var m = jsonRegex.Match(url);
                    if (m != null && m.Success) {
                        var id = m.Groups["id"].Value;
                        downloadByJson(url, id);
                    }
                    else if (listJsonRegex.IsMatch(url)) {
                        downloadByListJson(url);
                    }

                }
            }
        }

        private HttpClient mHttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };

        private int resolution(string input) {
            if (input == null) {
                return 0;
            }
            string pattern = @"^\d+";
            Match match = Regex.Match(input, pattern);

            if (match.Success) {
                return int.Parse(match.Value);
            }

            return 0; // 数字が見つからない場合は0を返す
        }

        private void downloadByJson(string url, string id) {
            Task.Run(() => {
                lock (mHttpClient) {
                    if (HandlingUrl == url) return;
                    HandlingUrl = url;
                    var jsonString = mHttpClient.GetStringAsync(url).Result;
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                    var actor = json.GetValue("Actor")?.ToString();
                    var title = json.GetValue("Title")?.ToString();
                    if(!string.IsNullOrEmpty(actor)) {
                        title = $"{actor}> {title}";
                    }
                    var files = json.GetValue("SampleFiles") as JArray;
                    if (files != null) {
                        var target = files.Aggregate((a, o) => {
                            var acc = a["FileName"]?.ToString();
                            var name = o["FileName"]?.ToString();
                            if (resolution(acc) < resolution(name)) {
                                return o;
                            }
                            else {
                                return a;
                            }
                        });
                        if (target != null) {
                            var targetUrl = target["URL"]?.ToString();
                            var filename = target["FileName"]?.ToString();
                            if (!string.IsNullOrEmpty(targetUrl)) {
                                DxxDownloader.RunOnUIThread(() => {
                                    DxxDriverManager.Instance.Download(targetUrl, $"{id}.mp4", title);
                                });
                            }
                        }
                    }
                }
            });
        }

        private void downloadByListJson(string url) {
            Task.Run(() => {
                lock (mHttpClient) {
                    if (HandlingUrl == url) return;
                    HandlingUrl = url;
                    var jsonString = mHttpClient.GetStringAsync(url).Result;
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                    var rows = json.GetValue("Rows") as JArray;
                    if (rows != null) {
                        foreach (var row in rows) {
                            var id = row["MovieID"]?.ToString();
                            var actor = row["Actor"]?.ToString();
                            var title = row["Title"]?.ToString();
                            if (!string.IsNullOrEmpty(actor)) {
                                title = actor + " " + title??"untitled";
                            }
                            var files = row["SampleFiles"] as JArray;
                            if (files != null) {
                                var target = files.Aggregate((a, o) => {
                                    var acc = a["FileName"]?.ToString();
                                    var name = o["FileName"]?.ToString();
                                    if (resolution(acc) < resolution(name)) {
                                        return o;
                                    }
                                    else {
                                        return a;
                                    }
                                });
                                if (target != null) {
                                    var targetUrl = target["URL"]?.ToString();
                                    var filename = target["FileName"]?.ToString();
                                    if (!string.IsNullOrEmpty(targetUrl)) {
                                        DxxDownloader.RunOnUIThread(() => {
                                            DxxDriverManager.Instance.Download(targetUrl, $"{id}.mp4", title);
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }
        private class Extractor : IDxxLinkExtractor {
            //WeakReference<IpondoDriver> mDriver;
            //IpondoDriver Driver => mDriver?.GetValue();

            public Extractor() {
            }

            public bool IsContainer(DxxUriEx url) {
                return false;
            }

            public bool IsContainerList(DxxUriEx url) {
                return false;
            }

            public bool IsTarget(DxxUriEx urx) {
                return DxxUrl.GetFileName(urx.Uri).EndsWith(".mp4");
            }

            public Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx url) {
                return null;
            }

            public Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx url) {
                return null;
            }
        }
        private class Storage : DxxFileBasedStorage {
            public Storage(IDxxDriver driver) : base(driver) {
            }
            protected override string LOG_CAT => "IPD";
            public override string GetPath(Uri uri) {
                var m = idRegex.Match(uri.ToString());
                if (m.Success) {
                    var id = m.Groups["id"].Value;
                    return Path.Combine(Driver.StoragePath, $"{id}.mp4");
                } else {
                    return base.GetPath(uri);
                }
            }
        }
    }
}
