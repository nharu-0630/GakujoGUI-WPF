using System.Windows;
using System.Windows.Controls;

namespace WpfPasswordBox
{
    public class PasswordBoxHelper : DependencyObject
    {
        public static readonly DependencyProperty IsAttachedProperty
            = DependencyProperty.RegisterAttached(
                "IsAttached",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(false, IsAttachedProperty_Changed));

        public static readonly DependencyProperty PasswordProperty
            = DependencyProperty.RegisterAttached(
                "Password",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, PasswordProperty_Changed));

        public static bool GetIsAttached(DependencyObject dp)
            => (bool)dp.GetValue(IsAttachedProperty);

        public static string GetPassword(DependencyObject dp)
            => (string)dp.GetValue(PasswordProperty);

        public static void SetIsAttached(DependencyObject dp, bool value)
            => dp.SetValue(IsAttachedProperty, value);

        public static void SetPassword(DependencyObject dp, string value)
            => dp.SetValue(PasswordProperty, value);

        private static void IsAttachedProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox) return;
            if ((bool)e.OldValue)
            {
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
            }
            if ((bool)e.NewValue)
            {
                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox) return;
            SetPassword(passwordBox, passwordBox.Password);
        }

        private static void PasswordProperty_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox) return;
            var newPassword = (string)e.NewValue;
            if (!GetIsAttached(passwordBox))
            {
                SetIsAttached(passwordBox, true);
            }
            if ((string.IsNullOrEmpty(passwordBox.Password) && string.IsNullOrEmpty(newPassword)) ||
                passwordBox.Password == newPassword)
            {
                return;
            }
            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
            passwordBox.Password = newPassword;
            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }
    }
}