using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using System.Reflection;
using YouTubeUploadBot.models;

class Program
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    static string PATH_TO_SECRETS = "../../../secret";

    static void Main(string[] args)
    {
        try
        {
            new Program().UploadVideos().Wait();
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
        UserCredential cred = await GetCredentials();
        YouTubeService youTubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = cred,
            ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
        });

        var filesToUpload = settings.programSettings.pathToUploadFolder;
        #endregion
    }

    private async Task<UserCredential> GetCredentials()
    {
        string pathToSecret = PATH_TO_SECRETS + "/client_secret.json";
        logger.Trace($"pathToSecret: {pathToSecret}");
        using (var stream = new FileStream(pathToSecret, FileMode.Open, FileAccess.Read))
        {
            var cred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).
                    Secrets,
                    new[] { YouTubeService.Scope.YoutubeUpload },
                    "user",
                    CancellationToken.None
            );
            return cred;
        }
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