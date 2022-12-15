namespace GakujoGUI.Models
{
    public class YearCredit
    {
        public string Year { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString() => $"{Year} {Credit}";
    }
}
