using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver {
    public class DefaultDriver : IDxxDriver {
        public string Name => throw new NotImplementedException();

        public string ID => throw new NotImplementedException();

        public bool HasSettings => throw new NotImplementedException();

        public IDxxLinkExtractor LinkExtractor => throw new NotImplementedException();

        public IDxxStorageManager StorageManager => throw new NotImplementedException();

        public string GetNameFromUri(Uri uri, string defName = "") {
            throw new NotImplementedException();
        }

        public bool IsSupported(string url) {
            throw new NotImplementedException();
        }

        public bool LoadSettins(XmlElement settings) {
            throw new NotImplementedException();
        }

        public bool SaveSettings(XmlElement settings) {
            throw new NotImplementedException();
        }

        public bool Setup(XmlElement settings, Window owner) {
            throw new NotImplementedException();
        }
    }
}
