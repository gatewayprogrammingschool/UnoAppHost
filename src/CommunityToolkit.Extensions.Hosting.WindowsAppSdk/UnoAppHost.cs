// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// ReSharper disable AccessToDisposedClosure

using Microsoft.Extensions.Hosting;

#pragma warning disable CS4014

// ReSharper disable NotAccessedField.Local

namespace Uno.Extensions.Hosting;

using Windows.UI.Xaml;


internal sealed class UnoAppHost<TApp> : IHost, IAsyncDisposable
    where TApp : Application, new()
{
    private readonly ILogger<UnoAppHost<TApp>> _logger;
    private readonly IHostLifetime _hostLifetime;
    private readonly ApplicationLifetime? _applicationLifetime;
    private readonly HostOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly PhysicalFileProvider _defaultProvider;
    private IEnumerable<IHostedService>? _hostedServices;
    private volatile bool _stopCalled;

    public UnoAppHost(IServiceProvider services,
                IHostEnvironment hostEnvironment,
                PhysicalFileProvider defaultProvider,
                IHostApplicationLifetime applicationLifetime,
                ILogger<UnoAppHost<TApp>> logger,
                IHostLifetime hostLifetime,
                IOptions<HostOptions> options)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _applicationLifetime
            = (applicationLifetime ??
               throw new ArgumentNullException(nameof(applicationLifetime))) as
            ApplicationLifetime;
        _hostEnvironment = hostEnvironment;
        _defaultProvider = defaultProvider;

        if (_applicationLifetime is null)
        {
            throw new ArgumentException(Resources.WindowsAppSdkHost_WindowsAppSdkHost_Replacing_IHostApplicationLifetime_is_not_supported_, nameof(applicationLifetime));
        }
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public IServiceProvider Services
    {
        get;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        //_logger.Starting();

        if (_applicationLifetime is not null)
        {
            using CancellationTokenSource combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _applicationLifetime.ApplicationStopping);
            CancellationToken combinedCancellationToken = combinedCancellationTokenSource.Token;

            try
            {
                await _hostLifetime.WaitForStartAsync(combinedCancellationToken).ConfigureAwait(false);

                combinedCancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                //ignore
            }
            _hostedServices = Services.GetService<IEnumerable<IHostedService>>()!;

            foreach (IHostedService hostedService in _hostedServices)
            {
                // Fire IHostedService.Start
                await hostedService.StartAsync(combinedCancellationToken).ConfigureAwait(false);

                if (hostedService is BackgroundService backgroundService)
                {
                    _ = TryExecuteBackgroundServiceAsync(backgroundService);
                }
            }

            //HostOptions.XamlCheckProcessRequirements();

            void OnAppOnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs args)
            {
                _logger.LogCritical((Exception?)args.Exception, "Unhandled exception.  Terminating.");
                RequestExit(combinedCancellationTokenSource);
            }

            //Windows.UI.Xaml.Application.Start(static _ => Program._app = new App());
            Application.Start(
                _ =>
                {
                    //var context = new DispatcherQueueSynchronizationContext(
                    //    DispatcherQueue.GetForCurrentThread()
                    //);
                    //SynchronizationContext.SetSynchronizationContext(context);
                    TApp app = Services.GetRequiredService<TApp>();

                    // ReSharper disable once SuspiciousTypeConversion.Global
                    if (app is CancelableApplication ca)
                    {
                        ca.Services = Services;
                        ca.Token = combinedCancellationToken;
                    }

                    app.UnhandledException += OnAppOnUnhandledException;

                    // Fire IHostApplicationLifetime.Started
                    _applicationLifetime.NotifyStarted();
                }
            );

            _logger.LogInformation("Application is exiting.");
            RequestExit(combinedCancellationTokenSource);
        }
    }

    private async Task RequestExit(CancellationTokenSource source)
    {
        CancellationToken token = source.Token;
        token.ThrowIfCancellationRequested();
        source.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            await StopAsync(token);
        }
        finally
        {
            await DisposeAsync();
        }
    }

    private async Task TryExecuteBackgroundServiceAsync(BackgroundService backgroundService)
    {
        try
        {
            await backgroundService.ExecuteTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // When the host is being stopped, it cancels the background services.
            // This isn't an error condition, so don't log it as an error.
            if (_stopCalled && backgroundService.ExecuteTask.IsCanceled && ex is OperationCanceledException)
            {
                return;
            }

            //_logger.BackgroundServiceFaulted(ex);
            if (_options.BackgroundServiceExceptionBehavior == BackgroundServiceExceptionBehavior.StopHost)
            {
                //_logger.BackgroundServiceStoppingHost(ex);
                _applicationLifetime.StopApplication();
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _stopCalled = true;
        //_logger.Stopping();

        using CancellationTokenSource cts = new(_options.ShutdownTimeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        CancellationToken token = linkedCts.Token;
        // Trigger IHostApplicationLifetime.ApplicationStopping
        _applicationLifetime.StopApplication();

        IList<Exception> exceptions = new List<Exception>();
        if (_hostedServices != null) // Started?
        {
            foreach (IHostedService hostedService in _hostedServices.Reverse())
            {
                try
                {
                    await hostedService.StopAsync(token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        // Fire IHostApplicationLifetime.Stopped
        _applicationLifetime.NotifyStopped();

        try
        {
            Application.Current.Exit();
            await _hostLifetime.StopAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }

        if (exceptions.Count > 0)
        {
            AggregateException ex = new("One or more hosted services failed to stop.", exceptions);
            //_logger.StoppedWithException(ex);
            throw ex;
        }

        //_logger.Stopped();
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        // The user didn't change the ContentRootFileProvider instance, we can dispose it
        if (ReferenceEquals(_hostEnvironment.ContentRootFileProvider, _defaultProvider))
        {
            // Dispose the content provider
            await DisposeAsyncLocal(_hostEnvironment.ContentRootFileProvider).ConfigureAwait(false);
        }
        else
        {
            // In the rare case that the user replaced the ContentRootFileProvider, dispose it and the one
            // we originally created
            await DisposeAsyncLocal(_hostEnvironment.ContentRootFileProvider).ConfigureAwait(false);
            await DisposeAsyncLocal(_defaultProvider).ConfigureAwait(false);
        }

        // Dispose the application.
        await DisposeAsyncLocal(Application.Current).ConfigureAwait(false);

        // Dispose the service provider
        await DisposeAsyncLocal(Services).ConfigureAwait(false);

        static async ValueTask DisposeAsyncLocal(object o)
        {
            switch (o)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
