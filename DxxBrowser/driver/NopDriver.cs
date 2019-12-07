using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DxxBrowser.driver {
    public class NopDriver : IDxxDriver {
        public string Name => "No Driver";

        public string ID => "NOP";

        public IDxxLinkExtractor LinkExtractor { get; } = new Extractor();

        public IDxxStorageManager StorageManager { get; } = new Storage();

        public bool HasSettings => false;

        public bool IsSupported(string url) {
            return false;
        }

        public bool LoadSettins(XmlElement settings) {
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            return false;
        }

        public bool Setup(XmlElement settings) {
            return false;
        }

        class Storage : IDxxStorageManager {
            public void Download(Uri url, string description, Action<bool> onCompleted) {
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
            public Task<IList<DxxTargetInfo>> ExtractContainerList(Uri url) {
                return Task.FromResult<IList<DxxTargetInfo>>(null);
            }

            public Task<IList<DxxTargetInfo>> ExtractTargets(Uri url) {
                return Task.FromResult<IList<DxxTargetInfo>>(null);
            }

            public bool IsContainerList(Uri url) {
                return false;
            }

            public bool IsContainer(Uri url) {
                return false;
            }

            public bool IsTarget(Uri uri) {
                return false;
            }
        }
    }
}
