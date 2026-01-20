using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WavForge.Services;
using WavForge.ViewModels;
using WavForge.Views;

namespace WavForge;

internal sealed class App : Application
{
#pragma warning disable S1450
    private IServiceProvider? _services;
#pragma warning restore S1450
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            _services = ConfigureServices();
            
            desktop.MainWindow = _services!.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

#pragma warning disable CA1859 // We don't need the improved performance
    private static IServiceProvider ConfigureServices()
#pragma warning restore CA1859
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<IWindowProvider, ClassicDesktopWindowProvider>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainWindow>(sp =>
        {
            var window = new MainWindow
            {
                DataContext = sp.GetRequiredService<MainWindowViewModel>()
            };
            return window;
        });
        
        return services.BuildServiceProvider();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
