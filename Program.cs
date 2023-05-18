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
    static Boolean TEST = false;

    static Logger logger = LogManager.GetCurrentClassLogger();
    static string PATH_TO_SECRETS = @"G:\Projects\YouTubeUploadBot\secret";

    static ProgramSettings programSettings;
    static YouTubeService youTubeService;
    string videoID;

    static void Main(string[] args)
    {
        try
        {
            logger.Info($"TEST: {TEST}");
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

        if (!TEST)
        {
            UserCredential cred = await GetCredentials();
            youTubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = cred,
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            });
        }

        await UploadVideos();
    }
    private async Task UploadVideos()
    {
        var filesToUpload = Directory.EnumerateFiles(programSettings.pathToUploadFolder, "*", SearchOption.AllDirectories);
        int uploadCount = 0;

        foreach(var filePath in filesToUpload)
        {
            if (uploadCount == 5)
            {
                logger.Info($"Quota reached! Stopping upload. {Environment.NewLine}");
                break;
            }

            logger.Trace("-------------------------------------------------------------------------------------------------------------");
            logger.Trace($"Currently working on: {filePath}");

            #region Get Title & Description
            string folderName = Directory.GetParent(filePath).Name.Substring(2);
            logger.Trace($"Trimmed folderName: {folderName}");

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
                case "5C Ramp":
                    currentDeck = new ChromaticRamp();
                    break;
                default:
                    continue;
            }
            logger.Trace($"currentDeck.deckName: {currentDeck.deckName}{Environment.NewLine}");

            string title = myDeck + " vs " + opponentsDeck + " | " + programSettings.rank + " | MTG Historic";
            logger.Info($"Title: {title}");

            string description = GetDescription(currentDeck, opponentsDeck);
            logger.Info($"Description: {description.Replace("\n", "[NEWLINE]")}{Environment.NewLine}");
            #endregion

            Video video = SetupVideo(title, description);
            bool videoSuccess = await UploadVideo(filePath, video);
            bool thumbnailSuccess = await UploadThumbnail(currentDeck, opponentsDeck);
            if (videoSuccess && thumbnailSuccess)
            {
                logger.Info($"Video uploaded successsfully and set to go live at {String.Format("{0:f}", programSettings.nextUploadDateTime)} {Environment.NewLine}");
                FileMover.MoveUploadedFile(filePath, currentDeck, TEST);
            }
            uploadCount++;
        }
        using (StreamWriter sw = File.CreateText(Path.Combine(PATH_TO_SECRETS, "settings.json")))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.Serialize(sw, programSettings);
        }
    }   

    // YouTube stuff: setting up of credentials and tokens, monitoring upload progress
    private static void Initialise()
    {
        using (StreamReader r = new StreamReader(PATH_TO_SECRETS + "/settings.json"))
        {
            string contents = r.ReadToEnd();

            programSettings = JsonConvert.DeserializeObject<ProgramSettings>(contents, new IsoDateTimeConverter { DateTimeFormat = "dd/MM/yyyy HH:mm:ss"} );

            logger.Trace($"nextUploadDateTime:   {programSettings.nextUploadDateTime}");
            logger.Trace($"rank:                 {programSettings.rank}");
            logger.Trace($"intervalHours:        {programSettings.intervalHours}");
            logger.Trace($"pathToUploadFolder:   {programSettings.pathToUploadFolder}{Environment.NewLine}");
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
        programSettings.nextUploadDateTime = programSettings.nextUploadDateTime.AddHours(programSettings.intervalHours);
        video.Status.PrivacyStatus = "private";
        video.Status.PublishAt = programSettings.nextUploadDateTime;

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
            if (!TEST)
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
        }
        catch (Exception e)
        {
            logger.Error(e.Message);
            return false;
        }
        return true;
    }
    private async Task<bool> UploadThumbnail(Deck myDeck, string opponentsDeck)
    {
        string thumbnailPath = GetThumbnailPath(myDeck, opponentsDeck);
        logger.Trace($"Thumbnail path: {thumbnailPath}");

        if (TEST)
            return true;

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
    private string GetDescription(Deck myDeck, string opponentsDeck)
    {
        StringBuilder sb = new StringBuilder("Gameplay of ")
            .Append(myDeck.deckName).Append(" vs ").Append(opponentsDeck).Append("\n\n")
            .Append("Deck tech: ").Append(myDeck.deckTech).Append("\nDeck list: ").Append(myDeck.deckList);
        return sb.ToString();
    }
    private string GetThumbnailPath(Deck myDeck, string opponentsDeck)
    {
        string thumbnailFolder = myDeck.thumbnailFolder;

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