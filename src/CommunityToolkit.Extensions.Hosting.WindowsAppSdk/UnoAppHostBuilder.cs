// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ReSharper disable CollectionNeverUpdated.Local

using Microsoft.Extensions.Hosting;

namespace Uno.Extensions.Hosting;

using Application = Windows.UI.Xaml.Application;

/// <summary>
/// A program initialization utility.
/// </summary>
public sealed class UnoAppHostBuilder<TApp> : IHostBuilder
    where TApp : Application, new()
    {
        private readonly List<Action<IConfigurationBuilder>> _configureHostConfigActions = new();
        private readonly List<Action<HostBuilderContext, IConfigurationBuilder>> _configureAppConfigActions = new();
        private readonly List<Action<HostBuilderContext, IServiceCollection>> _configureServicesActions = new();
        private readonly List<IConfigureContainerAdapter> _configureContainerActions = new();
        private IServiceProviderFactory<IServiceCollection> _serviceProviderFactory = new DefaultServiceProviderFactory();
        private bool _hostBuilt;
        private IConfiguration? _hostConfiguration;
        private IConfiguration? _appConfiguration;
        private HostBuilderContext? _hostBuilderContext;
        private HostingEnvironment? _hostingEnvironment;
        private IServiceProvider? _appServices;
        private PhysicalFileProvider? _defaultProvider;

        /// <summary>
        /// A central location for sharing state between components during the host building process.
        /// </summary>
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        /// <summary>
        /// Set up the configuration for the builder itself. This will be used to initialize the <see cref="IHostEnvironment"/>
        /// for use later in the build process. This can be called multiple times and the results will be additive.
        /// </summary>
        /// <param name="configureDelegate">The delegate for configuring the <see cref="IConfigurationBuilder"/> that will be used
        /// to construct the <see cref="IConfiguration"/> for the host.</param>
        /// <returns>The same instance of the <see cref="IHostBuilder"/> for chaining.</returns>
        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate)
        {
            _configureHostConfigActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
            return this;
        }

        /// <summary>
        /// Sets up the configuration for the remainder of the build process and application. This can be called multiple times and
        /// the results will be additive. The results will be available at <see cref="HostBuilderContext.Configuration"/> for
        /// subsequent operations, as well as in <see cref="IHost.Services"/>.
        /// </summary>
        /// <param name="configureDelegate">The delegate for configuring the <see cref="IConfigurationBuilder"/> that will be used
        /// to construct the <see cref="IConfiguration"/> for the host.</param>
        /// <returns>The same instance of the <see cref="IHostBuilder"/> for chaining.</returns>
        public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate)
        {
            _configureAppConfigActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
            return this;
        }

        /// <summary>
        /// Adds services to the container. This can be called multiple times and the results will be additive.
        /// </summary>
        /// <param name="configureDelegate">The delegate for configuring the <see cref="IConfigurationBuilder"/> that will be used
        /// to construct the <see cref="IConfiguration"/> for the host.</param>
        /// <returns>The same instance of the <see cref="IHostBuilder"/> for chaining.</returns>
        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            _configureServicesActions.Add(
                static (_, collection) =>
                {
                    collection.AddSingleton<TApp>();
                });
            _configureServicesActions.Add(configureDelegate ?? throw new ArgumentNullException(nameof(configureDelegate)));
            return this;
        }

        /// <inheritdoc />
        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            IServiceProviderFactory<TContainerBuilder> factory
        )
            where TContainerBuilder : notnull
        {
            if (factory is IServiceProviderFactory<IServiceCollection> spf)
            {
                _serviceProviderFactory = spf;
            }
            return this;
        }

        /// <inheritdoc />
        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(
            Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory
        )
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if(_hostBuilderContext is not null && factory(_hostBuilderContext) is IServiceProviderFactory<IServiceCollection> spf)
            {
                _serviceProviderFactory = spf;
            }

            return this;
        }

        /// <inheritdoc />
        public IHostBuilder ConfigureContainer<TContainerBuilder>(
            Action<HostBuilderContext, TContainerBuilder> configureDelegate
        )
        {
            if (_serviceProviderFactory is not TContainerBuilder builder)
            {
                return this;
            }

            if (_hostBuilderContext is not null)
            {
                configureDelegate(_hostBuilderContext, builder);
            }

            return this;
        }

        /// <summary>
        /// Run the given actions to initialize the host. This can only be called once.
        /// </summary>
        /// <returns>An initialized <see cref="IHost"/></returns>
        public IHost Build()
        {
            if (_hostBuilt)
            {
                throw new InvalidOperationException(GetResourceString("BuildCalled"));
            }
            _hostBuilt = true;

            // REVIEW: If we want to raise more events outside of these calls then we will need to
            // stash this in a field.
            using DiagnosticListener diagnosticListener = new("Microsoft.Extensions.Hosting");
            const string hostBuildingEventName = "HostBuilding";
            const string hostBuiltEventName = "HostBuilt";

            if (diagnosticListener.IsEnabled() && diagnosticListener.IsEnabled(hostBuildingEventName))
            {
                Write(diagnosticListener, hostBuildingEventName, this);
            }

            BuildHostConfiguration();
            CreateHostingEnvironment();
            CreateHostBuilderContext();
            BuildAppConfiguration();
            CreateServiceProvider();

            if (_appServices is not null)
            {
                IHost host = _appServices.GetRequiredService<IHost>();
                if (diagnosticListener.IsEnabled() && diagnosticListener.IsEnabled(hostBuiltEventName))
                {
                    UnoAppHostBuilder<TApp>.Write(diagnosticListener, hostBuiltEventName, host);
                }

                return host;
            }

#pragma warning disable CS8603
            return null;
#pragma warning restore CS8603
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:UnrecognizedReflectionPattern",
            Justification = "The values being passed into Write are being consumed by the application already.")]
        private static void Write<T>(
            DiagnosticSource diagnosticSource,
            string name,
            T value)
        {
            diagnosticSource.Write(name, value);
        }

        private void BuildHostConfiguration()
        {
            IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                .AddInMemoryCollection(); // Make sure there's some default storage since there are no default providers

            foreach (Action<IConfigurationBuilder> buildAction in _configureHostConfigActions)
            {
                buildAction(configBuilder);
            }
            _hostConfiguration = configBuilder.Build();
        }

        private void CreateHostingEnvironment()
        {
            if (_hostConfiguration is not null)
            {
                _hostingEnvironment = new()
                {
                    ApplicationName = _hostConfiguration[HostDefaults.ApplicationKey],
                    EnvironmentName = _hostConfiguration[HostDefaults.EnvironmentKey] ??
                                      Environments.Production,
                    ContentRootPath = UnoAppHostBuilder<TApp>.ResolveContentRootPath(
                        _hostConfiguration[HostDefaults.ContentRootKey],
                        AppContext.BaseDirectory
                    ),};
            }

            if (_hostingEnvironment is not null && string.IsNullOrEmpty(_hostingEnvironment.ApplicationName))
            {
                // Note GetEntryAssembly returns null for the net4x console test runner.
                _hostingEnvironment.ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name;
            }

            if (_hostingEnvironment is not null)
            {
                _hostingEnvironment.ContentRootFileProvider = _defaultProvider
                    = new PhysicalFileProvider(_hostingEnvironment.ContentRootPath);
            }
        }

        private static string ResolveContentRootPath(string contentRootPath, string basePath)
            => string.IsNullOrEmpty(contentRootPath)
                ? basePath
                : Path.IsPathRooted(contentRootPath)
                    ? contentRootPath
                    : Path.Combine(Path.GetFullPath(basePath), contentRootPath);

        private void CreateHostBuilderContext()
        {
            _hostBuilderContext = new HostBuilderContext(Properties)
            {
                HostingEnvironment = _hostingEnvironment,
                Configuration = _hostConfiguration
            };
        }

        private void BuildAppConfiguration()
        {
            IConfigurationBuilder configBuilder = new ConfigurationBuilder()
                .SetBasePath(_hostingEnvironment.ContentRootPath)
                .AddConfiguration(_hostConfiguration, true);

            foreach (Action<HostBuilderContext, IConfigurationBuilder> buildAction in _configureAppConfigActions)
            {
                buildAction(_hostBuilderContext, configBuilder);
            }
            _appConfiguration = configBuilder.Build();
            _hostBuilderContext.Configuration = _appConfiguration;
        }

        private void CreateServiceProvider()
        {
            ServiceCollection services = new();
#pragma warning disable CS0618 // Type or member is obsolete
            services.AddSingleton<IHostingEnvironment>(_hostingEnvironment);
#pragma warning restore CS0618 // Type or member is obsolete
            services.AddSingleton<IHostEnvironment>(_hostingEnvironment);
            services.AddSingleton(_hostBuilderContext);
            // register configuration as factory to make it dispose with the service provider
            services.AddSingleton(_ => _appConfiguration);
#pragma warning disable CS0618 // Type or member is obsolete
            services.AddSingleton(
                static s => (IApplicationLifetime)s.GetService<IHostApplicationLifetime>()!
            );
#pragma warning restore CS0618 // Type or member is obsolete
            services.AddSingleton<IHostApplicationLifetime, ApplicationLifetime>();

            AddLifetime(services);

            services.AddSingleton<IHost>(_ => new UnoAppHost<TApp>(_appServices,
                _hostingEnvironment,
                _defaultProvider,
                _appServices.GetRequiredService<IHostApplicationLifetime>(),
                _appServices.GetRequiredService<ILogger<UnoAppHost<TApp>>>(),
                _appServices.GetRequiredService<IHostLifetime>(),
                _appServices.GetRequiredService<IOptions<HostOptions>>())
            );
            services.AddOptions().Configure<HostOptions>(options => { options.Initialize(_hostConfiguration); });
            services.AddLogging();

            foreach (Action<HostBuilderContext, IServiceCollection> configureServicesAction in _configureServicesActions)
            {
                configureServicesAction(_hostBuilderContext, services);
            }

            IServiceCollection containerBuilder = _serviceProviderFactory.CreateBuilder(services);

            foreach (IConfigureContainerAdapter containerAction in _configureContainerActions)
            {
                containerAction.ConfigureContainer(_hostBuilderContext, containerBuilder);
            }

            _appServices = _serviceProviderFactory.CreateServiceProvider(containerBuilder);

            if (_appServices == null)
            {
                throw new InvalidOperationException(GetResourceString("NullIServiceProvider"));
            }

            // resolve configuration explicitly once to mark it as resolved within the
            // service provider, ensuring it will be properly disposed with the provider
            _ = _appServices.GetService<IConfiguration>();
        }
        private static void AddLifetime(ServiceCollection services)
        {
            services.AddSingleton<IHostLifetime, ConsoleLifetime>();
        }

        private string? GetResourceString(string resourceName)
        {
            ResourceManager strings = new(typeof(Resources));
            return strings.GetString(resourceName);
        }
    }
    internal interface IConfigureContainerAdapter
    {
        void ConfigureContainer(HostBuilderContext hostContext, object containerBuilder);
    }
