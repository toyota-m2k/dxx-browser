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
            StorageManager = new DxxFileBasedStorage(this);
        }

        private Regex idRegex = new Regex(@".*/movies/(?<id>[0-9_]+)/.*");
        public override string ReserveFilePath(Uri uri) {
            var id = idRegex.Match(uri.ToString()).Groups["id"].Value;
            return Path.Combine(StoragePath, $"{id}.mp4");
        }

        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("1pondo.tv");
        }   
        public override string GetNameFromUri(Uri uri, string defName = "") {
            return DxxUrl.GetFileName(uri);
        }

        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }

        private Regex jsonRegex = new Regex(@".*/movie_details/movie_id/(?<id>[0-9_]+).json");
        public override void HandleLinkedResource(string url, string refererUrl, string refererTitle) {
            if(IsSupported(url)) {
                if (url.EndsWith(".json")) { 
                    var m = jsonRegex.Match(url);
                    if(m!=null && m.Success) {
                        var id = m.Groups["id"].Value;
                        downloadByJson(url, id);
                    }
                }
            }
        }

        private HttpClient mHttpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };

        private void downloadByJson(string url, string id) {
            int resolution(string input) {
                if(input==null) {
                    return 0;
                }
                string pattern = @"^\d+";
                Match match = Regex.Match(input, pattern);

                if (match.Success) {
                    return int.Parse(match.Value);
                }

                return 0; // 数字が見つからない場合は0を返す
            }

            Task.Run(() => {
                lock(mHttpClient) {
                    var jsonString = mHttpClient.GetStringAsync(url).Result;
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                    var title = json.GetValue("Title")?.ToString();
                    var files = json.GetValue("SampleFiles") as JArray;
                    if(files!=null) {
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
                        if(target!=null) {
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
        private class Storage : IDxxStorageManager {

            public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted = null) {
                DxxDBStorage.Instance.Download(target, driver, onCompleted);
            }

            public string GetSavedFile(Uri url) {
                return DxxDBStorage.Instance.GetSavedFile(url);
            }

            public bool IsDownloaded(Uri url) {
                return DxxDBStorage.Instance.IsDownloaded(url);
            }
        }
    }
}
