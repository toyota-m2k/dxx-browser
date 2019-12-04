using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public class DxxTargetInfo {
        public string Url { get; }
        public string Description { get; }

        public string Name => DxxUrl.GetFileName(Url);

        public DxxTargetInfo(string url, string description) {
            Url = url;
            Description = description;
        }
    }
    public interface IDxxLinkExtractor
    {
        /**
         * ダウンロードターゲット（Videoなど）を含むコンテンツのURLか？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool IsContainer(Uri url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のリストを保持したページへのURL か？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool IsContainerList(Uri url);

        bool IsTarget(Uri uri);

        /**
         * ダウンロードターゲット(videoなど）のURLリストを取得
         */
        Task<IList<DxxTargetInfo>> ExtractTargets(Uri url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のURLリストを取得
         */
        Task<IList<DxxTargetInfo>> ExtractContainerList(Uri url);

    }
}
