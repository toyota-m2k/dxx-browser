using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser {
    public interface IDxxNGList {
        bool RegisterNG(string url);
        bool UnregisterNG(string url);
        bool IsNG(string url);

    }
}
