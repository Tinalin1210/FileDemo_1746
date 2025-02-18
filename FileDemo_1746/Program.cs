using System;  // 引入基本的系統命名空間
using System.Collections.Generic;  // 引入集合類型的命名空間，例如 List<T> 和 Dictionary<TKey, TValue>。
using System.IO;  // 引入文件與資料流操作的命名空間，提供讀寫檔案、目錄、流等功能。例如 File 類別用於讀取和寫入檔案。
using Newtonsoft.Json;  // 引入 JSON 處理的命名空間，提供序列化與反序列化 JSON 的功能。
using System.Threading;  // 引入多執行緒相關的命名空間


public class Config
{
    // 存儲目錄路徑與要監控的檔案
    public string DirectoryPath { get; set; }
    public List<string> FilesToMonitor { get; set; }
}

namespace FileDemo_1746
{
    class Program
    {
        // 儲存檔案最後修改時間
        static Dictionary<string, DateTime> fileLastWriteTime = new Dictionary<string, DateTime>();

        // 儲存每個檔案的內容
        static Dictionary<string, List<string>> fileContents = new Dictionary<string, List<string>>();

        // 設定檢查的時間間隔（毫秒）
        static int checkInterval = 2000; // 每2秒檢查一次

        static void Main(string[] args)
        {
            // 先檢查並創建資料夾與檔案
            CheckFolderFiles();

            // 讀取配置檔案 (config.json)，這樣就可以設定監控的目錄和檔案
            string configPath = "config.json";
            Config config = LoadConfig(configPath);

            if (config != null)
            {
                Console.WriteLine("監控目錄: " + config.DirectoryPath);
                Console.WriteLine("監控檔案:");

                // 顯示監控的檔案列表
                foreach (var file in config.FilesToMonitor)
                {
                    Console.WriteLine(" - " + file);

                    // 讀取檔案內容並儲存到變數中
                    string filePath = Path.Combine(config.DirectoryPath, file);
                    if (File.Exists(filePath))
                    {
                        List<string> fileContent = new List<string>(File.ReadAllLines(filePath));
                        fileContents[file] = fileContent;
                        fileLastWriteTime[file] = File.GetLastWriteTime(filePath); // 記錄檔案的初始修改時間
                    }
                    else
                    {
                        Console.WriteLine($"檔案 {file} 不存在！");
                    }
                }

                // 設定定時器，定期檢查檔案變動
                Timer timer = new Timer(CheckFiles, config, 0, checkInterval);

                Console.WriteLine("開始監控檔案變動...");
                Console.WriteLine("按任意鍵結束監控。");

                // 監控檔案變動，直到按下任意鍵結束
                while (true)
                {
                    // 檢查是否有按下任意鍵，若有則結束監控
                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey(); // 讀取鍵盤輸入
                        break;
                    }
                }

                // 停止定時器
                timer.Dispose();
                Console.WriteLine("監控已結束。");
            }
            else
            {
                Console.WriteLine("無法讀取配置檔案。");
            }
        }

        public static Config LoadConfig(string path)
        {
            try
            {
                // 讀取 JSON 檔案並將其反序列化為 Config 物件
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Config>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("讀取配置檔案錯誤: " + ex.Message);
                return null;
            }
        }

        // 定時檢查檔案是否有變動
        public static void CheckFiles(object state)
        {
            var config = (Config)state;

            foreach (var file in config.FilesToMonitor)
            {
                string filePath = Path.Combine(config.DirectoryPath, file);
                if (File.Exists(filePath))
                {
                    DateTime lastWriteTime = File.GetLastWriteTime(filePath);

                    if (lastWriteTime > fileLastWriteTime[file])
                    {
                        // 檔案有變動，處理變動
                        OnFileChanged(file, filePath);
                        fileLastWriteTime[file] = lastWriteTime; // 更新檔案的最後修改時間
                    }
                }
            }
        }

        // 當檔案變動時觸發
        public static void OnFileChanged(string fileName, string filePath)
        {
            Console.WriteLine($"檔案變動 (修改): {filePath}");

            int retryCount = 3;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    // 延遲一段時間再讀取檔案，以確保檔案已經完全更新
                    Thread.Sleep(300);

                    string fileContent = File.ReadAllText(filePath);

                    if (string.IsNullOrWhiteSpace(fileContent))
                    {
                        Console.WriteLine("檔案內容已刪除或清空");

                        fileContents[fileName].Clear();
                    }
                    else
                    {
                        // 顯示檔案的新增內容
                        ShowChanges(fileContent, fileName);

                        // 更新檔案內容
                        fileContents[fileName] = new List<string>(fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
                    }

                    return;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"讀取檔案失敗 ({ex})，等待重試...");
                    Thread.Sleep(500);
                }
            }

            Console.WriteLine($"無法讀取檔案內容: {filePath}");
        }

        // 檢查並創建資料夾與檔案
        public static void CheckFolderFiles()
        {
            string drivePath = @"C:\";  // 確保目錄存在
            string folderName = "FileDemo_1746"; // 你的資料夾名稱
            string folderPath = Path.Combine(drivePath, folderName);

            // 確保資料夾存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine($"資料夾 '{folderName}' 已建立。");
            }

            // 監控的預設檔案
            List<string> defaultFiles = new List<string> { "File1.txt", "File2.txt" };

            foreach (var fileName in defaultFiles)
            {
                string filePath = Path.Combine(folderPath, fileName);
                if (!File.Exists(filePath))
                {
                    using (File.Create(filePath)) { } // 確保釋放資源
                    Console.WriteLine($"檔案 '{fileName}' 已建立。");
                }
            }
        }

        // 顯示新增的檔案內容
        public static void ShowChanges(string currentContent, string fileName)
        {
            // 將當前檔案內容與舊內容
            var currentLines = currentContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var oldLines = fileContents[fileName];

            int maxLength = Math.Max(currentLines.Length, oldLines.Count); // 取較長的長度
            List<string> paddedOldLines = new List<string>(oldLines);

            while (paddedOldLines.Count < maxLength)
            {
                paddedOldLines.Add(string.Empty);
            }

            for (int i = 0; i < maxLength; i++)
            {
                //i 大於等於舊檔案的行數，或者當前行 (currentLines[i]) 和舊行 (paddedOldLines[i]) 不相等，表示該行是新增的或有所變更。
                if (i >= oldLines.Count || currentLines[i] != paddedOldLines[i])
                {
                    if (!string.IsNullOrEmpty(currentLines[i]))
                    {
                        Console.WriteLine($"新增: {currentLines[i]}");
                    }
                }
            }
        }
    }
}
