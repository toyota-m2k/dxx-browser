using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public interface IDxxLinkExtractor
    {
        /**
         * ダウンロードターゲット（Videoなど）を含むコンテンツのURLか？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool HasTargets(Uri url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のリストを保持したページへのURL か？
         * URLから判断できないなら、とりあえず、trueを返しておき、ExtractTargetを適切に処理すること。
         */
        bool HasTargetContainers(Uri url);

        /**
         * ダウンロードターゲット(videoなど）のURLリストを取得
         */
        Task<IList<string>> ExtractTargets(Uri url);
        /**
         * ダウンロードターゲットを含むコンテンツ(html)のURLリストを取得
         */
        Task<IList<string>> ExtractTargetContainers(Uri url);
    }
}
