using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace DxxBrowser {
    public class DxxBookmark : ObservableCollection<DxxBookmark.DxxBookmarkRec> {
        public class DxxBookmarkRec {
            public string Name { get; set; }
            public string Url { get; set; }

            public DxxBookmarkRec() {

            }

            public DxxBookmarkRec(string name, string url) {
                Name = name;
                Url = url;
            }
        }

        public bool Serialize() {
            try {
                var serializer = new XmlSerializer(this.GetType());
                //書き込むファイルを開く（UTF-8 BOM無し）
                using (var sw = new System.IO.StreamWriter("bookmarks.xml", false, new System.Text.UTF8Encoding(false))) {

                    //シリアル化し、XMLファイルに保存する
                    serializer.Serialize(sw, this);
                    sw.Close();
                    return true;
                }
            } catch (Exception e) {
                Debug.WriteLine(e);
                return false;
            }
        }

        public static DxxBookmark Deserialize() {
            try {
                //XmlSerializerオブジェクトを作成
                var serializer = new XmlSerializer(typeof(DxxBookmark));

                //読み込むファイルを開く
                using (var sr = new StreamReader("bookmarks.xml", new UTF8Encoding(false))) {

                    //XMLファイルから読み込み、逆シリアル化する
                    var obj = serializer.Deserialize(sr);
                    if(null==obj) {
                        throw new Exception("cannot be deserialized.");
                    }
                    return (DxxBookmark)obj;
                }
            } catch (Exception e) {
                Debug.WriteLine(e);
                return new DxxBookmark();
            }
        }


        public void AddBookmark(string name, string url) {
            RemoveBookmark(url);
            this.Insert(0, new DxxBookmarkRec(name, url));
        }

        public DxxBookmarkRec FindBookmark(string url) {
            var org = this.Where((bm) => bm.Url == url);
            if (!Utils.IsNullOrEmpty(org)) {
                return org.First();
            }
            return null;
        }

        public DxxBookmarkRec RemoveBookmark(string url) {
            var r = FindBookmark(url);
            if(null!=r) {
                Remove(r);
            }
            return r;
        }

        public DxxBookmarkRec BringUpBookmark(string url) {
            var r = FindBookmark(url);
            if(null!=r) {
                var i = IndexOf(r);
                if(i!=0) {
                    Move(i, 0);
                }
            }
            return r;
        }
    }
}
