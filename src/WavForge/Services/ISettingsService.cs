using WavForge.Models;

namespace WavForge.Services;

internal interface ISettingsService
{
    AppSettings Settings { get; }

    void Load();
    void Save();
}
