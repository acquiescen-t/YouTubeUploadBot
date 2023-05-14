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

        public static void MoveUploadedFile(string pathToUploadedFile, Deck myDeck, Boolean testMove)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(pathToUploadedFile);

                string destinationFolder = myDeck.uploadedFolder;
                logger.Trace($"destinationFolder: {destinationFolder}");

                string srcPath = fileInfo.FullName;
                logger.Trace($"srcPath: {srcPath} {Environment.NewLine}");

                string dstPath = CreateDestinationPath(myDeck, srcPath, testMove);
                logger.Trace($"dstPath: {dstPath}");

                if (!testMove)
                {
                    File.Delete(srcPath);
                    Directory.Delete(Path.GetDirectoryName(srcPath));

                    logger.Info($"File has been moved from {srcPath} to {dstPath}. {Environment.NewLine}");
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
        }

        public static string CreateDestinationPath(Deck myDeck, string filePath, Boolean testMove)
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

            if (!testMove)
            {
                Directory.CreateDirectory(nextAvailableDirectory);
                logger.Info($"Created directory: {nextAvailableDirectory}");
            }
            else
            {
                logger.Info($"Directory would be created at: {nextAvailableDirectory}");
            }

            nextAvailableDirectory = Path.Combine(nextAvailableDirectory, fileName);
            logger.Trace($"Returning path: {nextAvailableDirectory} {Environment.NewLine}");
            // G:\YouTube\MTG\footage\Grixis Truths\Uploaded\Jund Midrange 02\Jund Midrange.mp4

            return nextAvailableDirectory;
        }
    }
}
