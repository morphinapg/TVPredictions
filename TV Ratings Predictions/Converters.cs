using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace TV_Ratings_Predictions
{
    public class NullableValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null ? string.Empty : value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            string s = value as string;

            if (!string.IsNullOrWhiteSpace(s) && Double.TryParse(s, out double result))
            {
                return result;
            }

            return null;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }
    }

    public class NullableBooleanToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool?)
            {
                return (bool)value;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool isTrue ? isTrue : (object)false;
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool isTrue && isTrue) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility isVisible && isVisible == Visibility.Visible;
        }
    }

    public class DoubleToPercent : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((double)value).ToString("N2") + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((double)value).ToString("N2") + "%";
        }
    }

    public class DoubleToPercentUnrounded : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return ((double)value * 100) + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return ((double)value * 100) + "%";
        }
    }

    public class LastUpdateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter == null)
                return "";
            else
            {
                TimeSpan diference = (DateTime.Now - (DateTime)parameter);

                if (diference.TotalHours > 24)
                {
                    return " (updated " + diference.Days + " Days ago)";
                }
                else if (diference.TotalMinutes > 60)
                {
                    return " (updated " + diference.Hours + " Hours ago)";
                }
                else if (diference.TotalSeconds > 60)
                {
                    return " (updated " + diference.Minutes + " Minutes ago)";
                }
                else
                    return " (updated " + diference.Seconds + " Seconds ago)";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class NumberColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            else if ((double)value > 0)
                return new SolidColorBrush(Color.FromArgb(255, 0, 176, 80));
            else if ((double)value < 0)
                return new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            else
                return new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColor : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int number;

            if (value is double statusValue)
            {
                if (value == null || statusValue == 0)
                    number = 0;
                else if (statusValue > 0)
                    number = 1;
                else
                    number = -1;
            }
            else
                number = (int)value;


            if (number > 0)
                return new SolidColorBrush(Color.FromArgb(255, 0, 176, 80));
            else if (number < 0)
                return new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
            else
                return new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
