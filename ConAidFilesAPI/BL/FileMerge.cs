using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace ConAidFilesAPI.BL
{
    public class FileMerge
    {
        public string FileName { get; set; }
        public string TempFolder { get; set; }
        public int MaxFileSizeMB { get; set; }
        public List<String> FileParts { get; set; }

        public FileMerge()
        {
            FileParts = new List<string>();
        }

        /// <summary>
        /// original name + ".part_N.X" (N = file part number, X = total files)
        /// Objective = enumerate files in folder, look for all matching parts of split file. If found, merge and return true.
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        public bool MergeFile(string FileName)
        {
            bool result = false;
            // parse out the different tokens from the filename according to the convention 
            // Convention is FileName (with extension) +".part_N.X" (N = file part number, X = total number of parts)
            string partToken = ".part_";

            // Creates a substring that is equal to the filename by starting at 0 and going until it reaches ".part_"
            string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));

            // Creates a substring of everything after ".part_"
            string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);

            int FileIndex = 0;
            int TotalFileCount = 0;

            // Extracts the number in the trailingTokens string that represent the fule part number
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);

            // Extracts the number in the trailingTokens string that represents the total number of file parts
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out TotalFileCount);

            // get a list of all file parts in the temp folder
            string Searchpattern = Path.GetFileName(baseFileName) + partToken + "*";
            string[] FilesList = Directory.GetFiles(Path.GetDirectoryName(FileName), Searchpattern);
            //  merge .. improvement would be to confirm individual parts are there / correctly in sequence, a security check would also be important
            // only proceed if we have received all the file chunks
            if (FilesList.Count() == TotalFileCount)
            {
                // use a singleton to stop overlapping processes
                if (!MergeFileManager.Instance.InUse(baseFileName))
                {
                    MergeFileManager.Instance.AddFile(baseFileName);
                    if (File.Exists(baseFileName))
                    {
                        File.Delete(baseFileName);
                    }
                    // add each file located to a list so we can get them into 
                    // the correct order for rebuilding the file
                    List<SortedFile> MergeList = new List<SortedFile>();
                    foreach (string File in FilesList)
                    {
                        SortedFile sFile = new SortedFile();
                        sFile.FileName = File;
                        baseFileName = File.Substring(0, File.IndexOf(partToken));
                        trailingTokens = File.Substring(File.IndexOf(partToken) + partToken.Length);
                        int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
                        sFile.FileOrder = FileIndex;
                        MergeList.Add(sFile);
                    }
                    // sort by the file-part number to ensure we merge back in the correct order
                    var MergeOrder = MergeList.OrderBy(s => s.FileOrder).ToList();
                    using (FileStream FS = new FileStream(baseFileName, FileMode.Create))
                    {
                        // merge each file chunk back into one contiguous file stream
                        foreach (var chunk in MergeOrder)
                        {
                            try
                            {
                                using (FileStream fileChunk = new FileStream(chunk.FileName, FileMode.Open))
                                {
                                    fileChunk.CopyTo(FS);
                                }
                            }
                            catch (IOException ex)
                            {
                                throw ex;                               
                            }
                        }
                    }
                    result = true;
                    // unlock the file from singleton
                    MergeFileManager.Instance.RemoveFile(baseFileName);
                }
            }
            return result;
        }
    }
    public struct SortedFile
    {
        public int FileOrder { get; set; }
        public String FileName { get; set; }
    }
}