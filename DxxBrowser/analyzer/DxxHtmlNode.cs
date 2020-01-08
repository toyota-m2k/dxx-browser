using Common;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DxxBrowser {
    /**
     * リンク情報
     */
    public class DxxLink {
        /**
         * タグ名 (a, video, frame, iframe)
         */
        public string Name { get; }
        /**
         * URL (href, src)
         */
        public string Value { get; }

        public DxxLink(string name, string value) {
            Name = name;
            Value = value;
        }
    }

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

        private static IEnumerable<DxxLink> GetLinkOfNode(HtmlNode node) {
            DxxLink link = null;
            switch(node.Name) {
                case "a":
                    var href = node.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(href) && !href.StartsWith("#")) {
                        link = new DxxLink("a", href);
                    }
                    break;
                case "iframe":
                case "frame":
                case "video":
                    var src = node.GetAttributeValue("src", null);
                    if(!string.IsNullOrWhiteSpace(src)) {
                        link = new DxxLink(node.Name, src);
                    }
                    break;
                default:
                    break;
            }
            return (link != null) ? new DxxLink[] { link } : null;
        }

        public IEnumerable<DxxLink> Links {
            get {
                var lists = new IEnumerable<DxxLink>[] {
                    GetLinkOfNode(Node),
                    Node.SelectNodes(".//a")?.Where((v) => {
                        var a = v.GetAttributeValue("href", null);
                        return !string.IsNullOrWhiteSpace(a) && !a.StartsWith("#"); })?.Select((v)=>new DxxLink("a", v.GetAttributeValue("href", "??"))),
                    Node.SelectNodes(".//frame")?.Where((v) => !string.IsNullOrWhiteSpace(v.GetAttributeValue("src", null)))?.Select((v) => new DxxLink("frame", v.GetAttributeValue("src", "??"))),
                    Node.SelectNodes(".//iframe")?.Where((v) => !string.IsNullOrWhiteSpace(v.GetAttributeValue("src", null)))?.Select((v) => new DxxLink("iframe", v.GetAttributeValue("src", "??"))),
                    Node.SelectNodes(".//video")?.Where((v) => !string.IsNullOrWhiteSpace(v.GetAttributeValue("src", null)))?.Select((v) => new DxxLink("iframe", v.GetAttributeValue("src", "??"))) };

                return lists.Aggregate((IEnumerable<DxxLink>)null, (acc, v) => {
                    if (v != null) {
                        if (acc != null) {
                            acc.Union(v);
                        } else {
                            acc = v;
                        }
                    }
                    return acc;
                });
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

        public string FormattedHtml {
            get {
                var sb = new StringBuilder();
                FormatRecursive(sb, Node, 0);
                return sb.ToString();
            }
        }

        public string FormattedInnerText => FormatText(Node?.InnerText, 0);

        public HtmlAttributeCollection Attributes => Node?.Attributes;

        private static void writeSpaces(StringBuilder sb, int indent) {
            for (int i = 0; i < indent; i++) {
                sb.Append(" ");
            }
        }

        public void FormatRecursive(StringBuilder sb, HtmlNode node, int indent) {
            if(node==null) {
                return;
            }
            switch (node.NodeType) {
                case HtmlNodeType.Element:
                    writeSpaces(sb, indent);
                    sb.Append("<")
                      .Append(node.Name);
                    foreach(var attr in node.Attributes) {
                        sb.Append(" ");
                        if (string.IsNullOrEmpty(attr.Value)) {
                            sb.Append(attr.Name);
                        } else {
                            sb.Append(attr.Name)
                              .Append("=")
                              .Append(attr.Value);
                        }
                    }
                    if(node.HasChildNodes) {
                        sb.Append(">\r\n");
                        foreach(var child in node.ChildNodes) {
                            FormatRecursive(sb, child, indent + 1);
                        }
                        writeSpaces(sb, indent);
                        sb.Append("</")
                          .Append(node.Name)
                          .Append(">\r\n");
                    } else {
                        sb.Append("/>\r\n");
                    }
                    break;
                case HtmlNodeType.Text:
                    string t = FormatText(node.InnerText.Trim(), indent);
                    if(!string.IsNullOrEmpty(t)) {
                        sb.Append(t);
                    }
                    break;
                case HtmlNodeType.Comment:
                    if(node.ParentNode.Name=="script") {
                        // script内のコメントはスクリプト
                        string s = FormatText(node.InnerText.Trim(), indent);
                        if (!string.IsNullOrEmpty(s)) {
                            sb.Append(s);
                        }
                    }
                    break;
                case HtmlNodeType.Document:
                    writeSpaces(sb, indent);
                    sb.Append("#document\r\n");
                    break;
                default:
                    break;
            }
        }

        public static string FormatText(string src, int indent) {
            if(src==null) {
                return "";
            }
            src = src.Replace('\t', ' ');   // タブをスペースに変換
            var ary = src.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select((v) => v.TrimEnd()).Where((v)=>v.Length>0);
            if (Utils.IsNullOrEmpty(ary)) {
                return "";
            }
            var spc = "";
            var regex = new Regex("^(?<sp>[ ]*)");
            var match = regex.Match(ary.First());
            if(match.Success) {
                spc = match.Groups["sp"].Value;
            }
            return ary.Aggregate(new StringBuilder(), (sb, s) => {
                if (spc.Length>0 && s.StartsWith(spc)) {
                    s = s.Substring(spc.Length);
                }
                writeSpaces(sb, indent);
                return sb.AppendLine(s);
            }).ToString();
        }
    }
}
