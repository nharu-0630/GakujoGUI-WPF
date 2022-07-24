using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace GakujoGUI.Converters
{
    public class StringToSubjectsConverter : IValueConverter
    {
        public StringToSubjectsConverter()
        {
            NullContent = "Null";
        }

        public object NullContent { get; set; }

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) { return NullContent; }
            var stringValue = "";
            var isString = true;
            try { stringValue = (string)value; }
            catch { isString = false; }
            if (!isString) { return NullContent; }
            return Regex.Replace(stringValue, "（.*）(前|後)期.*", "");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
