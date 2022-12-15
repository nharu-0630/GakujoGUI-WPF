using System.Collections.Generic;
using System.Linq;

namespace GakujoGUI.Models
{
    public class ClassTableRow
    {
        public ClassTableCell Monday { get; set; } = new();
        public ClassTableCell Tuesday { get; set; } = new();
        public ClassTableCell Wednesday { get; set; } = new();
        public ClassTableCell Thursday { get; set; } = new();
        public ClassTableCell Friday { get; set; } = new();

        public ClassTableCell this[int index]
        {
            get => index switch
            {
                0 => Monday,
                1 => Tuesday,
                2 => Wednesday,
                3 => Thursday,
                4 => Friday,
                _ => new(),
            };
            set
            {
                switch (index)
                {
                    case 0:
                        Monday = value;
                        break;
                    case 1:
                        Tuesday = value;
                        break;
                    case 2:
                        Wednesday = value;
                        break;
                    case 3:
                        Thursday = value;
                        break;
                    case 4:
                        Friday = value;
                        break;
                }
            }
        }

        public List<LotteryRegistration> LotteryRegistrations => new List<List<LotteryRegistration>> { this[0].LotteryRegistrations, this[1].LotteryRegistrations, this[2].LotteryRegistrations, this[3].LotteryRegistrations, this[4].LotteryRegistrations }.SelectMany(_ => _).ToList();

        public List<LotteryRegistrationResult> LotteryRegistrationsResult => new List<List<LotteryRegistrationResult>> { this[0].LotteryRegistrationsResult, this[1].LotteryRegistrationsResult, this[2].LotteryRegistrationsResult, this[3].LotteryRegistrationsResult, this[4].LotteryRegistrationsResult }.SelectMany(_ => _).ToList();

        public List<GeneralRegistration> RegisterableGeneralRegistrations => new List<List<GeneralRegistration>> { this[0].GeneralRegistrations.RegisterableGeneralRegistrations, this[1].GeneralRegistrations.RegisterableGeneralRegistrations, this[2].GeneralRegistrations.RegisterableGeneralRegistrations, this[3].GeneralRegistrations.RegisterableGeneralRegistrations, this[4].GeneralRegistrations.RegisterableGeneralRegistrations }.SelectMany(_ => _).Where(x => x.KamokuCode != "" && x.ClassCode != "").ToList();

        public IEnumerator<ClassTableCell> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
            yield return this[2];
            yield return this[3];
            yield return this[4];
        }

        public override string ToString() => $"{this[0].SubjectsName} {this[1].SubjectsName} {this[2].SubjectsName} {this[3].SubjectsName} {this[4].SubjectsName}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            var objClassTableRow = (ClassTableRow)obj;
            return this[0].GetHashCode() == objClassTableRow[0].GetHashCode() && this[1].GetHashCode() == objClassTableRow[1].GetHashCode() && this[2].GetHashCode() == objClassTableRow[2].GetHashCode() && this[3].GetHashCode() == objClassTableRow[3].GetHashCode() && this[4].GetHashCode() == objClassTableRow[4].GetHashCode();
        }

        public override int GetHashCode() => this[0].GetHashCode() ^ this[1].GetHashCode() ^ this[2].GetHashCode() ^ this[3].GetHashCode() ^ this[4].GetHashCode();
    }
}
