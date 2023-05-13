using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using YouTubeUploadBot.models;

class Program
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    static string PATH_TO_SECRETS = @"G:\Projects\YouTubeUploadBot\secret";

    static Settings settings;
    static YouTubeService youTubeService;

    string videoID;

    static void Main(string[] args)
    {
        try
        {
            new Program().BatchUploadVideos().Wait();
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

    // TODO: Implement polymorphism

    private async Task BatchUploadVideos()
    {
        Initialise();
        UserCredential cred = await GetCredentials();
        youTubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = cred,
            ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
        });

        await UploadVideos();
    }
    private async Task UploadVideos()
    {
        var filesToUpload = Directory.EnumerateFiles(settings.programSettings.pathToUploadFolder, "*", SearchOption.AllDirectories);
        int uploadCount = 0;

        foreach(var filePath in filesToUpload)
        {
            if (uploadCount == 5)
            {
                logger.Info($"Quota reached! Stopping upload. {Environment.NewLine}");
                break;
            }

            logger.Trace($"Currently working on: {filePath}");

            #region Get Title & Description
            string folderName = Directory.GetParent(filePath).Name.Substring(2);
            // 01 Jeskai Truths vs Grixis Midrange -> Jeskai Truths vs Grixis Midrange

            var pattern = @"(.*?) vs (.*)";
            var match = Regex.Match(folderName, pattern);
            string myDeck = match.Groups[1].Value.Trim();
            string opponentsDeck = match.Groups[2].Value.Trim();

            Deck currentDeck = null;
            switch (myDeck)
            {
                case "Grixis Truths":
                    currentDeck = new GrixisTruths();
                    break;
                case "Jeskai Truths":
                    currentDeck = new JeskaiTruths();
                    break;
                case "Bant Yorion":
                    currentDeck = new BantYorion();
                    break;
                default:
                    continue;
            }

            string title = myDeck + " vs " + opponentsDeck + " | " + settings.programSettings.rank + " | MTG Historic";
            string description = GetDescription(myDeck, opponentsDeck);
            logger.Info($"Title: {title}");
            logger.Info($"Description: {description}");
            #endregion

            Video video = SetupVideo(title, description);
            bool videoSuccess = await UploadVideo(filePath, video);
            bool thumbnailSuccess = await UploadThumbnail(myDeck, opponentsDeck);
            if (videoSuccess && thumbnailSuccess)
            {
                logger.Info($"Video uploaded successsfully and set to go live at {String.Format("{0:f}", settings.programSettings.nextUploadDateTime)}");
                //FileMover.MoveUploadedFile(settings, filePath, myDeck);
                FileMover.MoveUploadedFileNew(settings, filePath, currentDeck);
            }
            uploadCount++;
        }
        using (StreamWriter sw = File.CreateText(Path.Combine(PATH_TO_SECRETS, "settings.json")))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(sw, settings);
        }
    }   

    // YouTube stuff: setting up of credentials and tokens, monitoring upload progress
    private static void Initialise()
    {
        using (StreamReader r = new StreamReader(PATH_TO_SECRETS + "/settings.json"))
        {
            string contents = r.ReadToEnd();

            settings = JsonConvert.DeserializeObject<Settings>(contents, new IsoDateTimeConverter { DateTimeFormat = "dd/MM/yyyy HH:mm:ss"} );

            logger.Trace($"nextUploadDateTime:   {settings.programSettings.nextUploadDateTime}");
            logger.Trace($"rank:                 {settings.programSettings.rank}");
            logger.Trace($"intervalHours:        {settings.programSettings.intervalHours}");
            logger.Trace($"pathToUploadFolder:   {settings.programSettings.pathToUploadFolder}{Environment.NewLine}");

            logger.Trace("--JESKAI TRUTHS SETTINGS--");
            logger.Trace($"uploadedFolder:       {settings.jeskaiTruths.uploadedFolder}");
            logger.Trace($"thumbnailFolder:      {settings.jeskaiTruths.thumbnailFolder}");
            logger.Trace($"deckList:             {settings.jeskaiTruths.deckList}");
            logger.Trace($"deckTech:             {settings.jeskaiTruths.deckTech}{Environment.NewLine}");

            logger.Trace("--GRIXIS TRUTHS SETTINGS--");
            logger.Trace($"uploadedFolder:       {settings.grixisTruths.uploadedFolder}");
            logger.Trace($"thumbnailFolder:      {settings.grixisTruths.thumbnailFolder}");
            logger.Trace($"deckList:             {settings.grixisTruths.deckList}");
            logger.Trace($"deckTech:             {settings.grixisTruths.deckTech}{Environment.NewLine}");

            logger.Trace("--BANT YORION SETTINGS--");
            logger.Trace($"uploadedFolder:       {settings.bantYorion.uploadedFolder}");
            logger.Trace($"thumbnailFolder:      {settings.bantYorion.thumbnailFolder}");
            logger.Trace($"deckList:             {settings.bantYorion.deckList}");
            logger.Trace($"deckTech:             {settings.bantYorion.deckTech}{Environment.NewLine}");
        }
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
    private Video SetupVideo(string title, string description)
    {
        var video = new Video();

        video.Snippet = new VideoSnippet();
        video.Snippet.Title = title;
        video.Snippet.Description = description;

        video.Status = new VideoStatus();
        settings.programSettings.nextUploadDateTime = settings.programSettings.nextUploadDateTime.AddHours(settings.programSettings.intervalHours);
        video.Status.PrivacyStatus = "private";
        video.Status.PublishAt = settings.programSettings.nextUploadDateTime;

        return video;
    }
    private void ProgressChanged(IUploadProgress progress)
    {
        switch (progress.Status)
        {
            case UploadStatus.Uploading:
                logger.Trace($"{progress.BytesSent} bytes sent.");
                break;
            case UploadStatus.Failed:
                logger.Error($"Error: {progress.Exception}");
                break;
        }
    }
    private void ResponseReceived(Video video)
    {
        videoID = video.Id;
        logger.Info($"Video id {video.Id} was successfully uploaded!");
    }
    private async Task<bool> UploadVideo(string filePath, Video video)
    {
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                logger.Trace("Uploading video...");
                var videosInsertRequest = youTubeService.Videos.Insert(video, "snippet, status", fileStream, "video/*");
                videosInsertRequest.ProgressChanged += ProgressChanged;
                videosInsertRequest.ResponseReceived += ResponseReceived;
                await videosInsertRequest.UploadAsync();
            }
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return false;
        }
        return true;
    }
    private async Task<bool> UploadThumbnail(string myDeck, string opponentsDeck)
    {
        string thumbnailPath = GetThumbnailPath(myDeck, opponentsDeck);
        if (!String.IsNullOrEmpty(thumbnailPath))
        {
            try
            {
                using (var thumbnailFileStream = new FileStream(thumbnailPath, FileMode.Open))
                {
                    logger.Trace("Uploading thumbnail...");
                    var thumbnailInsertRequest = youTubeService.Thumbnails.Set(videoID, thumbnailFileStream, "image/png");
                    thumbnailInsertRequest.ProgressChanged += ProgressChanged;
                    await thumbnailInsertRequest.UploadAsync();
                    return true;
                }
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return false;
            }
        }
        return false;
    }

    // Getting information about video
    private string GetDescription(string myDeck, string opponentsDeck)
    {
        StringBuilder sb = new StringBuilder("Gameplay of ")
            .Append(myDeck).Append(" vs ").Append(opponentsDeck).Append("\n\n");

        switch(myDeck)
        {
            case "Jeskai Truths":
                sb.Append("Deck tech: ").Append(settings.jeskaiTruths.deckTech).Append("\nDeck list: ").Append(settings.jeskaiTruths.deckList);
                break;
            case "Grixis Truths":
                sb.Append("Deck tech: ").Append(settings.grixisTruths.deckTech).Append("\nDeck list: ").Append(settings.grixisTruths.deckList);
                break;
            case "Bant Yorion":
                sb.Append("Deck tech: ").Append(settings.bantYorion.deckTech).Append("\nDeck list: ").Append(settings.bantYorion.deckList);
                break;
        }
        return sb.ToString();
    }
    private string GetThumbnailPath(string myDeck, string opponentsDeck)
    {
        string thumbnailFolder = String.Empty;
        switch(myDeck)
        {
            case "Jeskai Truths":
                thumbnailFolder = settings.jeskaiTruths.thumbnailFolder;
                break;
            case "Grixis Truths":
                thumbnailFolder = settings.grixisTruths.thumbnailFolder;
                break;
            case "Bant Yorion":
                thumbnailFolder = settings.bantYorion.thumbnailFolder;
                break;
            default:
                logger.Warn($"No thumbnail path for {myDeck}!");
                return thumbnailFolder;
        }
        var allThumbnails = Directory.EnumerateFiles(thumbnailFolder);
        List<string> matchingThumbnails = new List<string>();
        foreach (var thumbnail in allThumbnails)
            if (thumbnail.Contains(opponentsDeck))
                matchingThumbnails.Add(thumbnail);

        if (matchingThumbnails.Count == 0)
        {
            logger.Warn($"No thumbnail found for {opponentsDeck}! Create one and upload manually.");
            return String.Empty;
        }
        return matchingThumbnails[new Random().Next(0, matchingThumbnails.Count)];
    }
}