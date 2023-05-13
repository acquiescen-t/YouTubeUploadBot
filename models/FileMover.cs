using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeUploadBot.models
{
    public class FileMover
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        // TODO: GetDestinationFolder is called twice??

        public static void MoveUploadedFileNew(Settings settings, string pathToUploadedFile, Deck myDeck)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(pathToUploadedFile);

                string destinationFolder = myDeck.uploadedFolder;

                string srcPath = fileInfo.FullName;
                logger.Trace($"srcPath: {srcPath}");

                string dstPath = CreateDestinationPathNew(settings, myDeck, srcPath);
                logger.Trace($"dstPath: {dstPath}");

                File.Delete(srcPath);
                Directory.Delete(Path.GetDirectoryName(srcPath));

                logger.Info($"File has been moved from {srcPath} to {dstPath}. {Environment.NewLine}");
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        public static string CreateDestinationPathNew(Settings settings, Deck myDeck, string filePath)
        {
            logger.Trace($"deckName:            {myDeck.deckName}");
            logger.Trace($"filePath:            {filePath}");
            logger.Trace($"destinationFolder:   {myDeck.uploadedFolder}");

            string fileName = Path.GetFileName(filePath);
            logger.Trace($"fileName: {fileName}");

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            logger.Trace($"fileNameWithoutExtension: {fileNameWithoutExtension}");

            int start = 1;

            string nextAvailableDirectory = Path.Combine(myDeck.uploadedFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
            logger.Info($"Checking availability of: {nextAvailableDirectory}...");
            // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 01

            while (Directory.Exists(nextAvailableDirectory))
            {
                logger.Trace($"Folder {nextAvailableDirectory} already exists!");
                start++;
                nextAvailableDirectory = Path.Combine(myDeck.uploadedFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
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

        public static void MoveUploadedFile(Settings settings, string pathToUploadedFile, string myDeck)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(pathToUploadedFile);

                string destinationFolder = GetDestinationFolder(settings, myDeck);
                logger.Trace($"destinationFolder: {destinationFolder}");

                string srcPath = fileInfo.FullName;
                logger.Trace($"srcPath: {srcPath}");

                string dstPath = CreateDestinationPath(settings, myDeck, srcPath);
                logger.Trace($"dstPath: {dstPath}");

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

        // receives: "G:\YouTube\MTG\footage\Upload From Here\06 Jeskai Truths vs Izzet Wizards\Izzet Wizards.mp4"
        // returns:  "G:\YouTube\MTG\footage\Jeskai Truths\Uploaded\Izzet Wizards 01"
        public static string CreateDestinationPath(Settings settings, string myDeck, string filePath)
        {
            logger.Trace($"myDeck:      {myDeck}");
            logger.Trace($"filePath:    {filePath}");
            
            string destinationFolder = GetDestinationFolder(settings, myDeck);
            logger.Trace($"destinationFolder: {destinationFolder}");

            string fileName = Path.GetFileName(filePath);
            logger.Trace($"fileName: {fileName}");

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            logger.Trace($"fileNameWithoutExtension: {fileNameWithoutExtension}");

            int start = 1;

            string nextAvailableDirectory = Path.Combine(destinationFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
            logger.Info($"Checking availability of: {nextAvailableDirectory}...");
            // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 01

            while (Directory.Exists(nextAvailableDirectory))
            {
                logger.Trace($"Folder {nextAvailableDirectory} already exists!");
                start++;
                nextAvailableDirectory = Path.Combine(destinationFolder, fileNameWithoutExtension + " " + start.ToString("D2"));
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

        public static string GetDestinationFolder(Settings settings, string myDeck)
        {
            switch(myDeck)
            {
                case "Jeskai Truths":
                    return settings.jeskaiTruths.uploadedFolder;
                case "Grixis Truths":
                    return settings.grixisTruths.uploadedFolder;
                case "Bant Yorion":
                    return settings.bantYorion.uploadedFolder;
                default:
                    logger.Warn($"{myDeck} has no settings configured!");
                    return String.Empty;
            }
        }
    }
}
