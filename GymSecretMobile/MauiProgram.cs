using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.LifecycleEvents;
using GymSecretMobile.Views;

using GymSecretMobile.Service;
#if ANDROID
using Plugin.Firebase.Core.Platforms.Android;
#endif

namespace GymSecretMobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();
            builder
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                    {
                        CrossFirebase.Initialize(activity, () => activity);
                    }));
#endif
                });
            builder.Services.AddSingleton<GymService>();
            builder.Services.AddSingleton<SyncService>();
            builder.Services.AddTransient<VerClientesPage>();
            builder.Services.AddSingleton<MediaService>();
            builder.Services.AddSingleton<ImageSyncService>();
            return builder.Build();
        }
    }
}
