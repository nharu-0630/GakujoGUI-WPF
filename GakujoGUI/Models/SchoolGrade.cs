using System.Collections.Generic;
using System.Linq;

namespace GakujoGUI.Models
{
    public class SchoolGrade
    {
        public List<ClassResult> ClassResults { get; set; } = new();
        public List<EvaluationCredit> EvaluationCredits { get; set; } = new();
        public double PreliminaryGpa => 1.0 * ClassResults.Where(x => x.Score != 0).Select(x => x.Gp * x.Credit).Sum() / ClassResults.Where(x => x.Score != 0).Select(x => x.Credit).Sum();
        public DepartmentGpa DepartmentGpa { get; set; } = new();
        public List<YearCredit> YearCredits { get; set; } = new();
    }
}
