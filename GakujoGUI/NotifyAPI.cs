﻿using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Todoist.Net;
using Todoist.Net.Models;

namespace GakujoGUI
{
    internal class NotifyAPI
    {
        public Tokens tokens = new();

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
            set => _todoistResources = value;
        }

        private static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }

        public NotifyAPI()
        {
            if (File.Exists(GetJsonPath("Tokens")))
            {
                tokens = JsonConvert.DeserializeObject<Tokens>(File.ReadAllText(GetJsonPath("Tokens")))!;
            }
            Login();
        }

        public void SetTokens(string todoistToken, string discordChannel, string discordToken)
        {
            tokens.TodoistToken = todoistToken;
            tokens.DiscordChannel = ulong.Parse(discordChannel);
            tokens.DiscordToken = discordToken;
            try
            {
                File.WriteAllText(GetJsonPath("Token"), JsonConvert.SerializeObject(tokens, Formatting.Indented));
            }
            catch { }
        }


        public void Login()
        {
            try
            {
                todoistClient = new(tokens.TodoistToken);
            }
            catch { }
            try
            {
                discordSocketClient = new();
                discordSocketClient.LoginAsync(TokenType.Bot, tokens.DiscordToken).Wait();
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

    public class Tokens
    {
        public string TodoistToken { get; set; } = "";
        public ulong DiscordChannel { get; set; }
        public string DiscordToken { get; set; } = "";
    }
}