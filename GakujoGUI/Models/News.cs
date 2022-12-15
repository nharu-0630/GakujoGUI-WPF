using System;

namespace GakujoGUI.Models
{
    public class News
    {
        public int Index { get; set; }
        public string Type { get; set; } = "";
        public DateTime DateTime { get; set; }
        public string Title { get; set; } = "";
        public override string ToString() => $"[{Type}] {DateTime:yyyy/MM/dd} {Title}";
    }
}
