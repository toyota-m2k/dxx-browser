using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public delegate void PlayItemWillBeRemovedProc(string url);

    public interface IDxxNGList {
        bool RegisterNG(string url, bool fatalError=false);
        event PlayItemWillBeRemovedProc PlayItemWillBeRemoved;

        //bool UnregisterNG(string url);
        //bool IsNG(string url);
    }
}
