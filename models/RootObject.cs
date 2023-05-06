using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public class RootObject
    {
        public ProgramSettings programSettings { get; set; }
        public JeskaiSettings jeskaiSettings { get; set; }
        public GrixisSettings grixisSettings { get; set; }

    }
}
