using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public class ProgramSettings
    {
        public DateTime nextUploadDateTime { get; set; }
        public string rank { get; set; }
        public int intervalHours { get; set; }
        public string pathToUploadFolder { get; set; }
    }
}
