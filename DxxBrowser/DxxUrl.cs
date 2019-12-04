using DxxBrowser.driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxUrl {
        public Uri Uri { get; }
        public IDxxDriver Driver { get; }
        public string Description { get; }

        public DxxUrl(Uri uri, IDxxDriver driver, string description) {
            Uri = uri;
            Driver = driver;
            Description = description;
        }

        public DxxUrl(DxxTargetInfo info, IDxxDriver driver) {
            Uri = new Uri(info.Url);
            Driver = driver;
            Description = info.Description;
        }

        private bool mActualHasTargets = true;
        private bool mActualHasTargetContainers = true;

        public bool IsContainer => mActualHasTargets && Driver.LinkExtractor.IsContainer(Uri);

        public bool IsContainerList => mActualHasTargetContainers && Driver.LinkExtractor.IsContainerList(Uri);

        public bool IsTarget => Driver.LinkExtractor.IsTarget(Uri);

        public async Task<IList<DxxTargetInfo>> TryGetTargetContainers() {
            if(!IsContainerList) { 
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractContainerList(Uri);
            if(r==null||r.Count==0) {
                mActualHasTargetContainers = false;
                return null;
            }
            return r;
        }

        public async Task<IList<DxxTargetInfo>> TryGetTargets() {
            if(!IsContainer) {
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractTargets(Uri);
            if (r == null || r.Count == 0) {
                mActualHasTargets = false;
                return null;
            }
            return r;
        }

        public async void DownloadTargets(IList<DxxTargetInfo> targets) {
            if(targets==null||targets.Count==0) {
                return;
            }
            foreach(var t in targets) {
                var uri = new Uri(t.Url);
                if (Driver.LinkExtractor.IsTarget(uri)) {
                    if (!Driver.StorageManager.IsDownloaded(uri) && !DxxDownloader.Instance.IsDownloading(t.Url)) {
                        _ = Driver.StorageManager.Download(uri, t.Description);
                    }
                } else { 
                    var du = new DxxUrl(t, Driver);
                    var cnt = await du.TryGetTargetContainers();
                    if(cnt!=null && cnt.Count>0) {
                        DownloadTargets(cnt);
                    }
                    var tgt = await du.TryGetTargets();
                    if(tgt!=null && tgt.Count>0) {
                        DownloadTargets(tgt);
                    }
                }
            }
        }

        public async void Download() {
            if(Driver.LinkExtractor.IsTarget(Uri)) {
                _ = Driver.StorageManager.Download(Uri, Description);
            }
            DownloadTargets(await TryGetTargetContainers());
            DownloadTargets(await TryGetTargets());
        }

        public string Host => Uri.Host;
        public string FileName => GetFileName(Uri);
        public string URL => Uri.ToString();

        public static string GetFileName(Uri uri) {
            return uri.Segments.Last();
        }
    }
}
