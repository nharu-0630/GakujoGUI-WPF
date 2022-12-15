namespace GakujoGUI.Models
{
    public class SemesterGpa
    {
        public string Year { get; set; } = "";
        public string Semester { get; set; } = "";
        public double Gpa { get; set; }

        public override string ToString() => $"{Year}{Semester} {Gpa}";
    }
}
