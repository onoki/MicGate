using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace MicGate
{
    public class Utility
    {
        public static string ReadSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static void SaveSetting(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Remove(key);
            config.AppSettings.Settings.Add(key, value);
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static bool StrToBool(string input)
        {
            string[] trueValues = { "true", "1" };
            return trueValues.Contains(input.ToLower());
        }

        public static int StrToInt(string input)
        {
            if (!int.TryParse(input, out var result)) result = 0;
            return result;
        }

        public static float StrToFloat(string input)
        {
            input = input.Replace(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            input = input.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            if (!float.TryParse(input, out var result)) result = 0;
            return result;
        }

        public static int TimeToSamples(int sampleRate, int timeMs)
        {
            return sampleRate * timeMs / 1000;
        }
    }
}
