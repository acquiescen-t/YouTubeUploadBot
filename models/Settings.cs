using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public class Settings
    {
        public ProgramSettings programSettings { get; set; }
        public JeskaiTruths jeskaiSettings { get; set; }
        public GrixisTruths grixisSettings { get; set; }

    }
}
