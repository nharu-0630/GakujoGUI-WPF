using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GakujoGUI
{
    /// <summary>
    /// ClassTablesCell.xaml の相互作用ロジック
    /// </summary>
    public partial class ClassTablesCell : UserControl
    {
        public ClassTablesCell()
        {
            InitializeComponent();
        }

        public static readonly RoutedEvent ClassContactButtonClickEvent = EventManager.RegisterRoutedEvent("ClassContactButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTablesCell));
        public static readonly RoutedEvent ReportButtonClickEvent = EventManager.RegisterRoutedEvent("ReportButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTablesCell));
        public static readonly RoutedEvent QuizButtonClickEvent = EventManager.RegisterRoutedEvent("QuizButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ClassTablesCell));

        public event RoutedEventHandler ClassContactButtonClick
        {
            add { AddHandler(ClassContactButtonClickEvent, value); }
            remove { RemoveHandler(ClassContactButtonClickEvent, value); }
        }

        public event RoutedEventHandler ReportButtonClick
        {
            add { AddHandler(QuizButtonClickEvent, value); }
            remove { RemoveHandler(QuizButtonClickEvent, value); }
        }

        public event RoutedEventHandler QuizButtonClick
        {
            add { RemoveHandler(QuizButtonClickEvent, value); }
            remove { RemoveHandler(QuizButtonClickEvent, value); }
        }

        private void ClassContactButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(ClassContactButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void ReportButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(ReportButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }

        private void QuizButton_Click(object sender, RoutedEventArgs e)
        {
            RoutedEventArgs routedEventArgs = new(QuizButtonClickEvent);
            RaiseEvent(routedEventArgs);
        }
    }
}
