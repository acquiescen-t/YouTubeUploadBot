using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public abstract class DeckSettings
    {
        public string GetUploadedFolder { get; set; }
        public string GetThumbnailFolder { get; set; }

        public string GetDeckTech { get; set; }
        public string GetDeckList { get; set; }
    }
}
