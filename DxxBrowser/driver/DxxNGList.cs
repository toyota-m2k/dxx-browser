using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DxxBrowser.driver {
    public static class DxxNGList {
        public static IDxxNGList Instance => DxxDBStorage.Instance;
    }
}
