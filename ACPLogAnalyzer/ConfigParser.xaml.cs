using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ACPLogAnalyzer {
    /// <summary>
    /// Логика взаимодействия для ConfigParser.xaml
    /// </summary>
    public partial class ConfigParser : Window {
        public ConfigParser() {
            InitializeComponent();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Parser_Culture = ((ParserCulture)dtFormat.SelectedItem).Key;
            Properties.Settings.Default.Parser_Encoding = ((ParserEncoding)lfEncoding.SelectedItem).Key;
            Properties.Settings.Default.Save();
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs ea) {
            List<ParserCulture> parserCultures = new List<ParserCulture> {
                new ParserCulture("system", "System settings [default]")
            };
            List<CultureInfo> cultures = new List<CultureInfo>(CultureInfo.GetCultures(CultureTypes.AllCultures));
            cultures.Sort((a, b) => String.Compare(a.EnglishName, b.EnglishName));
            foreach (CultureInfo c in cultures) {
                if (!c.Name.Equals("")) {
                    parserCultures.Add(new ParserCulture(c.Name, c.EnglishName + " [" + c.Name + "]"));
                }
            }
            dtFormat.SelectedValuePath = "Key";
            dtFormat.DisplayMemberPath = "Value";
            dtFormat.ItemsSource = parserCultures;
            dtFormat.SelectedValue = Properties.Settings.Default.Parser_Culture;

            List<ParserEncoding> parserEncodings = new List<ParserEncoding> {
                new ParserEncoding("system", "System settings [default]")
            };
            List<EncodingInfo> encodings = new List<EncodingInfo>(Encoding.GetEncodings());
            encodings.Sort((a, b) => String.Compare(a.DisplayName, b.DisplayName));
            foreach (EncodingInfo e in encodings) {
                    parserEncodings.Add(new ParserEncoding(e.Name, e.DisplayName + " [" + e.Name + "]"));
            }
            lfEncoding.SelectedValuePath = "Key";
            lfEncoding.DisplayMemberPath = "Value";
            lfEncoding.ItemsSource = parserEncodings;
            lfEncoding.SelectedValue = Properties.Settings.Default.Parser_Encoding;
        }
    }

    public class ParserCulture {
        public string Key { get; set; }
        public string Value { get; set; }

        public ParserCulture(string key, string value) {
            Key = key;
            Value = value;
        }
    }

    public class ParserEncoding {
        public string Key { get; set; }
        public string Value { get; set; }

        public ParserEncoding(string key, string value) {
            Key = key;
            Value = value;
        }
    }
}
