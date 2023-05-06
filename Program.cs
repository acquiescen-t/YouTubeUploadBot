using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using System.Runtime.Serialization;
using YouTubeUploadBot.models;

class Program
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    static string PATH_TO_SECRETS = "../../../secret";

    static void Main(string[] args)
    {
        try
        {
            Initialise();
            Console.ReadKey();
            // UploadVideos().Wait();
        }
        catch (AggregateException ex)
        {
            foreach(var e in ex.InnerExceptions)
            {
                logger.Error($"AggregateException: {e}");
            }
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    private async Task UploadVideos()
    {
        #region Initialisation
        var settings = Initialise();
        #endregion
        Initialise();
    }

    private static RootObject Initialise()
    {
        using (StreamReader r = new StreamReader(PATH_TO_SECRETS + "/settings.json"))
        {
            string contents = r.ReadToEnd();
            logger.Trace($"contents: {contents}");

            RootObject rootObject = JsonConvert.DeserializeObject<RootObject>(contents, new IsoDateTimeConverter { DateTimeFormat = "dd/MM/yyyy HH:mm:ss"} );

            logger.Info($"nextUploadDateTime:   {rootObject.programSettings.nextUploadDateTime}");
            logger.Info($"rank:                 {rootObject.programSettings.rank}");
            logger.Info($"intervalHours:        {rootObject.programSettings.intervalHours}");
            logger.Info($"pathToUploadFolder:   {rootObject.programSettings.pathToUploadFolder}{Environment.NewLine}");

            logger.Info("--JESKAI SETTINGS--");
            logger.Info($"uploadedFolder:       {rootObject.jeskaiSettings.uploadedFolder}");
            logger.Info($"thumbnailFolder:      {rootObject.jeskaiSettings.thumbnailFolder}");
            logger.Info($"untappedLink:         {rootObject.jeskaiSettings.untappedLink}{Environment.NewLine}");

            logger.Info("--GRIXIS SETTINGS--");
            logger.Info($"uploadedFolder:       {rootObject.grixisSettings.uploadedFolder}");
            logger.Info($"thumbnailFolder:      {rootObject.grixisSettings.thumbnailFolder}");
            logger.Info($"untappedLink:         {rootObject.grixisSettings.untappedLink}");
            return rootObject;
        }
    }
}