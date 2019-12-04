using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace DxxBrowser.driver.dmm
{
    public class DmmDriver : IDxxDriver
    {
        private string StoragePath { get; set; }

        public string Name => "DMM";

        public string ID => "dmmFileBasedDriver";

        public IDxxLinkExtractor LinkExtractor { get; private set; }

        public IDxxStorageManager StorageManager { get; private set; }

        public bool HasSettings => true;

        private const string KEY_STORAGE_PATH = "StoragePath";

        public bool LoadSettins(XmlElement settings) {
            StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, StoragePath);
            return true;
        }

        public bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("dmm.co.jp");
        }

        public bool Setup(XmlElement settings) {
            var dlg = new DmmSettingsDialog(StoragePath);
            if(dlg.ShowDialog() ?? false) {
                StoragePath = dlg.Path;
                if (null!=settings) {
                    SaveSettings(settings);
                }
                return true;
            }
            return false;
        }

        public DmmDriver() {
            LinkExtractor = new Extractor(this);
            StorageManager = new Storage(this);
        }

        class Storage : IDxxStorageManager {
            WeakReference<DmmDriver> mDriver;
            DmmDriver Driver => mDriver?.GetValue();

            public Storage(DmmDriver driver) {
                mDriver = new WeakReference<DmmDriver>(driver);
            }

            public async Task<bool> Download(Uri uri, string description) {
                if(!Driver.LinkExtractor.IsTarget(uri)) {
                    return false;
                }
                var path = GetPath(uri);
                if(File.Exists(path)) {
                    return false;
                }
                return await DxxDownloader.Instance.DownloadAsync(uri, path, description);
            }

            private string GetPath(Uri uri) {
                var filename = DxxUrl.GetFileName(uri);
                return Path.Combine(Driver.StoragePath, filename);
            }

            public string GetSavedFile(Uri uri) {
                var path = GetPath(uri);
                return File.Exists(path) ? path : null;
            }

            public bool IsDownloaded(Uri uri) {
                var path = GetPath(uri);
                return File.Exists(path);
            }
        }

        class Extractor : IDxxLinkExtractor {
            WeakReference<DmmDriver> mDriver;
            DmmDriver Driver => mDriver?.GetValue();

            public Extractor(DmmDriver driver) {
                mDriver = new WeakReference<DmmDriver>(driver);
            }

            public async Task<IList<DxxTargetInfo>> ExtractContainerList(Uri uri) {
                if(!IsContainerList(uri)) {
                    return null;
                }
                // <div>
                //< p class="tmb"><a href = "https://www.dmm.co.jp/litevideo/-/detail/=/cid=bf00392/" >
                // < span class="img"><img src = "https://pics.dmm.co.jp/digital/video/bf00392/bf00392pt.jpg" alt="美尻にぴったり密着タイトスカートSEX8時間"></span> 
                //<span class="txt">美尻にぴったり密着タイト...</span> 
                //<!--/tmb--></a></p>

                var web = new HtmlWeb();
                var html = await web.LoadFromWebAsync(uri.ToString());
                if(null==html) {
                    return null;
                }
                var para = html.DocumentNode.SelectNodes("//p[@class='tmb']");
                if(para==null||para.Count==0) {
                    return null;
                }
                var list = para.Select((p) => {
                    var href = p.SelectSingleNode("a")?.GetAttributeValue("href", null);
                    var desc = p.SelectSingleNode("a/span/img")?.GetAttributeValue("alt", null);
                    if(desc==null) {
                        desc = p.SelectSingleNode("a/span[@class='txt']")?.InnerText;
                    }
                    if (string.IsNullOrEmpty(href)) {
                        return null;
                    } else {
                        return new DxxTargetInfo(href, desc);
                    }
                }).Where((v) => v != null);
                return list.ToList();
            }

            private string ensureUrl(string url) {
                if(url.StartsWith("//")) {
                    return "https:" + url;
                } else {
                    return url;
                }
            }

            public async Task<IList<DxxTargetInfo>> ExtractTargets(Uri uri) {
                if(!IsContainer(uri)) {
                    return null;
                }
                var web = new HtmlWeb();
                var outer = await web.LoadFromWebAsync(uri.ToString());

                var frames = outer.DocumentNode.SelectNodes("//iframe").Select((f) => {
                    return f.GetAttributeValue("src", null);
                });

                var list = new List<DxxTargetInfo>();
                foreach (var frame in frames) {
                    var innerUrl = ensureUrl(frame);
                    if(!Driver.IsSupported(innerUrl)) {
                        continue;
                    }
                    var inner = await web.LoadFromWebAsync(innerUrl);
                    var txt = inner.DocumentNode.SelectSingleNode("//script[contains(text(), 'params')]");
                    if (null != txt) {
                        var regex = new Regex("{.*}");
                        var v = regex.Match(txt.InnerText);
                        if (v.Success) {
                            //var params =
                            //    {
                            //                                "id":"dmmplayer","type":"litevideo","service":"litevideo","mode":"detail","cid":"ssni00529","eid":"FhJeUFYGVAAB",
                            //        "gid":"Myd9eiAHMHFvN0Z5ZU5efCMCZ2k_","width":"560px","height":"360px","videoId":"video","videoType":"mp4",
                            //        "src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_mhb_w.mp4",
                            //        "title":"\u7f8e\u4eba\u4e0a\u53f8\u3068\u7ae5\u8c9e\u90e8\u4e0b\u304c\u51fa\u5f35\u5148\u306e\u76f8\u90e8\u5c4b\u30db\u30c6\u30eb\u3067\u2026\u3044\u305f\u305a\u3089\u8a98\u60d1\u3092\u771f\u306b\u53d7\u3051\u305f\u90e8\u4e0b\u304c10\u767a\u5c04\u7cbe\u306e\u7d76\u502b\u6027\u4ea4 \u5929\u4f7f\u3082\u3048","titleLink":"","titleLinkTarget":"_top","autoPlay":true,
                            //        "poster":"\/\/pics.litevideo.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529.jpg",
                            //        "replay":false,"playIconSize":"100%","loop":false,"muted":false,
                            //        "bitrates":[
                            //            {"bitrate":300,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_sm_w.mp4"},
                            //            {"bitrate":1000,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_dm_w.mp4"},
                            //            {"bitrate":1500,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_dmb_w.mp4"},
                            //            {"bitrate":3000,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_mhb_w.mp4"}],
                            //        "affiliateId":"","controls":{"header":true,"panel":true,"title":true,"seek":true,"duration":true,"rewind60":false,"rewind10":true,"playpause":true,
                            //        "forward10":true,"forward60":false,"bitrate":true,"volume":true,"fullscreen":true},"isDebug":false,"isVideoDebug":false,"isDisplayPlayCount":false
                            //    }
                            var js = JObject.Parse(v.Value);
                            var ary = js["bitrates"];
                            if (ary != null && ary.Type == JTokenType.Array) {
                                int br = 0;
                                string src = js["src"].Value<string>();
                                foreach (var e in ary) {
                                    var i = e["bitrate"].Value<int>();
                                    if (i > br) {
                                        br = i;
                                        src = e["src"].Value<string>();
                                    }
                                }
                                if (src != null) {
                                    list.Add(new DxxTargetInfo(ensureUrl(src), js["title"].Value<string>()));
                                }
                            }
                        }
                    }
                }
                return list;
            }

            public bool IsContainerList(Uri uri) {
                if (!uri.Host.Contains("dmm.co.jp")) {
                    return false;
                }
                return uri.ToString().Contains("/list/");
            }

            public bool IsContainer(Uri uri) {
                if (!uri.Host.Contains("dmm.co.jp")) {
                    return false;
                }
                var s = uri.ToString();
                return s.Contains("/cid=");
            }

            public bool IsTarget(Uri uri) {
                return DxxUrl.GetFileName(uri).EndsWith(".mp4");
            }
        }
    }
}
