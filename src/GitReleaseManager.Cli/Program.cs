using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using GitReleaseManager.Cli.Logging;
using GitReleaseManager.Core;
using GitReleaseManager.Core.Commands;
using GitReleaseManager.Core.Configuration;
using GitReleaseManager.Core.Helpers;
using GitReleaseManager.Core.Model;
using GitReleaseManager.Core.Options;
using GitReleaseManager.Core.Provider;
using GitReleaseManager.Core.ReleaseNotes;
using GitReleaseManager.Core.Templates;
using Microsoft.Extensions.DependencyInjection;
using NGitLab;
using Octokit;
using Serilog;

namespace GitReleaseManager.Cli
{
    public static class Program
    {
        private static IServiceProvider _serviceProvider;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The main entry point can not be named with an Async Suffix")]
        private static async Task<int> Main(string[] args)
        {
            // Just add the TLS 1.2 protocol to the Service Point manager until
            // we've upgraded to latest Octokit.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            try
            {
                return await Parser.Default.ParseArguments<CreateSubOptions, DiscardSubOptions, AddAssetSubOptions, CloseSubOptions, OpenSubOptions, PublishSubOptions, ExportSubOptions, InitSubOptions, ShowConfigSubOptions, LabelSubOptions>(args)
                    .WithParsed<BaseSubOptions>(LogConfiguration.ConfigureLogging)
                    .WithParsed<BaseSubOptions>(CreateFiglet)
                    .WithParsed<BaseSubOptions>(LogOptions)
                    .WithParsed<BaseSubOptions>(RegisterServices)
                    .MapResult(
                    (CreateSubOptions opts) => ExecuteCommand(opts),
                    (DiscardSubOptions opts) => ExecuteCommand(opts),
                    (AddAssetSubOptions opts) => ExecuteCommand(opts),
                    (CloseSubOptions opts) => ExecuteCommand(opts),
                    (OpenSubOptions opts) => ExecuteCommand(opts),
                    (PublishSubOptions opts) => ExecuteCommand(opts),
                    (ExportSubOptions opts) => ExecuteCommand(opts),
                    (InitSubOptions opts) => ExecuteCommand(opts),
                    (ShowConfigSubOptions opts) => ExecuteCommand(opts),
                    (LabelSubOptions opts) => ExecuteCommand(opts),
                    errs => Task.FromResult(1)).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                Log.Fatal("{Message}", ex.Message);
                foreach (var innerException in ex.InnerExceptions)
                {
                    Log.Fatal(innerException, "{Message}", innerException.Message);
                }

                return 1;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "{Message}", ex.Message);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
                DisposeServices();
            }
        }

        private static void RegisterServices(BaseSubOptions options)
        {
            var fileSystem = new FileSystem(options);
            var logger = Log.ForContext<VcsService>();
            var mapper = AutoMapperConfiguration.Configure();
            var configuration = ConfigurationProvider.Provide(options.TargetDirectory ?? Environment.CurrentDirectory, fileSystem);

            var serviceCollection = new ServiceCollection()
                .AddSingleton(logger)
                .AddSingleton(mapper)
                .AddSingleton(configuration)
                .AddSingleton(configuration.Export)
                .AddSingleton<ICommand<AddAssetSubOptions>, AddAssetsCommand>()
                .AddSingleton<ICommand<CloseSubOptions>, CloseCommand>()
                .AddSingleton<ICommand<CreateSubOptions>, CreateCommand>()
                .AddSingleton<ICommand<DiscardSubOptions>, DiscardCommand>()
                .AddSingleton<ICommand<ExportSubOptions>, ExportCommand>()
                .AddSingleton<ICommand<InitSubOptions>, InitCommand>()
                .AddSingleton<ICommand<LabelSubOptions>, LabelCommand>()
                .AddSingleton<ICommand<OpenSubOptions>, OpenCommand>()
                .AddSingleton<ICommand<PublishSubOptions>, PublishCommand>()
                .AddSingleton<ICommand<ShowConfigSubOptions>, ShowConfigCommand>()
                .AddSingleton<IFileSystem>(fileSystem)
                .AddSingleton<IReleaseNotesExporter, ReleaseNotesExporter>()
                .AddSingleton<IReleaseNotesBuilder, ReleaseNotesBuilder>()
                .AddSingleton<IVcsService, VcsService>();

            if (options is BaseVcsOptions vcsOptions)
            {
                if (string.IsNullOrWhiteSpace(vcsOptions.Token))
                {
                    throw new Exception("The token option is not defined");
                }

                RegisterVcsProvider(vcsOptions, serviceCollection);
            }

            if (options is CreateSubOptions createOptions && !string.IsNullOrEmpty(createOptions.OutputPath))
            {
                configuration.Create.AllowUpdateToPublishedRelease = false;
            }

            serviceCollection = serviceCollection
                .AddTransient((services) => new TemplateFactory(services.GetRequiredService<IFileSystem>(), services.GetRequiredService<Config>(), TemplateKind.Create));

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        private static void DisposeServices()
        {
            if (_serviceProvider is IDisposable serviceProvider)
            {
                serviceProvider.Dispose();
            }
        }

        private static void CreateFiglet(BaseSubOptions options)
        {
            if (options.NoLogo)
            {
                return;
            }

            var version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
#pragma warning disable CA1307 // Specify StringComparison ; Reason, we can't do this because of targeting .NET Framework
            if (version.Contains("+"))
#pragma warning restore CA1307 // Specify StringComparison
            {
                version = version.Substring(0, version.IndexOf("+", StringComparison.Ordinal));
            }

            // The following ugly formats is to prevent incorrect indentation
            // detected by editorconfig formatters.
            const string shortFormat = "\n   ____ ____  __  __\n"
                + "  / ___|  _ \\|  \\/  |\n"
                + " | |  _| |_) | |\\/| |\n"
                + " | |_| |  _ <| |  | |\n"
                + "  \\____|_| \\_\\_|  |_|\n"
                + "{0,21}\n";
            const string longFormat = "\n   ____ _ _   ____      _                     __  __\n"
                + "  / ___(_) |_|  _ \\ ___| | ___  __ _ ___  ___|  \\/  | __ _ _ __   __ _  __ _  ___ _ __\n"
                + " | |  _| | __| |_) / _ \\ |/ _ \\/ _` / __|/ _ \\ |\\/| |/ _` | '_ \\ / _` |/ _` |/ _ \\ '__|\n"
                + " | |_| | | |_|  _ <  __/ |  __/ (_| \\__ \\  __/ |  | | (_| | | | | (_| | (_| |  __/ |\n"
                + "  \\____|_|\\__|_| \\_\\___|_|\\___|\\__,_|___/\\___|_|  |_|\\__,_|_| |_|\\__,_|\\__, |\\___|_|\n"
                + "                                                                       |___/\n"
                + "{0,87}\n";

            if (GetConsoleWidth() > 87)
            {
                Log.Information(longFormat, version);
            }
            else
            {
                Log.Information(shortFormat, version);
            }
        }

        private static int GetConsoleWidth()
        {
            try
            {
                return Console.WindowWidth;
            }
            catch
            {
                Log.Verbose("Unable to get the width of the console.");
            }

            try
            {
                return Console.BufferWidth;
            }
            catch
            {
                Log.Verbose("Unable to get the width of the buffer");
                return int.MaxValue;
            }
        }

        private static Task<int> ExecuteCommand<TOptions>(TOptions options)
            where TOptions : BaseSubOptions
        {
            var command = _serviceProvider.GetRequiredService<ICommand<TOptions>>();
            return command.ExecuteAsync(options);
        }

        private static void LogOptions(BaseSubOptions options)
            => Log.Debug("{@Options}", options);

        private static void RegisterKeyedVcsProvider<TVcsImplementation>(object provider, IServiceCollection serviceCollection)
            where TVcsImplementation : class, IVcsProvider
        {
            static IVcsProvider ResolveService(IServiceProvider service, object key) => service.GetRequiredKeyedService<IVcsProvider>(key);

            provider ??= "null";

            Log.Debug("Registering {Type} with Service Key {Key}", typeof(IVcsProvider), provider);

            if (typeof(TVcsImplementation) != typeof(NullReleasesProvider))
            {
                serviceCollection.AddKeyedSingleton<IVcsProvider, TVcsImplementation>(provider);
            }

            serviceCollection
                .AddKeyedTransient<IAssetsProvider>(provider, ResolveService)
                .AddKeyedTransient<ICommitsProvider>(provider, ResolveService)
                .AddKeyedTransient<IIssuesProvider>(provider, ResolveService)
                .AddKeyedTransient<IMilestonesProvider>(provider, ResolveService)
                .AddKeyedTransient<IReleasesProvider>(provider, ResolveService);
        }

        private static void RegisterVcsProvider(BaseVcsOptions vcsOptions, IServiceCollection serviceCollection)
        {
            Log.Information("Using {Provider} as VCS Provider", vcsOptions.Provider);

            serviceCollection.AddKeyedSingleton<IVcsProvider>("null", (service, _) => new NullReleasesProvider(vcsOptions.Provider.ToString(), service.GetRequiredService<ILogger>()));
            RegisterKeyedVcsProvider<NullReleasesProvider>(null, serviceCollection);
            RegisterKeyedVcsProvider<GitHubProvider>(VcsProvider.GitHub, serviceCollection);

            serviceCollection
                .AddSingleton<IGitLabClient>((_) => new GitLabClient("https://gitlab.com", vcsOptions.Token));

            serviceCollection
                .AddSingleton<IGitHubClient>((_) => new GitHubClient(new ProductHeaderValue("GitReleaseManager")) { Credentials = new Credentials(vcsOptions.Token) });

            serviceCollection.AddTransient((service) => service.GetKeyedService<IVcsProvider>(vcsOptions.Provider) ?? service.GetRequiredKeyedService<IVcsProvider>("null"));

            if (vcsOptions is CreateSubOptions createOptions && !string.IsNullOrEmpty(createOptions.OutputPath))
            {
                serviceCollection.AddSingleton<IReleasesProvider>((service) => new LocalProvider(
                    service.GetRequiredService<IFileSystem>(),
                    createOptions.OutputPath));
            }
            else
            {
                serviceCollection.AddTransient((service) => service.GetKeyedService<IReleasesProvider>(vcsOptions.Provider) ?? service.GetRequiredKeyedService<IReleasesProvider>("null"));
            }
        }
    }
}