using System;  // 引入基本的系統命名空間
using System.Collections.Generic;  // 引入集合類型的命名空間，例如 List<T> 和 Dictionary<TKey, TValue>。
using System.IO;  // 引入文件與資料流操作的命名空間，提供讀寫檔案、目錄、流等功能。例如 File 類別用於讀取和寫入檔案。
using Newtonsoft.Json;  // 引入 JSON 處理的命名空間，提供序列化與反序列化 JSON 的功能。
using System.Threading;  // 引入多執行緒相關的命名空間


public class Config
{
    
    public string DirectoryPath { get; set; }          //用來儲存要監控的目錄路徑。
    public List<string> FilesToMonitor { get; set; }  // 用來儲存需要監控的檔案名稱列表。
}

namespace FileDemo_1746
{
    class Program
    {
        // 儲存上次檔案內容的變數
        static string lastFileContent = string.Empty;

        // 儲存每個檔案的內容
        //其中 string ，代表檔案的名稱 而 List<string>，代表檔案的內容
        static Dictionary<string, List<string>> fileContents = new Dictionary<string, List<string>>();

        static void Main(string[] args)
        {
            // 先檢查並創建資料夾與檔案
            CheckFolderFiles();

            //讀取並反序列化配置檔案（config.json）。該方法會將 JSON 格式的配置檔案轉換為 Config 類型的物件
            string configPath = "config.json";
            Config config = LoadConfig(configPath);

            if (config != null)
            {
                Console.WriteLine("監控目錄: " + config.DirectoryPath);
                Console.WriteLine("監控檔案:");

                // 顯示監控的檔案列表
                foreach (var file in config.FilesToMonitor) //列表中的每個檔案名稱。這些檔案是需要被監控的檔案。
                {
                    Console.WriteLine(" - " + file);

                    // 組合目錄路徑與檔案名稱
                    string filePath = Path.Combine(config.DirectoryPath, file);
                    if (File.Exists(filePath))
                    {
                        //檔案存在，就讀取它的內容
                        List<string> fileContent = new List<string>(File.ReadAllLines(filePath));
                        fileContents[file] = fileContent;  //存入fileContents字典中
                    }
                    else
                    {
                        Console.WriteLine($"檔案 {file} 不存在！");
                    }
                }

                //創建一個 FileSystemWatcher 物件，並設定它監控指定的目錄
                FileSystemWatcher watcher = new FileSystemWatcher(config.DirectoryPath)
                {
                    // 監控檔案的更動、檔名變動、檔案大小變動
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                };

                // 來記錄最後一次處理檔案變動的時間，避免重複處理已經處理過的檔案變動。
                DateTime lastProcessedTime = DateTime.MinValue;

                //為 FileSystemWatcher 的 Changed 事件添加一個事件處理器，當檔案發生變動時，該事件處理器會被觸發。
                watcher.Changed += (sender, e) =>
                {
                    // 取得檔案最後寫入的時間
                    //這樣可以確定檔案在上次處理後是否已經被修改。
                    DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);

                    if (lastWriteTime > lastProcessedTime)
                    {
                        OnFileChanged(e);
                        lastProcessedTime = lastWriteTime;
                    }
                };

                watcher.EnableRaisingEvents = true; // 啟動檔案監控

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

                // 停止檔案監控
                watcher.EnableRaisingEvents = false;
                Console.WriteLine("監控已結束。");
            }
            else
            {
                Console.WriteLine("無法讀取配置檔案。");
            }
        }

        //反序列化
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
                Console.WriteLine("讀取配置檔案錯誤: " + ex);
                return null;
            }
        }


        // 當檔案變動時觸發  
        public static void OnFileChanged(FileSystemEventArgs e)
        {
            //e 檔案路徑
            Console.WriteLine($"檔案變動 (修改): {e.FullPath}");

            int retryCount = 3;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    // 延遲一段時間再讀取檔案，以確保檔案已經完全更新
                    Thread.Sleep(300);

                    string fileContent = File.ReadAllText(e.FullPath);

                    if (string.IsNullOrWhiteSpace(fileContent))  //檢查檔案內容是否為空
                    {
                        Console.WriteLine("檔案內容已刪除或清空");

                        lastFileContent = string.Empty;  //清空之前儲存的檔案內容
                        if (fileContents.ContainsKey(e.Name)) //檢查是否存在對應檔案名有資料
                        {
                            fileContents[e.Name].Clear();
                        }
                    }
                    else
                    {
                        // 顯示檔案的新增內容
                        ShowChanges(fileContent, e.Name);

                        // 最新內容存儲到fileContents中，方便後續的比對和顯示變更。
                        fileContents[e.Name] = new List<string>(fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None));
                    }

                    return;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"讀取檔案失敗 ({ex})，等待重試...");
                    Thread.Sleep(500);
                }
            }

            Console.WriteLine($"無法讀取檔案內容: {e.FullPath}");
        }

        // 檢查並創建資料夾與檔案
        public static void CheckFolderFiles()
        {
            string drivePath = @"C:\";
            string folderName = "FileDemo_1746"; // 資料夾名稱
            string folderPath = Path.Combine(drivePath, folderName);  //組合在一起完整的資料夾路徑

            // 確保資料夾存在
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);  //創建該資料夾
                Console.WriteLine($"資料夾 '{folderName}' 已建立。");
            }

            // 監控的預設檔案
            List<string> defaultFiles = new List<string> { "File1.txt", "File2.txt" }; //預設檔案名稱的列表

            foreach (var fileName in defaultFiles)  //逐一檢查每個檔案
            {
                string filePath = Path.Combine(folderPath, fileName);  //檔案的完整路徑
                if (!File.Exists(filePath)) //檢查檔案室否存在
                {
                    //使用 File.Create(filePath) 創建檔案，並且 using 語法確保檔案創建後會正確釋放資源。
                    //using 是用來確保一個物件在不再需要時能夠自動釋放資源。
                    using (File.Create(filePath)) { } 
                    Console.WriteLine($"檔案 '{fileName}' 已建立。");
                }
            }
        }

        // 顯示新增的檔案內容
        public static void ShowChanges(string currentContent, string fileName)
        {
            // 將當前檔案內容與舊內容
            var currentLines = currentContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);  //儲存為一個字串陣列
            var oldLines = fileContents[fileName];   //取出先前存儲的檔案內容。

            int maxLength = Math.Max(currentLines.Length, oldLines.Count); // 取較長的長度
            List<string> paddedOldLines = new List<string>(oldLines); //創建一個新的列表 paddedOldLines，並將 oldLines 中的所有內容複製過來。

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
