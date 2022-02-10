using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Todoist.Net;
using Todoist.Net.Models;
using Discord;
using Discord.WebSocket;

namespace GakujoGUI
{
    internal class NotifyAPI
    {
        public Token token = new();

        private TodoistClient? todoistClient;
        private DiscordSocketClient? discordSocketClient;

        private DateTime todoistUpdateDateTime = new();
        private Resources? _todoistResources;
        private Resources TodoistResources
        {
            get
            {
                if (todoistUpdateDateTime + TimeSpan.FromMinutes(3) < DateTime.Now)
                {
                    todoistUpdateDateTime = DateTime.Now;
                    TodoistResources = todoistClient!.GetResourcesAsync().Result;
                }
                return _todoistResources!;
            }
            set { _todoistResources = value; }
        }

        public static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }

        public NotifyAPI()
        {
            LoadJson();
            Login();
        }

        public void SetToken(string todoistToken, string discordChannel, string discordToken)
        {
            token.TodoistToken = todoistToken;
            token.DiscordChannel = ulong.Parse(discordChannel);
            token.DiscordToken = discordToken;
            SaveJson();
        }

        private void LoadJson()
        {
            if (File.Exists(GetJsonPath("Token")))
            {
                token = JsonConvert.DeserializeObject<Token>(File.ReadAllText(GetJsonPath("Token")))!;
            }
        }

        private void SaveJson()
        {
            try
            {
                File.WriteAllText(GetJsonPath("Token"), JsonConvert.SerializeObject(token, Formatting.Indented));
            }
            catch { }
        }

        public void Login()
        {
            try
            {
                todoistClient = new(token.TodoistToken);
            }
            catch { }
            try
            {
                discordSocketClient = new();
                discordSocketClient.LoginAsync(TokenType.Bot, token.DiscordToken).Wait();
            }
            catch { }
        }

        private bool ExistsTodoistTask(string content, DateTime dateTime)
        {
            if (dateTime < DateTime.Now)
            {
                return true;
            }
            foreach (Item item in TodoistResources.Items)
            {
                if (item.DueDate == null)
                {
                    continue;
                }
                if (item.Content == content && item.DueDate.Date == dateTime)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                if (!ExistsTodoistTask(content, dateTime))
                {
                    todoistClient!.Items.AddAsync(new Item(content) { DueDate = new DueDate(dateTime + TimeSpan.FromHours(9)) }).Wait();
                }
            }
            catch { }
        }

        private void ArchiveTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                foreach (Item item in TodoistResources.Items)
                {
                    if (item.DueDate == null)
                    {
                        continue;
                    }
                    if (item.Content == content && item.DueDate.Date == dateTime && item.IsArchived == false)
                    {
                        todoistClient!.Items.ArchiveAsync(item.Id).Wait();
                    }
                }
            }
            catch { }
        }

        public void SetTodoistTask(List<Report> reports)
        {
            foreach (Report report in reports)
            {
                if (report.Status == "受付中" && report.SubmittedDateTime == new DateTime())
                {
                    AddTodoistTask(report.ToShortString(), report.EndDateTime);
                }
                else
                {
                    ArchiveTodoistTask(report.ToShortString(), report.EndDateTime);
                }
            }
        }

        public void SetTodoistTask(List<Quiz> quizzes)
        {
            foreach (Quiz quiz in quizzes)
            {
                if (quiz.Status == "受付中" && quiz.SubmissionStatus == "未提出")
                {
                    AddTodoistTask(quiz.ToShortString(), quiz.EndDateTime);
                }
                else
                {
                    AddTodoistTask(quiz.ToShortString(), quiz.EndDateTime);
                }
            }
        }
    }
    public class Token
    {
        public string TodoistToken = "";
        public ulong DiscordChannel;
        public string DiscordToken = "";
    }
}
