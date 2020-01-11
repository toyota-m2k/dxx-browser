using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver {
    public class NopDriver : IDxxDriver {
        public string Name => "No Driver";

        public string ID => "NOP";

        public IDxxLinkExtractor LinkExtractor { get; } = new Extractor();

        public IDxxStorageManager StorageManager { get; } = new Storage();

        public string StoragePath { get; } = null;

        public void Download(DxxTargetInfo target, Action<bool> onCompleted) {
            onCompleted?.Invoke(false);
        }

        public bool HasSettings => false;

        public bool IsSupported(string url) {
            return false;
        }

        public string GetNameFromUri(Uri uri, string defName) {
            return defName;
        }

        public bool LoadSettins(XmlElement settings) {
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            return false;
        }

        public bool Setup(XmlElement settings, Window owner) {
            return false;
        }

        class Storage : IDxxStorageManager {
            public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted) {
                onCompleted?.Invoke(false);
            }

            public string GetSavedFile(Uri url) {
                return null;
            }

            public bool IsDownloaded(Uri url) {
                return false;
            }
        }

        class Extractor : IDxxLinkExtractor {
            public Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx) {
                return Task.FromResult<IList<DxxTargetInfo>>(null);
            }

            public Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                return Task.FromResult<IList<DxxTargetInfo>>(null);
            }

            public bool IsContainerList(DxxUriEx urx) {
                return false;
            }

            public bool IsContainer(DxxUriEx urx) {
                return false;
            }

            public bool IsTarget(DxxUriEx urx) {
                return false;
            }
        }
    }
}
