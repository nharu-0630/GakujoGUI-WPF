using System;
using System.Globalization;
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
            return !isString ? NullContent : GakujoApi.ReplaceSubjectsShort(stringValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
