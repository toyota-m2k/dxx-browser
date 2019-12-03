using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxUrl {
        public enum TargetType { 
            None,
            Target,
            TargetContainer, // video を含むコンテント
            TargetContainerList,    // Targetリストを含むコンテント
        }

        public Uri Uri { get; private set; }
        public IDxxDriver Driver { get; private set; }

        public DxxUrl(Uri uri, IDxxDriver driver) {
            Uri = uri;
            Driver = driver;
        }

        private TargetType? mActualType = null;

        public TargetType Type {
            get {
                if(mActualType!=null) {
                    return mActualType.Value;
                }
                if(Driver.LinkExtractor.HasTargets(Uri)) {
                    return TargetType.TargetContainer;
                } else if(Driver.LinkExtractor.HasTargetContainers(Uri)) {
                    return TargetType.TargetContainerList;
                } else {
                    return TargetType.None;
                }
            }
        }

        public async Task<IList<string>> TryGetTargetContainers() {
            if(Type!=TargetType.TargetContainerList) {
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractTargetContainers(Uri);
            if(r==null||r.Count==0) {
                mActualType = TargetType.None;
                return null;
            }
            return r;
        }

        public async Task<IList<string>> TryGetTarget() {
            if (Type != TargetType.TargetContainer) {
                return null;
            }
            var r = await Driver.LinkExtractor.ExtractTargets(Uri);
            if (r == null || r.Count == 0) {
                mActualType = TargetType.None;
                return null;
            }
            return r;
        }



        public string Host => Uri.Host;
        public string FileName => Uri.Segments.Last();
        public string URL => Uri.ToString();
    }
}
