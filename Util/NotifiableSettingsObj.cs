using HarmonyLib;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BetterSongSearch.Util
{
    internal class NotifiableSettingsObj : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            try
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Error Invoking PropertyChanged: {ex.Message}");
                Plugin.Log?.Error(ex);
            }
        }

        internal void NotifyPropertiesChanged()
        {
            foreach (System.Reflection.PropertyInfo x in AccessTools.GetDeclaredProperties(GetType()))
            {
                NotifyPropertyChanged(x.Name);
            }
        }
    }
}
