using System;
using System.Collections.Generic;

namespace GakujoGUI.Models
{
    public class DepartmentGpa
    {
        public int Grade { get; set; }
        public double Gpa { get; set; }
        public List<SemesterGpa> SemesterGpas { get; set; } = new();
        public DateTime CalculationDate { get; set; }
        public int[] DepartmentRank { get; set; } = new int[2];
        public int[] CourseRank { get; set; } = new int[2];
        public string DepartmentImage { get; set; } = "";
        public string CourseImage { get; set; } = "";

        public override string ToString()
        {
            var value = $"学年 {Grade}年";
            value += $"\n累積GPA {Gpa}";
            value += "\n学期GPA";
            SemesterGpas.ForEach(x => value += $"\n{x}");
            value += $"\n学科内順位 {DepartmentRank[0]}/{DepartmentRank[1]}";
            value += $"\nコース内順位 {CourseRank[0]}/{CourseRank[1]}";
            value += $"\n算出日 {CalculationDate:yyyy/MM/dd}";
            return value;
        }
    }
}
