using System;
using System.IO;
using Verse;

namespace RimTalkStyleExpand
{
    public static class StyleWatcher
    {
        private static FileSystemWatcher _watcher;
        private static DateTime _lastEvent = DateTime.MinValue;
        private static readonly TimeSpan _debounceTime = TimeSpan.FromSeconds(1);

        public static void Start()
        {
            if (_watcher != null) return;
            
            var mod = LoadedModManager.GetMod<StyleExpandMod>();
            if (mod == null) return;
            
            var stylesPath = Path.Combine(mod.Content.RootDir, "Styles");
            if (!Directory.Exists(stylesPath))
            {
                Directory.CreateDirectory(stylesPath);
            }
            
            _watcher = new FileSystemWatcher(stylesPath, "*.txt")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            
            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            
            Logger.Message("Style file watcher started");
        }

        public static void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileChanged;
                _watcher.Changed -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileRenamed;
                _watcher.Dispose();
                _watcher = null;
                Logger.Message("Style file watcher stopped");
            }
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (DateTime.Now - _lastEvent < _debounceTime) return;
            _lastEvent = DateTime.Now;
            
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return;
            
            var styleName = Path.GetFileNameWithoutExtension(e.Name);
            
            try
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        Logger.Message($"Style file created: {styleName}");
                        StyleRetriever.ScanStyleFiles();
                        break;
                    
                    case WatcherChangeTypes.Changed:
                        Logger.Message($"Style file changed: {styleName}");
                        EmbeddingCache.Clear(styleName);
                        var style = settings.Styles.Find(s => s.Name == styleName);
                        if (style != null)
                        {
                            style.IsChunked = false;
                            style.ChunkCount = 0;
                        }
                        break;
                    
                    case WatcherChangeTypes.Deleted:
                        Logger.Message($"Style file deleted: {styleName}");
                        settings.RemoveStyle(styleName);
                        EmbeddingCache.Clear(styleName);
                        SafeWriteSettings(settings);
                        break;
                }
            }
            catch (IOException ioEx) when (ioEx.HResult == -2146232800 || ioEx.Message.Contains("sharing") || ioEx.Message.Contains("Sharing"))
            {
                // Ignore sharing violation - game is writing to the same file
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error handling file change: {ex.Message}");
            }
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (DateTime.Now - _lastEvent < _debounceTime) return;
            _lastEvent = DateTime.Now;
            
            var settings = StyleExpandSettings.Instance;
            if (settings == null) return;
            
            var oldName = Path.GetFileNameWithoutExtension(e.OldName);
            var newName = Path.GetFileNameWithoutExtension(e.Name);
            
            Logger.Message($"Style file renamed: {oldName} -> {newName}");
            
            EmbeddingCache.Clear(oldName);
            settings.RemoveStyle(oldName);
            
            StyleRetriever.ScanStyleFiles();
            SafeWriteSettings(settings);
        }
        
        private static void SafeWriteSettings(StyleExpandSettings settings)
        {
            try
            {
                settings.Write();
            }
            catch (IOException ioEx) when (ioEx.HResult == -2146232800 || ioEx.Message.Contains("sharing") || ioEx.Message.Contains("Sharing"))
            {
                // Ignore sharing violation - game is writing to the same file
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error writing settings: {ex.Message}");
            }
        }
    }
}