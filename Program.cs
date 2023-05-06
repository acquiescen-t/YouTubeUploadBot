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
    static string PATH_TO_SECRETS = "../../../secret";

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

    private async Task BatchUploadVideos()
    {
        #region Initialisation
        var settings = Initialise();
        UserCredential cred = await GetCredentials();
        YouTubeService youTubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = cred,
            ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
        });

        #endregion
        await UploadVideos(settings, youTubeService);
    }
    private async Task UploadVideos(RootObject settings, YouTubeService youTubeService)
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

            var pattern = @"(.*?) vs (.*)";
            var match = Regex.Match(folderName, pattern);
            string myDeck = match.Groups[1].Value.Trim();
            string opponentsDeck = match.Groups[2].Value.Trim();

            string title = myDeck + " vs " + opponentsDeck + " | " + settings.programSettings.rank + " | MTG Historic";
            string description = GetDescription(myDeck, opponentsDeck, settings);
            logger.Info($"Title: {title}");
            logger.Info($"Description: {description}");
            #endregion

            Video video = SetupVideo(title, description, settings);
            bool videoSuccess = await UploadVideo(youTubeService, filePath, video);
            bool thumbnailSuccess = await UploadThumbnail(youTubeService, myDeck, opponentsDeck, settings);
            if (videoSuccess && thumbnailSuccess)
            {
                logger.Info($"Video uploaded successsfully and set to go live at {String.Format("{0:f}", settings.programSettings.nextUploadDateTime)}");
                MoveUploadedFile(filePath, myDeck);
            }
            uploadCount++;
        }
        using (StreamWriter sw = File.CreateText(Path.Combine(PATH_TO_SECRETS, "settings.json")))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(sw, settings);
        }
    }



    // My Stuff: Folder manipulation within computer, updating nextUploadDateTime
    private string CreateDestinationPath(string path)
    {
        FileInfo destinationPath = new FileInfo(path);
        logger.Trace($"Received parameter: {path}");
        // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange.mp4

        string fileName = Path.GetFileName(path);
        logger.Trace($"fileName: {fileName}");
        // Jund Midrange.mp4

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        logger.Trace($"fileNameWithoutExtension: {fileNameWithoutExtension}");
        // Jund Midrange

        int start = 1;
        string containingFolder = Directory.GetParent(path).FullName;
        // G:\YouTube\MTG\footage\Grixis Truths\Uploaded

        string nextAvailableDirectory = Path.Combine(containingFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
        logger.Info($"Checking availability of: {nextAvailableDirectory}...");
        // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 01

        while (Directory.Exists(nextAvailableDirectory))
        {
            logger.Trace($"Folder {nextAvailableDirectory} already exists!");
            start++;
            nextAvailableDirectory = Path.Combine(containingFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
            // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 02

            logger.Trace($"Trying {nextAvailableDirectory}...");
        }
        Directory.CreateDirectory(nextAvailableDirectory);
        logger.Info($"Created directory: {nextAvailableDirectory}");

        nextAvailableDirectory = Path.Combine(nextAvailableDirectory, fileName);
        logger.Trace($"Returning path: {nextAvailableDirectory}");
        // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 02\Jund Midrange.mp4

        return nextAvailableDirectory;
    }
    private void MoveUploadedFile(string filePath, string myDeck)
    {
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            string destinationFolder = String.Empty;

            // src: "G:\YouTube\MTG\footage\Upload From Here\01 Grixis Truths vs Jund Midrange\Jund Midrange.mp4"
            // dst: "G:\YouTube\MTG\footage\Grixis Truths\Uploaded"

            switch (myDeck)
            {
                case "Jeskai Truths":
                    destinationFolder = @"G:\YouTube\MTG\footage\Jeskai Truths\Uploaded";
                    break;
                case "Grixis Truths":
                    destinationFolder = @"G:\YouTube\MTG\footage\Grixis Truths\Uploaded";
                    break;
                case "Azorius Truths":
                    destinationFolder = @"G:\YouTube\MTG\footage\Azorius Truths\Uploaded";
                    break;
                default:
                    destinationFolder = @"G:\YouTube\MTG\footage\Put Uploaded Videos Here";
                    break;
            }

            string srcPath = fileInfo.FullName;
            logger.Trace($"srcPath: {srcPath}");
            string dstPath = Path.Combine(destinationFolder, fileInfo.Name);
            logger.Trace($"dstPath: {dstPath}");

            dstPath = CreateDestinationPath(dstPath);
            File.Copy(srcPath, dstPath, false);

            File.Delete(srcPath);
            Directory.Delete(Path.GetDirectoryName(srcPath));

            logger.Info($"File has been moved from {srcPath} to {dstPath}. {Environment.NewLine}");
        }
        catch (Exception e)
        {
            logger.Error(e);
        }
    }

    // YouTube stuff: setting up of credentials and tokens, monitoring upload progress
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
            logger.Info($"deckList:             {rootObject.jeskaiSettings.deckList}");
            logger.Info($"deckTech:             {rootObject.jeskaiSettings.deckTech}{Environment.NewLine}");

            logger.Info("--GRIXIS SETTINGS--");
            logger.Info($"uploadedFolder:       {rootObject.grixisSettings.uploadedFolder}");
            logger.Info($"thumbnailFolder:      {rootObject.grixisSettings.thumbnailFolder}");
            logger.Info($"deckList:             {rootObject.grixisSettings.deckList}");
            logger.Info($"deckTech:             {rootObject.grixisSettings.deckTech}{Environment.NewLine}");
            return rootObject;
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
    private Video SetupVideo(string title, string description, RootObject settings)
    {
        var video = new Video();

        video.Snippet = new VideoSnippet();
        video.Snippet.Title = title;
        video.Snippet.Description = description;

        video.Status = new VideoStatus();
        video.Status.MadeForKids = false;
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
    private async Task<bool> UploadVideo(YouTubeService youtubeService, string filePath, Video video)
    {
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                logger.Trace("Uploading video...");
                var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet, status", fileStream, "video/*");
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
    private async Task<bool> UploadThumbnail(YouTubeService youtubeService, string myDeck, string opponentsDeck, RootObject settings)
    {
        string thumbnailPath = GetThumbnailPath(myDeck, opponentsDeck, settings);
        if (!String.IsNullOrEmpty(thumbnailPath))
        {
            try
            {
                using (var thumbnailFileStream = new FileStream(thumbnailPath, FileMode.Open))
                {
                    logger.Trace("Uploading thumbnail...");
                    var thumbnailInsertRequest = youtubeService.Thumbnails.Set(videoID, thumbnailFileStream, "image/png");
                    thumbnailInsertRequest.ProgressChanged += ProgressChanged;
                    await thumbnailInsertRequest.UploadAsync();
                }
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
                return false;
            }
        }
        return true;
    }

    // Getting information about video
    private string GetDescription(string myDeck, string opponentsDeck, RootObject settings)
    {
        StringBuilder sb = new StringBuilder("Gameplay of ")
            .Append(myDeck).Append(" vs ").Append(opponentsDeck).Append("\n\n");

        switch(myDeck)
        {
            case "Jeskai Truths":
                sb.Append("Deck tech: ").Append(settings.jeskaiSettings.deckTech).Append("\nDeck list: ").Append(settings.jeskaiSettings.deckList);
                break;
            case "Grixis Truths":
                sb.Append("Deck tech: ").Append(settings.grixisSettings.deckTech).Append("\nDeck list: ").Append(settings.grixisSettings.deckList);
                break;
        }
        return sb.ToString();
    }
    private string GetThumbnailPath(string myDeck, string opponentsDeck, RootObject settings)
    {
        string thumbnailFolder = String.Empty;
        switch(myDeck)
        {
            case "Jeskai Truths":
                thumbnailFolder = settings.jeskaiSettings.thumbnailFolder;
                break;
            case "Grixis Truths":
                thumbnailFolder = settings.grixisSettings.thumbnailFolder;
                break;
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