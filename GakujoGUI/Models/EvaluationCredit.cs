namespace GakujoGUI.Models
{
    public class EvaluationCredit
    {
        public string Evaluation { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString() => $"{Evaluation} {Credit}";
    }
}
