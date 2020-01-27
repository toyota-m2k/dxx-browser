using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public delegate void PlayItemRemovingHandler(string url);

    public interface IDxxNGList {
        bool RegisterNG(string url, bool fatalError = false);
        int UnregisterNG(IEnumerable<string> urls);
        event PlayItemRemovingHandler PlayItemRemoving;

        //bool UnregisterNG(string url);
        //bool IsNG(string url);
    }

    public static class DxxNGListExtension {
        public static IObservable<string> AsPlayItemRemovingObservable(this IDxxNGList list) {
            return Observable.FromEvent<PlayItemRemovingHandler,string>(
                (handler)=>list.PlayItemRemoving+=handler,
                (handler) => list.PlayItemRemoving -= handler);
        }
    }
}
