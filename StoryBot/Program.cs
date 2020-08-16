using System;
using VkNet;
using VkNet.Model;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using VkNet.AudioBypassService.Extensions;
using Microsoft.Extensions.DependencyInjection;
using VkNet.Enums.Filters;
using Newtonsoft.Json;

namespace StoryBot
{
    class Program
    {
        public static readonly string DIR_BASE = AppDomain.CurrentDomain.BaseDirectory;
        static void Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddAudioBypass();

            var vkapi = new VkApi(services);

            string[] words = File.ReadAllLines(DIR_BASE + "words.txt");
            List<string> wordsList = new List<string>(words);
            var settings = File.ReadAllText(DIR_BASE + "settings.json");
            JObject data = JObject.Parse(settings);
            var login = data["Login"].ToString();
            var pass = data["Password"].ToString();
            var token = data["Token"].ToString();
            try
            {
                if (token.Length > 3)
                {
                    vkapi.Authorize(new ApiAuthParams
                    {
                        AccessToken = token
                    });
                }
                else
                {
                    vkapi.Authorize(new ApiAuthParams
                    {
                        Login = login,
                        Password = pass,
                        Settings = Settings.All | Settings.Offline,
                        TwoFactorAuthorization = () =>
                        {
                            Console.WriteLine("Введите код двухфакторки: ");

                            return Console.ReadLine();
                        }
                    });
                    data["Token"] = vkapi.Token;
                    File.WriteAllText("settings.json", JsonConvert.SerializeObject(data, Formatting.Indented));
                }
            }
            catch (Exception e ) { Console.WriteLine(e.Message); }
            // а мне похуй я далбаеб у меня справка
            for (int w = 0; w < wordsList.Count; w++)
            {
                var word = wordsList[w];
                var execute = vkapi.Execute.Execute("var b = API.stories.search({\"q\": \"" + word + "\", \"count\":\"100\"}); return b;");
                var json = execute.RawJson;
                JObject parse = JObject.Parse(json);
                int count = Convert.ToInt32(parse["response"]["count"]);
                for (int i = 0; i < count; i++)
                {
                    var owner = parse["response"]["items"][i][0]["owner_id"].ToString();
                    var id = parse["response"]["items"][i][0]["id"].ToString();
                    Thread.Sleep(400);
                    try
                    {
                        var mark_seen = vkapi.Execute.Execute("var a = API.stories.markSeen({\"owner_id\":\"" + owner + "\", \"story_id\":\"" + id + "\"}); return a;");
                        if (mark_seen.RawJson == "{\"response\":1}")
                        {
                            Task.Run(() =>
                            {
                                drawTextProgressBar($" Слово: {word}", i, count);
                            });
                        }
                    }
                    catch (VkNet.Exception.ParameterMissingOrInvalidException e)
                    {
                        if (e.Message.Contains("story is private"))
                        {
                            continue;
                        }
                    }
                }

            }            
            Console.ReadLine();
        }
        public static void drawTextProgressBar(string stepDescription, int progress, int total)
        {
            int totalChunks = 30;

            //draw empty progress bar
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = totalChunks + 1;
            Console.Write("]"); //end
            Console.CursorLeft = 1;

            double pctComplete = Convert.ToDouble(progress) / total;
            int numChunksComplete = Convert.ToInt16(totalChunks * pctComplete);

            //draw completed chunks
            Console.BackgroundColor = ConsoleColor.Green;
            Console.Write("".PadRight(numChunksComplete));

            //draw incomplete chunks
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.Write("".PadRight(totalChunks - numChunksComplete));

            //draw totals
            Console.CursorLeft = totalChunks + 5;
            Console.BackgroundColor = ConsoleColor.Black;

            //string output = progress.ToString() + " of " + total.ToString();
            var output = (double)progress / total * 100;
            var answer = (int)output;
            Console.Write(answer.ToString() + $"% [{progress.ToString()} of {total.ToString()}]" + stepDescription); //pad the output so when changing from 3 to 4 digits we avoid text shifting
        }
    }
}
