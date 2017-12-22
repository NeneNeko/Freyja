using Fizzler;
using HtmlAgilityPack;
using Ionic.Zip;
using NeneNeko.NovelTools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NekoExtract
{
    class NekoExtract
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);

        public static string NewLine = Environment.NewLine;
        private static string Base_directory = AppDomain.CurrentDomain.BaseDirectory;
        private static Dictionary<string, string> App_config = new Dictionary<string, string>();
        private static Dictionary<string, string> Extract_config = new Dictionary<string, string>();
        private static Dictionary<string, string> File_list = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            AppConfig.Change(Path.Combine(Base_directory, "config/nekoextract_config.xml"));
            App_config = ConfigurationManager.AppSettings.AllKeys
                .ToDictionary(key => key, value => ConfigurationManager.AppSettings[value].Replace("{AppPath}", Base_directory));

            string config_file = "nekoextract.xml";
            if (args.Length >= 1)
            {
                config_file = args[0];
            }
            AppConfig.Change(Path.Combine(App_config["extract_config_path"], config_file));
            Extract_config = ConfigurationManager.AppSettings.AllKeys
                .ToDictionary(key => key, value => ConfigurationManager.AppSettings[value].Replace("{AppPath}", Base_directory));

            var process = Process.GetCurrentProcess();
            SetWindowText(process.MainWindowHandle, "NekoExtract - " + Extract_config["name"]);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("|" + Extract_config["name"] + "| |2| |Text|");
            Console.ResetColor();
            Console.WriteLine("+-+ +-+ +-+ +-+ +-+ +-+ +-+ +-+");

            if (args.Length >= 2)
            {
                foreach (var arg in args)
                {
                    if (Directory.Exists(arg) || File.Exists(arg))
                    {
                        FileAttributes attr = File.GetAttributes(arg);
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            foreach (string file in Directory.EnumerateFiles(arg, "*.*", SearchOption.AllDirectories))
                            {
                                GetText(file);
                            }
                        }
                        else
                        {
                            GetText(arg);
                        }
                    }
                }
                if (Extract_config["backup_name"] != "false")
                {
                    Console.Write("Create backup raw file...");
                    ZipFile zip = new ZipFile();
                    Directory.CreateDirectory(Path.Combine(App_config["backup_raw_path"]));
                    Extract_config["backup_name"] = Path.Combine(App_config["backup_raw_path"], Extract_config["backup_name"]);
                    if (File.Exists(Extract_config["backup_name"]))
                    {
                        zip = ZipFile.Read(Extract_config["backup_name"]);
                    }
                    foreach (KeyValuePair<string, string> file in File_list)
                    {
                        ZipEntry e = zip[file.Key];
                        if (e != null)
                        {
                            zip.RemoveEntry(file.Key);
                        }
                        zip.AddFile(file.Value, "").FileName = file.Key;
                    }
                    zip.Save(Extract_config["backup_name"]);
                    Console.Write("Done");
                    Console.WriteLine();
                }
                if (Extract_config["delete_file"] == "true")
                {
                    foreach (KeyValuePair<string, string> file in File_list)
                    {
                        File.Delete(file.Value);
                    }
                }
                if (Extract_config["pause"] == "true")
                {
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine("No input file");
                Console.ReadKey();
            }
        }

        static void GetText(string file)
        {
            string base_fullname = Path.GetDirectoryName(file);
            string file_fullname = Path.GetFileName(file);
            string file_extension = Path.GetExtension(file);
            string[] extensions = Extract_config["extension"].Split(new string[] { ", " }, StringSplitOptions.None);

            bool accept = false;
            foreach (string extension in extensions)
            {
                string ext = "." + extension.Trim();
                if (ext == file_extension.ToLower())
                {
                    accept = true;
                    break;
                }
            }

            if (accept)
            {
                string html = File.ReadAllText(file);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                if (Extract_config["save_path"] != "false")
                {
                    base_fullname = Extract_config["save_path"];
                }
                try
                {
                    if (doc.DocumentNode.SelectSingleNode(Extract_config["xpath_title"]) != null)
                    {
                        string title = doc.DocumentNode.SelectSingleNode(Extract_config["xpath_title"]).InnerText;
                        string link = doc.DocumentNode.SelectSingleNode(Extract_config["xpath_url"])
                            .Attributes[Extract_config["attribute_url"]].Value;
                        //string raw_contents = doc.DocumentNode.SelectSingleNode(Extract_config["xpath_content"]).InnerHtml;
                        var raw_html = doc.DocumentNode.SelectNodes(Extract_config["xpath_content"]);
                        
                        var buffer = new StringBuilder();
                        foreach (var line_content in raw_html)
                        {
                            buffer.Append(line_content.InnerHtml + NewLine);
                        }
                        string raw_contents = buffer.ToString();
                        title = WebUtility.HtmlDecode(title);
                        Match chapter = Regex.Match(title, Extract_config["regex_title"]);

                        string chapter_number = "0";
                        if (chapter.Success)
                        {
                            chapter_number = Extract_config["regex_result"].Replace("$1", chapter.Groups[1].Value.PadLeft(3, '0'));
                            if (chapter.Groups[2].Length != 0)
                            {
                                chapter_number = chapter_number.Replace("$2", chapter.Groups[2].Value);
                            }
                            else
                            {
                                chapter_number = Regex.Replace(chapter_number, @"[\.|-]?\$2", "");
                            }
                            if (chapter.Groups[3].Length != 0)
                            {
                                chapter_number = chapter_number.Replace("$3", chapter.Groups[3].Value);
                            }
                            else
                            {
                                chapter_number = Regex.Replace(chapter_number, @"[\.|-]?\$3", "");
                            }
                            chapter_number = chapter_number.Trim();
                        }
                        else
                        {
                            chapter = Regex.Match(title, @"(\d+)");
                            if (chapter.Success)
                            {
                                chapter_number = chapter.Groups[1].Value;
                            }
                        }

                        File_list.Add(chapter_number + ".html", file);

                        if (Extract_config["remove_newline"] == "true")
                        {
                            raw_contents = raw_contents.Replace("\n", "<br>");
                        }
                        raw_contents = raw_contents.ConvertHtml();
                        if (Extract_config["fix_url"] != "false")
                        {
                            link = Extract_config["fix_url"].Replace("{URL}", link).Replace("{CHAPTER}", chapter_number);
                        }
                        if (Extract_config["add_title"] == "true")
                        {
                            raw_contents = link + NewLine + title + NewLine + raw_contents;
                        }
                        else
                        {
                            raw_contents = link + NewLine + raw_contents;
                        }
                        string contents = raw_contents.CleanUp();
                        string filename = chapter_number + ".txt";
                        string file_save = Path.Combine(base_fullname, filename);
                        if (File.Exists(file_save))
                        {
                            File.Delete(Path.Combine(base_fullname, "backup_" + filename));
                            File.Move(file_save, Path.Combine(base_fullname, "backup_" + filename));
                        }
                        File.WriteAllText(file_save, contents);

                        long length = Encoding.UTF8.GetByteCount(contents) - 4;
                        Console.Write("> " + file_fullname + " -> ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(filename);
                        Console.ResetColor();
                        Console.Write(" -> ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(length.BytesToString());
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.Write("> " + file_fullname + " -> ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("Can't read html data");
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Helper.ErrorLogging(ex);
                }
            }
        }
    }

}