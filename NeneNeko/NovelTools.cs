using HtmlAgilityPack;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NeneNeko.NovelTools
{

    public static class NovelTools
    {

        private static string NewLine = Environment.NewLine;
        private static string Split_by = "##";
        private static string Base_directory = AppDomain.CurrentDomain.BaseDirectory;

        public static String CleanUp(this string raw_contents)
        {
            // config
            string[] line_regex = File.ReadLines(Path.Combine(Base_directory, "config/noveltools_regex.txt")).ToArray();
            line_regex = line_regex.Where(x => !string.IsNullOrEmpty(x.Trim())).ToArray(); // cleanup empty line
            string[] line_regex_replace = line_regex.Where(x => x.Substring(0, 1) != "@")
                    .Where(x => x.Substring(0, 1) != "|").Where(x => x.Substring(0, 1) != "!")
                    .Select(x => x + Split_by).ToArray();
            string[] title_regex_replace = line_regex.Where(x => x.Substring(0, 1) == "@")
                    .Select(x=>x.Substring(1) + Split_by).ToArray();
            string[] line_regex_break = line_regex.Where(x => x.Substring(0, 1) == "|")
                    .Select(x => x.Substring(1)).ToArray();

             // remove white space and remove empty line
            string[] content_lines = raw_contents.Explode("\n");
            content_lines = content_lines.Select(x => x.Trim()).ToArray();
            content_lines = content_lines.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            // split content line by line
            string host_name = "common";
            if (content_lines[0].Substring(0, 4) == "http")
            {
                host_name = content_lines[0].GetHostName();
            }

            // regex replace line by line
           foreach (string regex_replace in line_regex_replace)
            {
                string[] match_replace = regex_replace.Explode(Split_by);
                if (match_replace[0].Trim() == host_name || match_replace[0] == "common")
                {
                    match_replace[2] = match_replace[2].Replace("newline", NewLine + NewLine);
                    content_lines = content_lines.Select(x => Regex.Replace(x, match_replace[1], match_replace[2])).ToArray();
                }
            }

            // remove white space and remove empty line
            content_lines = content_lines.Select(x => x.Trim()).ToArray();
            content_lines = content_lines.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            var contents = new StringBuilder();
            Action process_line = delegate
            {
                foreach (var content_line in ForEachHelper.WithIndex(content_lines))
                {
                    if (content_line.Index == 0) // url section on line 1
                    {
                        contents.Append(Uri.UnescapeDataString(content_line.Value).Trim() + NewLine);
                    }
                    else if (content_line.Index == 1) // title section on line 2
                    {
                        foreach (string title_replace in title_regex_replace)
                        {
                            string[] match_replace = title_replace.Explode(Split_by);
                            if (match_replace[0].Trim() == host_name || match_replace[0] == "common")
                            {
                                content_line.Value = Regex.Replace(content_line.Value, match_replace[1], match_replace[2]);
                            }
                        }
                        contents.Append(content_line.Value.Trim() + NewLine);
                    }
                    else  // content section on line 3 to end
                    {
                        // check line regex break match on content
                        if (content_line.Value == content_lines[1]) continue; // remove duplicate titles
                        foreach (string regex_break in line_regex_break)
                        {
                            string[] match_break = regex_break.Explode(Split_by);
                            if (match_break[0].Trim() == host_name || match_break[0] == "common")
                            {
                                Match match = Regex.Match(content_line.Value, match_break[1]);
                                if (match.Success) return;
                            }
                        }
                        // fix url on content
                        Match inline_uri = Regex.Match(content_line.Value, @"http\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(/\S*)?$");
                        if (inline_uri.Success)
                        {
                            content_line.Value = content_line.Value.Replace(inline_uri.Value, Uri.UnescapeDataString(inline_uri.Value));
                        }
                        contents.Append(content_line.Value.Trim() + NewLine);
                    }
                    if (content_line.Index >= 1)
                    {
                        contents.Append(NewLine);
                    }
                }
            };
            process_line();
            return contents.ToString().Trim();
        }

    }

    public static class HtmlToText
    {
        // stackoverflow.com/questions/731649/how-can-i-convert-html-to-text-in-c#25178738
        public static string Convert(this string path)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.Load(path);
            return ConvertDoc(doc);
        }

        public static string ConvertHtml(this string html)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html.Replace("\n", "  "));
            return ConvertDoc(doc);
        }

        public static string ConvertDoc(this HtmlDocument doc)
        {
            using (StringWriter sw = new StringWriter())
            {
                ConvertTo(doc.DocumentNode, sw);
                sw.Flush();
                return sw.ToString();
            }
        }

        internal static void ConvertContentTo(HtmlNode node, TextWriter outText, PreceedingDomTextInfo textInfo)
        {
            foreach (HtmlNode subnode in node.ChildNodes)
            {
                ConvertTo(subnode, outText, textInfo);
            }
        }

        public static void ConvertTo(HtmlNode node, TextWriter outText)
        {
            ConvertTo(node, outText, new PreceedingDomTextInfo(false));
        }

        internal static void ConvertTo(HtmlNode node, TextWriter outText, PreceedingDomTextInfo textInfo)
        {
            string html;
            switch (node.NodeType)
            {
                case HtmlNodeType.Comment:
                    // don't output comments
                    break;
                case HtmlNodeType.Document:
                    ConvertContentTo(node, outText, textInfo);
                    break;
                case HtmlNodeType.Text:
                    // script and style must not be output
                    string parentName = node.ParentNode.Name;
                    if ((parentName == "script") || (parentName == "style"))
                    {
                        break;
                    }
                    // get text
                    html = ((HtmlTextNode)node).Text;
                    // is it in fact a special closing node output as text?
                    if (HtmlNode.IsOverlappedClosingElement(html))
                    {
                        break;
                    }
                    // check the text is meaningful and not a bunch of whitespaces
                    if (html.Length == 0)
                    {
                        break;
                    }
                    if (!textInfo.WritePrecedingWhiteSpace || textInfo.LastCharWasSpace)
                    {
                        html = html.TrimStart();
                        if (html.Length == 0) { break; }
                        textInfo.IsFirstTextOfDocWritten.Value = textInfo.WritePrecedingWhiteSpace = true;
                    }
                    outText.Write(HtmlEntity.DeEntitize(Regex.Replace(html.TrimEnd(), @"\s{2,}", " ")));
                    if (textInfo.LastCharWasSpace = char.IsWhiteSpace(html[html.Length - 1]))
                    {
                        outText.Write(' ');
                    }
                    break;
                case HtmlNodeType.Element:
                    string endElementString = null;
                    bool isInline;
                    bool skip = false;
                    int listIndex = 0;
                    switch (node.Name)
                    {
                        case "nav":
                            skip = true;
                            isInline = false;
                            break;
                        case "body":
                        case "section":
                        case "article":
                        case "aside":
                        case "h1":
                        case "h2":
                        case "header":
                        case "footer":
                        case "address":
                        case "main":
                        case "div":
                        case "p": // stylistic - adjust as you tend to use
                            if (textInfo.IsFirstTextOfDocWritten)
                            {
                                outText.Write("\r\n");
                            }
                            endElementString = "\r\n";
                            isInline = false;
                            break;
                        case "br":
                            outText.Write("\r\n");
                            skip = true;
                            textInfo.WritePrecedingWhiteSpace = false;
                            isInline = true;
                            break;
                        case "hr":
                            outText.Write("---------------------");
                            endElementString = "\r\n";
                            skip = true;
                            textInfo.WritePrecedingWhiteSpace = false;
                            isInline = true;
                            break;
                        case "a":
                            if (node.Attributes.Contains("href"))
                            {
                                string href = node.Attributes["href"].Value.Trim();
                                if (node.InnerText.IndexOf(href, StringComparison.InvariantCultureIgnoreCase) == -1)
                                {
                                    //endElementString = "<" + href + ">";
                                }
                            }
                            isInline = true;
                            break;
                        case "li":
                            if (textInfo.ListIndex > 0)
                            {
                                outText.Write("\r\n{0}.\t", textInfo.ListIndex++);
                            }
                            else
                            {
                                outText.Write("\r\n*\t"); //using '*' as bullet char, with tab after, but whatever you want eg "\t->", if utf-8 0x2022
                            }
                            isInline = false;
                            break;
                        case "ol":
                            listIndex = 1;
                            goto case "ul";
                        case "ul": //not handling nested lists any differently at this stage - that is getting close to rendering problems
                            endElementString = "\r\n";
                            isInline = false;
                            break;
                        case "img": //inline-block in reality
                            if (node.Attributes.Contains("alt"))
                            {
                                //outText.Write('[' + node.Attributes["alt"].Value);
                                //endElementString = "]";
                            }
                            if (node.Attributes.Contains("src"))
                            {
                                //outText.Write('<' + node.Attributes["src"].Value + '>');
                            }
                            isInline = true;
                            break;
                        default:
                            isInline = true;
                            break;
                    }
                    if (!skip && node.HasChildNodes)
                    {
                        ConvertContentTo(node, outText, isInline ? textInfo : new PreceedingDomTextInfo(textInfo.IsFirstTextOfDocWritten) { ListIndex = listIndex });
                    }
                    if (endElementString != null)
                    {
                        outText.Write(endElementString);
                    }
                    break;
            }
        }
    }

    internal class PreceedingDomTextInfo
    {
        public PreceedingDomTextInfo(BoolWrapper isFirstTextOfDocWritten)
        {
            IsFirstTextOfDocWritten = isFirstTextOfDocWritten;
        }
        public bool WritePrecedingWhiteSpace { get; set; }
        public bool LastCharWasSpace { get; set; }
        public readonly BoolWrapper IsFirstTextOfDocWritten;
        public int ListIndex { get; set; }
    }

    internal class BoolWrapper
    {
        public BoolWrapper() { }
        public bool Value { get; set; }
        public static implicit operator bool(BoolWrapper boolWrapper)
        {
            return boolWrapper.Value;
        }
        public static implicit operator BoolWrapper(bool boolWrapper)
        {
            return new BoolWrapper { Value = boolWrapper };
        }
    }

}
