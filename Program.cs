/* Copyright (c) 2010 Derrick Coetzee

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using DotNetWikiBot;

namespace RotateBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Site site = new Site("http://commons.wikimedia.org", args[0], args[1]);
            while (true)
            {
                PageList list = new PageList(site);
                list.FillAllFromCategory("Category:Rotate270");
                foreach (Page page in list)
                {
                    if (!page.title.ToLower().EndsWith(".jpg")) continue;
                    page.Load();
                    if (!page.text.Contains("{{rotate"))
                    {
                        page.text = "{{rotate|270}}\n" + page.text;
                        page.Save("Add {{rotate|270}} tag since it's in Category:Rotate270", /*isMinorEdit*/false);
                    }
                }

                list = new PageList(site);
                list.FillAllFromCategory("Category:Images requiring rotation");
                foreach (Page page in list)
                {
                    Regex regex = new Regex(@"\{\{Rotate\|nobot=true\|reason='''Reason''': corrupt JPEG file.\|([^}]*)\}\}", RegexOptions.IgnoreCase);
                    page.Load();
                    Match m = regex.Match(page.text);
                    if (m.Success)
                    {
                        page.text = regex.Replace(page.text, "{{Rotate|" + m.Groups[1].Value + "}}");
                        page.Save("Undo mistagging by [[User:Rotatebot]], JPEG should not be corrupt", /*isMinorEdit*/false);
                    }
                }

                list = new PageList(site);
                list.FillAllFromCategory("Category:Images requiring rotation by bot");
                try
                {
                    foreach (Page page in list)
                    {
                        if (!page.title.ToLower().EndsWith(".jpg")) continue;
                        page.Load();
                        MatchCollection matches = new Regex(@"\{\{rotate\|([0-9]+)\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                        if (matches.Count == 0)
                        {
                            matches = new Regex(@"\{\{rotate\|degree=([0-9]+)\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                            if (matches.Count == 0)
                            {
                                matches = new Regex(@"\{\{rotate\|([0-9]+)\|[^}]*\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                                if (matches.Count == 0)
                                {
                                    continue;
                                }
                            }
                        }
                        if (matches.Count > 1)
                        {
                            while (matches.Count > 1)
                            {
                                page.text = page.text.Remove(matches[0].Index, matches[0].Length);
                                matches = new Regex(@"\{\{rotate\|([0-9]+)\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                                if (matches.Count == 0)
                                {
                                    matches = new Regex(@"\{\{rotate\|degree=([0-9]+)\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                                    if (matches.Count == 0)
                                    {
                                        matches = new Regex(@"\{\{rotate\|([0-9]+)\|[^}]*\}\}", RegexOptions.IgnoreCase).Matches(page.text);
                                    }
                                }
                            }
                            page.Save("Remove redundant rotate tags", /*isMinorEdit*/false);
                        }

                        Match m = matches[0];
                        int degrees = int.Parse(m.Groups[1].Value);
                        page.DownloadImage("tempin.jpg");
                        File.Delete("tempout.jpg");
                        ProcessStartInfo info = new ProcessStartInfo(@"..\..\jpegtran.exe", "-rotate " + degrees + " tempin.jpg tempout.jpg");
                        info.CreateNoWindow = true;
                        info.UseShellExecute = false;
                        Process p = Process.Start(info);
                        p.WaitForExit();
                        if (File.Exists("tempout.jpg"))
                        {
                            page.UploadImage("tempout.jpg", "Losslessly rotate by " + degrees + " degrees per request using jpegtran", "", "", "");
                            page.Load();
                            page.text = page.text.Replace(m.Value + "\n", "");
                            page.text = page.text.Replace(m.Value, "");
                            page.text = page.text.Replace("[[Category:Rotate270]]\n", "");
                            page.text = page.text.Replace("[[Category:Rotate270]]", "");
                            page.Save("Done with rotation by " + degrees + " degrees, removing tag", /*isMinorEdit*/false);
                        }
                    }
                    System.Threading.Thread.Sleep(new TimeSpan(0, 2, 0));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Caught exception: " + e.Message + e.StackTrace);
                }
            }
        }
    }
}
