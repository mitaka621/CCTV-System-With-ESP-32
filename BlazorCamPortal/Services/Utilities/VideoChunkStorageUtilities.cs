namespace CamPortal.Core.Utilities
{
    public static class VideoChunkStorageUtilities
    {
        private const string ChunkStartTimeMarker = "_=";
        private const int DayFolderNameLength = 10;

        public static string GetChunkDayFolder(string fileName)
        {
            var start = fileName.IndexOf(ChunkStartTimeMarker, StringComparison.Ordinal) + ChunkStartTimeMarker.Length;

            return fileName.Substring(start, DayFolderNameLength);
        }

        public static bool TryGetChunkDayFolder(string fileName, out string dayFolder)
        {
            dayFolder = string.Empty;

            var markerIndex = fileName.IndexOf(ChunkStartTimeMarker, StringComparison.Ordinal);
            var start = markerIndex + ChunkStartTimeMarker.Length;

            if (markerIndex < 0 || fileName.Length < start + DayFolderNameLength)
            {
                return false;
            }

            dayFolder = fileName.Substring(start, DayFolderNameLength);

            return VideoChunkUtilities.TryParseChunkDayFolderName(dayFolder, out _);
        }
    }
}
