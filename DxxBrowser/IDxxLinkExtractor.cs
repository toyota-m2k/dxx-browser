using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxTargetInfo : DxxUriEx {
        public string Description { get; }

        public string Name { get; } // => DxxUrl.GetFileName(Url);

        public DxxTargetInfo(string url, string name, string description) : base(url) {
            Name = name;
            Description = description;
        }
        public DxxTargetInfo(Uri uri, string name, string description) : base(uri) {
            Name = name;
            Description = description;
        }

    }
    public interface IDxxLinkExtractor
    {
        /**
         * ダウンロードターゲット（Videoなど）を含むコンテンツのURLか？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool IsContainer(DxxUriEx url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のリストを保持したページへのURL か？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool IsContainerList(DxxUriEx url);

        bool IsTarget(DxxUriEx url);

        /**
         * ダウンロードターゲット(videoなど）のURLリストを取得
         */
        Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のURLリストを取得
         */
        Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx url);

    }
}
