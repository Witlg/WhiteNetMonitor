using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WhiteNet
{
    internal static class RegStore
    {
        private const string Base = @"Software\WhiteNetMonitor";

        public static string Get(string name, string def)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(Base))
                    return k?.GetValue(name) as string ?? def;
            }
            catch { return def; }
        }

        public static void Set(string name, string val)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(Base))
                    k.SetValue(name, val);
            }
            catch { }
        }
    }

    internal static class Loc
    {
        public static event Action Changed;

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["tab.net"]   = "🌐  Сеть",
            ["tab.sys"]   = "📊  Система",
            ["tab.set"]   = "⚙  Настройки",
            ["st.check"]  = "Проверка…",
            ["st.ok"]     = "Онлайн",
            ["st.fail"]   = "Нет сети",
            ["st.busy"]   = "Переключаю…",
            ["net.hint.on"]  = "нажми на кружок — выключить интернет",
            ["net.hint.off"] = "нажми на кружок — включить интернет обратно",
            ["msg.confirm"]  = "Выключить интернет на этом компьютере?",
            ["st.sub"]    = "Подключение проверяется каждые 5 сек",
            ["ip.cap"]    = "ЛОКАЛЬНЫЙ IP-АДРЕС",
            ["dl.cap"]    = "ЗАГРУЗКА ↓",
            ["ul.cap"]    = "ОТДАЧА ↑",
            ["sp.sub"]    = "реальная скорость интерфейсов",
            ["p.cap"]     = "ПИНГ ДО 8.8.8.8",
            ["p.hint"]    = "нажми «Проверить»",
            ["p.busy"]    = "измеряю…",
            ["p.fail"]    = "нет ответа",
            ["p.val"]     = "{0} мс",
            ["btn.check"] = "⟳  Проверить",
            ["cpu.cap"]   = "ПРОЦЕССОР",
            ["ram.cap"]   = "ОПЕРАТИВНАЯ ПАМЯТЬ",
            ["disk.cap"]  = "ДИСК C:",
            ["ram.det"]   = "занято {0} из {1}",
            ["disk.det"]  = "занято {0} · свободно {1}",
            ["os.cap"]    = "СИСТЕМА",
            ["up.cap"]    = "ВРЕМЯ РАБОТЫ",
            ["u.d"]       = "д",
            ["u.h"]       = "ч",
            ["u.m"]       = "мин",
            ["sp.b"]      = "Б/с",
            ["sp.k"]      = "КБ/с",
            ["sp.m"]      = "МБ/с",
            ["u.gb"]      = "ГБ",
            ["u.mb"]      = "МБ",
            ["start.chk"] = "Запускать с Windows",
            ["set.lang"]  = "ЯЗЫК",
            ["set.note"]  = "Применяется мгновенно и запоминается в системе",
            ["lang.ru.sub"] = "Русский язык",
            ["lang.en.sub"] = "Английский язык",
            ["about.cap"] = "О ПРОГРАММЕ",
            ["about.l2"]  = "Сделано White Team · .NET Framework 4.7.2",
            ["donate.btn"] = "❤  Пожертвования (Поддержка)",
            ["tray.open"]    = "Открыть монитор",
            ["tray.refresh"] = "Обновить",
            ["tray.exit"]    = "Выход",
            ["tray.online"]  = "онлайн",
            ["tray.offline"] = "нет сети"
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["tab.net"]   = "🌐  Network",
            ["tab.sys"]   = "📊  System",
            ["tab.set"]   = "⚙  Settings",
            ["st.check"]  = "Checking…",
            ["st.ok"]     = "Online",
            ["st.fail"]   = "No network",
            ["st.busy"]   = "Switching…",
            ["net.hint.on"]  = "click the dot — turn the internet off",
            ["net.hint.off"] = "click the dot — turn the internet back on",
            ["msg.confirm"]  = "Turn off the internet on this computer?",
            ["st.sub"]    = "Connection is checked every 5 sec",
            ["ip.cap"]    = "LOCAL IP ADDRESS",
            ["dl.cap"]    = "DOWNLOAD ↓",
            ["ul.cap"]    = "UPLOAD ↑",
            ["sp.sub"]    = "real interface speed",
            ["p.cap"]     = "PING TO 8.8.8.8",
            ["p.hint"]    = "press “Check”",
            ["p.busy"]    = "measuring…",
            ["p.fail"]    = "no reply",
            ["p.val"]     = "{0} ms",
            ["btn.check"] = "⟳  Check",
            ["cpu.cap"]   = "CPU",
            ["ram.cap"]   = "MEMORY",
            ["disk.cap"]  = "DISK C:",
            ["ram.det"]   = "{0} used of {1}",
            ["disk.det"]  = "{0} used · {1} free",
            ["os.cap"]    = "OS",
            ["up.cap"]    = "UPTIME",
            ["u.d"]       = "d",
            ["u.h"]       = "h",
            ["u.m"]       = "min",
            ["sp.b"]      = "B/s",
            ["sp.k"]      = "KB/s",
            ["sp.m"]      = "MB/s",
            ["u.gb"]      = "GB",
            ["u.mb"]      = "MB",
            ["start.chk"] = "Start with Windows",
            ["set.lang"]  = "LANGUAGE",
            ["set.note"]  = "Applied instantly and remembered",
            ["lang.ru.sub"] = "Russian",
            ["lang.en.sub"] = "English",
            ["about.cap"] = "ABOUT",
            ["about.l2"]  = "Made by White Team · .NET Framework 4.7.2",
            ["donate.btn"] = "❤  Donations (Support)",
            ["tray.open"]    = "Open monitor",
            ["tray.refresh"] = "Refresh",
            ["tray.exit"]    = "Exit",
            ["tray.online"]  = "online",
            ["tray.offline"] = "offline"
        };

        public static string Lang { get; private set; }

        static Loc()
        {
            Lang = RegStore.Get("Lang", "ru");
            if (Lang != "en") Lang = "ru";
        }

        public static void Set(string lang)
        {
            lang = lang == "en" ? "en" : "ru";
            if (lang == Lang) return;
            Lang = lang;
            RegStore.Set("Lang", lang);
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            var d = Lang == "en" ? En : Ru;
            string v;
            return d.TryGetValue(key, out v) ? v : key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}
