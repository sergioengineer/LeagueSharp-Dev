﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Bootstrap.cs is part of SFXChallenger.

 SFXChallenger is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXChallenger is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXChallenger. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

namespace SFXChallenger
{
    #region

    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Resources;
    using Abstracts;
    using Helpers;
    using Interfaces;
    using LeagueSharp;
    using LeagueSharp.Common;
    using SFXLibrary.Logger;

    #endregion

    public class Bootstrap
    {
        private static IChampion _champion;

        public static void Init()
        {
            try
            {
                SetupLogger();
                SetupLanguage();

                CustomEvents.Game.OnGameLoad += delegate
                {
                    try
                    {
                        _champion = LoadChampion();

                        if (_champion != null)
                        {
                            Core.Init(_champion, 50);
                            Core.Boot();

                            Update.Init(Global.UpdatePath, 10000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.AddItem(new LogItem(ex));
                    }
                };
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static IChampion LoadChampion()
        {
            var type =
                Assembly.GetAssembly(typeof (IChampion))
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof (IChampion).IsAssignableFrom(t))
                    .FirstOrDefault(t => t.Name.Equals(ObjectManager.Player.ChampionName, StringComparison.OrdinalIgnoreCase));

            return type != null ? (Champion) DynamicInitializer.NewInstance(type) : null;
        }

        private static void SetupLogger()
        {
            Global.Logger = new FileLogger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Global.Name + " - Logs")) {LogLevel = LogLevel.High};

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs eventArgs)
            {
                var ex = sender as Exception;
                if (ex != null)
                    Global.Logger.AddItem(new LogItem(ex));
            };
        }

        private static void SetupLanguage()
        {
            Global.Lang.Default = "en";

            var currentAsm = Assembly.GetExecutingAssembly();
            foreach (var resName in currentAsm.GetManifestResourceNames())
            {
                ResourceReader resReader = null;
                using (var stream = currentAsm.GetManifestResourceStream(resName))
                {
                    if (stream != null)
                        resReader = new ResourceReader(stream);

                    if (resReader != null)
                    {
                        var en = resReader.GetEnumerator();

                        while (en.MoveNext())
                        {
                            if (en.Key.ToString().StartsWith("language_"))
                                Global.Lang.Parse(en.Value.ToString());
                        }
                    }
                }
            }

            var lang =
                Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, string.Format(@"{0}.Global.Lang.*", Global.Name),
                    SearchOption.TopDirectoryOnly).Select(Path.GetExtension).FirstOrDefault();
            if (lang != null && Global.Lang.Languages.Any(l => l.Equals(lang.Substring(1))))
                Global.Lang.Current = lang.Substring(1);
            else
                Global.Lang.Current = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        }
    }
}