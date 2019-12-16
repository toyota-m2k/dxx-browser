using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxHtmlNode {
        public HtmlNode Node { get; }
        private IEnumerable<DxxHtmlNode> _children;

        public DxxHtmlNode(HtmlNode node) {
            Node = node;
        }

        public IEnumerable<DxxHtmlNode> ChildNodes {
            get {
                if (_children != null) {
                    return _children;
                }
                _children = Node.ChildNodes.Where((v)=>v.NodeType==HtmlNodeType.Element).Select((v) => {
                    return new DxxHtmlNode(v);
                });
                return _children;
            }
        }

        public IEnumerable<DxxHtmlNode> SelectNodes(string xpath) {
            try {
                var r = Node.SelectNodes(xpath);
                return r?.Select((v) => {
                    return new DxxHtmlNode(v);
                });
            } catch (Exception e) {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        public string Description {
            get {
                if(Node.NodeType==HtmlNodeType.Element) {
                    switch(Node.Name.ToLower()) {
                        case "a": {
                            var href = Node.GetAttributeValue("href", "??");
                            return $"A (href={href})";
                        }
                        case "iframe":
                        case "frame":
                        case "video":
                        case "img": {
                            var src = Node.GetAttributeValue("src", "??");
                            return $"{Node.Name.ToUpper()} (src={src})";
                        }
                        default:
                            break;
                    }
                }
                return Node.Name;
            }
        }
    }
}
