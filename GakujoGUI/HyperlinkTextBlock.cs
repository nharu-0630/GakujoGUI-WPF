using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace HyperlinkTextBlock
{
    public class HyperlinkTextBlock : TextBlock
    {
        public static readonly DependencyProperty ArticleContentProperty =
            DependencyProperty.RegisterAttached(
                "Inline",
                typeof(string),
                typeof(HyperlinkTextBlock),
                new PropertyMetadata(null, OnInlinePropertyChanged));

        public static string? GetInline(TextBlock textBlock)
        {
            return (textBlock != null) ? textBlock.GetValue(ArticleContentProperty) as string : string.Empty;
        }

        public static void SetInline(TextBlock textBlock, string value)
        {
            if (textBlock != null)
            {
                textBlock.SetValue(ArticleContentProperty, value);
            }
        }

        private static void OnInlinePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            TextBlock textBlock = (dependencyObject as TextBlock)!;
            string? message = e.NewValue as string;
            if (textBlock == null || message == null)
                return;
            message = message.TrimEnd('\n').TrimEnd('\r');
            List<int> newLine = new();
            int i = 0;
            while ((i = message.IndexOf("\r\n", i)) >= 0)
            {
                newLine.Add(i - (newLine.Count * 2));
                i += 2;
            }
            newLine.Sort();
            Regex regex = new(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string text = message.Replace("\r\n", "");
            MatchCollection matchCollection = regex.Matches(text);
            if (matchCollection.Count > 0)
            {
                textBlock.Text = null;
                textBlock.Inlines.Clear();
                int position = 0;
                int l = 0;
                foreach (Match match in matchCollection)
                {
                    int index = match.Groups[0].Index;
                    int length = match.Groups[0].Length;
                    string tag = match.Groups[0].Value;
                    if (position < index)
                    {
                        while (position < text.Length)
                        {
                            if (newLine.Count - l > 0 && newLine[l] < index)
                            {
                                string? buffer = text[position..newLine[l]];
                                textBlock.Inlines.Add(new Run(buffer));
                                textBlock.Inlines.Add(new LineBreak());
                                position = newLine[l];
                                l++;
                            }
                            else
                            {
                                string? buffer = text[position..index];
                                textBlock.Inlines.Add(new Run(buffer));
                                position = index;
                                break;
                            }
                        }
                    }
                    Hyperlink hyperlink = new();
                    hyperlink.TextDecorations = null;
                    hyperlink.Foreground = textBlock.Foreground;
                    hyperlink.NavigateUri = new Uri(tag);
                    hyperlink.RequestNavigate += new RequestNavigateEventHandler(RequestNavigate);
                    hyperlink.MouseEnter += new MouseEventHandler(MouseEnter);
                    hyperlink.MouseLeave += new MouseEventHandler(MouseLeave);
                    while (position < text.Length)
                    {
                        if (newLine.Count - l > 0 && newLine[l] < index + length)
                        {
                            string? buffer = text[position..newLine[l]];
                            hyperlink.Inlines.Add(new Run(buffer));
                            hyperlink.Inlines.Add(new LineBreak());
                            position = newLine[l];
                            l++;
                        }
                        else
                        {
                            string? buffer = text[position..(index + length)];
                            hyperlink.Inlines.Add(new Run(buffer));
                            position = index + length;
                            break;
                        }
                    }
                    textBlock.Inlines.Add(hyperlink);
                }
                while (position < text.Length)
                {
                    if (newLine.Count - l > 0)
                    {
                        var buff = text[position..newLine[l]];
                        textBlock.Inlines.Add(new Run(buff));
                        textBlock.Inlines.Add(new LineBreak());
                        position = newLine[l];
                        l++;
                    }
                    else
                    {
                        var buff = text[position..];
                        textBlock.Inlines.Add(new Run(buff));
                        position = text.Length;
                        break;
                    }
                }
            }
            else
            {
                textBlock.Text = message;
            }
        }

        private static void RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch
            { }
        }

        private static new void MouseEnter(object sender, MouseEventArgs e)
        {
            Hyperlink? hyperlink = sender as Hyperlink;
            if (hyperlink == null)
            {
                return;
            }
            hyperlink.Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
        }

        private static new void MouseLeave(object sender, MouseEventArgs e)
        {
            Hyperlink? hyperlink = sender as Hyperlink;
            TextBlock? textBlock = hyperlink!.Parent as TextBlock;
            if (hyperlink == null || textBlock == null)
            {
                return;
            }
            hyperlink.Foreground = textBlock.Foreground;
        }
    }
}