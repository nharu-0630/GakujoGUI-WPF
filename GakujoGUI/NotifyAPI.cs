using Discord;
using Discord.WebSocket;
using GakujoGUI.Models;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Todoist.Net;
using Todoist.Net.Models;

namespace GakujoGUI
{
    internal class NotifyApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private TodoistClient? todoistClient;
        private DiscordSocketClient? discordSocketClient;
        private DateTime todoistUpdateDateTime;
        private Resources? todoistResources;

        private Resources TodoistResources
        {
            get
            {
                if (todoistUpdateDateTime + TimeSpan.FromMinutes(3) >= DateTime.Now || todoistClient == null)
                    return todoistResources!;
                todoistUpdateDateTime = DateTime.Now;
                TodoistResources = todoistClient!.GetResourcesAsync().Result;
                return todoistResources!;
            }
            set => todoistResources = value;
        }
        public Tokens Tokens { get; set; } = new();

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName)))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName));
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @$"{AssemblyName}\{value}.json");
        }

        private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        public NotifyApi()
        {
            if (File.Exists(GetJsonPath("Tokens")))
            {
                Tokens = JsonConvert.DeserializeObject<Tokens>(File.ReadAllText(GetJsonPath("Tokens")))!;
                Logger.Info("Load Tokens.");
            }
            Login();
        }

        public void SetTokens(string todoistToken, string discordChannel, string discordToken)
        {
            Tokens.TodoistToken = GakujoApi.Protect(todoistToken, null!, DataProtectionScope.CurrentUser);
            Tokens.DiscordChannel = ulong.Parse(discordChannel);
            Tokens.DiscordToken = GakujoApi.Protect(discordToken, null!, DataProtectionScope.CurrentUser);
            try { File.WriteAllText(GetJsonPath("Tokens"), JsonConvert.SerializeObject(Tokens, Formatting.Indented)); }
            catch (Exception exception) { Logger.Error(exception, "Error Save Tokens."); }
        }

        public void Login()
        {
            try
            {
                todoistClient = new(GakujoApi.Unprotect(Tokens.TodoistToken, null!, DataProtectionScope.CurrentUser));
                TodoistResources = todoistClient!.GetResourcesAsync().Result;
                Logger.Info("Login Todoist.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Login Todoist."); }
            try
            {
                discordSocketClient = new();
                discordSocketClient.LoginAsync(TokenType.Bot, GakujoApi.Unprotect(Tokens.DiscordToken, null!, DataProtectionScope.CurrentUser)).Wait();
                discordSocketClient.StartAsync().Wait();
                Logger.Info("Login Discord.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Login Discord."); }
        }

        #region Todoist

        private bool ExistsTodoistTask(string content, DateTime dateTime) => dateTime < DateTime.Now || TodoistResources.Items.Any(x => x.DueDate != null && x.Content == content && x.DueDate.Date == dateTime);

        private void AddTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                if (!ExistsTodoistTask(content, dateTime))
                {
                    Logger.Info($"Add Todoist task {todoistClient!.Items.AddAsync(new Item(content) { DueDate = new DueDate(dateTime.ToLocalTime()) }).Result}.");
                }
            }
            catch (Exception exception) { Logger.Error(exception, "Error Add Todoist task."); }
        }

        private void ArchiveTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                TodoistResources.Items.Where(x => x.DueDate != null && x.Content == content && x.DueDate.Date == dateTime && x.IsChecked != true).ToList().ForEach(x =>
                {
                    todoistClient!.Items.CloseAsync(x.Id).Wait();
                    Logger.Info($"Archive Todoist task {x.Id}.");
                });
            }
            catch (Exception exception) { Logger.Error(exception, "Error Archive Todoist task."); }
        }

        public void SetTodoistTask(List<Report> reports)
        {
            Logger.Info("Start Set Todoist task reports.");
            reports.Where(x => x.IsSubmittable).ToList().ForEach(x => AddTodoistTask(x.ToShortString(), x.EndDateTime));
            reports.Where(x => !x.IsSubmittable).ToList().ForEach(x => ArchiveTodoistTask(x.ToShortString(), x.EndDateTime));
            Logger.Info("End Set Todoist task reports.");
        }

        public void SetTodoistTask(List<Quiz> quizzes)
        {
            Logger.Info("Start Set Todoist task quizzes.");
            quizzes.Where(x => x.IsSubmittable).ToList().ForEach(x => AddTodoistTask(x.ToShortString(), x.EndDateTime));
            quizzes.Where(x => !x.IsSubmittable).ToList().ForEach(x => ArchiveTodoistTask(x.ToShortString(), x.EndDateTime));
            Logger.Info("End Set Todoist task quizzes.");
        }

        #endregion

        #region Discord

        public void NotifyDiscord(ClassContact classContact)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classContact.Title);
                embedBuilder.WithDescription(classContact.Content);
                embedBuilder.WithAuthor(classContact.Subjects);
                embedBuilder.WithTimestamp(classContact.ContactDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                Logger.Info("Notify Discord ClassContact.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify Discord ClassContact."); }
        }

        public void NotifyDiscord(Report report)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(report.Title);
                embedBuilder.WithDescription($"{report.StartDateTime} -> {report.EndDateTime}");
                embedBuilder.WithAuthor(report.Subjects);
                embedBuilder.WithTimestamp(report.StartDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                Logger.Info("Notify Discord Report.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify Discord Report."); }
        }

        public void NotifyDiscord(Quiz quiz)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(quiz.Title);
                embedBuilder.WithDescription($"{quiz.StartDateTime} -> {quiz.EndDateTime}");
                embedBuilder.WithAuthor(quiz.Subjects);
                embedBuilder.WithTimestamp(quiz.StartDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                Logger.Info("Notify Discord Quiz.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify Discord Quiz."); }
        }

        public void NotifyDiscord(ClassSharedFile classSharedFile)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classSharedFile.Title);
                embedBuilder.WithDescription(classSharedFile.Description);
                embedBuilder.WithAuthor(classSharedFile.Subjects);
                embedBuilder.WithTimestamp(classSharedFile.UpdateDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                Logger.Info("Notify Discord ClassSharedFile.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify Discord ClassSharedFile."); }
        }

        public void NotifyDiscord(ClassResult classResult, bool hideDetail)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classResult.Subjects);
                if (!hideDetail) { embedBuilder.WithDescription($"{classResult.Score} ({classResult.Evaluation})   {classResult.Gp:F1}"); }
                embedBuilder.WithAuthor(classResult.TeacherName);
                embedBuilder.WithTimestamp(classResult.ReportDate);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                Logger.Info("Notify Discord ClassResult.");
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify Discord ClassResult."); }
        }

        #endregion

        #region LINE

        public void NotifyLine(string content)
        {
            try
            {
                Logger.Info("Notify LINE.");
                using HttpClient httpClient = new();
                HttpRequestMessage httpRequestMessage = new(new("POST"), "https://notify-api.line.me/api/notify");
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Tokens.LineToken}");
                httpRequestMessage.Content = new StringContent($"message={HttpUtility.UrlEncode(content, Encoding.UTF8)}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                var httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info("POST https://notify-api.line.me/api/notify");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                httpClient.Dispose();
            }
            catch (Exception exception) { Logger.Error(exception, "Error Notify LINE."); }
        }

        #endregion
    }

    public class Tokens
    {
        public string TodoistToken { get; set; } = "";
        public ulong DiscordChannel { get; set; }
        public string DiscordToken { get; set; } = "";
        public string LineToken { get; set; } = "";
    }
}
