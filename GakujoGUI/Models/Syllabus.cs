namespace GakujoGUI.Models
{
    public class Syllabus
    {
        public string SubjectsName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string Affiliation { get; set; } = "";
        public string ResearchRoom { get; set; } = "";
        public string SharingTeacherName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string SemesterName { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public string TargetGrade { get; set; } = "";
        public string Credit { get; set; } = "";
        public string WeekdayPeriod { get; set; } = "";
        public string ClassRoom { get; set; } = "";
        public string Keyword { get; set; } = "";
        public string ClassTarget { get; set; } = "";
        public string LearningDetail { get; set; } = "";
        public string ClassPlan { get; set; } = "";
        public string ClassRequirement { get; set; } = "";
        public string Textbook { get; set; } = "";
        public string ReferenceBook { get; set; } = "";
        public string PreparationReview { get; set; } = "";
        public string EvaluationMethod { get; set; } = "";
        public string OfficeHour { get; set; } = "";
        public string Message { get; set; } = "";
        public string ActiveLearning { get; set; } = "";
        public string TeacherPracticalExperience { get; set; } = "";
        public string TeacherCareerClassDetail { get; set; } = "";
        public string TeachingProfessionSection { get; set; } = "";
        public string RelatedClassSubjects { get; set; } = "";
        public string Other { get; set; } = "";
        public string HomeClassStyle { get; set; } = "";
        public string HomeClassStyleDetail { get; set; } = "";

        public override string ToString()
        {
            var value = $"## {SubjectsName}\n";
            value += "|担当教員名|所属等|研究室|分担教員名|\n";
            value += "|-|-|-|-|\n";
            value += $"|{TeacherName}|{Affiliation}|{ResearchRoom}|{SharingTeacherName}|\n";
            value += "\n";
            value += "|クラス|学期|必修選択区分|\n";
            value += "|-|-|-|\n";
            value += $"|{ClassName}|{SemesterName}|{SelectionSection}|\n";
            value += "\n";
            value += "|対象学年|単位数|曜日・時限|\n";
            value += "|-|-|-|\n";
            value += $"|{TargetGrade}|{Credit}|{WeekdayPeriod}|\n";
            value += "### 教室\n";
            value += $"{ClassRoom}  \n";
            value += "### キーワード\n";
            value += $"{Keyword}  \n";
            value += "### 授業の目標\n";
            value += $"{ClassTarget}  \n";
            value += "### 学習内容\n";
            value += $"{LearningDetail}  \n";
            value += "### 授業計画\n";
            value += $"{ClassPlan}  \n";
            value += "### 受講要件\n";
            value += $"{ClassRequirement}  \n";
            value += "### テキスト\n";
            value += $"{Textbook}  \n";
            value += "### 参考書\n";
            value += $"{ReferenceBook}  \n";
            value += "### 予習・復習について\n";
            value += $"{PreparationReview}  \n";
            value += "### 成績評価の方法･基準\n";
            value += $"{EvaluationMethod}  \n";
            value += "### オフィスアワー\n";
            value += $"{OfficeHour}  \n";
            value += "### 担当教員からのメッセージ\n";
            value += $"{Message}  \n";
            value += "### アクティブ・ラーニング\n";
            value += $"{ActiveLearning}  \n";
            value += "### 実務経験のある教員の有無\n";
            value += $"{TeacherPracticalExperience}  \n";
            value += "### 実務経験のある教員の経歴と授業内容\n";
            value += $"{TeacherCareerClassDetail}  \n";
            value += "### 教職科目区分\n";
            value += $"{TeachingProfessionSection}  \n";
            value += "### 関連授業科目\n";
            value += $"{RelatedClassSubjects}  \n";
            value += "### その他\n";
            value += $"{Other}  \n";
            value += "### 在宅授業形態\n";
            value += $"{HomeClassStyle}  \n";
            value += "### 在宅授業形態(詳細)\n";
            value += $"{HomeClassStyleDetail}  \n";
            return value;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            var objSyllabus = (Syllabus)obj;
            return SubjectsName == objSyllabus.SubjectsName && TeacherName == objSyllabus.TeacherName;
        }

        public override int GetHashCode() => SubjectsName.GetHashCode() ^ TeacherName.GetHashCode();
    }
}
