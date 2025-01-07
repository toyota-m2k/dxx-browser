using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class AnchorNodeComparator : IEqualityComparer<HtmlNode> {
        public bool Equals(HtmlNode x, HtmlNode y) {
            return x.Attributes["href"].Value == y.Attributes["href"].Value;
        }

        public int GetHashCode(HtmlNode obj) {
            return obj.Attributes["href"].Value?.GetHashCode() ?? 0;
        }

        public static AnchorNodeComparator Instance { get; } = new AnchorNodeComparator();
    }
}
