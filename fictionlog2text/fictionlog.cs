using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NeneNeko.NovelTools;

namespace fictionlog
{
    class fictionlog
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowText(IntPtr hWnd, string text);

        static void Main(string[] args)
        {
            var process = Process.GetCurrentProcess();
            SetWindowText(process.MainWindowHandle, "fictionlog2text");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("|Fictionlog| |2| |Text|");
            Console.ResetColor();
            Console.WriteLine("----------------------------------------");
            if (args.Any())
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
                    else
                    {
                        Console.WriteLine("No file or folder");
                    }
                }
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine("No input file");
                Console.ReadKey();
            }
        }

        static void GetText(string file)
        {
            string base_directory = AppDomain.CurrentDomain.BaseDirectory;
            string base_fullname = Path.GetDirectoryName(file) + "\\";
            string file_fullname = Path.GetFileName(file);
            string file_extension = Path.GetExtension(file);
            if (file_extension == ".htm" || file_extension == ".html")
            {
                string html = File.ReadAllText(file);
                Match match = Regex.Match(html, @"<script.*>window.__data=(.*);<\/script>");
                if (match.Success)
                {
                    try
                    {
                        string json_string = match.Result("$1");
                        dynamic json = JsonConvert.DeserializeObject(json_string);
                        string name = json.storyDetailData.bookData.title;
                        string link = "https://fictionlog.co" + json.routing.locationBeforeTransitions.pathname;
                        string title = json.storyDetailData.chapterData.title;
                        string story = json.storyDetailData.chapterData.content;
                        Match chapter = Regex.Match(title, @"[Chaptor|ตอนที่|บทที่|บทนำ]\s{0,}?(\d+):?");
                        string chapter_number = json.storyDetailData.chapterData.title;
                        if (chapter.Success)
                        {
                            chapter_number = chapter.Result("$1");
                        }
                        string filename = chapter_number.Trim().PadLeft(3, '0') + ".txt";
                        string raw_contents = HtmlToText.ConvertHtml(story.Replace("\n\n", "<br>")).Trim();
                        string contents = NovelTools.CleanUp(link + Environment.NewLine + title + Environment.NewLine + raw_contents);
                        File.WriteAllText(base_fullname + filename, contents);
                        File.Delete(file);

                        long length = Encoding.UTF8.GetByteCount(contents) - 4;
                        Console.Write("> " + file_fullname + " -> ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(name.Trim() + " " + title.Trim());
                        Console.ResetColor();
                        Console.Write(" -> ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(filename + "(" + Helper.BytesToString(length) + ")");
                        Console.WriteLine();
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Helper.ErrorLogging(ex);
                    }
                }
                else
                {
                    Console.Write("> " + file_fullname + " -> ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("Can't read json data");
                    Console.WriteLine();
                    Console.ResetColor();
                }
            }
            else
            {
                Console.Write("> " + file_fullname + " -> ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("File not support");
                Console.WriteLine();
                Console.ResetColor();
            }
        }

    }
}
