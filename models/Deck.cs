using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public abstract class Deck
    {
        public string uploadedFolder { get; set; }
        public string thumbnailFolder { get; set; }
        public string untappedLink { get; set; }
    }
}
