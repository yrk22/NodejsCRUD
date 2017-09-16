using System.Configuration;


namespace VideoDownloadGenerator
{
    public static class Settings
    {
        public static string AWSAccessKey
        {
            get
            {
                return GetValue("AWSAccessKey");
            }
        }

        public static string AWSSecretKey
        {
            get
            {
                return GetValue("AWSSecretKey");
            }
        }

        public static string VideoLocation
        {
            get
            {
                return GetValue("VideoLocation");
            }
        }

        public static string Original
        {
            get
            {
                return GetValue("original");
            }
        }
        public static string FFMPEG
        {
            get
            {
                return GetValue("ffmpeg");
            }
        }
        public static string FinalPath
        {
            get
            {
                return GetValue("final");
            }
        }
        public static string OriginalLocal
        {
            get
            {
                return GetValue("originallocal");
            }
        }
        public static string FFMPEGLocal
        {
            get
            {
                return GetValue("ffmpeglocal");
            }
        }
        public static string FinalPathLocal
        {
            get
            {
                return GetValue("finallocal");
            }
        }

        public static string VideoBucket
        {
            get
            {
                return GetValue("VideoBucket");
            }
        }

        public static string VideoBucketWedding
        {
            get
            {
                return GetValue("VideoBucketWedding");
            }
        }
        public static string DestinationDirectory
        {
            get
            {
                return GetValue("DestinationDirectory");
            }
        }
        public static string DestinationDirectoryTest
        {
            get
            {
                return GetValue("DestinationDirectoryTest");
            }
        }

        public static string GetValue(string key)
        {
            return ConfigurationManager.AppSettings[key].ToString();
        }
    }
}
